using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// Selling, repairs and walls (docs/implementation/M3_base_economy.md, 4.8, 6): what a sale gives back, what a repair costs, and a
/// line of walls paid segment by segment.
/// </summary>
public sealed class SellRepairWallsTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private uint _sequence;

    private static GameSimulation Match(int credits = 10_000, params CellPos[] water)
    {
        var map = new MapGrid(40, 30, TestRules.Standard.Land);
        foreach (var cell in water)
        {
            map.SetLand(cell, LandType.Deep);
        }

        return new GameSimulation(new SimulationSettings(1, map) { StartingCredits = credits }, TestRules.Standard);
    }

    private static Building Build(GameSimulation simulation, string id, int x, int y, PlayerId? owner = null, bool ready = true) =>
        simulation.World.CreateBuilding(owner ?? Player1, TestRules.BuildingDef(id), new CellPos(x, y), ready, null);

    private static int Index(string id) => TestRules.Standard.Database.IndexOfBuilding(id);

    private static int Credits(GameSimulation simulation) => simulation.World.Player(Player1).Credits;

    private List<SimEvent> Send(GameSimulation simulation, Command command)
    {
        simulation.Enqueue(command);
        simulation.Step();
        return simulation.LastTickEvents.ToList();
    }

    private List<SimEvent> Sell(GameSimulation simulation, Building building, PlayerId? player = null) =>
        Send(simulation, new SellCommand(player ?? Player1, simulation.CurrentTick, _sequence++, building.Id));

    private List<SimEvent> Repair(GameSimulation simulation, Building building, PlayerId? player = null) =>
        Send(simulation, new ToggleRepairCommand(player ?? Player1, simulation.CurrentTick, _sequence++, building.Id));

    /// <summary>A yard, and a wall produced and waiting in the defense queue.</summary>
    private void ReadyWall(GameSimulation simulation)
    {
        Build(simulation, "construction_yard", 2, 2);
        // The wall needs the barracks (RA2), far from the walls of the tests, with the power for it.
        Build(simulation, "barracks", 34, 24);
        Build(simulation, "reactor", 30, 24);
        simulation.Step();
        Send(simulation, new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, Index("wall"), 1));
        for (var i = 0; i < 40 && !simulation.World.Player(Player1).Queue(ProductionCategory.Defense)[0].Ready; i++)
        {
            simulation.Step();
        }
    }

    private List<SimEvent> Walls(GameSimulation simulation, params CellPos[] cells) =>
        Send(simulation, new PlaceWallsCommand(Player1, simulation.CurrentTick, _sequence++, Index("wall"), [.. cells]));

    private static CellPos[] Row(int fromX, int toX, int y) => Enumerable.Range(fromX, toX - fromX + 1).Select(x => new CellPos(x, y)).ToArray();

    [Theory]
    [InlineData(500, 50)]   // half the cost of 100 at full hit points
    [InlineData(250, 25)]
    [InlineData(99, 9)]     // 100 × 50 % × 99 / 500 = 9.9, rounded down
    [InlineData(1, 0)]
    public void A_sale_gives_back_half_the_cost_times_the_share_of_hit_points(int hp, int refund)
    {
        var simulation = Match();
        var reactor = Build(simulation, "reactor", 10, 10);
        reactor.Hp = hp;
        var before = Credits(simulation);

        var events = Sell(simulation, reactor);

        Assert.Equal(refund, Credits(simulation) - before);
        Assert.Contains(new BuildingSold(simulation.CurrentTick - 1, reactor.Id, Player1, Index("reactor"), refund), events);
        Assert.False(simulation.World.TryGet(reactor.Id, out _));
        Assert.False(simulation.World.Map.HasBuilding(new CellPos(11, 11)));
        Assert.True(simulation.World.Map.CanEnter(new CellPos(11, 11), Locomotor.Track));
    }

    [Fact]
    public void A_growing_building_and_someone_elses_are_not_sold()
    {
        var simulation = Match();
        var growing = Build(simulation, "reactor", 10, 10, ready: false);
        var theirs = Build(simulation, "reactor", 20, 10, Player2);

        Assert.DoesNotContain(Sell(simulation, growing), e => e is BuildingSold);
        Assert.DoesNotContain(Sell(simulation, theirs), e => e is BuildingSold);
        Assert.True(growing.IsAlive && theirs.IsAlive);
    }

    [Fact]
    public void A_harvester_of_a_sold_refinery_goes_to_another()
    {
        var simulation = Match();
        var sold = Build(simulation, "refinery", 10, 10);
        var other = Build(simulation, "refinery", 30, 10);
        var harvester = simulation.World.SpawnUnit(Player1, UnitType.Harvester, new CellPos(12, 18).Center);
        harvester.Harvester!.Load = 500;
        harvester.Harvester.Mode = HarvesterMode.SeekRefinery;
        simulation.Step();
        Assert.Equal(sold.Id, harvester.Harvester.Refinery);

        Sell(simulation, sold);
        simulation.Step();

        Assert.Equal(other.Id, harvester.Harvester.Refinery);
    }

    [Fact]
    public void Selling_a_prerequisite_lets_the_started_building_finish_but_refuses_a_new_order()
    {
        var simulation = Match();
        Build(simulation, "construction_yard", 2, 2);
        var reactor = Build(simulation, "reactor", 6, 2);
        simulation.Step();
        Send(simulation, new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, Index("barracks"), 1));

        Sell(simulation, reactor);
        var ready = false;
        for (var i = 0; i < 40 && !ready; i++)
        {
            simulation.Step();
            ready = simulation.LastTickEvents.OfType<ProductionReady>().Any();
        }

        Assert.True(ready);
        Send(simulation, new CancelProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, Index("barracks")));
        Send(simulation, new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, Index("barracks"), 1));
        Assert.Empty(simulation.World.Player(Player1).Queue(ProductionCategory.Building));
    }

    [Fact]
    public void A_whole_repair_costs_fifteen_percent_of_the_cost_and_turns_off_when_done()
    {
        var simulation = Match();
        var reactor = Build(simulation, "reactor", 10, 10);
        reactor.Hp = 1;
        var before = Credits(simulation);

        var toggled = Repair(simulation, reactor);
        Assert.Contains(new RepairToggled(simulation.CurrentTick - 1, reactor.Id, true), toggled);
        // RA2: 8 hit points every 14 ticks.
        Assert.InRange(reactor.Hp, 1, 1 + 8);

        var done = false;
        for (var i = 0; i < 1000 && !done; i++)
        {
            simulation.Step();
            done = simulation.LastTickEvents.OfType<RepairDone>().Any();
        }

        Assert.True(done);
        Assert.Equal(500, reactor.Hp);
        Assert.False(reactor.Repairing);

        // RA2 RepairPercent: 100 × 15 % × 499 / 500 = 14.97: the carried remainders add up to 14 exactly.
        Assert.Equal(14, before - Credits(simulation));
    }

    [Fact]
    public void Without_credits_a_repair_stands_and_goes_on_when_they_come()
    {
        var simulation = Match(credits: 0);
        var reactor = Build(simulation, "reactor", 10, 10);
        reactor.Hp = 100;
        Repair(simulation, reactor);
        for (var i = 0; i < 30; i++)
        {
            simulation.Step();
        }

        // A step of 8 costs 0.24 credits here: the first ones round down to nothing, until the carried remainder makes a credit.
        Assert.True(reactor.Repairing);
        Assert.InRange(reactor.Hp, 100, 100 + (8 * 4));

        simulation.World.Player(Player1).Credits = 1000;
        for (var i = 0; i < 800; i++)
        {
            simulation.Step();
        }

        Assert.Equal(500, reactor.Hp);
        Assert.True(Credits(simulation) >= 1000 - 15);
    }

    [Fact]
    public void A_repair_turned_off_stops_and_someone_elses_building_is_not_repaired()
    {
        var simulation = Match();
        var reactor = Build(simulation, "reactor", 10, 10);
        var theirs = Build(simulation, "reactor", 20, 10, Player2);
        reactor.Hp = theirs.Hp = 100;

        Repair(simulation, reactor);
        Repair(simulation, reactor);
        var hp = reactor.Hp;
        Repair(simulation, theirs);
        for (var i = 0; i < 10; i++)
        {
            simulation.Step();
        }

        Assert.Equal(hp, reactor.Hp);
        Assert.False(theirs.Repairing);
        Assert.Equal(100, theirs.Hp);
    }

    [Fact]
    public void A_line_of_five_walls_takes_the_ready_one_and_pays_for_four()
    {
        var simulation = Match();
        ReadyWall(simulation);
        var before = Credits(simulation);

        var events = Walls(simulation, Row(6, 10, 3));

        Assert.Equal(5, events.OfType<BuildingPlaced>().Count());
        Assert.Equal(4 * 100, before - Credits(simulation));
        Assert.Empty(simulation.World.Player(Player1).Queue(ProductionCategory.Defense));
    }

    [Fact]
    public void A_wall_line_skips_water_and_ends_when_the_credits_do()
    {
        var simulation = Match(credits: 10_000, new CellPos(8, 3));
        ReadyWall(simulation);
        simulation.World.Player(Player1).Credits = 250;

        var events = Walls(simulation, Row(6, 13, 3));

        // 6 (ready), 7 (paid), 8 water, 9 (paid), then 50 credits are left.
        Assert.Equal([new CellPos(6, 3), new CellPos(7, 3), new CellPos(9, 3)], events.OfType<BuildingPlaced>().Select(placed => placed.Cell));
        Assert.Equal(50, Credits(simulation));
    }

    [Fact]
    public void A_wall_goes_on_from_a_wall_but_no_other_building_stands_by_a_wall()
    {
        var simulation = Match();
        ReadyWall(simulation);

        var events = Walls(simulation, Row(6, 21, 3));

        Assert.Equal(16, events.OfType<BuildingPlaced>().Count());
        Assert.Equal(
            PlacementProblem.TooFar,
            PlacementRules.Check(simulation.World, Player1, TestRules.BuildingDef("reactor"), new CellPos(20, 4)));
    }

    [Fact]
    public void Walls_need_a_ready_wall()
    {
        var simulation = Match();
        Build(simulation, "construction_yard", 2, 2);
        Build(simulation, "barracks", 34, 24);
        simulation.Step();

        Assert.Contains(Walls(simulation, Row(6, 8, 3)), e => e is CannotPlace { Problem: PlacementProblem.NotReady });
    }

    [Fact]
    public void A_replay_with_a_sale_and_a_repair_plays_back_the_same()
    {
        var simulation = Match();
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Player1, 0, _sequence++, UnitType.Mcv, new CellPos(10, 10).Center));
        simulation.Step();
        recorder.AfterStep();
        var mcv = simulation.World.Entities.OfType<Unit>().Single();
        simulation.Enqueue(new DeployCommand(Player1, simulation.CurrentTick, _sequence++, mcv.Id));
        for (var i = 0; i < 40; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var yard = simulation.World.BuildingAt(new CellPos(10, 10))!;
        simulation.Enqueue(new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, yard.Cell, 300));
        simulation.Enqueue(new ToggleRepairCommand(Player1, simulation.CurrentTick, _sequence++, yard.Id));
        for (var i = 0; i < 30; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        simulation.Enqueue(new SellCommand(Player1, simulation.CurrentTick, _sequence++, yard.Id));
        simulation.Step();
        recorder.AfterStep();

        Assert.False(yard.IsAlive);
        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }
}
