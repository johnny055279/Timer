using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Timer.Application.Interfaces;
using Timer.Application.Models;

namespace Timer.Infrastructure.Logging;

// Pushes the in-memory log to a Loki-compatible endpoint (e.g. Grafana Alloy's
// loki.source.api) only when the user asks to. The endpoint is injected at
// build time (Timer.csproj: LogUploadUrl / TIMER_LOG_UPLOAD_URL) so it never
// lands in the public repo; builds without it simply hide the upload button.
public sealed class LokiLogUploader : ILogUploader
{
    private const string UploadUrlMetadataKey = "LogUploadUrl";
    // No 0/O/1/I/L so the ID survives being read aloud or retyped from a screenshot.
    private const string ReportIdAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int ReportIdLength = 6;
    private const int MaxUploadChars = 512 * 1024;
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly Uri? _endpoint;

    public LokiLogUploader(HttpClient httpClient)
        : this(httpClient, ReadConfiguredEndpoint())
    {
    }

    public LokiLogUploader(HttpClient httpClient, string? endpoint)
    {
        _httpClient = httpClient;
        _endpoint = TryParseEndpoint(endpoint);
    }

    public bool IsConfigured => _endpoint is not null;

    public async Task<string> UploadAsync(IReadOnlyList<LogEntry> entries)
    {
        if (_endpoint is null)
        {
            throw new InvalidOperationException("Log upload is not configured in this build.");
        }

        var reportId = RandomNumberGenerator.GetString(ReportIdAlphabet, ReportIdLength);
        var version = typeof(LokiLogUploader).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        var payload = new
        {
            streams = new[]
            {
                new
                {
                    stream = new Dictionary<string, string>
                    {
                        ["app"] = "timer",
                        ["version"] = version,
                        ["report_id"] = reportId
                    },
                    values = BuildValues(entries)
                }
            }
        };

        using var cts = new CancellationTokenSource(UploadTimeout);
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(_endpoint, content, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Upload rejected: {(int)response.StatusCode} {response.StatusCode}");
        }

        return reportId;
    }

    // Loki wants [unix-nanoseconds, line] pairs in ascending order. Keep the newest
    // entries that fit the size cap, and nudge equal timestamps apart so lines
    // logged in the same tick keep their order in Grafana.
    private static List<string[]> BuildValues(IReadOnlyList<LogEntry> entries)
    {
        var kept = new List<LogEntry>();
        var totalChars = 0;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            totalChars += entries[i].Text.Length;
            if (totalChars > MaxUploadChars && kept.Count > 0)
            {
                break;
            }

            kept.Add(entries[i]);
        }

        kept.Reverse();
        var values = new List<string[]>(kept.Count);
        var previous = long.MinValue;
        foreach (var entry in kept)
        {
            var nanoseconds = (entry.Timestamp.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
            if (nanoseconds <= previous)
            {
                nanoseconds = previous + 1;
            }

            previous = nanoseconds;
            values.Add([nanoseconds.ToString(CultureInfo.InvariantCulture), $"[{entry.Level}] {entry.Text}"]);
        }

        return values;
    }

    private static string? ReadConfiguredEndpoint()
    {
        return typeof(LokiLogUploader).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == UploadUrlMetadataKey)
            ?.Value;
    }

    // Same rule as the EventSub URL: https only, plain http solely to loopback
    // for local testing, so logs never cross the internet unencrypted.
    private static Uri? TryParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback
            ? uri
            : null;
    }
}
