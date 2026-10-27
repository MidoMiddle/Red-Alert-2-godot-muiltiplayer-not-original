using System.Diagnostics;
using System.Globalization;
using Rts.Data;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>A match of a matrix: its number from 0, its pair, map and seed, and whether A and B swap starts.</summary>
public sealed record ArenaJob(int Number, ArenaPair Pair, MapSpec Map, ulong Seed, bool Swap);

/// <summary>
/// Runs a matrix (docs/implementation/M8_ai_difficulty.md, 4.10, step 9): every match on its own simulation, as many at once as
/// <c>jobs</c> lets — the matches share nothing, so the results do not depend on it — then keeps and checks the replays the matrix asks for,
/// with the journals of their bots, and writes <c>results.csv</c>, <c>timings.csv</c> and <c>summary.md</c>.
/// </summary>
public static class ArenaRunner
{
    /// <summary>The matches of <paramref name="config"/> in order: pair by pair, map by map, <see cref="ArenaConfig.Games"/> each.</summary>
    public static List<ArenaJob> Jobs(ArenaConfig config)
    {
        var jobs = new List<ArenaJob>(config.Total);
        foreach (var pair in config.Pairs)
        {
            foreach (var map in config.Maps)
            {
                for (var game = 0; game < config.Games; game++)
                {
                    jobs.Add(new ArenaJob(jobs.Count, pair, map, config.BaseSeed + (ulong)jobs.Count, config.SwapSides && game % 2 == 1));
                }
            }
        }

        return jobs;
    }

    /// <summary>Plays the matrix into <paramref name="folder"/>; 0 when every kept replay matches, else 1.</summary>
    public static int Run(ArenaConfig config, GameData data, string folder, int parallel, TextWriter output)
    {
        Directory.CreateDirectory(folder);
        var jobs = Jobs(config);
        var outcomes = new MatchOutcome[jobs.Count];
        var clock = Stopwatch.StartNew();
        output.WriteLine($"{config.Pairs.Length} pairs × {config.Maps.Length} maps × {config.Games} matches = {jobs.Count}, up to {Clock(config.MaxTicks)}, {parallel} at once");
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = parallel }, job =>
        {
            var outcome = ArenaMatch.Play(data, job.Map.For(job.Seed, data.Rules.MapTypes), job.Map.Label, job.Pair.A, job.Pair.B, job.Number, job.Seed, job.Swap, config.MaxTicks);
            outcomes[job.Number] = outcome;
            lock (output)
            {
                output.WriteLine(
                    $"game {job.Number + 1}, {job.Pair.A} against {job.Pair.B} on {job.Map.Label}, seed {job.Seed}: "
                    + (outcome.Winner == "draw" ? "draw" : $"{outcome.Winner.ToUpperInvariant()} won") + $" at {Clock(outcome.EndTick)}");
            }
        });

        var kept = Kept(config.Save, outcomes);
        var checks = new SortedDictionary<int, string>();
        foreach (var (game, _) in kept)
        {
            var outcome = outcomes[game];
            var file = Path.Combine(folder, $"game-{game + 1:0000}.rtsreplay");
            File.WriteAllBytes(file, ReplayFormat.Write(outcome.Replay));
            var journals = new Dictionary<PlayerId, string>();
            checks[game] = ArenaMatch.Check(
                ReplayFormat.Read(File.ReadAllBytes(file), data.Rules.MapTypes), data.Rules, journals);
            foreach (var (player, journal) in journals)
            {
                File.WriteAllText(Path.Combine(folder, $"game-{game + 1:0000}-p{player.Value}.log"), journal);
            }
        }

        File.WriteAllLines(Path.Combine(folder, "results.csv"), [string.Join(",", ArenaCommand.ResultColumns), .. outcomes.Select(o => Row(o, checks))]);
        File.WriteAllLines(Path.Combine(folder, "timings.csv"), [string.Join(",", TimingColumns), .. outcomes.Select(Timing)]);
        clock.Stop();
        File.WriteAllText(Path.Combine(folder, "summary.md"), ArenaSummary.Write(config, outcomes, kept, checks, clock.Elapsed));
        var mismatches = checks.Values.Count(check => check != "match");
        output.WriteLine($"{jobs.Count} matches in {clock.Elapsed.TotalSeconds:0} s; {checks.Count} replays kept, {mismatches} not matching; "
                         + $"results in {Path.Combine(folder, "summary.md")}");
        return mismatches == 0 ? 0 : 1;
    }

    /// <summary>
    /// The matches whose replays <paramref name="save"/> keeps, with why: every one for <c>all</c>, the draws, the upsets, the longest N
    /// — of two as long the first.
    /// </summary>
    public static SortedDictionary<int, List<string>> Kept(ReplaySelection save, IReadOnlyList<MatchOutcome> outcomes)
    {
        var kept = new SortedDictionary<int, List<string>>();
        void Keep(int game, string reason)
        {
            if (!kept.TryGetValue(game, out var reasons))
            {
                reasons = [];
                kept[game] = reasons;
            }

            reasons.Add(reason);
        }

        foreach (var outcome in outcomes)
        {
            if (save.All)
            {
                Keep(outcome.Game, "all");
            }

            if (save.Draws && outcome.Winner == "draw")
            {
                Keep(outcome.Game, "draw");
            }

            if (save.Upsets && outcome.Upset)
            {
                Keep(outcome.Game, "upset");
            }
        }

        foreach (var outcome in outcomes.OrderByDescending(o => o.EndTick).ThenBy(o => o.Game).Take(save.Longest))
        {
            Keep(outcome.Game, "longest");
        }

        return kept;
    }

    public static readonly string[] TimingColumns =
        ["game", "a_ai_ms_per_turn", "a_ai_ms_longest", "a_slow_turns", "b_ai_ms_per_turn", "b_ai_ms_longest", "b_slow_turns", "match_seconds"];

    private static string Row(MatchOutcome outcome, SortedDictionary<int, string> checks)
    {
        static string Side(SideResult side) => string.Join(
            ",",
            side.UnitsBuilt, side.UnitsLost, side.BuildingsBuilt, side.BuildingsLost, side.Harvested, side.Spent, side.FirstAttack,
            string.Join(";", side.UnitsByType.Select(kind => $"{kind.Key}:{kind.Value}")), side.HarvestersKilled);

        return string.Join(
            ",",
            outcome.Game + 1, outcome.Map, outcome.Seed, outcome.A.Profile, outcome.B.Profile, outcome.A.StartIndex, outcome.B.StartIndex,
            outcome.Winner, outcome.EndTick, Clock(outcome.EndTick), Side(outcome.A), Side(outcome.B), checks.GetValueOrDefault(outcome.Game, "-"));
    }

    private static string Timing(MatchOutcome outcome)
    {
        static string Ms(TimeSpan time) => time.TotalMilliseconds.ToString("0.000", CultureInfo.InvariantCulture);
        static string Side(SideResult side) =>
            $"{Ms(side.AiTurns == 0 ? TimeSpan.Zero : side.AiTotal / side.AiTurns)},{Ms(side.AiLongest)},{side.SlowTurns}";

        return $"{outcome.Game + 1},{Side(outcome.A)},{Side(outcome.B)},{outcome.Wall.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    public static string Clock(int tick) => $"{tick / ArenaMatch.TicksPerMinute}:{tick % ArenaMatch.TicksPerMinute / 15:00}";
}
