using System.Diagnostics;
using Rts.AI;
using Rts.Data;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>What one side did in a match of the Arena.</summary>
public sealed record SideResult(
    BotProfile Profile, int StartIndex, int UnitsBuilt, int UnitsLost, int BuildingsBuilt, int BuildingsLost, int Harvested, int Spent, int FirstAttack,
    TimeSpan AiTotal, TimeSpan AiLongest, int AiTurns);

/// <summary>A match of the Arena: who won (<c>a</c>, <c>b</c> or <c>draw</c>), on which tick it ended, both sides, its replay.</summary>
public sealed record MatchOutcome(int Game, string Map, ulong Seed, string Winner, int EndTick, SideResult A, SideResult B, Replay Replay, TimeSpan Wall);

/// <summary>
/// One match of bots against bots (docs/implementation/M8_ai_difficulty.md, 4.10, step 4): A in slot 1, B in slot 2, every seat a bot, as
/// fast as the machine goes, no window, until a winner or the time is up — then a draw. On an odd game A and B swap starts. The replay
/// keeps the match as it was; the bots are those of the game, <see cref="MatchBots"/>.
/// </summary>
public static class ArenaMatch
{
    public const int TicksPerMinute = 900;

    public static MatchOutcome Play(GameData data, MapDefinition map, BotProfile a, BotProfile b, int game, ulong seed, int minutes)
    {
        if (map.Starts.Length < 2)
        {
            throw new ArenaException($"the map '{map.Id}' has {map.Starts.Length} starts: a match needs two");
        }

        var (startA, startB) = game % 2 == 0 ? (map.Starts[0], map.Starts[1]) : (map.Starts[1], map.Starts[0]);
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
        var wall = Stopwatch.StartNew();
        while (simulation.World.Result is null && simulation.CurrentTick < minutes * TicksPerMinute)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        wall.Stop();
        var result = simulation.World.Result;
        var winner = result is null || result.Winners.IsEmpty ? "draw" : result.Winners.Contains(new PlayerId(1)) ? "a" : "b";
        return new MatchOutcome(
            game, map.Id, seed, winner, result?.Tick ?? simulation.CurrentTick,
            Side(simulation.World, bots[0], a, startA.Slot), Side(simulation.World, bots[1], b, startB.Slot), recorder.Build(), wall.Elapsed);
    }

    /// <summary>
    /// Plays <paramref name="replay"/> back without its bots and with them run again with the personalities of
    /// <paramref name="personalityOf"/>: <c>match</c> when both give the recorded checksums, else what went wrong, in one word or two.
    /// </summary>
    public static string Check(Replay replay, GameRules rules, Func<PlayerId, string> personalityOf)
    {
        var plain = ReplayPlayer.Verify(replay, rules);
        if (plain.Status != ReplayStatus.Match)
        {
            return $"without-bots:{Word(plain)}";
        }

        var setup = replay.Settings.Match ?? throw new ArenaException("the replay has no match setup: it is not a match of the Arena");
        var withBots = ReplayPlayer.VerifyWithBots(replay, rules, player => MatchBots.Create(setup, player, rules, personalityOf(player.Id)));
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

    private static SideResult Side(GameWorld world, TimedBot bot, BotProfile profile, int start)
    {
        var stats = world.Stats.Of(bot.Player);
        return new SideResult(
            profile, start, stats.UnitsBuilt, stats.UnitsLost, stats.BuildingsBuilt, stats.BuildingsLost, stats.CreditsHarvested, stats.CreditsSpent,
            bot.FirstAttack, bot.Total, bot.Longest, bot.Turns);
    }
}
