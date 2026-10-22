using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// The <see cref="DeployCommand"/> (docs/implementation/M3_base_economy.md, 4.9, M3 step 7): the MCV turns into a construction yard
/// centred on its cell when the yard may stand there, and stays an MCV when it may not.
/// </summary>
public sealed class DeployTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private uint _sequence;

    /// <summary>A match on a 20 × 16 test map with the given codes in the given cells.</summary>
    private static GameSimulation Match(params (int X, int Y, char Code)[] cells)
    {
        var rows = new char[16][];
        for (var y = 0; y < rows.Length; y++)
        {
            rows[y] = Enumerable.Repeat('.', 20).ToArray();
        }

        foreach (var (x, y, code) in cells)
        {
            rows[y][x] = code;
        }

        var map = TestMapParser.Parse(string.Join("\n", rows.Select(row => string.Join(" ", row))), TestRules.Standard).Grid;
        return new GameSimulation(new SimulationSettings(1, map), TestRules.Standard);
    }

    private Unit Spawn(GameSimulation simulation, UnitType type, int x, int y, PlayerId? owner = null)
    {
        simulation.Enqueue(new SpawnTestUnitCommand(owner ?? Player1, simulation.CurrentTick, _sequence++, type, new CellPos(x, y).Center));
        simulation.Step();
        return simulation.World.Entities.OfType<Unit>().Last();
    }

    private List<SimEvent> Deploy(GameSimulation simulation, Unit unit, PlayerId? player = null)
    {
        simulation.Enqueue(new DeployCommand(player ?? Player1, simulation.CurrentTick, _sequence++, unit.Id));
        simulation.Step();
        return simulation.LastTickEvents.ToList();
    }

    [Fact]
    public void An_mcv_on_free_ground_becomes_a_growing_construction_yard_around_its_cell()
    {
        var simulation = Match();
        var mcv = Spawn(simulation, UnitType.Mcv, 10, 10);
        var tick = simulation.CurrentTick;

        var events = Deploy(simulation, mcv);

        var yard = simulation.World.BuildingAt(new CellPos(10, 10))!;
        Assert.Equal(("construction_yard", new CellPos(9, 9), Player1, false), (yard.Def!.Id, yard.Cell, yard.Owner, yard.IsReady));
        Assert.Contains(new Deployed(tick, mcv.Id, yard.Id), events);
        Assert.Contains(new BuildingPlaced(tick, yard.Id, Player1, yard.DefIndex, new CellPos(9, 9)), events);
        Assert.False(simulation.World.TryGet(mcv.Id, out _));
        Assert.True(simulation.World.Reservations.IsFree(new CellPos(10, 10)));
    }

    [Fact]
    public void The_yard_of_an_mcv_is_a_base_for_the_next_building()
    {
        var simulation = Match();
        Deploy(simulation, Spawn(simulation, UnitType.Mcv, 10, 10));
        while (!simulation.World.BuildingAt(new CellPos(10, 10))!.IsReady)
        {
            simulation.Step();
        }

        var reactor = TestRules.Standard.Database.IndexOfBuilding("reactor");
        simulation.Enqueue(new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, reactor, 1));
        for (var i = 0; i < 100 && !simulation.LastTickEvents.OfType<ProductionReady>().Any(); i++)
        {
            simulation.Step();
        }

        simulation.Enqueue(new PlaceBuildingCommand(Player1, simulation.CurrentTick, _sequence++, reactor, new CellPos(13, 9)));
        simulation.Step();

        Assert.Contains(simulation.LastTickEvents, e => e is BuildingPlaced);
    }

    [Theory]
    [InlineData('T', PlacementProblem.Taken)]
    [InlineData('~', PlacementProblem.NotBuildable)]
    [InlineData('#', PlacementProblem.NotBuildable)]
    public void A_yard_that_cannot_stand_there_leaves_the_mcv_as_it_is(char code, PlacementProblem problem)
    {
        var simulation = Match((11, 11, code));
        var mcv = Spawn(simulation, UnitType.Mcv, 10, 10);
        var tick = simulation.CurrentTick;

        Assert.Equal([new CannotDeploy(tick, Player1, mcv.Id, problem)], Deploy(simulation, mcv).OfType<CannotDeploy>());
        Assert.True(mcv.IsAlive);
        Assert.Null(simulation.World.BuildingAt(new CellPos(10, 10)));
    }

    [Fact]
    public void Another_unit_next_to_the_mcv_stops_it()
    {
        var simulation = Match();
        Spawn(simulation, UnitType.Conscript, 9, 10, Player2);
        var mcv = Spawn(simulation, UnitType.Mcv, 10, 10);

        Assert.Contains(Deploy(simulation, mcv), e => e is CannotDeploy { Problem: PlacementProblem.Units });
        Assert.True(mcv.IsAlive);
    }

    [Fact]
    public void An_mcv_at_the_edge_has_no_room()
    {
        var simulation = Match();
        var mcv = Spawn(simulation, UnitType.Mcv, 0, 5);

        Assert.Contains(Deploy(simulation, mcv), e => e is CannotDeploy { Problem: PlacementProblem.OutsideMap });
    }

    [Fact]
    public void A_tank_and_someone_elses_mcv_do_not_deploy()
    {
        var simulation = Match();
        var tank = Spawn(simulation, UnitType.HeavyTank, 4, 4);
        var mcv = Spawn(simulation, UnitType.Mcv, 10, 10);

        Assert.DoesNotContain(Deploy(simulation, tank), e => e is Deployed or CannotDeploy);
        Assert.DoesNotContain(Deploy(simulation, mcv, Player2), e => e is Deployed or CannotDeploy);
        Assert.True(tank.IsAlive);
        Assert.True(mcv.IsAlive);
    }

    [Fact]
    public void A_replay_with_a_deploy_and_a_building_plays_back_the_same()
    {
        var simulation = Match();
        var recorder = new ReplayRecorder(simulation);
        var mcv = Spawn(simulation, UnitType.Mcv, 10, 10);
        recorder.AfterStep();
        Deploy(simulation, mcv);
        recorder.AfterStep();
        for (var i = 0; i < 30; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var reactor = TestRules.Standard.Database.IndexOfBuilding("reactor");
        simulation.Enqueue(new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, reactor, 1));
        for (var i = 0; i < 20; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        simulation.Enqueue(new PlaceBuildingCommand(Player1, simulation.CurrentTick, _sequence++, reactor, new CellPos(13, 9)));
        for (var i = 0; i < 40; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));

        Assert.Equal(150, simulation.World.Player(Player1).PowerOutput);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }
}
