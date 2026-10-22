using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Commands;

/// <summary>The commands that changed nothing are kept for the tests and the tools (docs/implementation/M5_first_bot.md, 6, step 4).</summary>
public sealed class RefusedCommandsTests
{
    [Fact]
    public void A_refused_command_is_kept_and_an_accepted_one_is_not()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);
        var player = new PlayerId(1);
        var spawn = new SpawnTestUnitCommand(player, 0, 0, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var place = new PlaceBuildingCommand(player, 0, 1, TestRules.Standard.Database.IndexOfBuilding("reactor"), new CellPos(20, 20));
        simulation.Enqueue(spawn);
        simulation.Enqueue(place);
        simulation.Step();

        var tank = simulation.World.Entities.OfType<Unit>().Single();
        var deployTank = new DeployCommand(player, 1, 2, tank.Id);
        simulation.Enqueue(deployTank);
        simulation.Step();

        Assert.Equal([place, deployTank], simulation.RefusedCommands);
        Assert.Equal(3, simulation.ExecutedCommands.Count);
    }
}
