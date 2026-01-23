using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timer.Domain.Entities;

namespace Timer.Application.Interfaces;

public interface ITwitchClient
{
    string EventSubWebSocketUrl { get; set; }

    event EventHandler<string>? StatusChanged;
    event EventHandler<(string UserCode, string VerifyUrl)>? DeviceCodeReceived;
    event EventHandler<string>? RewardRedeemed;
    event EventHandler<int>? BitsCheered;
    event EventHandler<string>? PollEnded;

    Task ConnectAsync();
    Task<IReadOnlyList<TwitchReward>> LoadRewardsAsync();
    Task StartPollAsync(string title, int durationSeconds);
    Task DisconnectAsync();
    void SimulateRewardRedeemed(string rewardId);
    void SimulateBitsCheered(int bits);
    Task<bool> TryReconnectAsync();
    void NotifyStatus(string status);
    Task<(bool HasToken, DateTimeOffset? ExpiresAt, bool HasRequiredScopes)> GetTokenStatusAsync();
}
