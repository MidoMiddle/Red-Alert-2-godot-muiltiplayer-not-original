using Rts.Client.App;
using Rts.Client.Hud;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

public sealed class HealthPipsTests
{
    [Fact]
    public void A_soldier_has_four_pips_a_vehicle_eight_and_a_building_two_per_cell_of_width_up_to_twelve()
    {
        var world = new GameWorld(seed: 1, widthCells: 20, heightCells: 20, TestRules.Standard);

        Assert.Equal(4, HealthPips.CountOf(world.SpawnUnit(new PlayerId(1), UnitType.Conscript, new CellPos(2, 2).Center)));
        Assert.Equal(8, HealthPips.CountOf(world.SpawnUnit(new PlayerId(1), UnitType.HeavyTank, new CellPos(4, 4).Center)));
        Assert.Equal(8, HealthPips.CountOf(world.CreateBuilding(new PlayerId(1), TestRules.BuildingDef("construction_yard"), new CellPos(10, 10), true, null)));
    }

    [Fact]
    public void The_filled_pips_round_up_and_the_colour_follows_the_damage_state()
    {
        Assert.Equal(8, HealthPips.Filled(8, 400, 400));
        Assert.Equal(1, HealthPips.Filled(8, 1, 400));
        Assert.Equal(4, HealthPips.Filled(8, 200, 400));
        Assert.Equal(5, HealthPips.Filled(8, 201, 400));
        Assert.Equal(0, HealthPips.Filled(8, 0, 400));

        Assert.Equal((0.2f, 0.85f, 0.25f), HealthPips.ColorOf(300, 400));
        Assert.Equal((0.95f, 0.8f, 0.15f), HealthPips.ColorOf(150, 400));
        Assert.Equal((0.9f, 0.2f, 0.15f), HealthPips.ColorOf(50, 400));
    }

    [Fact]
    public void A_bar_shows_for_the_selected_and_the_damaged_always_or_for_the_selected_only()
    {
        Assert.False(HealthPips.Shows(HealthBarMode.Damaged, selected: false, 400, 400));
        Assert.True(HealthPips.Shows(HealthBarMode.Damaged, selected: true, 400, 400));
        Assert.True(HealthPips.Shows(HealthBarMode.Damaged, selected: false, 399, 400));
        Assert.True(HealthPips.Shows(HealthBarMode.Always, selected: false, 400, 400));
        Assert.False(HealthPips.Shows(HealthBarMode.Selected, selected: false, 10, 400));
        Assert.True(HealthPips.Shows(HealthBarMode.Selected, selected: true, 400, 400));
    }
}
