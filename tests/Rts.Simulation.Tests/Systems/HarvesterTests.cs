using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// The harvesters (docs/implementation/M3_base_economy.md, 4.13, 6): the headless scenarios of the plan — income of one harvester, five
/// at one refinery, a field that runs out, no ore, a refinery that falls — and the single rules of harvesting, unloading and orders.
/// </summary>
public sealed class HarvesterTests(ITestOutputHelper output)
{
    private static readonly PlayerId Player1 = new(1);
    private const int FiveMinutes = 5 * 60 * 15;

    private uint _sequence;

    /// <summary>A 48 × 40 match of clear land with square fields of full ore: (x, y, side) each.</summary>
    private static GameSimulation Match(params (int X, int Y, int Side)[] fields)
    {
        var map = new MapGrid(48, 40, TestRules.Standard.Land);
        foreach (var (fx, fy, side) in fields)
        {
            for (var y = fy; y < fy + side; y++)
            {
                for (var x = fx; x < fx + side; x++)
                {
                    map.SetOre(new CellPos(x, y), new OreCell(OreKind.Ore, OreCell.MaxStage));
                }
            }
        }

        return new GameSimulation(new SimulationSettings(1, map) { StartingCredits = 0 }, TestRules.Standard);
    }

    private static Building Refinery(GameSimulation simulation, int x, int y, bool ready = true) =>
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("refinery"), new CellPos(x, y), ready, null);

    private static Unit Harvester(GameSimulation simulation, int x, int y) =>
        simulation.World.SpawnUnit(Player1, UnitType.Harvester, new CellPos(x, y).Center);

    private static int Credits(GameSimulation simulation) => simulation.World.Player(Player1).Credits;

    /// <summary>Plays the ticks and counts the unloads of each harvester.</summary>
    private static Dictionary<EntityId, int> Play(GameSimulation simulation, int ticks)
    {
        var unloads = new Dictionary<EntityId, int>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            foreach (var unloading in simulation.LastTickEvents.OfType<HarvesterUnloading>())
            {
                unloads[unloading.Harvester] = unloads.GetValueOrDefault(unloading.Harvester) + 1;
            }
        }

        return unloads;
    }

    private static int IncomeOf(int harvesters)
    {
        var simulation = Match((18, 18, 8));
        Refinery(simulation, 10, 10);
        for (var i = 0; i < harvesters; i++)
        {
            Harvester(simulation, 8 + 2 * i, 16);
        }

        Play(simulation, FiveMinutes);
        return Credits(simulation);
    }

    [Fact]
    public void One_harvester_brings_a_steady_income_in_five_minutes()
    {
        var income = IncomeOf(1);
        output.WriteLine($"one harvester, five minutes: {income}");

        // A load of 1000 takes 40 stages of 12 ticks (32 s), plus the drive, the unloading and the searches: five loads in five minutes
        // on this map (M3 step 10). The range leaves room for tuning; the balance pass of step 14 sets it anew.
        Assert.InRange(income, 4000, 7000);
    }

    [Fact]
    public void The_dock_is_in_the_footprint_open_to_drive_on_and_closed_to_build_on()
    {
        // RA2: the pad the harvesters unload on is the refinery's own, on its east side.
        var simulation = Match((18, 18, 8));
        var refinery = Refinery(simulation, 10, 10);
        var dock = Rts.Simulation.Systems.HarvesterSystem.DockOf(refinery);
        var map = simulation.World.Map;

        Assert.True(refinery.Covers(dock));
        Assert.True(map.IsDock(dock));
        Assert.True(map.CanEnter(dock, Locomotor.Track));
        Assert.False(map.CanEnter(new CellPos(dock.X - 1, dock.Y), Locomotor.Track));
        Assert.NotEqual(PlacementProblem.None, PlacementRules.CellProblem(simulation.World, dock, map.Heights.CornerLevel(dock.X, dock.Y)));
        Assert.Equal(new CellPos(dock.X + 1, dock.Y), Rts.Simulation.Systems.HarvesterSystem.EntranceOf(refinery));
    }

    [Fact]
    public void Five_harvesters_at_one_refinery_all_work_and_the_dock_limits_the_income()
    {
        var simulation = Match((18, 18, 8));
        Refinery(simulation, 10, 10);
        var harvesters = Enumerable.Range(0, 5).Select(i => Harvester(simulation, 8 + 2 * i, 16)).ToList();

        var unloads = Play(simulation, FiveMinutes);

        output.WriteLine(string.Join("; ", harvesters.Select(h => $"{h.Id} {h.Harvester!.Mode} load {h.Harvester.Load} at {h.Position.ToCell().X},{h.Position.ToCell().Y} target {h.Harvester.Target} unloads {unloads.GetValueOrDefault(h.Id)}")));
        foreach (var harvester in harvesters)
        {
            Assert.True(unloads.GetValueOrDefault(harvester.Id) >= 2, $"{harvester.Id} unloaded {unloads.GetValueOrDefault(harvester.Id)} times");
        }

        var (one, three, five) = (IncomeOf(1), IncomeOf(3), IncomeOf(5));
        output.WriteLine($"income: 1 → {one}, 3 → {three}, 5 → {five}");
        Assert.True(three > one * 2, $"three harvesters earn {three}, one {one}");
        Assert.True(five >= three, $"five harvesters earn {five}, three {three}");
    }

    [Fact]
    public void A_harvester_moves_on_to_the_next_field_when_one_runs_out()
    {
        var simulation = Match((16, 12, 1), (34, 30, 3));
        simulation.World.Map.SetOre(new CellPos(16, 12), new OreCell(OreKind.Ore, 0));
        Refinery(simulation, 10, 10);
        Harvester(simulation, 12, 14);

        Play(simulation, FiveMinutes);

        Assert.True(simulation.World.Map.OreAt(new CellPos(16, 12)).IsEmpty);
        var left = Enumerable.Range(34, 3).SelectMany(x => Enumerable.Range(30, 3).Select(y => simulation.World.Map.OreAt(new CellPos(x, y)).Stage)).Sum();
        Assert.True(left < 9 * OreCell.MaxStage, "the far field was not touched");
        Assert.True(Credits(simulation) > 25);
    }

    [Fact]
    public void Without_ore_a_harvester_waits_and_searches_only_now_and_then()
    {
        var simulation = Match();
        Refinery(simulation, 10, 10);
        var harvester = Harvester(simulation, 12, 14);

        Play(simulation, 300);

        Assert.Equal(HarvesterMode.Waiting, harvester.Harvester!.Mode);
        Assert.InRange(harvester.Harvester.Searches, 1, 300 / Rts.Simulation.Systems.HarvesterSystem.RetryTicks + 1);
    }

    [Fact]
    public void When_its_refinery_falls_a_harvester_unloads_at_another_or_waits_with_its_load()
    {
        var simulation = Match((18, 18, 4));
        var near = Refinery(simulation, 14, 10);
        var far = Refinery(simulation, 36, 4);
        var harvester = Harvester(simulation, 16, 16);
        for (var i = 0; i < 2000 && harvester.Harvester!.Mode != HarvesterMode.MovingToRefinery; i++)
        {
            simulation.Step();
        }

        Assert.Equal(near.Id, harvester.Harvester!.Refinery);
        simulation.Enqueue(new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, near.Cell, DamageMapObjectCommand.MaxAmount));
        var unloads = Play(simulation, 600);

        Assert.Equal(far.Id, harvester.Harvester.Refinery == EntityId.None ? far.Id : harvester.Harvester.Refinery);
        Assert.True(unloads.GetValueOrDefault(harvester.Id) >= 1);
        Assert.True(Credits(simulation) >= 1000);

        // Without any refinery it keeps its load and waits.
        simulation.Enqueue(new DamageMapObjectCommand(Player1, simulation.CurrentTick, _sequence++, far.Cell, DamageMapObjectCommand.MaxAmount));
        Play(simulation, 1500);
        Assert.Equal(HarvesterMode.Waiting, harvester.Harvester.Mode);
        Assert.True(harvester.Harvester.Load > 0);
    }

    [Fact]
    public void A_stage_comes_off_every_harvest_time_and_an_empty_cell_disappears()
    {
        var simulation = Match((20, 20, 1));
        simulation.World.Map.SetOre(new CellPos(20, 20), new OreCell(OreKind.Ore, 1));
        var harvester = Harvester(simulation, 20, 20);
        simulation.Step();
        Assert.Equal(HarvesterMode.MovingToOre, harvester.Harvester!.Mode);
        simulation.Step();
        Assert.Equal(HarvesterMode.Harvesting, harvester.Harvester.Mode);

        var harvestTime = harvester.Archetype.HarvestTime;
        for (var i = 0; i < harvestTime - 1; i++)
        {
            simulation.Step();
        }

        Assert.Equal(0, harvester.Harvester.Load);
        simulation.Step();
        Assert.Equal(25, harvester.Harvester.Load);
        Assert.Equal(new OreCell(OreKind.Ore, 0), simulation.World.Map.OreAt(new CellPos(20, 20)));

        var events = new List<SimEvent>();
        for (var i = 0; i < harvestTime; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        Assert.Equal(50, harvester.Harvester.Load);
        Assert.True(simulation.World.Map.OreAt(new CellPos(20, 20)).IsEmpty);
        Assert.Contains(new OreDepleted(simulation.CurrentTick - 1, new CellPos(20, 20)), events);
    }

    [Fact]
    public void The_load_never_passes_the_capacity_and_unloads_whole_at_the_dock()
    {
        var simulation = Match((18, 18, 6));
        Refinery(simulation, 10, 10);
        var harvester = Harvester(simulation, 16, 16);
        var maxLoad = 0;
        var credits = new List<int>();
        for (var i = 0; i < 1500; i++)
        {
            simulation.Step();
            maxLoad = System.Math.Max(maxLoad, harvester.Harvester!.Load);
            if (harvester.Harvester.Mode == HarvesterMode.Unloading)
            {
                credits.Add(Credits(simulation));
            }
        }

        Assert.Equal(1000, maxLoad);
        Assert.True(credits.Count >= 20);
        Assert.Equal(50, credits[1] - credits[0]);
        Assert.Equal(0, Credits(simulation) % 25);
        Assert.True(Credits(simulation) >= 1000);
    }

    [Fact]
    public void Two_harvesters_never_claim_the_same_cell()
    {
        var simulation = Match((20, 20, 1));
        var first = Harvester(simulation, 18, 20);
        var second = Harvester(simulation, 22, 20);
        simulation.Step();

        Assert.Equal(new CellPos(20, 20), first.Harvester!.Target);
        Assert.NotEqual(new CellPos(20, 20), second.Harvester!.Target);
        Assert.Equal(HarvesterMode.Waiting, second.Harvester.Mode);
    }

    [Fact]
    public void A_grown_refinery_gives_a_free_harvester_at_its_dock()
    {
        var simulation = Match((20, 20, 3));
        var refinery = Refinery(simulation, 10, 10, ready: false);
        var events = Play(simulation, 30);

        var harvester = simulation.World.Entities.OfType<Unit>().Single();
        Assert.Equal(UnitType.Harvester, harvester.Type);
        Assert.Equal(new CellPos(13, 11), Rts.Simulation.Systems.HarvesterSystem.DockOf(refinery));
        Assert.Contains(harvester.Harvester!.Mode, new[] { HarvesterMode.MovingToOre, HarvesterMode.Harvesting });
    }

    [Fact]
    public void A_harvest_order_sends_it_to_another_field_and_a_move_onto_the_refinery_unloads()
    {
        var simulation = Match((14, 18, 2), (36, 30, 2));
        var refinery = Refinery(simulation, 10, 10);
        var harvester = Harvester(simulation, 12, 16);
        simulation.Step();
        Assert.Equal(new CellPos(14, 18), harvester.Harvester!.Field);

        simulation.Enqueue(new HarvestCommand(Player1, simulation.CurrentTick, _sequence++, [harvester.Id], new CellPos(36, 30)));
        simulation.Step();
        simulation.Step();
        Assert.Equal(new CellPos(36, 30), harvester.Harvester.Target);

        for (var i = 0; i < 600 && harvester.Harvester.Load < 200; i++)
        {
            simulation.Step();
        }

        simulation.Enqueue(new MoveCommand(Player1, simulation.CurrentTick, _sequence++, [harvester.Id], new CellPos(11, 11).Center));
        simulation.Step();
        simulation.Step();
        Assert.Equal(refinery.Id, harvester.Harvester.Refinery);
    }

    [Fact]
    public void After_a_plain_order_a_harvester_goes_back_to_work_in_thirty_seconds()
    {
        var simulation = Match((20, 20, 2));
        var harvester = Harvester(simulation, 20, 20);
        simulation.Step();

        simulation.Enqueue(new MoveCommand(Player1, simulation.CurrentTick, _sequence++, [harvester.Id], new CellPos(5, 5).Center));
        simulation.Step();
        Assert.Equal(HarvesterMode.Manual, harvester.Harvester!.Mode);

        Play(simulation, Rts.Simulation.Systems.HarvesterSystem.ManualTicks - 2);
        Assert.Equal(HarvesterMode.Manual, harvester.Harvester.Mode);
        Play(simulation, 3);
        Assert.Equal(HarvesterMode.MovingToOre, harvester.Harvester.Mode);
    }

    [Fact]
    public void A_replay_with_harvesting_plays_back_the_same()
    {
        var simulation = Match((18, 18, 4));
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Player1, 0, _sequence++, UnitType.Harvester, new CellPos(14, 14).Center));
        for (var i = 0; i < 900; i++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        Assert.True(simulation.World.Entities.OfType<Unit>().Single().Harvester!.Load > 0);
        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }
}
