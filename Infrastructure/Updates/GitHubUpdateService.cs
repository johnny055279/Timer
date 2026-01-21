using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Timer.Application.Interfaces;
using Timer.Application.Models;

namespace Timer.Infrastructure.Updates;

public sealed class GitHubUpdateService : IUpdateService
{
    private readonly HttpClient _client;
    private readonly string _repo;

    public GitHubUpdateService(string repo, string userAgent)
    {
        _repo = repo;
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    public async Task<UpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{_repo}/releases/latest";
        using var response = await _client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(payload);
        if (!doc.RootElement.TryGetProperty("tag_name", out var tagNameElement))
        {
            return null;
        }

        var versionText = tagNameElement.GetString() ?? string.Empty;
        if (versionText.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            versionText = versionText[1..];
        }

        var dashIndex = versionText.IndexOf('-');
        if (dashIndex >= 0)
        {
            versionText = versionText[..dashIndex];
        }

        if (!Version.TryParse(versionText, out var latestVersion))
        {
            return null;
        }

        var releaseUrl = doc.RootElement.TryGetProperty("html_url", out var htmlElement)
            ? htmlElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(releaseUrl))
        {
            releaseUrl = $"https://github.com/{_repo}/releases/latest";
        }

        return new UpdateInfo(latestVersion, releaseUrl);
    }
}
