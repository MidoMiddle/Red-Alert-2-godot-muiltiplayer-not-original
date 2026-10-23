using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.Matches;

/// <summary>How the match went for the person at the keyboard.</summary>
public enum MatchOutcome
{
    Victory,
    Defeat,
    Draw,
}

/// <summary>
/// A row of the results: a player of the match, whether it is the person at the keyboard, its team, whether it won, and the nine numbers
/// of <see cref="PlayerStats"/> the table shows, in its order. A bot shows its level and its personality as the match setup has them
/// (M8 step 13) — «random» too: the screen asks the bots which one was drawn.
/// </summary>
public sealed record SummaryRow(PlayerId Player, bool IsYou, PlayerKind Kind, int Team, bool Won, ImmutableArray<int> Numbers)
{
    public AiDifficulty Difficulty { get; init; } = AiDifficulty.Medium;

    public string Personality { get; init; } = MatchPlayer.DefaultPersonality;
}

/// <summary>
/// What the results screen shows (docs/implementation/M5_first_bot.md, 4.10, step 12), worked out from the world once the match is
/// over — or once the person at the keyboard is beaten: the outcome for the person, the length of the match, a row for every player of the match in the order of the
/// slots, and the series of the strength of every player's army. Pure — no Godot — and tested in Rts.Simulation.Tests.
/// </summary>
public sealed record MatchSummary(
    MatchOutcome Outcome, string Duration, ImmutableArray<SummaryRow> Rows, ImmutableArray<ImmutableArray<StatsSample>> Series, int DurationTicks)
{
    /// <summary>The keys of the columns of numbers, in the order of <see cref="SummaryRow.Numbers"/>.</summary>
    public static readonly ImmutableArray<string> Columns =
    [
        "UI_RESULTS_UNITS_BUILT", "UI_RESULTS_UNITS_LOST", "UI_RESULTS_UNITS_DESTROYED",
        "UI_RESULTS_BUILDINGS_BUILT", "UI_RESULTS_BUILDINGS_LOST", "UI_RESULTS_BUILDINGS_DESTROYED",
        "UI_RESULTS_HARVESTED", "UI_RESULTS_SPENT", "UI_RESULTS_SOLD",
    ];

    /// <summary>The summary of the match of <paramref name="world"/> for <paramref name="local"/>; null outside a match.</summary>
    public static MatchSummary? Of(GameWorld world, PlayerId local, int ticksPerSecond)
    {
        if (world.Match is not { } match)
        {
            return null;
        }

        var winners = world.Result?.Winners ?? [];
        var rows = ImmutableArray.CreateBuilder<SummaryRow>();
        var series = ImmutableArray.CreateBuilder<ImmutableArray<StatsSample>>();
        var players = new List<MatchPlayer>(match.Players);
        players.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        foreach (var player in players)
        {
            var stats = world.Stats.Of(player.Id);
            rows.Add(new SummaryRow(
                player.Id,
                player.Id == local,
                player.Kind,
                player.Team,
                winners.Contains(player.Id),
                [
                    stats.UnitsBuilt, stats.UnitsLost, stats.UnitsDestroyed,
                    stats.BuildingsBuilt, stats.BuildingsLost, stats.BuildingsDestroyed,
                    stats.CreditsHarvested, stats.CreditsSpent, stats.CreditsFromSales,
                ])
            {
                Difficulty = player.Difficulty,
                Personality = player.Personality,
            });
            series.Add([.. stats.Samples]);
        }

        // The person beaten while bots of other teams fight on (M8 step 13: one against three): a defeat, the match not over yet.
        var beaten = world.Result is null && world.Player(local).DefeatedTick is not null;
        var outcome = beaten ? MatchOutcome.Defeat
            : winners.IsEmpty ? MatchOutcome.Draw
            : winners.Contains(local) ? MatchOutcome.Victory : MatchOutcome.Defeat;
        var duration = world.Stats.Duration;
        return new MatchSummary(outcome, Clock(duration, ticksPerSecond), rows.ToImmutable(), series.ToImmutable(), duration);
    }

    /// <summary>«m:ss» of <paramref name="ticks"/>.</summary>
    public static string Clock(int ticks, int ticksPerSecond)
    {
        var seconds = ticks / ticksPerSecond;
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>The strongest army of any player at any point: the top of the graph.</summary>
    public int MaxStrength
    {
        get
        {
            var max = 0;
            foreach (var samples in Series)
            {
                foreach (var sample in samples)
                {
                    max = System.Math.Max(max, sample.ArmyStrength);
                }
            }

            return max;
        }
    }

    /// <summary>
    /// The points of the line of <paramref name="samples"/> in a graph of <paramref name="width"/> × <paramref name="height"/>, its origin
    /// top-left as on screen: the time from 0 to <paramref name="durationTicks"/> left to right, the strength from 0 to
    /// <paramref name="maxStrength"/> bottom to top. With no strength at all the line lies on the bottom.
    /// </summary>
    public static List<(float X, float Y)> GraphPoints(IReadOnlyList<StatsSample> samples, float width, float height, int maxStrength, int durationTicks)
    {
        var points = new List<(float X, float Y)>(samples.Count);
        var span = System.Math.Max(durationTicks, 1);
        foreach (var sample in samples)
        {
            var x = width * System.Math.Min(sample.Tick, span) / span;
            var y = maxStrength > 0 ? height - height * sample.ArmyStrength / maxStrength : height;
            points.Add((x, y));
        }

        return points;
    }
}
