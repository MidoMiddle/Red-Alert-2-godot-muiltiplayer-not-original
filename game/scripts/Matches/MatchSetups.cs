using System;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;
using Rts.Simulation.Matches;

namespace Rts.Client.Matches;

/// <summary>
/// The matches the client sets up until the skirmish screen of M6 does (docs/implementation/M5_first_bot.md, step 2): one against one —
/// the person on the first start, a «Medium» bot on the second, the other starts of the map closed. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public static class MatchSetups
{
    /// <summary>Whether <paramref name="map"/> can be played as a match: it has two starts or more.</summary>
    public static bool IsPlayable(MapDefinition map) => map.Starts.Length >= 2;

    /// <summary>The person (blue, team 1) against a «Medium» bot (red, team 2) on <paramref name="map"/>, both playing <paramref name="faction"/>.</summary>
    public static MatchSetup OneOnOne(MapDefinition map, string faction, ulong seed)
    {
        if (!IsPlayable(map))
        {
            throw new ArgumentException($"The map '{map.Id}' has {map.Starts.Length} starts: a match needs two.", nameof(map));
        }

        var person = map.Starts[0];
        var bot = map.Starts[1];
        return new MatchSetup(
            map.Id,
            seed,
            new MatchOptions(),
            [
                new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, faction, Color: 0, Team: 1, person.Slot, person.Cell),
                new MatchPlayer(2, PlayerKind.Ai, AiDifficulty.Medium, faction, Color: 1, Team: 2, bot.Slot, bot.Cell),
            ],
            [.. map.Starts.Select(start => start.Cell)]);
    }

    /// <summary>A new seed for a match: the client may take chance, the replay keeps the number.</summary>
    public static ulong NewSeed() => (ulong)Random.Shared.NextInt64(1, long.MaxValue);
}
