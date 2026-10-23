using System.Collections.Immutable;
using System.Diagnostics;
using Rts.AI;
using Rts.Data;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Events;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>What one side did in a match of the Arena.</summary>
/// <param name="UnitsByType">The army units it built by their id, the harvesters aside (step 9).</param>
/// <param name="HarvestersKilled">The harvesters of the enemy it destroyed (step 9).</param>
/// <param name="SlowTurns">Its turns longer than <see cref="TimedBot.SlowTurn"/> (step 9).</param>
public sealed record SideResult(
    BotProfile Profile, int StartIndex, int UnitsBuilt, int UnitsLost, int BuildingsBuilt, int BuildingsLost, int Harvested, int Spent, int FirstAttack,
    ImmutableSortedDictionary<string, int> UnitsByType, int HarvestersKilled, TimeSpan AiTotal, TimeSpan AiLongest, int AiTurns, int SlowTurns);

/// <summary>A match of the Arena: who won (<c>a</c>, <c>b</c> or <c>draw</c>), on which tick it ended, both sides, its replay.</summary>
public sealed record MatchOutcome(int Game, string Map, ulong Seed, string Winner, int EndTick, SideResult A, SideResult B, Replay Replay, TimeSpan Wall)
{
    /// <summary>Whether a lower level beat a higher one.</summary>
    public bool Upset => Winner == "a" ? A.Profile.Rank < B.Profile.Rank : Winner == "b" && B.Profile.Rank < A.Profile.Rank;
}

/// <summary>
/// One match of bots against bots (docs/implementation/M8_ai_difficulty.md, 4.10, steps 4 and 9): A in slot 1, B in slot 2, every seat a
/// bot, as fast as the machine goes, no window, until a winner or the time is up — then a draw. With <c>swap</c> A and B swap starts.
/// The replay keeps the match as it was; the bots are those of the game, <see cref="MatchBots"/>.
/// </summary>
public static class ArenaMatch
{
    public const int TicksPerMinute = 900;

    /// <summary>How often the harvesters on the map are looked up, in ticks: one made and destroyed between two looks is not counted.</summary>
    private const int HarvesterLook = 15;

    public static MatchOutcome Play(GameData data, MapDefinition map, string mapLabel, BotProfile a, BotProfile b, int game, ulong seed, bool swap, int maxTicks)
    {
        if (map.Starts.Length < 2)
        {
            throw new ArenaException($"the map '{mapLabel}' has {map.Starts.Length} starts: a match needs two");
        }

        var (startA, startB) = swap ? (map.Starts[1], map.Starts[0]) : (map.Starts[0], map.Starts[1]);
        var setup = new MatchSetup(
            map.Id,
            seed,
            new MatchOptions(),
            [
                new MatchPlayer(1, PlayerKind.Ai, a.Level, a.Faction, Color: 0, Team: 1, startA.Slot, startA.Cell),
                new MatchPlayer(2, PlayerKind.Ai, b.Level, b.Faction, Color: 1, Team: 2, startB.Slot, startB.Cell),
            ],
            [.. map.Starts.Select(start => start.Cell)]);
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, map.ToMapGrid()), data.Rules);
        var bots = setup.Players.Select(player => Bot(setup, player, data, player.Slot == 1 ? a : b)).ToList();
        foreach (var bot in bots)
        {
            simulation.AttachAi(bot);
        }

        var recorder = new ReplayRecorder(simulation);
        var built = new[] { new SortedDictionary<string, int>(StringComparer.Ordinal), new SortedDictionary<string, int>(StringComparer.Ordinal) };
        var killed = new int[2];
        var harvesters = new SortedDictionary<int, PlayerId>();
        var wall = Stopwatch.StartNew();
        while (simulation.World.Result is null && simulation.CurrentTick < maxTicks)
        {
            if (simulation.CurrentTick % HarvesterLook == 0)
            {
                Harvesters(simulation.World, harvesters);
            }

            simulation.Step();
            recorder.AfterStep();
            Count(simulation, built, killed, harvesters);
        }

        wall.Stop();
        var result = simulation.World.Result;
        var winner = result is null || result.Winners.IsEmpty ? "draw" : result.Winners.Contains(new PlayerId(1)) ? "a" : "b";
        return new MatchOutcome(
            game, mapLabel, seed, winner, result?.Tick ?? simulation.CurrentTick,
            Side(simulation.World, bots[0], a, startA.Slot, built[0], killed[0]), Side(simulation.World, bots[1], b, startB.Slot, built[1], killed[1]),
            recorder.Build(), wall.Elapsed);
    }

    /// <summary>
    /// Plays <paramref name="replay"/> back without its bots and with them run again with the personalities of
    /// <paramref name="personalityOf"/>: <c>match</c> when both give the recorded checksums, else what went wrong, in one word or two. With
    /// <paramref name="journals"/> the bots run again write their journals into it, by player.
    /// </summary>
    public static string Check(Replay replay, GameRules rules, Func<PlayerId, string> personalityOf, IDictionary<PlayerId, string>? journals = null)
    {
        var plain = ReplayPlayer.Verify(replay, rules);
        if (plain.Status != ReplayStatus.Match)
        {
            return $"without-bots:{Word(plain)}";
        }

        var setup = replay.Settings.Match ?? throw new ArenaException("the replay has no match setup: it is not a match of the Arena");
        var bots = new List<JournalBot>();
        var withBots = ReplayPlayer.VerifyWithBots(replay, rules, player =>
        {
            var bot = new JournalBot((AiPlayer)MatchBots.Create(setup, player, rules, personalityOf(player.Id)));
            bots.Add(bot);
            return bot;
        });
        if (journals is not null)
        {
            foreach (var bot in bots)
            {
                journals[bot.Player] = bot.Journal.ToString();
            }
        }

        return withBots.Status == ReplayStatus.Match ? "match" : $"with-bots:{Word(withBots)}";
    }

    private static string Word(ReplayVerification verification) =>
        verification.Status == ReplayStatus.Desync ? $"desync@{verification.Expected?.Tick}" : verification.Status.ToString().ToLowerInvariant();

    private static TimedBot Bot(MatchSetup setup, MatchPlayer player, GameData data, BotProfile profile)
    {
        try
        {
            return new TimedBot((AiPlayer)MatchBots.Create(setup, player, data.Rules, profile.Personality));
        }
        catch (ArgumentException e)
        {
            throw new ArenaException($"{profile} cannot play: {e.Message}");
        }
    }

    /// <summary>The harvesters on the map and their owners, so that the one destroyed — gone from the world by then — is known (step 9).</summary>
    internal static void Harvesters(GameWorld world, SortedDictionary<int, PlayerId> harvesters)
    {
        harvesters.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { Harvester: not null, IsAlive: true } unit)
            {
                harvesters[unit.Id.Value] = unit.Owner;
            }
        }
    }

    /// <summary>The units each side built by kind and the harvesters of the other it destroyed, from the events of the tick.</summary>
    internal static void Count(GameSimulation simulation, SortedDictionary<string, int>[] built, int[] killed, SortedDictionary<int, PlayerId> harvesters)
    {
        foreach (var simEvent in simulation.LastTickEvents)
        {
            switch (simEvent)
            {
                case UnitProduced produced when produced.Player.Value is 1 or 2 && simulation.World.TryGet(produced.Unit, out var entity) && entity is Unit unit:
                    if (unit.Harvester is not null)
                    {
                        harvesters[unit.Id.Value] = unit.Owner;
                        break;
                    }

                    var kinds = built[produced.Player.Value - 1];
                    kinds[unit.Archetype.Id] = kinds.GetValueOrDefault(unit.Archetype.Id) + 1;

                    break;
                case EntityDestroyed destroyed when harvesters.TryGetValue(destroyed.Entity.Value, out var owner)
                                                     && destroyed.KillerOwner.Value is 1 or 2 && destroyed.KillerOwner != owner:
                    killed[destroyed.KillerOwner.Value - 1]++;
                    harvesters.Remove(destroyed.Entity.Value);
                    break;
            }
        }
    }

    private static SideResult Side(GameWorld world, TimedBot bot, BotProfile profile, int start, SortedDictionary<string, int> built, int killed)
    {
        var stats = world.Stats.Of(bot.Player);
        return new SideResult(
            profile, start, stats.UnitsBuilt, stats.UnitsLost, stats.BuildingsBuilt, stats.BuildingsLost, stats.CreditsHarvested, stats.CreditsSpent,
            bot.FirstAttack, built.ToImmutableSortedDictionary(StringComparer.Ordinal), killed, bot.Total, bot.Longest, bot.Turns, bot.SlowTurns);
    }
}
