using System.Globalization;
using System.Text;

namespace Rts.Arena;

/// <summary>
/// <c>summary.md</c> of a run (docs/implementation/M8_ai_difficulty.md, 4.10, step 9): the wins of every pair, over all its maps and map by
/// map, with the 95 % interval of Wilson; the profiles and the personalities — first attack, harvesters destroyed, the units they build;
/// the replays kept. All of it the same on every run of the same matrix but the last part, the time the bots took — the machine's.
/// </summary>
public static class ArenaSummary
{
    /// <summary>The 95 % interval of Wilson for <paramref name="wins"/> of <paramref name="games"/>: a share with a margin that holds for few games too.</summary>
    public static (double Low, double High) Wilson(int wins, int games)
    {
        if (games == 0)
        {
            return (0, 1);
        }

        const double z = 1.96;
        var share = (double)wins / games;
        var spread = 1 + z * z / games;
        var centre = (share + z * z / (2 * games)) / spread;
        var margin = z * Math.Sqrt(share * (1 - share) / games + z * z / (4.0 * games * games)) / spread;
        return (Math.Max(0, centre - margin), Math.Min(1, centre + margin));
    }

    public static string Write(
        ArenaConfig config, IReadOnlyList<MatchOutcome> outcomes, SortedDictionary<int, List<string>> kept, SortedDictionary<int, string> checks, TimeSpan wall)
    {
        var text = new StringBuilder();
        text.AppendLine("# Arena — summary").AppendLine();
        text.AppendLine(Invariant($"{outcomes.Count} matches: {config.Pairs.Length} pairs × {config.Maps.Length} maps × {config.Games}, up to {ArenaRunner.Clock(config.MaxTicks)}, seeds from {config.BaseSeed}{(config.SwapSides ? ", sides swapping starts" : "")}."));
        text.AppendLine();

        text.AppendLine("## Wins").AppendLine();
        text.AppendLine("| A | B | Map | Matches | A won | B won | Draws | A wins (95 %) | Mean length, decided | Draws, % |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var pair in config.Pairs)
        {
            var ofPair = outcomes.Where(o => o.A.Profile == pair.A && o.B.Profile == pair.B).ToList();
            Wins(text, pair, "all", ofPair);
            if (config.Maps.Length > 1)
            {
                foreach (var map in config.Maps)
                {
                    Wins(text, pair, map.Label, ofPair.Where(o => o.Map == map.Label).ToList());
                }
            }
        }

        text.AppendLine().AppendLine("## Profiles").AppendLine();
        text.AppendLine("| Profile | Sides played | First attack, mean | No attack | Harvesters destroyed, mean | Units built, most |");
        text.AppendLine("|---|---|---|---|---|---|");
        foreach (var group in Sides(outcomes).GroupBy(side => side.Profile.ToString()).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var sides = group.ToList();
            var units = sides.SelectMany(side => side.UnitsByType).GroupBy(kind => kind.Key)
                .Select(kind => (Kind: kind.Key, Count: kind.Sum(k => k.Value))).OrderByDescending(kind => kind.Count).ThenBy(kind => kind.Kind, StringComparer.Ordinal).Take(3);
            text.AppendLine(Invariant(
                $"| {group.Key} | {sides.Count} | {FirstAttack(sides)} | {sides.Count(side => side.FirstAttack < 0)} | {sides.Average(side => side.HarvestersKilled):0.0} | {string.Join(", ", units.Select(kind => $"{kind.Kind} {kind.Count}"))} |"));
        }

        text.AppendLine().AppendLine("## Personalities").AppendLine();
        text.AppendLine("| Personality | Sides played | First attack, mean | Harvesters destroyed, mean |");
        text.AppendLine("|---|---|---|---|");
        foreach (var group in Sides(outcomes).GroupBy(side => side.Profile.Personality).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var sides = group.ToList();
            text.AppendLine(Invariant($"| {group.Key} | {sides.Count} | {FirstAttack(sides)} | {sides.Average(side => side.HarvestersKilled):0.0} |"));
        }

        text.AppendLine().AppendLine("## Replays kept").AppendLine();
        if (kept.Count == 0)
        {
            text.AppendLine("None: the matrix keeps none (saveReplays).");
        }

        foreach (var (game, reasons) in kept)
        {
            var outcome = outcomes[game];
            var result = outcome.Winner == "draw" ? "draw" : $"{outcome.Winner.ToUpperInvariant()} won";
            text.AppendLine(Invariant(
                $"- game {game + 1} ({string.Join(", ", reasons)}): {outcome.A.Profile} against {outcome.B.Profile} on {outcome.Map}, {result} at {ArenaRunner.Clock(outcome.EndTick)} — replay {checks.GetValueOrDefault(game, "-")}"));
        }

        text.AppendLine().AppendLine("## Time of the bots (differs from run to run)").AppendLine();
        text.AppendLine(Invariant($"The run took {wall.TotalSeconds:0} s. The budget of 4.11: 1 ms a turn on average, 4 ms at the most; the turns over 4 ms count against it."));
        text.AppendLine();
        text.AppendLine("| Profile | Turns | Mean, ms | Longest, ms | Over 4 ms, % | Within 4.11 |");
        text.AppendLine("|---|---|---|---|---|---|");
        foreach (var group in Sides(outcomes).GroupBy(side => side.Profile.ToString()).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var turns = group.Sum(side => (long)side.AiTurns);
            var mean = turns == 0 ? 0 : group.Sum(side => side.AiTotal.TotalMilliseconds) / turns;
            var longest = group.Max(side => side.AiLongest.TotalMilliseconds);
            var slow = turns == 0 ? 0 : 100.0 * group.Sum(side => (long)side.SlowTurns) / turns;
            var within = mean <= 1 && slow <= 5;
            text.AppendLine(Invariant($"| {group.Key} | {turns} | {mean:0.000} | {longest:0.0} | {slow:0.00} | {(within ? "yes" : "**no**")} |"));
        }

        return text.ToString();
    }

    private static void Wins(StringBuilder text, ArenaPair pair, string map, List<MatchOutcome> games)
    {
        var (a, b) = (games.Count(o => o.Winner == "a"), games.Count(o => o.Winner == "b"));
        var draws = games.Count - a - b;
        var (low, high) = Wilson(a, games.Count);
        var decided = games.Where(o => o.Winner != "draw").ToList();
        var length = decided.Count == 0 ? "-" : ArenaRunner.Clock((int)decided.Average(o => o.EndTick));
        var share = games.Count == 0 ? 0 : 100.0 * a / games.Count;
        var drawShare = games.Count == 0 ? 0 : 100.0 * draws / games.Count;
        text.AppendLine(Invariant(
            $"| {pair.A} | {pair.B} | {map} | {games.Count} | {a} | {b} | {draws} | {share:0} % ({100 * low:0}–{100 * high:0}) | {length} | {drawShare:0} |"));
    }

    private static IEnumerable<SideResult> Sides(IEnumerable<MatchOutcome> outcomes) => outcomes.SelectMany(o => new[] { o.A, o.B });

    private static string FirstAttack(List<SideResult> sides)
    {
        var attacks = sides.Where(side => side.FirstAttack >= 0).ToList();
        return attacks.Count == 0 ? "-" : ArenaRunner.Clock((int)attacks.Average(side => side.FirstAttack));
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
