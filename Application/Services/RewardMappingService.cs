using System;
using System.Collections.Generic;
using System.Linq;
using Timer.Application.Interfaces;
using Timer.Domain.Entities;

namespace Timer.Application.Services;

public sealed class RewardMappingService : IRewardMappingService
{
    private readonly List<TwitchRewardMapping> _mappings = new();

    public IReadOnlyList<TwitchRewardMapping> Mappings => _mappings;

    public void AddOrUpdateMapping(TwitchReward reward, TwitchRewardTarget target, TwitchRewardAction action, int minutes)
    {
        var existing = _mappings.FirstOrDefault(item =>
            string.Equals(item.RewardId, reward.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _mappings.Remove(existing);
        }

        _mappings.Add(new TwitchRewardMapping(reward.Id, reward.Title, target, action, minutes));
    }

    public void ReplaceMappings(IEnumerable<TwitchRewardMapping> mappings)
    {
        _mappings.Clear();
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.RewardId))
            {
                _mappings.Add(mapping);
            }
        }
    }

    public void RemoveMapping(TwitchRewardMapping mapping)
    {
        _mappings.Remove(mapping);
    }

    public TwitchRewardMapping? TryGetMapping(string rewardId)
    {
        var mapping = _mappings.FirstOrDefault(item =>
            string.Equals(item.RewardId, rewardId, StringComparison.OrdinalIgnoreCase));
        return mapping;
    }
}
