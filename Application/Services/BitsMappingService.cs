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
    }

    public void RemoveMapping(TwitchBitsMapping mapping)
    {
        _mappings.Remove(mapping);
    }

    public TwitchBitsMapping? TryGetMapping(int bits)
    {
        return _mappings.FirstOrDefault(item => item.Bits == bits);
    }
}
