using System.Collections.Generic;
using Timer.Domain.Entities;

namespace Timer.Application.Interfaces;

public interface IRewardMappingService
{
    IReadOnlyList<TwitchRewardMapping> Mappings { get; }
    void AddOrUpdateMapping(TwitchReward reward, TwitchRewardTarget target, TwitchRewardAction action, int minutes);
    void ReplaceMappings(IEnumerable<TwitchRewardMapping> mappings);
    void RemoveMapping(TwitchRewardMapping mapping);
    TwitchRewardMapping? TryGetMapping(string rewardId);
}
