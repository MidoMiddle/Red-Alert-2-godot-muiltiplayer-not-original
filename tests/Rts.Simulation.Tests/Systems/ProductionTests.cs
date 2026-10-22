using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.World;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// Production (docs/implementation/M3_base_economy.md, 4.10, 6): queues, gradual payment that adds up to the cost, waiting for credits,
/// pause, cancel with the refund of all that was paid, the multipliers of power and factories, and units coming out of the main factory.
/// </summary>
public sealed class ProductionTests
{
    private static readonly PlayerId Player1 = new(1);

    // A heavy tank of an odd cost and time, so that the payments do not split evenly.
    private const int TankCost = 937;
    private const int TankTime = 41;

    private uint _sequence;

    /// <summary>The rules of the tests with the heavy tank at <see cref="TankCost"/> and <see cref="TankTime"/>.</summary>
    private static GameRules Rules { get; } = OddTank();

    private static GameRules OddTank()
    {
        var database = TestRules.Database();
        var builder = new RulesBuilder();
        foreach (var definition in database.Locomotors)
        {
            builder.Add(definition);
        }

        foreach (var definition in database.Terrains)
        {
            builder.Add(definition);
        }

        foreach (var definition in database.Units)
        {
            builder.Add(definition.Id == "heavy_tank" ? definition with { Cost = TankCost, BuildTime = TankTime } : definition);
        }

        foreach (var definition in database.Buildings)
        {
            builder.Add(definition);
        }

        builder.Add(database.Factions.Single());
        builder.Add(database.Economies.Single());
        return GameRules.From(builder.Build());
    }

    private static int Unit(string id) => Rules.Database.IndexOfUnit(id);

    private static int BuildingIndex(string id) => Rules.Database.IndexOfBuilding(id);

    private static GameSimulation Match(int credits = 10_000) =>
        new(new SimulationSettings(1) { MapWidthCells = 48, MapHeightCells = 32, StartingCredits = credits }, Rules);

    /// <summary>A ready building of player 1; play a tick afterwards so that technologies and power know it.</summary>
    private static Building Build(GameSimulation simulation, string id, int x, int y) =>
        simulation.World.CreateBuilding(Player1, Rules.Database.Buildings[BuildingIndex(id)], new CellPos(x, y), ready: true, null);

    /// <summary>A yard, <paramref name="factories"/> war factories in a row and, unless <paramref name="lowPower"/>, enough reactors.</summary>
    private static List<Building> Base(GameSimulation simulation, int factories, bool lowPower = false)
    {
        Build(simulation, "construction_yard", 1, 1);
        var built = new List<Building>();
        for (var i = 0; i < factories; i++)
        {
            built.Add(Build(simulation, "war_factory", 1 + 4 * i, 6));
        }

        for (var i = 0; !lowPower && i < factories; i++)
        {
            Build(simulation, "reactor", 5 + 3 * i, 1);
        }

        simulation.Step();
        return built;
    }

    private PlayerState Player(GameSimulation simulation) => simulation.World.Player(Player1);

    private List<SimEvent> Send(GameSimulation simulation, Command command)
    {
        simulation.Enqueue(command);
        simulation.Step();
        return simulation.LastTickEvents.ToList();
    }

    private List<SimEvent> Order(GameSimulation simulation, ProductionKind kind, int index, int count = 1) =>
        Send(simulation, new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, kind, index, count));

    private List<SimEvent> Tanks(GameSimulation simulation, int count = 1) => Order(simulation, ProductionKind.Unit, Unit("heavy_tank"), count);

    /// <summary>Plays until a unit is produced; returns the ticks it took, or -1.</summary>
    private static int UntilProduced(GameSimulation simulation, int limit = 400)
    {
        for (var i = 1; i <= limit; i++)
        {
            simulation.Step();
            if (simulation.LastTickEvents.OfType<UnitProduced>().Any())
            {
                return i;
            }
        }

        return -1;
    }

    [Theory]
    [InlineData(1, false, -1)]
    [InlineData(2, false, -1)]
    [InlineData(3, false, 7)]
    [InlineData(5, false, 2)]
    [InlineData(1, true, -1)]
    [InlineData(2, true, 13)]
    public void The_payments_add_up_to_the_cost(int factories, bool lowPower, int pauseAt)
    {
        var simulation = Match();
        Base(simulation, factories, lowPower);
        var before = Player(simulation).Credits;

        Tanks(simulation);
        if (pauseAt >= 0)
        {
            for (var i = 0; i < pauseAt; i++)
            {
                simulation.Step();
            }

            Send(simulation, new PauseProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Unit, Unit("heavy_tank")));
            var paused = Player(simulation).Credits;
            for (var i = 0; i < 10; i++)
            {
                simulation.Step();
            }

            Assert.Equal(paused, Player(simulation).Credits);
            Tanks(simulation);
        }

        Assert.NotEqual(-1, UntilProduced(simulation, 1000));
        Assert.Equal(TankCost, before - Player(simulation).Credits);
    }

    [Fact]
    public void Without_credits_production_waits_and_goes_on_when_they_come()
    {
        var simulation = Match(credits: 300);
        Base(simulation, 1);
        Tanks(simulation);
        var events = new List<SimEvent>();
        for (var i = 0; i < 60; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
            Assert.True(Player(simulation).Credits >= 0);
        }

        var item = Player(simulation).Queue(ProductionCategory.Vehicle)[0];
        Assert.Single(events.OfType<InsufficientFunds>());
        Assert.True(item.WaitingForFunds);
        Assert.Equal(300, item.Paid + Player(simulation).Credits);
        var stalled = item.Progress;
        simulation.Step();
        Assert.Equal(stalled, item.Progress);

        Player(simulation).Credits += 1000;
        Assert.NotEqual(-1, UntilProduced(simulation));
        Assert.Equal(1300 - TankCost, Player(simulation).Credits);
    }

    [Fact]
    public void Cancelling_gives_back_all_that_was_paid_and_nothing_for_an_item_not_started()
    {
        var simulation = Match();
        Base(simulation, 1);
        var before = Player(simulation).Credits;
        Tanks(simulation, count: 2);
        for (var i = 0; i < 10; i++)
        {
            simulation.Step();
        }

        var paid = Player(simulation).Queue(ProductionCategory.Vehicle)[0].Paid;
        Assert.True(paid > 0);

        // The last one goes first: it has not started.
        var cancel = new CancelProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Unit, Unit("heavy_tank"));
        Assert.Contains(new ProductionCancelled(simulation.CurrentTick, Player1, ProductionKind.Unit, Unit("heavy_tank"), 0), Send(simulation, cancel));

        var paidNow = Player(simulation).Queue(ProductionCategory.Vehicle)[0].Paid;
        cancel = new CancelProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Unit, Unit("heavy_tank"));
        var refunded = Send(simulation, cancel).OfType<ProductionCancelled>().Single().Refund;

        Assert.True(refunded >= paidNow);
        Assert.Equal(before, Player(simulation).Credits);
        Assert.Empty(Player(simulation).Queue(ProductionCategory.Vehicle));
    }

    [Fact]
    public void A_ready_building_cancelled_gives_back_its_whole_cost()
    {
        var simulation = Match();
        Base(simulation, 1);
        var before = Player(simulation).Credits;
        Order(simulation, ProductionKind.Building, BuildingIndex("reactor"));
        for (var i = 0; i < 30; i++)
        {
            simulation.Step();
        }

        Assert.True(Player(simulation).Queue(ProductionCategory.Building)[0].Ready);
        Assert.Equal(before - 100, Player(simulation).Credits);

        Send(simulation, new CancelProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Building, BuildingIndex("reactor")));

        Assert.Equal(before, Player(simulation).Credits);
    }

    [Theory]
    [InlineData(1, false, TankTime)]
    [InlineData(2, false, 28)]   // 1.5 times faster: 41 000 / 1500, rounded up
    [InlineData(3, false, 21)]   // twice
    [InlineData(5, false, 14)]   // 300 %, the most: 41 000 / 3000
    [InlineData(1, true, 164)]   // no power at all: 25 %
    public void Factories_and_power_set_the_speed(int factories, bool lowPower, int ticks)
    {
        var simulation = Match();
        Base(simulation, factories, lowPower);

        Tanks(simulation);

        // The order tick already counts.
        Assert.Equal(ticks, UntilProduced(simulation, 400) + 1);
    }

    [Theory]
    [InlineData(150, 200, 750)]
    [InlineData(100, 200, 500)]
    [InlineData(10, 200, 250)]
    [InlineData(0, 50, 250)]
    public void Low_power_slows_production_to_output_by_drain_but_not_below_a_quarter(int output, int drain, int rate)
    {
        var simulation = Match();
        var player = Player(simulation);
        player.PowerOutput = output;
        player.PowerDrain = drain;
        player.LowPower = true;

        Assert.Equal(rate, ProductionSystem.RatePermille(simulation.World, player, factories: 1));
    }

    [Fact]
    public void Without_a_factory_nothing_is_ordered_and_a_lost_factory_cancels_its_queue_with_a_refund()
    {
        var simulation = Match();
        Build(simulation, "construction_yard", 1, 1);
        simulation.Step();
        Tanks(simulation);
        Assert.Empty(Player(simulation).Queue(ProductionCategory.Vehicle));

        var factory = Build(simulation, "war_factory", 1, 6);
        Build(simulation, "reactor", 5, 1);
        simulation.Step();
        Tanks(simulation);
        for (var i = 0; i < 5; i++)
        {
            simulation.Step();
        }

        var queue = Player(simulation).Queue(ProductionCategory.Vehicle);
        var paid = queue.Sum(item => item.Paid);
        var credits = Player(simulation).Credits;
        Assert.True(paid > 0);
        Send(simulation, new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, factory.Cell, DamageMapObjectCommand.MaxAmount));

        // M4 step 9, as in RA2: the last factory of the category fell, so its queue is cancelled and what was paid comes back.
        Assert.Empty(queue);
        Assert.Equal(credits + paid, Player(simulation).Credits);
        Assert.Contains(simulation.LastTickEvents, e => e is ProductionCancelled { Kind: ProductionKind.Unit } cancelled && cancelled.Refund > 0);
    }

    [Fact]
    public void A_lost_prerequisite_refuses_new_orders_but_lets_the_started_one_finish()
    {
        var simulation = Match();
        Base(simulation, 1);
        Build(simulation, "radar", 20, 1);
        var techCenter = Build(simulation, "tech_center", 24, 1);
        Build(simulation, "reactor", 30, 1);
        simulation.Step();
        Order(simulation, ProductionKind.Unit, Unit("tesla_tank"));
        Assert.Single(Player(simulation).Queue(ProductionCategory.Vehicle));

        Send(simulation, new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, techCenter.Cell, DamageMapObjectCommand.MaxAmount));
        simulation.Step();
        Order(simulation, ProductionKind.Unit, Unit("tesla_tank"));

        Assert.Single(Player(simulation).Queue(ProductionCategory.Vehicle));
        Assert.NotEqual(-1, UntilProduced(simulation));
    }

    [Fact]
    public void A_queue_of_units_holds_fifteen_and_a_building_queue_one()
    {
        var simulation = Match();
        Base(simulation, 1);
        for (var i = 0; i < 4; i++)
        {
            Tanks(simulation, count: 5);
        }

        Assert.Equal(ProductionRules.MaxUnitQueue, Player(simulation).Queue(ProductionCategory.Vehicle).Count);

        Order(simulation, ProductionKind.Building, BuildingIndex("reactor"));
        Order(simulation, ProductionKind.Building, BuildingIndex("refinery"));
        Assert.Equal([BuildingIndex("reactor")], Player(simulation).Queue(ProductionCategory.Building).Select(item => item.DefIndex));
    }

    [Fact]
    public void A_paused_item_costs_nothing_and_an_order_resumes_it()
    {
        var simulation = Match();
        Base(simulation, 1);
        Tanks(simulation);
        Send(simulation, new PauseProductionCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Unit, Unit("heavy_tank")));
        var item = Player(simulation).Queue(ProductionCategory.Vehicle)[0];
        var (progress, credits) = (item.Progress, Player(simulation).Credits);
        for (var i = 0; i < 10; i++)
        {
            simulation.Step();
        }

        Assert.True(item.Paused);
        Assert.Equal((progress, credits), (item.Progress, Player(simulation).Credits));

        Tanks(simulation);
        Assert.False(item.Paused);
        Assert.Single(Player(simulation).Queue(ProductionCategory.Vehicle));
        Assert.True(item.Progress > progress);
    }

    [Fact]
    public void A_building_not_produced_cannot_be_placed()
    {
        var simulation = Match();
        Base(simulation, 1);

        var events = Send(simulation, new PlaceBuildingCommand(Player1, simulation.CurrentTick, _sequence++, BuildingIndex("reactor"), new CellPos(5, 1)));

        Assert.Contains(events, e => e is CannotPlace { Problem: PlacementProblem.NotReady });
    }

    [Fact]
    public void A_unit_comes_out_of_the_last_factory_built_and_drives_one_cell_on()
    {
        var simulation = Match();
        var factories = Base(simulation, 2);
        Tanks(simulation);
        UntilProduced(simulation);

        var produced = simulation.LastTickEvents.OfType<UnitProduced>().Single();
        Assert.Equal(factories[1].Id, produced.Factory);
        simulation.World.TryGet(produced.Unit, out var entity);
        var tank = (Unit)entity;
        var exit = new CellPos(factories[1].Cell.X + 1, factories[1].Cell.Y + 4);
        Assert.Equal(exit, tank.Position.ToCell());

        for (var i = 0; i < 60; i++)
        {
            simulation.Step();
        }

        Assert.Equal(new CellPos(exit.X, exit.Y + 1), tank.Position.ToCell());
    }

    [Fact]
    public void A_unit_drives_to_the_rally_point_of_its_factory()
    {
        var simulation = Match();
        var factory = Base(simulation, 1)[0];
        Send(simulation, new SetRallyPointCommand(Player1, simulation.CurrentTick, _sequence++, factory.Id, new CellPos(30, 20).Center));
        Assert.Equal(new CellPos(30, 20).Center, factory.RallyPoint);

        Tanks(simulation);
        UntilProduced(simulation);
        var tank = simulation.World.Entities.OfType<Unit>().Single();
        for (var i = 0; i < 300 && tank.Position.ToCell() != new CellPos(30, 20); i++)
        {
            simulation.Step();
        }

        Assert.Equal(new CellPos(30, 20), tank.Position.ToCell());
    }

    [Fact]
    public void A_unit_waits_while_the_exit_is_taken()
    {
        var simulation = Match();
        var factory = Base(simulation, 1)[0];
        var exit = new CellPos(factory.Cell.X + 1, factory.Cell.Y + 4);
        var blocker = simulation.World.SpawnForTest(Player1, exit.Center, speed: 0, UnitType.HeavyTank);
        Tanks(simulation);

        Assert.Equal(-1, UntilProduced(simulation, 80));
        Assert.True(Player(simulation).Queue(ProductionCategory.Vehicle)[0].Ready);

        blocker.MarkDead();
        Assert.NotEqual(-1, UntilProduced(simulation, 5));
        Assert.Empty(Player(simulation).Queue(ProductionCategory.Vehicle));
    }

    [Fact]
    public void A_replay_with_production_plays_back_the_same()
    {
        var simulation = Match();
        var recorder = new ReplayRecorder(simulation);
        Base(simulation, 1);
        recorder.AfterStep();
        simulation.Enqueue(new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, ProductionKind.Unit, Unit("heavy_tank"), 3));
        for (var i = 0; i < 250; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        Assert.Equal(3, simulation.World.Entities.OfType<Unit>().Count());

        // The base is not built by commands: the replay starts from the same world by playing the same setup first.
        var replay = recorder.Build();
        var again = Match();
        Base(again, 1);
        foreach (var command in replay.Commands)
        {
            again.Enqueue(command);
        }

        while (again.CurrentTick < simulation.CurrentTick)
        {
            again.Step();
        }

        Assert.Equal(StateHasher.Compute(simulation.World), StateHasher.Compute(again.World));
    }
}
