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

    private const int MaxLoggedBodyChars = 500;
    private const int MaxRememberedMessageIds = 500;

    private readonly HttpClient _httpClient;
    private readonly WindowsCredentialStore _credentialStore;
    private readonly ILogService _logService;
    private readonly string _clientId;
    private string _eventSubWebSocketUrl = DefaultEventSubWebSocketUrl;
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string? _userId;
    private string? _displayName;
    private int _reconnectDelaySeconds = 2;
    private volatile bool _subscribeAuthFailed;
    private readonly object _seenMessagesGate = new();
    private readonly HashSet<string> _seenMessageIds = new(StringComparer.Ordinal);
    private readonly Queue<string> _seenMessageOrder = new();

    public TwitchClient(
        string clientId,
        HttpClient httpClient,
        WindowsCredentialStore credentialStore,
        ILogService logService,
        string? eventSubWebSocketUrl = null)
    {
        _clientId = clientId;
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _logService = logService;
        EventSubWebSocketUrl = eventSubWebSocketUrl ?? DefaultEventSubWebSocketUrl;
    }

    public string EventSubWebSocketUrl
    {
        get => _eventSubWebSocketUrl;
        set => _eventSubWebSocketUrl = IsAllowedEventSubUrl(value)
            ? value.Trim()
            : DefaultEventSubWebSocketUrl;
    }

    // Only wss:// is trusted for a real Twitch connection; plain ws:// is allowed
    // solely to loopback so the CLI debug workflow keeps working. This blocks a
    // tampered settings.json (EventSubWebSocketUrl is stored in plaintext, not
    // validated) from silently redirecting the socket to an attacker-controlled
    // host that could then inject fake reward/bits/poll events.
    private static bool IsAllowedEventSubUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(uri.Scheme, "ws", StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
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
        await ConnectEventSubAsync();

        _logService.LogInfo($"Twitch connected as {_displayName ?? "(unknown)"}.");
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

    public async Task RevokeAsync()
    {
        var token = await LoadTokenAsync();
        if (token is not null)
        {
            try
            {
                using var revokeContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("token", token.AccessToken)
                });
                await _httpClient.PostAsync("https://id.twitch.tv/oauth2/revoke", revokeContent);
            }
            catch
            {
                // Best-effort: still clear the local credential below even if
                // Twitch's revoke endpoint is unreachable.
            }
        }

        _credentialStore.Delete(TokenKey);
        await DisconnectAsync();
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
            _logService.LogInfo("Twitch auto-connect skipped: no saved token.");
            return false;
        }

        if (token.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await RefreshTokenAsync(token.RefreshToken);
            if (token is null)
            {
                _logService.LogWarning("Twitch auto-connect failed: token refresh failed.");
                _credentialStore.Delete(TokenKey);
                return false;
            }
        }

        var hasScopes = await HasRequiredScopesAsync(token);
        if (!hasScopes)
        {
            _logService.LogWarning("Twitch auto-connect failed: token is invalid or missing scopes.");
            _credentialStore.Delete(TokenKey);
            return false;
        }

        await EnsureUserAsync(token);
        await ConnectEventSubAsync();
        _logService.LogInfo($"Twitch auto-connected as {_displayName ?? "(unknown)"}.");
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

    // Background-safe variant of EnsureTokenAsync: refreshes an expiring token but
    // never starts the interactive device flow; returns null when the user must reconnect.
    private async Task<TwitchToken?> LoadValidTokenAsync()
    {
        var token = await LoadTokenAsync();
        if (token is null || token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return token;
        }

        _logService.LogInfo("Twitch token expiring; refreshing.");
        return await RefreshTokenAsync(token.RefreshToken);
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
            // The error body only carries status/message, never token material.
            _logService.LogWarning($"Twitch token refresh failed: {(int)tokenResponse.StatusCode} {Truncate(payload)}");
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

    private async Task ConnectEventSubAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _socket?.Dispose();
        var socket = new ClientWebSocket();
        _socket = socket;
        _logService.LogInfo($"EventSub connecting to {_eventSubWebSocketUrl}.");
        await socket.ConnectAsync(new Uri(_eventSubWebSocketUrl), cts.Token);
        _reconnectDelaySeconds = 2;
        _ = Task.Run(() => ReceiveEventSubLoopAsync(socket, cts.Token));
    }

    private async Task ReceiveEventSubLoopAsync(ClientWebSocket socket, CancellationToken ct)
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
                        // Twitch's close code says why, e.g. 4003 = nothing subscribed in time.
                        _logService.LogWarning($"EventSub closed by server: {(int?)result.CloseStatus} {result.CloseStatusDescription}");
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);
                        await ScheduleReconnectAsync();
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                HandleEventSubMessage(builder.ToString());
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logService.LogError("EventSub connection lost.", ex);
            StatusChanged?.Invoke(this, $"Twitch disconnected: {ex.Message}");
            await ScheduleReconnectAsync();
        }
    }

    private void HandleEventSubMessage(string json)
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
                    _logService.LogInfo("EventSub session started.");
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        _ = Task.Run(() => SubscribeEventSubAsync(sessionId));
                    }
                    break;
                case "session_reconnect":
                    var reconnectUrl = root.GetProperty("payload").GetProperty("session").GetProperty("reconnect_url").GetString();
                    _logService.LogInfo("EventSub asked to reconnect.");
                    if (!string.IsNullOrWhiteSpace(reconnectUrl))
                    {
                        _ = Task.Run(() => ReconnectEventSubAsync(reconnectUrl));
                    }
                    break;
                case "session_keepalive":
                    // After a subscribe was rejected for auth, don't let the keepalive flip
                    // the status back to "connected": the socket is up but no events will come.
                    if (!_subscribeAuthFailed)
                    {
                        StatusChanged?.Invoke(this, "Twitch connected");
                    }
                    break;
                case "notification":
                    var messageId = metadata.TryGetProperty("message_id", out var messageIdProperty)
                        ? messageIdProperty.GetString()
                        : null;
                    if (!string.IsNullOrEmpty(messageId) && !TryMarkMessageSeen(messageId))
                    {
                        _logService.LogWarning($"Duplicate EventSub notification ignored ({messageId}).");
                        break;
                    }

                    HandleEventSubNotification(root);
                    break;
                case "revocation":
                    var revoked = root.GetProperty("payload").GetProperty("subscription");
                    _logService.LogWarning($"EventSub subscription revoked: {revoked.GetProperty("type").GetString()} ({revoked.GetProperty("status").GetString()}).");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError("EventSub message parse failed.", ex);
            StatusChanged?.Invoke(this, $"Event parse error: {ex.Message}");
        }
    }

    private async Task SubscribeEventSubAsync(string sessionId)
    {
        // Session IDs from the Twitch CLI mock server mean nothing to the real Helix
        // API; the CLI debug workflow pushes forced triggers without subscribing.
        if (IsLoopbackEventSub())
        {
            _logService.LogInfo("EventSub debug server: skipping Helix subscriptions.");
            return;
        }

        try
        {
            // Re-read the token on every welcome. Reconnects used to subscribe with the
            // token captured at connect time; hours into a stream it had expired, every
            // subscribe got a 401, and the status still read "connected".
            var token = await LoadValidTokenAsync();
            if (token is null)
            {
                _subscribeAuthFailed = true;
                _logService.LogWarning("EventSub subscribe skipped: no valid Twitch token.");
                StatusChanged?.Invoke(this, "Twitch authorization expired. Please reconnect.");
                return;
            }

            _subscribeAuthFailed = false;
            await SubscribeEventAsync(token, sessionId, "channel.channel_points_custom_reward_redemption.add");
            // channel.bits.use covers chat cheers and Power-ups; channel.cheer only sees cheers.
            await SubscribeEventAsync(token, sessionId, "channel.bits.use");
            await SubscribeEventAsync(token, sessionId, "channel.poll.end");
        }
        catch (Exception ex)
        {
            _logService.LogError("EventSub subscribe failed.", ex);
            StatusChanged?.Invoke(this, $"EventSub subscribe failed: {ex.Message}");
        }
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
        using var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logService.LogInfo($"EventSub subscribed: {type}.");
            return;
        }

        // 409 = already exists, e.g. carried over by a session_reconnect.
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logService.LogInfo($"EventSub already subscribed: {type}.");
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _subscribeAuthFailed = true;
        }

        var bodyText = await response.Content.ReadAsStringAsync();
        _logService.LogWarning($"EventSub subscribe failed: {type} {(int)response.StatusCode} {Truncate(bodyText)}");
        StatusChanged?.Invoke(this, $"EventSub subscribe failed: {response.StatusCode}");
    }

    private void HandleEventSubNotification(JsonElement root)
    {
        var payload = root.GetProperty("payload");
        var subscription = payload.GetProperty("subscription");
        var type = subscription.GetProperty("type").GetString();
        var eventData = payload.GetProperty("event");

        if (string.Equals(type, "channel.channel_points_custom_reward_redemption.add", StringComparison.OrdinalIgnoreCase))
        {
            var reward = eventData.GetProperty("reward");
            var rewardId = reward.GetProperty("id").GetString() ?? string.Empty;
            var rewardTitle = reward.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null;
            _logService.LogInfo($"Reward redeemed: {rewardTitle ?? "(untitled)"} ({rewardId}).");
            RewardRedeemed?.Invoke(this, rewardId);
            return;
        }

        // channel.cheer is no longer subscribed, but stays parsed so the Twitch CLI
        // "twitch event trigger cheer" debug workflow keeps working.
        if (string.Equals(type, "channel.bits.use", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "channel.cheer", StringComparison.OrdinalIgnoreCase))
        {
            if (eventData.TryGetProperty("bits", out var bitsProperty)
                && bitsProperty.ValueKind == JsonValueKind.Number)
            {
                var bits = bitsProperty.GetInt32();
                var source = eventData.TryGetProperty("type", out var sourceProperty)
                    && sourceProperty.ValueKind == JsonValueKind.String
                        ? sourceProperty.GetString()
                        : "cheer";
                _logService.LogInfo($"Bits received: {bits} ({source}).");
                BitsCheered?.Invoke(this, bits);
            }
            else
            {
                _logService.LogWarning($"{type} event had no numeric bits field.");
            }

            return;
        }

        if (string.Equals(type, "channel.poll.end", StringComparison.OrdinalIgnoreCase))
        {
            var winner = GetPollWinner(eventData);
            _logService.LogInfo($"Poll ended, winner: {winner ?? "(none)"}.");
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

    private async Task ReconnectEventSubAsync(string reconnectUrl)
    {
        try
        {
            _cts?.Cancel();
            var cts = new CancellationTokenSource();
            _cts = cts;
            _socket?.Dispose();
            var socket = new ClientWebSocket();
            _socket = socket;
            await socket.ConnectAsync(new Uri(reconnectUrl), cts.Token);
            _reconnectDelaySeconds = 2;
            _ = Task.Run(() => ReceiveEventSubLoopAsync(socket, cts.Token));
        }
        catch (Exception ex)
        {
            _logService.LogError("EventSub reconnect URL failed; starting a fresh connection.", ex);
            await ScheduleReconnectAsync();
        }
    }

    // Retries with backoff until a connect sticks or a manual connect/disconnect
    // cancels _cts. It used to try once, so a single failed attempt (e.g. Wi-Fi
    // still down) ended reconnects for the rest of the stream.
    private async Task ScheduleReconnectAsync()
    {
        while (_cts is { IsCancellationRequested: false } current)
        {
            var delay = TimeSpan.FromSeconds(_reconnectDelaySeconds);
            _reconnectDelaySeconds = Math.Min(_reconnectDelaySeconds * 2, 60);
            _logService.LogInfo($"EventSub reconnecting in {delay.TotalSeconds:0}s.");
            try
            {
                await Task.Delay(delay, current.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await ConnectEventSubAsync();
                return;
            }
            catch (Exception ex)
            {
                _logService.LogError("EventSub reconnect failed.", ex);
                StatusChanged?.Invoke(this, $"Twitch disconnected: {ex.Message}");
            }
        }
    }

    // EventSub delivers at least once, so the same notification can arrive twice
    // (e.g. around a reconnect); applying it again would add the time twice.
    // The set outlives individual sockets on purpose.
    private bool TryMarkMessageSeen(string messageId)
    {
        lock (_seenMessagesGate)
        {
            if (!_seenMessageIds.Add(messageId))
            {
                return false;
            }

            _seenMessageOrder.Enqueue(messageId);
            if (_seenMessageOrder.Count > MaxRememberedMessageIds)
            {
                _seenMessageIds.Remove(_seenMessageOrder.Dequeue());
            }

            return true;
        }
    }

    private bool IsLoopbackEventSub()
    {
        return Uri.TryCreate(_eventSubWebSocketUrl, UriKind.Absolute, out var uri) && uri.IsLoopback;
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaxLoggedBodyChars ? text : text[..MaxLoggedBodyChars] + "...";
    }

    private const string TokenKey = "JohnnyTimerEventSubWPF.TwitchToken";
}
