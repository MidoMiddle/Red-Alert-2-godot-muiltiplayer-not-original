using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;

namespace Rts.Client.Matches;

/// <summary>
/// The matches the client sets up until the skirmish screen of M6 does (docs/implementation/M5_first_bot.md, step 2): one against one —
/// the person on the first start, a «Medium» bot on the second, the other starts of the map closed; or, on a map of four starts or more,
/// two against two (M6, A6) — the person and a «Medium» bot ally on the first two starts against two «Medium» bots on the next two.
/// Both without the fog of war, as the skirmish screen by default (owner's decision): what the person explored stays in sight.
/// Every player plays a country of its faction drawn from the seed, when <c>countries</c> are given (RA2: a player always plays a country,
/// the owner, 2026-10-26); without them, the whole faction, as the matches of the bots' tests are pinned. Pure, tested in Rts.Data.Tests.
/// </summary>
public static class MatchSetups
{
    /// <summary>Whether <paramref name="map"/> can be played as a match: it has two starts or more.</summary>
    public static bool IsPlayable(MapDefinition map) => map.Starts.Length >= 2;

    /// <summary>The person (blue, team 1) against a «Medium» bot (red, team 2) on <paramref name="map"/>, both playing <paramref name="faction"/>.</summary>
    public static MatchSetup OneOnOne(MapDefinition map, string faction, ulong seed, IReadOnlyList<CountryDef>? countries = null)
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
            new MatchOptions(FogOfWar: false),
            [
                new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, faction, Color: 0, Team: 1, person.Slot, person.Cell),
                new MatchPlayer(2, PlayerKind.Ai, AiDifficulty.Medium, faction, Color: 1, Team: 2, bot.Slot, bot.Cell),
            ],
            [.. map.Starts.Select(start => start.Cell)]).WithCountries(countries);
    }

    /// <summary>Whether <paramref name="map"/> can be played two against two: it has four starts or more.</summary>
    public static bool IsPlayableTwoOnTwo(MapDefinition map) => map.Starts.Length >= 4;

    /// <summary>
    /// The person (blue) and a «Medium» bot ally on the first two starts, team 1, against two «Medium» bots on the next two, team 2, on
    /// <paramref name="map"/>, all playing <paramref name="faction"/>; each its own colour.
    /// </summary>
    public static MatchSetup TwoOnTwo(MapDefinition map, string faction, ulong seed, IReadOnlyList<CountryDef>? countries = null)
    {
        if (!IsPlayableTwoOnTwo(map))
        {
            throw new ArgumentException($"The map '{map.Id}' has {map.Starts.Length} starts: two against two needs four.", nameof(map));
        }

        return new MatchSetup(
            map.Id,
            seed,
            new MatchOptions(FogOfWar: false),
            [
                .. map.Starts.Take(4).Select((start, i) => new MatchPlayer(
                    i + 1,
                    i == 0 ? PlayerKind.Human : PlayerKind.Ai,
                    AiDifficulty.Medium,
                    faction,
                    Color: i,
                    Team: i < 2 ? 1 : 2,
                    start.Slot,
                    start.Cell)),
            ],
            [.. map.Starts.Select(start => start.Cell)]).WithCountries(countries);
    }

    // Every player a country of its faction, drawn from the seed of the match; none without countries.
    private static MatchSetup WithCountries(this MatchSetup setup, IReadOnlyList<CountryDef>? countries)
    {
        if (countries is null)
        {
            return setup;
        }

        var random = new Rts.Simulation.Random.DeterministicRandom(setup.Seed, CountryStream);
        return setup with
        {
            Players =
            [
                .. setup.Players.Select(player => countries.Where(country => country.Faction == player.Faction).Select(country => country.Id).ToList() is { Count: > 0 } own
                    ? player with { Country = own[random.NextInt(own.Count)] }
                    : player),
            ],
        };
    }

    // The stream of the seed the countries are drawn from ("COUNT" as in the skirmish).
    private const ulong CountryStream = 0x434F_554E_5400;

    /// <summary>A new seed for a match: the client may take chance, the replay keeps the number.</summary>
    public static ulong NewSeed() => (ulong)Random.Shared.NextInt64(1, long.MaxValue);
}
