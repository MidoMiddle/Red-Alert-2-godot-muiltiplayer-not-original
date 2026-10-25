using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// The <see cref="PlaceBuildingCommand"/> (docs/implementation/M3_base_economy.md, 4.8, 6): a building produced first (step 8) is placed
/// where the rules allow, closes its cells, grows for its <c>build_up</c> and then gives power and opens technologies; units find their
/// way around it.
/// </summary>
public sealed class PlaceBuildingTests
{
    private static readonly PlayerId Player1 = new(1);

    private uint _sequence;

    private static GameSimulation Match() =>
        new(new SimulationSettings(1) { MapWidthCells = 30, MapHeightCells = 20 }, TestRules.Standard);

    private static int Index(string id) => TestRules.Standard.Database.IndexOfBuilding(id);

    /// <summary>A ready construction yard, and one tick played so that the technologies know it, as after a deploy.</summary>
    private static Building Yard(GameSimulation simulation, int x, int y)
    {
        var yard = simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("construction_yard"), new CellPos(x, y), ready: true, null);
        simulation.Step();
        return yard;
    }

    /// <summary>Orders the building and plays until it is ready, or for 100 ticks when the order is refused.</summary>
    private void Produce(GameSimulation simulation, int defIndex, int player = 1)
    {
        simulation.Enqueue(new ProduceCommand(new PlayerId(player), simulation.CurrentTick, _sequence++, ProductionKind.Building, defIndex, 1));
        for (var i = 0; i < 100 && !simulation.LastTickEvents.OfType<ProductionReady>().Any(); i++)
        {
            simulation.Step();
        }
    }

    /// <summary>Sends the place command for this tick, plays the tick and returns its events.</summary>
    private List<SimEvent> PlaceNow(GameSimulation simulation, int defIndex, int x, int y, int player = 1)
    {
        simulation.Enqueue(new PlaceBuildingCommand(new PlayerId(player), simulation.CurrentTick, _sequence++, defIndex, new CellPos(x, y)));
        simulation.Step();
        return simulation.LastTickEvents.ToList();
    }

    /// <summary>Produces the building, then places it: the events of the place tick.</summary>
    private List<SimEvent> Place(GameSimulation simulation, int defIndex, int x, int y, int player = 1)
    {
        if (defIndex >= 0 && defIndex < 100)
        {
            Produce(simulation, defIndex, player);
        }

        return PlaceNow(simulation, defIndex, x, y, player);
    }

    private List<SimEvent> Place(GameSimulation simulation, string id, int x, int y) => Place(simulation, Index(id), x, y);

    private static Building BuildingAt(GameSimulation simulation, int x, int y) => simulation.World.BuildingAt(new CellPos(x, y))!;

    [Fact]
    public void A_placed_building_closes_its_cells_and_grows_before_it_works()
    {
        var simulation = Match();
        Yard(simulation, 2, 2);

        Produce(simulation, Index("reactor"));
        var placedOn = simulation.CurrentTick;
        var events = PlaceNow(simulation, Index("reactor"), 6, 2);

        var reactor = BuildingAt(simulation, 6, 2);
        Assert.Contains(new BuildingPlaced(placedOn, reactor.Id, Player1, Index("reactor"), new CellPos(6, 2)), events);
        Assert.False(reactor.IsReady);
        Assert.True(simulation.World.Map.HasBuilding(new CellPos(7, 3)));
        Assert.False(simulation.World.Map.CanEnter(new CellPos(7, 3), Locomotor.Track));

        // 23 ticks of build-up, the first one on the tick of placing: ready 22 ticks later.
        var ready = -1;
        for (var i = 0; i < 30 && ready < 0; i++)
        {
            Assert.Equal(0, simulation.World.Player(Player1).PowerOutput);
            simulation.Step();
            if (simulation.LastTickEvents.Contains(new BuildUpComplete(simulation.CurrentTick - 1, reactor.Id)))
            {
                ready = simulation.CurrentTick - 1;
            }
        }

        Assert.Equal(placedOn + 22, ready);
        Assert.True(reactor.IsReady);
        Assert.True(simulation.World.Player(Player1).CanBuildBuilding(Index("barracks")));

        // Power counts the building from the next tick on (4.15).
        simulation.Step();
        Assert.Equal(150, simulation.World.Player(Player1).PowerOutput);
    }

    [Fact]
    public void The_barracks_wait_for_a_ready_reactor()
    {
        var simulation = Match();
        Yard(simulation, 2, 2);

        Assert.Contains(Place(simulation, "barracks", 6, 2), e => e is CannotPlace { Problem: PlacementProblem.NotAvailable });

        // While the reactor grows the barracks cannot even be ordered.
        Place(simulation, "reactor", 6, 2);
        simulation.Enqueue(new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, Index("barracks"), 1));
        simulation.Step();
        Assert.Empty(simulation.World.Player(Player1).Queue(ProductionCategory.Building));

        while (!BuildingAt(simulation, 6, 2).IsReady)
        {
            simulation.Step();
        }

        Assert.Contains(Place(simulation, "barracks", 2, 6), e => e is BuildingPlaced);
    }

    [Fact]
    public void A_place_the_rules_refuse_is_reported_and_nothing_is_built()
    {
        var simulation = Match();
        Yard(simulation, 2, 2);
        var entities = simulation.World.Entities.Count;

        Produce(simulation, Index("reactor"));
        var tick = simulation.CurrentTick;
        Assert.Contains(new CannotPlace(tick, Player1, Index("reactor"), new CellPos(12, 2), PlacementProblem.TooFar), PlaceNow(simulation, Index("reactor"), 12, 2));
        Assert.Contains(new CannotPlace(tick + 1, Player1, Index("reactor"), new CellPos(3, 3), PlacementProblem.Taken), PlaceNow(simulation, Index("reactor"), 3, 3));

        // Refused, the building stays ready in its queue.
        Assert.True(simulation.World.Player(Player1).Queue(ProductionCategory.Building)[0].Ready);
        Assert.Equal(entities, simulation.World.Entities.Count);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(999, 1)]
    [InlineData(0, 0)]
    public void A_wrong_building_or_player_is_ignored(int defIndex, int player)
    {
        var simulation = Match();
        Yard(simulation, 2, 2);
        var entities = simulation.World.Entities.Count;

        Assert.Empty(Place(simulation, defIndex < 0 || defIndex > 100 ? defIndex : Index("reactor"), 6, 2, player));
        Assert.Equal(entities, simulation.World.Entities.Count);
    }

    [Fact]
    public void A_unit_on_its_way_drives_around_a_new_building()
    {
        var simulation = Match();
        Yard(simulation, 12, 8);
        simulation.Enqueue(new SpawnTestUnitCommand(Player1, simulation.CurrentTick, _sequence++, UnitType.HeavyTank, new CellPos(3, 12).Center));
        simulation.Step();
        var tank = simulation.World.Entities.OfType<Unit>().Single();
        simulation.Enqueue(new MoveCommand(Player1, simulation.CurrentTick, _sequence++, [tank.Id], new CellPos(25, 12).Center));
        for (var i = 0; i < 8; i++)
        {
            simulation.Step();
        }

        // The reactor, under the yard, closes x 12…14 of row 12, right across the tank's straight way.
        Assert.Contains(Place(simulation, "reactor", 12, 12), e => e is BuildingPlaced);

        var footprint = new[]
        {
            new CellPos(12, 12), new CellPos(13, 12), new CellPos(14, 12), new CellPos(12, 13), new CellPos(13, 13), new CellPos(14, 13),
        };
        for (var i = 0; i < 600 && tank.Position.ToCell() != new CellPos(25, 12); i++)
        {
            simulation.Step();
            Assert.DoesNotContain(tank.Position.ToCell(), footprint);
        }

        Assert.Equal(new CellPos(25, 12), tank.Position.ToCell());
    }
}
