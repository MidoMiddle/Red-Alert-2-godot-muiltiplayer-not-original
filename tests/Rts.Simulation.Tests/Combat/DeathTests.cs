using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// Death and destruction (docs/implementation/M4_combat.md, 4.16): the event names the weapon; a unit leaves its place at the end of
/// the tick, a building opens its cells then; power is worked out again; the queue of the last factory of a category is cancelled with
/// what was paid, while another factory carries on.
/// </summary>
public sealed class DeathTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private uint _sequence;

    private static int Weapon(string id) => TestRules.Standard.Database.IndexOfWeapon(id);

    private static GameWorld World() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    private static GameSimulation Match() =>
        new(new SimulationSettings(1) { MapWidthCells = 48, MapHeightCells = 32, StartingCredits = 10_000 }, TestRules.Standard);

    private static Building Build(GameWorld world, PlayerId owner, string id, int x, int y) =>
        world.CreateBuilding(owner, TestRules.BuildingDef(id), new CellPos(x, y), ready: true, null);

    private static Hit HitOn(Entity target, string weapon, WorldPos point, int source) =>
        new(new EntityId(source), new EntityId(source), Player1, Weapon(weapon), point, target.Id);

    private static List<SimEvent> Apply(GameWorld world, params Hit[] hits)
    {
        var events = new List<SimEvent>();
        world.Hits.AddRange(hits);
        DamageSystem.Update(world, events);
        return events;
    }

    private void Knock(GameSimulation simulation, Building building) =>
        simulation.Enqueue(new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, building.Cell, DamageMapObjectCommand.MaxAmount));

    private void Produce(GameSimulation simulation, ProductionKind kind, string id) =>
        simulation.Enqueue(new ProduceCommand(
            Player1, simulation.CurrentTick, _sequence++, kind,
            kind == ProductionKind.Unit ? TestRules.Standard.Database.IndexOfUnit(id) : TestRules.Standard.Database.IndexOfBuilding(id), 1));

    private static List<SimEvent> Play(GameSimulation simulation, int ticks)
    {
        var events = new List<SimEvent>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void A_shot_dead_unit_names_the_weapon_and_leaves_its_place_at_the_end_of_the_tick()
    {
        var world = World();
        var conscript = world.SpawnUnit(Player2, UnitType.Conscript, new CellPos(10, 10).Center);

        var events = Apply(world, HitOn(conscript, "tesla_tank_bolt", conscript.Position, 900));

        Assert.Contains(new EntityDestroyed(0, conscript.Id, new EntityId(900), DeathCause.Weapon, Weapon("tesla_tank_bolt")), events);
        Assert.Equal(1, world.Reservations.InfantryCount(new CellPos(10, 10)));
        world.RemoveDead();
        Assert.Equal(0, world.Reservations.InfantryCount(new CellPos(10, 10)));
    }

    [Fact]
    public void A_crushed_unit_has_no_weapon()
    {
        var world = World();
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(9, 10).Center);
        var conscript = world.SpawnUnit(Player2, UnitType.Conscript, new CellPos(10, 10).Center);
        var events = new List<SimEvent>();

        Assert.True(CrushSystem.TryCrush(world, tank, new CellPos(10, 10), events));

        Assert.Contains(new EntityDestroyed(0, conscript.Id, tank.Id, DeathCause.Crushed, -1), events);
    }

    [Fact]
    public void The_load_of_a_dead_harvester_is_lost()
    {
        var world = World();
        var harvester = world.SpawnUnit(Player1, UnitType.Harvester, new CellPos(10, 10).Center);
        harvester.Harvester!.Load = 500;
        var credits = world.Player(Player1).Credits;

        DeathSystem.Kill(world, harvester, EntityId.None, DeathCause.Weapon, -1, null);
        world.RemoveDead();

        Assert.False(world.TryGet(harvester.Id, out _));
        Assert.Equal(credits, world.Player(Player1).Credits);
    }

    [Fact]
    public void A_fallen_building_keeps_its_cells_closed_until_the_end_of_the_tick()
    {
        var world = World();
        Build(world, Player2, "construction_yard", 16, 10);
        var reactor = Build(world, Player2, "reactor", 20, 10);
        var point = new CellPos(20, 10).Center;
        TechTree.Update(world);

        var events = Apply(world, Enumerable.Range(0, 13).Select(i => HitOn(reactor, "heavy_tank_cannon", point, 910 + i)).ToArray());

        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Entity == reactor.Id && destroyed.Weapon == Weapon("heavy_tank_cannon"));
        Assert.Contains(events, e => e is BuildingDestroyed destroyed && destroyed.Building == reactor.Id);
        Assert.Null(world.BuildingAt(new CellPos(20, 10)));
        Assert.False(world.Map.CanEnter(new CellPos(21, 11), Rts.Simulation.Map.Locomotor.Track));
        Assert.NotEqual(PlacementProblem.None, PlacementRules.Check(world, Player2, TestRules.BuildingDef("reactor"), new CellPos(20, 10)));
        Assert.DoesNotContain(events, e => e is MapChanged);

        DeathSystem.Update(world, events);

        Assert.Contains(events, e => e is MapChanged);
        Assert.True(world.Map.CanEnter(new CellPos(21, 11), Rts.Simulation.Map.Locomotor.Track));
        Assert.Equal(PlacementProblem.None, PlacementRules.Check(world, Player2, TestRules.BuildingDef("reactor"), new CellPos(20, 10)));
    }

    [Fact]
    public void A_sold_building_still_opens_its_cells_at_once()
    {
        var world = World();
        var reactor = Build(world, Player1, "reactor", 20, 10);

        Assert.True(CommandProcessor.Apply(world, new SellCommand(Player1, 0, 0, reactor.Id)));

        Assert.True(world.Map.CanEnter(new CellPos(21, 11), Rts.Simulation.Map.Locomotor.Track));
    }

    [Fact]
    public void A_destroyed_reactor_takes_its_power_away_on_the_next_tick()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Player1, "construction_yard", 1, 1);
        var reactor = Build(world, Player1, "reactor", 5, 1);
        Build(world, Player1, "barracks", 8, 1);
        simulation.Step();
        var output = world.Player(Player1).PowerOutput;
        Assert.True(output > 0);

        Knock(simulation, reactor);
        Play(simulation, 2);

        Assert.Equal(output - TestRules.BuildingDef("reactor").Power, world.Player(Player1).PowerOutput);
        Assert.True(world.Player(Player1).PowerDrain > world.Player(Player1).PowerOutput);
    }

    [Fact]
    public void The_last_barracks_falling_cancels_its_queue_and_gives_back_what_was_paid()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Player1, "construction_yard", 1, 1);
        Build(world, Player1, "reactor", 5, 1);
        var barracks = Build(world, Player1, "barracks", 8, 1);
        simulation.Step();
        Produce(simulation, ProductionKind.Unit, "conscript");
        Play(simulation, 5);
        var queue = world.Player(Player1).Queue(ProductionCategory.Infantry);
        Assert.True(queue[0].Paid > 0);

        Knock(simulation, barracks);
        var events = Play(simulation, 1);

        Assert.Empty(queue);
        Assert.Equal(10_000, world.Player(Player1).Credits);
        Assert.Contains(events, e => e is ProductionCancelled { Kind: ProductionKind.Unit } cancelled && cancelled.Refund > 0);
    }

    [Fact]
    public void With_another_barracks_the_production_goes_on_and_the_unit_comes_out_there()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Player1, "construction_yard", 1, 1);
        Build(world, Player1, "reactor", 5, 1);
        Build(world, Player1, "reactor", 5, 4);
        var other = Build(world, Player1, "barracks", 8, 1);
        var last = Build(world, Player1, "barracks", 12, 1);
        simulation.Step();
        Produce(simulation, ProductionKind.Unit, "conscript");
        Play(simulation, 5);

        Knock(simulation, last);
        var events = Play(simulation, 600);

        Assert.DoesNotContain(events, e => e is ProductionCancelled);
        Assert.Contains(events, e => e is UnitProduced produced && produced.Factory == other.Id);
    }

    [Fact]
    public void A_fallen_construction_yard_gives_back_the_building_waiting_to_be_placed()
    {
        var simulation = Match();
        var world = simulation.World;
        var yard = Build(world, Player1, "construction_yard", 1, 1);
        simulation.Step();
        Produce(simulation, ProductionKind.Building, "reactor");
        var ready = false;
        for (var tick = 0; tick < 2000 && !ready; tick++)
        {
            simulation.Step();
            ready = simulation.LastTickEvents.OfType<ProductionReady>().Any();
        }

        Assert.True(ready);
        Assert.Equal(10_000 - TestRules.BuildingDef("reactor").Cost, world.Player(Player1).Credits);

        Knock(simulation, yard);
        var events = Play(simulation, 1);

        Assert.Empty(world.Player(Player1).Queue(ProductionCategory.Building));
        Assert.Equal(10_000, world.Player(Player1).Credits);
        Assert.Contains(events, e => e is ProductionCancelled { Kind: ProductionKind.Building } cancelled
            && cancelled.Refund == TestRules.BuildingDef("reactor").Cost);
    }
}
