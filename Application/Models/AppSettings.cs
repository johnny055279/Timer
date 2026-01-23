using System.Collections.Generic;

namespace Timer.Application.Models;

public sealed class AppSettings
{
    public string CountdownTitle { get; set; } = string.Empty;
    public string CounterTitle { get; set; } = string.Empty;
    public string EventSubWebSocketUrl { get; set; } = string.Empty;
    public bool UseDebugEventSub { get; set; }
    public List<TwitchRewardMappingSetting> RewardMappings { get; set; } = new();
    public List<TwitchRewardMappingSetting> DebugRewardMappings { get; set; } = new();
    public List<TwitchBitsMappingSetting> BitsMappings { get; set; } = new();
    public List<TwitchBitsMappingSetting> DebugBitsMappings { get; set; } = new();
}

public sealed record TwitchRewardMappingSetting(
    string RewardId,
    string Title,
    string Target,
    string Action,
    int Amount);

public sealed record TwitchBitsMappingSetting(
    int Bits,
    string Target,
    string Action,
    int Amount);
