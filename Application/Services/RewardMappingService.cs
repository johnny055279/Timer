using System.Collections.Generic;
using System.Linq;
using Timer.Application.Interfaces;
using Timer.Domain.Entities;

namespace Timer.Application.Services;

public sealed class RewardMappingService : IRewardMappingService
{
    private readonly List<TwitchRewardMapping> _mappings = new();

    public IReadOnlyList<TwitchRewardMapping> Mappings => _mappings;

    public void AddOrUpdateMapping(TwitchReward reward, int minutes)
    {
        var existing = _mappings.FirstOrDefault(item =>
            string.Equals(item.RewardId, reward.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _mappings.Remove(existing);
        }

        _mappings.Add(new TwitchRewardMapping(reward.Id, reward.Title, minutes));
    }

    public void RemoveMapping(TwitchRewardMapping mapping)
    {
        _mappings.Remove(mapping);
    }

    public int? TryGetDelta(string rewardId)
    {
        var mapping = _mappings.FirstOrDefault(item =>
            string.Equals(item.RewardId, rewardId, StringComparison.OrdinalIgnoreCase));
        return mapping?.Minutes;
    }
}
