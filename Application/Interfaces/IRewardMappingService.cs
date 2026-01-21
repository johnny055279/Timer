using System.Collections.Generic;
using Timer.Domain.Entities;

namespace Timer.Application.Interfaces;

public interface IRewardMappingService
{
    IReadOnlyList<TwitchRewardMapping> Mappings { get; }
    void AddOrUpdateMapping(TwitchReward reward, int minutes);
    void RemoveMapping(TwitchRewardMapping mapping);
    int? TryGetDelta(string rewardId);
}
