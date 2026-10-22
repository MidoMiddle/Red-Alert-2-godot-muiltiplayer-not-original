using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>When the modules of a bot think (docs/implementation/M5_first_bot.md, 4.3).</summary>
public sealed class AiScheduleTests
{
    private static readonly AiSettings Medium = AiSettings.Medium;

    private static List<int> Ticks(AiModuleKind kind, int player, int ticks) =>
        Enumerable.Range(0, ticks).Where(tick => AiSchedule.IsDue(Medium, kind, new PlayerId(player), tick)).ToList();

    [Theory]
    [InlineData(AiModuleKind.Strategist, 45, 0)]
    [InlineData(AiModuleKind.Economy, 15, 1)]
    [InlineData(AiModuleKind.BuildPlanner, 15, 2)]
    [InlineData(AiModuleKind.Production, 15, 4)]
    [InlineData(AiModuleKind.Scouting, 45, 5)]
    [InlineData(AiModuleKind.Squads, 15, 0)]
    [InlineData(AiModuleKind.Defense, 5, 3)]
    public void Each_module_thinks_once_in_its_period_on_the_ticks_of_its_offset(AiModuleKind kind, int period, int offset)
    {
        // Player 1: the ticks t where t + 3 + offset is a multiple of the period.
        var ticks = Ticks(kind, player: 1, ticks: 3 * period);

        Assert.Equal(3, ticks.Count);
        Assert.All(ticks, tick => Assert.Equal(0, (tick + 3 + offset) % period));
        Assert.All(ticks.Skip(1).Zip(ticks), pair => Assert.Equal(period, pair.First - pair.Second));
    }

    [Fact]
    public void The_bot_of_the_next_player_thinks_three_ticks_earlier()
    {
        var first = Ticks(AiModuleKind.Strategist, player: 1, ticks: 200);
        var second = Ticks(AiModuleKind.Strategist, player: 2, ticks: 200);

        Assert.Equal([42, 87, 132, 177], first);
        Assert.Equal([39, 84, 129, 174], second);
    }

    [Fact]
    public void In_90_ticks_the_strategist_thinks_twice_and_the_defense_18_times()
    {
        Assert.Equal(2, Ticks(AiModuleKind.Strategist, player: 1, ticks: 90).Count);
        Assert.Equal(18, Ticks(AiModuleKind.Defense, player: 1, ticks: 90).Count);
    }
}
