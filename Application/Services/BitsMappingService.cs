using System;
using System.Collections.Generic;
using System.Linq;
using Timer.Application.Interfaces;
using Timer.Domain.Entities;

namespace Timer.Application.Services;

public sealed class BitsMappingService : IBitsMappingService
{
    private readonly List<TwitchBitsMapping> _mappings = new();

    public IReadOnlyList<TwitchBitsMapping> Mappings => _mappings;

    public void AddOrUpdateMapping(int bits, TwitchRewardTarget target, TwitchRewardAction action, int amount)
    {
        var existing = _mappings.FirstOrDefault(item => item.Bits == bits);
        if (existing is not null)
        {
            _mappings.Remove(existing);
        }

        _mappings.Add(new TwitchBitsMapping(bits, target, action, amount));
        _mappings.Sort((left, right) => left.Bits.CompareTo(right.Bits));
    }

    public void ReplaceMappings(IEnumerable<TwitchBitsMapping> mappings)
    {
        _mappings.Clear();
        foreach (var mapping in mappings)
        {
            if (mapping.Bits > 0)
            {
                _mappings.Add(mapping);
            }
        }

        _mappings.Sort((left, right) => left.Bits.CompareTo(right.Bits));
    }

    public void RemoveMapping(TwitchBitsMapping mapping)
    {
        _mappings.Remove(mapping);
    }

    // Threshold match: the highest mapping at or below the amount wins, so with
    // 100 and 500 configured a 250-bit cheer hits 100 and a 600-bit one hits 500.
    public TwitchBitsMapping? TryGetMapping(int bits)
    {
        return _mappings.Where(item => item.Bits <= bits).MaxBy(item => item.Bits);
    }
}
