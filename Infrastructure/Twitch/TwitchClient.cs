using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Timer.Application.Interfaces;
using Timer.Domain.Entities;
using Timer.Infrastructure.Security;

namespace Timer.Infrastructure.Twitch;

public sealed class TwitchClient : ITwitchClient
{
    private const string DefaultEventSubWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";
    private sealed record TwitchToken(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);
    private static readonly string[] RequiredScopes =
    [
        "channel:read:redemptions",
        "bits:read",
        "channel:read:polls",
        "channel:manage:polls"
    ];

    private readonly HttpClient _httpClient;
    private readonly WindowsCredentialStore _credentialStore;
    private readonly string _clientId;
    private string _eventSubWebSocketUrl;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string? _userId;
    private string? _displayName;
    private int _reconnectDelaySeconds = 2;

    public TwitchClient(string clientId, HttpClient httpClient, WindowsCredentialStore credentialStore, string? eventSubWebSocketUrl = null)
    {
        _clientId = clientId;
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _eventSubWebSocketUrl = string.IsNullOrWhiteSpace(eventSubWebSocketUrl)
            ? DefaultEventSubWebSocketUrl
            : eventSubWebSocketUrl;
    }

    public string EventSubWebSocketUrl
    {
        get => _eventSubWebSocketUrl;
        set => _eventSubWebSocketUrl = string.IsNullOrWhiteSpace(value)
            ? DefaultEventSubWebSocketUrl
            : value.Trim();
    }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<(string UserCode, string VerifyUrl)>? DeviceCodeReceived;
    public event EventHandler<string>? RewardRedeemed;
    public event EventHandler<int>? BitsCheered;
    public event EventHandler<string>? PollEnded;

    public async Task ConnectAsync()
    {
        var token = await EnsureTokenAsync();

        if (token is null)
        {
            StatusChanged?.Invoke(this, "Authorization failed");
            return;
        }

        await EnsureUserAsync(token);
        await ConnectEventSubAsync(token);

        StatusChanged?.Invoke(this, _displayName is null ? "Connected" : $"Connected as {_displayName}");
    }

    public async Task<IReadOnlyList<TwitchReward>> LoadRewardsAsync()
    {
        var token = await EnsureTokenAsync();
        if (token is null)
        {
            throw new InvalidOperationException("Connect Twitch first to load rewards.");
        }

        await EnsureUserAsync(token);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={_userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("Client-Id", _clientId);

        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch authorization expired. Please reconnect.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch permission denied. Please reconnect and approve scopes.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            StatusChanged?.Invoke(this, "Twitch rate limit reached. Try again later.");
            return Array.Empty<TwitchReward>();
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var data = doc.RootElement.GetProperty("data");
        var rewards = new List<TwitchReward>();
        foreach (var reward in data.EnumerateArray())
        {
            var id = reward.GetProperty("id").GetString() ?? string.Empty;
            var title = reward.GetProperty("title").GetString() ?? string.Empty;
            var cost = reward.GetProperty("cost").GetInt32();
            if (!string.IsNullOrWhiteSpace(id))
            {
                rewards.Add(new TwitchReward(id, title, cost));
            }
        }

        return rewards;
    }

    public async Task StartPollAsync(string title, int durationSeconds)
    {
        var token = await EnsureTokenAsync();
        if (token is null)
        {
            throw new InvalidOperationException("Connect Twitch first to start a poll.");
        }

        await EnsureUserAsync(token);

        var body = new
        {
            broadcaster_id = _userId,
            title,
            choices = new[]
            {
                new { title = "Agree" },
                new { title = "Disagree" }
            },
            duration = durationSeconds
        };

        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/polls");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("Client-Id", _clientId);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch authorization expired. Please reconnect.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch permission denied. Please reconnect and approve scopes.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("Twitch rate limit reached. Please retry later.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync();
            Debug.WriteLine(bodyText);
            throw new InvalidOperationException($"Unable to start poll: {response.StatusCode}");
        }
    }

    public Task DisconnectAsync()
    {
        _cts?.Cancel();
        _socket?.Dispose();
        return Task.CompletedTask;
    }

    public void SimulateRewardRedeemed(string rewardId)
    {
        if (!string.IsNullOrWhiteSpace(rewardId))
        {
            RewardRedeemed?.Invoke(this, rewardId);
        }
    }

    public void SimulateBitsCheered(int bits)
    {
        if (bits > 0)
        {
            BitsCheered?.Invoke(this, bits);
        }
    }

    public async Task<bool> TryReconnectAsync()
    {
        var token = await LoadTokenAsync();
        if (token is null)
        {
            return false;
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await RefreshTokenAsync(token.RefreshToken);
            if (token is null)
            {
                _credentialStore.Delete(TokenKey);
                return false;
            }
        }

        var hasScopes = await HasRequiredScopesAsync(token);
        if (!hasScopes)
        {
            _credentialStore.Delete(TokenKey);
            return false;
        }

        await EnsureUserAsync(token);
        await ConnectEventSubAsync(token);
        StatusChanged?.Invoke(this, _displayName is null ? "Connected" : $"Connected as {_displayName}");
        return true;
    }

    public void NotifyStatus(string status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            StatusChanged?.Invoke(this, status);
        }
    }

    public async Task<(bool HasToken, DateTimeOffset? ExpiresAt, bool HasRequiredScopes)> GetTokenStatusAsync()
    {
        var token = await LoadTokenAsync();
        if (token is null)
        {
            return (false, null, false);
        }

        var hasScopes = await HasRequiredScopesAsync(token);
        return (true, token.ExpiresAt, hasScopes);
    }

    private async Task<TwitchToken?> LoadTokenAsync()
    {
        return await Task.Run(() =>
        {
            var json = _credentialStore.Read(TokenKey);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TwitchToken>(json);
        });
    }

    private async Task SaveTokenAsync(TwitchToken token)
    {
        var json = JsonSerializer.Serialize(token);
        await Task.Run(() => _credentialStore.Write(TokenKey, json));
    }

    private async Task<TwitchToken?> EnsureTokenAsync()
    {
        var token = await LoadTokenAsync();
        if (token is null)
        {
            return await AuthorizeDeviceAsync();
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await RefreshTokenAsync(token.RefreshToken);
            if (token is null)
            {
                _credentialStore.Delete(TokenKey);
                return await AuthorizeDeviceAsync();
            }
        }

        var hasScopes = await HasRequiredScopesAsync(token);
        if (!hasScopes)
        {
            _credentialStore.Delete(TokenKey);
            return await AuthorizeDeviceAsync();
        }

        return token;
    }

    private async Task<TwitchToken?> AuthorizeDeviceAsync()
    {
        var scope = string.Join(' ', RequiredScopes);
        using var deviceContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("scopes", scope)
        });

        var deviceResponse = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/device", deviceContent);
        deviceResponse.EnsureSuccessStatusCode();

        var devicePayload = await deviceResponse.Content.ReadAsStringAsync();
        using var deviceDoc = JsonDocument.Parse(devicePayload);
        var root = deviceDoc.RootElement;
        var deviceCode = root.GetProperty("device_code").GetString() ?? string.Empty;
        var userCode = root.GetProperty("user_code").GetString() ?? string.Empty;
        var verifyUrl = root.GetProperty("verification_uri").GetString() ?? string.Empty;
        var interval = root.GetProperty("interval").GetInt32();
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        DeviceCodeReceived?.Invoke(this, (userCode, verifyUrl));
        StatusChanged?.Invoke(this, "Complete verification");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        var wait = TimeSpan.FromSeconds(interval);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(wait);
            var token = await PollDeviceTokenAsync(deviceCode);
            if (token is not null)
            {
                await SaveTokenAsync(token);
                return token;
            }
        }

        return null;
    }

    private async Task<TwitchToken?> PollDeviceTokenAsync(string deviceCode)
    {
        using var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("device_code", deviceCode),
            new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
        });

        var tokenResponse = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", tokenContent);
        var payload = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            using var errorDoc = JsonDocument.Parse(payload);
            if (errorDoc.RootElement.TryGetProperty("message", out var messageProperty))
            {
                var message = messageProperty.GetString();
                if (string.Equals(message, "authorization_pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(message, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return null;
        }

        using var tokenDoc = JsonDocument.Parse(payload);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var expiresIn = tokenDoc.RootElement.GetProperty("expires_in").GetInt32();
        var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var refreshElement)
            ? refreshElement.GetString()
            : null;
        return new TwitchToken(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    private async Task<TwitchToken?> RefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        using var tokenContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _clientId),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
            new KeyValuePair<string, string>("grant_type", "refresh_token")
        });

        var tokenResponse = await _httpClient.PostAsync("https://id.twitch.tv/oauth2/token", tokenContent);
        var payload = await tokenResponse.Content.ReadAsStringAsync();
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var tokenDoc = JsonDocument.Parse(payload);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
        var expiresIn = tokenDoc.RootElement.GetProperty("expires_in").GetInt32();
        var newRefreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var refreshElement)
            ? refreshElement.GetString()
            : refreshToken;
        var refreshed = new TwitchToken(accessToken, newRefreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        await SaveTokenAsync(refreshed);
        return refreshed;
    }

    private async Task<bool> HasRequiredScopesAsync(TwitchToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", token.AccessToken);
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("scopes", out var scopesElement)
            || scopesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var scopes = scopesElement.EnumerateArray()
            .Select(scope => scope.GetString())
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RequiredScopes.All(scope => scopes.Contains(scope));
    }

    private async Task EnsureUserAsync(TwitchToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/users");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("Client-Id", _clientId);
        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch authorization expired. Please reconnect.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _credentialStore.Delete(TokenKey);
            throw new InvalidOperationException("Twitch permission denied. Please reconnect and approve scopes.");
        }
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            StatusChanged?.Invoke(this, "Twitch rate limit reached. Try again later.");
            return;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(payload);
        var user = doc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
        if (user.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Unable to resolve Twitch user.");
        }

        _userId = user.GetProperty("id").GetString();
        _displayName = user.GetProperty("display_name").GetString();
    }

    private async Task ConnectEventSubAsync(TwitchToken token)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _socket?.Dispose();
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(new Uri(_eventSubWebSocketUrl), _cts.Token);
        _reconnectDelaySeconds = 2;
        _ = Task.Run(() => ReceiveEventSubLoopAsync(token, _socket, _cts.Token));
    }

    private async Task ReceiveEventSubLoopAsync(TwitchToken token, ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[1024 * 8];
        var builder = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                builder.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);
                        await ScheduleReconnectAsync(token);
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                HandleEventSubMessage(token, builder.ToString());
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            StatusChanged?.Invoke(this, $"Twitch disconnected: {ex.Message}");
            await ScheduleReconnectAsync(token);
        }
    }

    private void HandleEventSubMessage(TwitchToken token, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("metadata", out var metadata)
                || !metadata.TryGetProperty("message_type", out var messageTypeProperty))
            {
                return;
            }

            var messageType = messageTypeProperty.GetString();
            switch (messageType)
            {
                case "session_welcome":
                    var sessionId = root.GetProperty("payload").GetProperty("session").GetProperty("id").GetString();
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        _ = Task.Run(() => SubscribeEventSubAsync(token, sessionId));
                    }
                    break;
                case "session_reconnect":
                    var reconnectUrl = root.GetProperty("payload").GetProperty("session").GetProperty("reconnect_url").GetString();
                    if (!string.IsNullOrWhiteSpace(reconnectUrl))
                    {
                        _ = Task.Run(() => ReconnectEventSubAsync(token, reconnectUrl));
                    }
                    break;
                case "session_keepalive":
                    StatusChanged?.Invoke(this, "Twitch connected");
                    break;
                case "notification":
                    HandleEventSubNotification(root);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Event parse error: {ex.Message}");
        }
    }

    private async Task SubscribeEventSubAsync(TwitchToken token, string sessionId)
    {
        await SubscribeEventAsync(token, sessionId, "channel.channel_points_custom_reward_redemption.add");
        await SubscribeEventAsync(token, sessionId, "channel.cheer");
        await SubscribeEventAsync(token, sessionId, "channel.poll.end");
    }

    private async Task SubscribeEventAsync(TwitchToken token, string sessionId, string type)
    {
        var body = new
        {
            type,
            version = "1",
            condition = new { broadcaster_user_id = _userId },
            transport = new { method = "websocket", session_id = sessionId }
        };

        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("Client-Id", _clientId);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync();
            StatusChanged?.Invoke(this, $"EventSub subscribe failed: {response.StatusCode}");
            Debug.WriteLine(bodyText);
        }
    }

    private void HandleEventSubNotification(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        var subscription = payload.GetProperty("subscription");
        var type = subscription.GetProperty("type").GetString();
        var eventData = payload.GetProperty("event");

        if (string.Equals(type, "channel.channel_points_custom_reward_redemption.add", StringComparison.OrdinalIgnoreCase))
        {
            var rewardId = eventData.GetProperty("reward").GetProperty("id").GetString() ?? string.Empty;
            RewardRedeemed?.Invoke(this, rewardId);
            return;
        }

        if (string.Equals(type, "channel.cheer", StringComparison.OrdinalIgnoreCase))
        {
            if (eventData.TryGetProperty("bits", out var bitsProperty)
                && bitsProperty.ValueKind == JsonValueKind.Number)
            {
                BitsCheered?.Invoke(this, bitsProperty.GetInt32());
            }

            return;
        }

        if (string.Equals(type, "channel.poll.end", StringComparison.OrdinalIgnoreCase))
        {
            var winner = GetPollWinner(eventData);
            if (!string.IsNullOrWhiteSpace(winner))
            {
                PollEnded?.Invoke(this, winner);
            }
        }
    }

    private static string? GetPollWinner(JsonElement eventData)
    {
        if (!eventData.TryGetProperty("choices", out var choicesElement) || choicesElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? winnerTitle = null;
        var maxVotes = -1;
        foreach (var choice in choicesElement.EnumerateArray())
        {
            var votes = choice.GetProperty("votes").GetInt32();
            var bitsVotes = choice.GetProperty("bits_votes").GetInt32();
            var channelPointsVotes = choice.GetProperty("channel_points_votes").GetInt32();
            var total = votes + bitsVotes + channelPointsVotes;
            if (total > maxVotes)
            {
                maxVotes = total;
                winnerTitle = choice.GetProperty("title").GetString();
            }
        }

        return winnerTitle;
    }

    private async Task ReconnectEventSubAsync(TwitchToken token, string reconnectUrl)
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _socket?.Dispose();
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(new Uri(reconnectUrl), _cts.Token);
            _reconnectDelaySeconds = 2;
            _ = Task.Run(() => ReceiveEventSubLoopAsync(token, _socket, _cts.Token));
        }
        catch
        {
            await ScheduleReconnectAsync(token);
        }
    }

    private async Task ScheduleReconnectAsync(TwitchToken token)
    {
        if (_cts is null || _cts.IsCancellationRequested)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(_reconnectDelaySeconds);
        _reconnectDelaySeconds = Math.Min(_reconnectDelaySeconds * 2, 60);
        await Task.Delay(delay, _cts.Token);
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        await ConnectEventSubAsync(token);
    }

    private const string TokenKey = "JohnnyTimerEventSubWPF.TwitchToken";
}
