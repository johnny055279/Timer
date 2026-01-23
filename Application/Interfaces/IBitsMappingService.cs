using System.Collections.Generic;
using Timer.Domain.Entities;

namespace Timer.Application.Interfaces;

public interface IBitsMappingService
{
    IReadOnlyList<TwitchBitsMapping> Mappings { get; }
    void AddOrUpdateMapping(int bits, TwitchRewardTarget target, TwitchRewardAction action, int amount);
    void ReplaceMappings(IEnumerable<TwitchBitsMapping> mappings);
    void RemoveMapping(TwitchBitsMapping mapping);
    TwitchBitsMapping? TryGetMapping(int bits);
}
