using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

public sealed class PlayerSwitchTests
{
    [Fact]
    public void The_switch_goes_round_the_players_that_have_units_or_buildings()
    {
        var world = new GameWorld(seed: 1, widthCells: 20, heightCells: 20, TestRules.Standard);
        world.SpawnUnit(new PlayerId(1), UnitType.Conscript, new CellPos(2, 2).Center);
        world.CreateBuilding(new PlayerId(3), TestRules.BuildingDef("reactor"), new CellPos(10, 10), true, null);
        world.SpawnUnit(PlayerId.Neutral, UnitType.Conscript, new CellPos(4, 4).Center);

        Assert.Equal(new PlayerId(3), PlayerSwitch.Next(world, new PlayerId(1)));
        Assert.Equal(new PlayerId(1), PlayerSwitch.Next(world, new PlayerId(3)));
    }

    [Fact]
    public void A_lone_player_stays()
    {
        var world = new GameWorld(seed: 1, widthCells: 20, heightCells: 20, TestRules.Standard);
        world.SpawnUnit(new PlayerId(1), UnitType.Conscript, new CellPos(2, 2).Center);

        Assert.Equal(new PlayerId(1), PlayerSwitch.Next(world, new PlayerId(1)));
    }
}
