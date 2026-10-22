using Rts.Client.Hud;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The credits counter, the power strip and the radar rule of the minimap (docs/implementation/M3_base_economy.md, 4.17).</summary>
public sealed class SidebarGaugesTests
{
    private static readonly PlayerId Player1 = new(1);

    [Theory]
    [InlineData(10_000, 9_400)]
    [InlineData(9_400, 10_000)]
    [InlineData(0, 3)]
    [InlineData(5, 5)]
    public void The_credits_roll_to_the_amount_without_passing_it(int shown, int actual)
    {
        var frames = 0;
        while (shown != actual)
        {
            var next = CreditsRoll.Next(shown, actual);
            // Every frame comes closer and stays on its side of the amount.
            Assert.InRange(next, System.Math.Min(shown, actual), System.Math.Max(shown, actual));
            Assert.NotEqual(shown, next);
            shown = next;
            frames++;
        }

        Assert.InRange(frames, 0, 60);
    }

    [Theory]
    [InlineData(0, 0, false, 100, PowerLevel.Normal)]
    [InlineData(150, 20, false, 200, PowerLevel.Normal)]
    [InlineData(150, 140, false, 200, PowerLevel.Tight)]
    [InlineData(150, 170, true, 300, PowerLevel.Low)]
    [InlineData(0, 20, true, 100, PowerLevel.Low)]
    public void The_power_strip_scales_in_hundreds_with_room_on_top(int output, int drain, bool low, int scale, PowerLevel level)
    {
        var gauge = PowerGauge.Of(output, drain, low);

        Assert.Equal((scale, level), (gauge.Scale, gauge.Level));
        Assert.Equal(output / (float)scale, gauge.Output);
        Assert.Equal(drain / (float)scale, gauge.Drain);
        Assert.True(gauge.Output <= 0.8f && gauge.Drain <= 0.8f);
    }

    [Fact]
    public void The_minimap_needs_a_ready_radar_and_power_when_the_rule_is_on()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 30, TestRules.Standard.Land)), TestRules.Standard);
        var world = simulation.World;
        Assert.True(MinimapRule.Online(world, Player1, ruleOn: false));
        Assert.False(MinimapRule.Online(world, Player1, ruleOn: true));

        world.CreateBuilding(Player1, TestRules.BuildingDef("radar"), new CellPos(10, 10), ready: false, null);
        Assert.False(MinimapRule.Online(world, Player1, ruleOn: true));

        world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(2, 2), ready: true, null);
        world.CreateBuilding(Player1, TestRules.BuildingDef("radar"), new CellPos(14, 10), ready: true, null);
        simulation.Step();
        Assert.True(MinimapRule.Online(world, Player1, ruleOn: true));
        Assert.False(MinimapRule.Online(world, new PlayerId(2), ruleOn: true));

        // Three more radars drain 200 against the 150 of the reactor.
        for (var i = 0; i < 3; i++)
        {
            world.CreateBuilding(Player1, TestRules.BuildingDef("radar"), new CellPos(18 + 4 * i, 10), ready: true, null);
        }

        simulation.Step();
        Assert.True(world.Player(Player1).LowPower);
        Assert.False(MinimapRule.Online(world, Player1, ruleOn: true));
    }
}
