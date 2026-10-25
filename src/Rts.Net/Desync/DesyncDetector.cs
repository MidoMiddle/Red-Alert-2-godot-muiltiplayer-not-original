using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.World;

namespace Rts.Net.Desync;

/// <summary>A desync: on <see cref="Tick"/> the computers' checksums differ; each player's, by id.</summary>
public sealed record Desync(int Tick, ImmutableSortedDictionary<int, ulong> Hashes)
{
    public override string ToString() => $"Desync on tick {Tick}: " + string.Join(", ", Hashes.Select(pair => $"player {pair.Key} {pair.Value:x16}"));
}

/// <summary>
/// Compares the checksums of the computers of a match (docs/implementation/M9_network.md, 4.10): once every player still in the match
/// has given its checksum of a tick, they must be one. They come in any order and any player first; one who leaves is no longer
/// awaited. The first tick they differ on is the desync, and it stays.
/// </summary>
public sealed class DesyncDetector
{
    private readonly HashSet<PlayerId> _players;
    private readonly SortedDictionary<int, Dictionary<PlayerId, ulong>> _ticks = new();

    public DesyncDetector(IEnumerable<PlayerId> players)
    {
        _players = players.ToHashSet();
    }

    /// <summary>The first desync found; null while the checksums agree.</summary>
    public Desync? Found { get; private set; }

    /// <summary>The last tick everyone's checksums agreed on; -1 before the first.</summary>
    public int LastAgreedTick { get; private set; } = -1;

    /// <summary>The checksum of <paramref name="player"/> on <paramref name="tick"/>. Returns the desync when this one shows it.</summary>
    public Desync? Report(PlayerId player, int tick, ulong hash)
    {
        if (Found is not null || !_players.Contains(player) || tick <= LastAgreedTick)
        {
            return null;
        }

        if (!_ticks.TryGetValue(tick, out var hashes))
        {
            hashes = new Dictionary<PlayerId, ulong>();
            _ticks.Add(tick, hashes);
        }

        hashes[player] = hash;
        return Settle();
    }

    /// <summary>A player left: its checksums are no longer awaited, and those it gave do not count.</summary>
    public Desync? Drop(PlayerId player)
    {
        _players.Remove(player);
        foreach (var hashes in _ticks.Values)
        {
            hashes.Remove(player);
        }

        return Settle();
    }

    // Every tick in order whose checksums are all here: agreed, or the desync.
    private Desync? Settle()
    {
        while (Found is null && _ticks.Count > 0)
        {
            var (tick, hashes) = _ticks.First();
            if (!_players.All(hashes.ContainsKey))
            {
                break;
            }

            _ticks.Remove(tick);
            if (hashes.Values.Distinct().Count() > 1)
            {
                Found = new Desync(tick, hashes.ToImmutableSortedDictionary(pair => pair.Key.Value, pair => pair.Value));
                return Found;
            }

            LastAgreedTick = tick;
        }

        return null;
    }
}
