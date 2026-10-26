using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// Ore and gem sources (docs/implementation/M3_base_economy.md, 4.12, step 16): a drill renews the field within its radius, a field
/// without one runs out; an eruption adds a stage of the source's own kind to one cell that can take it, on the source's own phase of
/// the interval; a map without sources draws nothing from the generator; a source stops movers, buildings and ore on its cell.
/// </summary>
public sealed class OreSourceTests(ITestOutputHelper output)
{
    private static readonly PlayerId Player1 = new(1);
    private const int Minute = 60 * 15;

    /// <summary>A map of clear land, <paramref name="width"/> × <paramref name="height"/>, with the extra lines of a test map.</summary>
    private static MapGrid Map(int width, int height, params string[] lines)
    {
        var row = string.Join(" ", Enumerable.Repeat(".", width));
        var text = string.Join("\n", Enumerable.Repeat(row, height).Concat(lines));
        return TestMapParser.Parse(text, TestRules.Standard).Grid;
    }

    /// <summary>A 40 × 30 map with a ring of eight cells of full ore round (24, 15), and an ore source in the middle when asked.</summary>
    private static MapGrid RingField(bool source)
    {
        string[] ring = ["ore 23 14 3 1 11", "ore 23 15 1 1 11", "ore 25 15 1 1 11", "ore 23 16 3 1 11"];
        return Map(40, 30, source ? ["object ore_source 24 15", .. ring] : ring);
    }

    private static int CreditsWithin(GameWorld world, CellPos center, int radius)
    {
        var credits = 0;
        for (var y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
            {
                credits += world.Rules.CreditsOf(world.Map.OreAt(new CellPos(x, y)));
            }
        }

        return credits;
    }

    /// <summary>One harvester at a refinery by the ring for 14 minutes: the credits, the unloads of the last five minutes, the ore left.</summary>
    private (int Credits, int LateUnloads, int OreLeft) Harvest(bool source)
    {
        var simulation = new GameSimulation(new SimulationSettings(1, RingField(source)) { StartingCredits = 0 }, TestRules.Standard);
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("refinery"), new CellPos(12, 13), true, null);
        simulation.World.SpawnUnit(Player1, UnitType.Harvester, new CellPos(14, 18).Center);
        var lateUnloads = 0;
        for (var tick = 0; tick < 14 * Minute; tick++)
        {
            simulation.Step();
            if (tick >= 9 * Minute)
            {
                lateUnloads += simulation.LastTickEvents.OfType<HarvesterUnloading>().Count();
            }
        }

        var result = (simulation.World.Player(Player1).Credits, lateUnloads, CreditsWithin(simulation.World, new CellPos(24, 15), 3));
        output.WriteLine($"source {source}: {result}");
        return result;
    }

    [Fact]
    public void A_field_with_a_source_renews_and_one_without_runs_out()
    {
        var without = Harvest(source: false);
        var with = Harvest(source: true);

        // Without a drill the eight cells (2400 credits and what grew before the harvester came) are gone long before the end; nothing grows on empty cells.
        Assert.Equal(0, without.LateUnloads);
        Assert.Equal(0, without.OreLeft);
        Assert.InRange(without.Credits, 2400, 3000);

        // With one the harvester still unloads in the last five minutes, and the field brought in well more than it held.
        Assert.True(with.LateUnloads >= 1, $"{with.LateUnloads} unloads in the last five minutes");
        Assert.True(with.Credits > 2 * without.Credits, $"{with.Credits} with the source, {without.Credits} without");
    }

    [Fact]
    public void A_source_erupts_on_its_own_phase_of_the_interval()
    {
        var interval = TestRules.Standard.Economy.SourceInterval;
        for (var id = 1; id <= 3; id++)
        {
            var ticks = Enumerable.Range(0, 3 * interval + 1).Where(t => OreSourceSystem.EruptsOn(t, id, interval)).ToList();
            Assert.Equal(3, ticks.Count);
            Assert.All(ticks, t => Assert.Equal(0, (t + id * OreSourceSystem.PhaseStep) % interval));
            Assert.DoesNotContain(0, ticks);
        }

        Assert.NotEqual(Next(1, interval), Next(2, interval));

        static int Next(int id, int interval) => Enumerable.Range(1, interval).First(t => OreSourceSystem.EruptsOn(t, id, interval));
    }

    /// <summary>The events of the first eruption of the map's only source.</summary>
    private static (GameWorld World, List<SimEvent> Events) FirstEruption(MapGrid map)
    {
        var world = new GameSimulation(new SimulationSettings(5, map), TestRules.Standard).World;
        world.CurrentTick = Enumerable.Range(1, 1000).First(t => OreSourceSystem.EruptsOn(t, map.Sources[0].Id, world.Rules.Economy.SourceInterval));
        var events = new List<SimEvent>();
        OreSourceSystem.Update(world, events);
        return (world, events);
    }

    [Fact]
    public void An_eruption_puts_stage_zero_of_its_kind_on_an_empty_cell_within_the_radius()
    {
        var (world, events) = FirstEruption(Map(24, 20, "object gem_source 10 10"));

        var erupted = Assert.IsType<OreSourceErupted>(Assert.Single(events));
        Assert.Equal((world.Map.Sources[0].Id, OreKind.Gems), (erupted.Source, erupted.Kind));
        Assert.Equal(new OreCell(OreKind.Gems, 0), world.Map.OreAt(erupted.Cell));
        var (dx, dy) = (erupted.Cell.X - 10, erupted.Cell.Y - 10);
        Assert.InRange(dx * dx + dy * dy, 1, world.Rules.Economy.SourceRadius * world.Rules.Economy.SourceRadius);
    }

    [Fact]
    public void An_eruption_adds_a_stage_to_a_cell_of_its_kind_below_the_last()
    {
        // Within radius 3 of (10, 10) every cell is full gems but (12, 10), of stage 4: the only cell that can take a stage.
        var map = Map(24, 20, "object gem_source 10 10", "gems 7 7 7 3", "gems 7 10 3 1", "gems 11 10 3 1", "gems 7 11 7 3");
        map.SetOre(new CellPos(12, 10), new OreCell(OreKind.Gems, 4));

        var (world, events) = FirstEruption(map);

        var erupted = Assert.IsType<OreSourceErupted>(Assert.Single(events));
        Assert.Equal(new CellPos(12, 10), erupted.Cell);
        Assert.Equal(new OreCell(OreKind.Gems, 5), world.Map.OreAt(erupted.Cell));
        Assert.True(world.Map.OreAt(new CellPos(10, 10)).IsEmpty);
    }

    [Fact]
    public void A_source_leaves_the_other_kind_alone_and_with_nothing_to_take_it_does_not_erupt()
    {
        // An ore source in a full field of ore within its radius; a gem source in a field of ore.
        var full = Map(24, 20, "object ore_source 10 10", "ore 6 6 9 4 11", "ore 6 10 4 1 11", "ore 11 10 4 1 11", "ore 6 11 9 4 11");
        var foreign = Map(24, 20, "object gem_source 10 10", "ore 6 6 9 4 3", "ore 6 10 4 1 3", "ore 11 10 4 1 3", "ore 6 11 9 4 3");
        foreach (var map in new[] { full, foreign })
        {
            var world = new GameSimulation(new SimulationSettings(5, map), TestRules.Standard).World;
            var digest = map.Digest;
            var random = world.Random.State;
            var events = new List<SimEvent>();
            for (var tick = 1; tick <= 4 * world.Rules.Economy.SourceInterval; tick++)
            {
                world.CurrentTick = tick;
                OreSourceSystem.Update(world, events);
            }

            Assert.Empty(events);
            Assert.Equal(digest, map.Digest);
            Assert.Equal(random, world.Random.State);
        }
    }

    [Fact]
    public void A_map_without_sources_draws_nothing_from_the_generator()
    {
        var world = new GameSimulation(new SimulationSettings(5, RingField(source: false)), TestRules.Standard).World;
        var random = world.Random.State;
        for (var tick = 1; tick <= 3 * world.Rules.Economy.SourceInterval; tick++)
        {
            world.CurrentTick = tick;
            OreSourceSystem.Update(world, null);
        }

        Assert.Equal(random, world.Random.State);
    }

    [Fact]
    public void A_source_stops_movers_buildings_and_ore_on_its_cell()
    {
        var simulation = new GameSimulation(new SimulationSettings(5, RingField(source: true)), TestRules.Standard);
        var cell = new CellPos(24, 15);
        foreach (var locomotor in Enum.GetValues<Locomotor>().Where(l => l != Locomotor.Naval))
        {
            Assert.False(simulation.World.Map.CanEnter(cell, locomotor), $"{locomotor} enters the source");
        }

        Assert.Equal(PlacementProblem.Taken, PlacementRules.CellProblem(simulation.World, cell, simulation.World.Map.Heights.CellLevel(cell)));
        Assert.False(OreGrowthSystem.CanTakeOre(simulation.World.Map, cell));
        Assert.False(MapObjectTypes.Get(MapObjectKind.OreSource).Destructible);
        Assert.False(MapObjectTypes.Get(MapObjectKind.GemSource).Destructible);
    }

    [Fact]
    public void A_match_with_sources_replays_to_the_same_hashes()
    {
        var simulation = new GameSimulation(new SimulationSettings(9, Map(32, 24, "object ore_source 8 8", "ore 9 8 2 2 2",
            "object gem_source 20 14", "gems 18 14 2 2 0")), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        var eruptions = 0;
        for (var i = 0; i < 3 * Minute; i++)
        {
            simulation.Step();
            recorder.AfterStep();
            eruptions += simulation.LastTickEvents.OfType<OreSourceErupted>().Count();
        }

        // 3 minutes of a 6 s interval: 30 eruptions of each source.
        Assert.Equal(60, eruptions);
        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }
}
