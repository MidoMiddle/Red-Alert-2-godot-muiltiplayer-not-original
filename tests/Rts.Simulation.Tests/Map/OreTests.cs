using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.World;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>
/// Ore (docs/implementation/M3_base_economy.md, 4.12, 6): a layer of the map that grows and spreads by the world's random generator
/// every 30 seconds, takes part in the hash, the copies and the replays, stops buildings and lets units pass.
/// </summary>
public sealed class OreTests
{
    private static readonly PlayerId Player1 = new(1);

    /// <summary>
    /// A 32 × 24 map: clear land, a water pond, a road, a rock and a tree near the field, a 4 × 4 field of ore of stage 3 at (10, 10)
    /// and two cells of gems.
    /// </summary>
    private static MapGrid FieldMap()
    {
        var rows = new char[24][];
        for (var y = 0; y < rows.Length; y++)
        {
            rows[y] = Enumerable.Repeat('.', 32).ToArray();
        }

        foreach (var (x, y, code) in new[] { (9, 9, '~'), (10, 9, '~'), (14, 10, '='), (14, 11, '='), (9, 12, '#'), (12, 14, 'T'), (13, 14, ',') })
        {
            rows[y][x] = code;
        }

        var map = TestMapParser.Parse(string.Join("\n", rows.Select(row => string.Join(" ", row))), TestRules.Standard).Grid;
        for (var y = 10; y < 14; y++)
        {
            for (var x = 10; x < 14; x++)
            {
                map.SetOre(new CellPos(x, y), new OreCell(OreKind.Ore, 3));
            }
        }

        map.SetOre(new CellPos(20, 5), new OreCell(OreKind.Gems, 7));
        map.SetOre(new CellPos(21, 5), new OreCell(OreKind.Gems, 11));
        return map;
    }

    private static GameSimulation Match(ulong seed = 7) => new(new SimulationSettings(seed, FieldMap()), TestRules.Standard);

    /// <summary>Rounds of growth on a world without units, faster than playing the ticks.</summary>
    private static void Grow(GameWorld world, int rounds)
    {
        for (var i = 1; i <= rounds; i++)
        {
            world.CurrentTick = i * world.Rules.Economy.OreGrowthInterval;
            OreGrowthSystem.Update(world);
        }
    }

    private static (int Cells, int Credits) Count(GameWorld world)
    {
        var (cells, credits) = (0, 0);
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                var ore = world.Map.OreAt(new CellPos(x, y));
                cells += ore.IsEmpty ? 0 : 1;
                credits += world.Rules.CreditsOf(ore);
            }
        }

        return (cells, credits);
    }

    [Fact]
    public void Ten_minutes_of_growth_give_the_reference_field()
    {
        var simulation = Match();
        Assert.Equal((18, 1600 + 400 + 600), Count(simulation.World));

        for (var i = 0; i < 9000; i++)
        {
            simulation.Step();
        }

        // 20 rounds of growth. Changes only when the rules of growth, the random generator or the map digest change.
        // Rules v99: the map's digest takes the docks open in buildings (none here); the field is the same.
        Assert.Equal((20, 4075, 0xc659f737b237789fUL), (Count(simulation.World).Cells, Count(simulation.World).Credits, simulation.World.Map.Digest));
    }

    [Fact]
    public void The_same_seed_grows_the_same_ore_and_another_seed_other_ore()
    {
        var first = Match(seed: 7);
        var second = Match(seed: 7);
        var other = Match(seed: 8);
        Grow(first.World, 20);
        Grow(second.World, 20);
        Grow(other.World, 20);

        Assert.Equal(first.World.Map.OreBytes(), second.World.Map.OreBytes());
        Assert.NotEqual(first.World.Map.OreBytes(), other.World.Map.OreBytes());
    }

    [Fact]
    public void Nothing_grows_before_the_interval()
    {
        var simulation = Match();
        var before = simulation.World.Map.OreBytes();

        // Ticks 0 … interval − 1: no round yet.
        for (var i = 0; i < simulation.World.Rules.Economy.OreGrowthInterval; i++)
        {
            simulation.Step();
        }

        Assert.Equal(before, simulation.World.Map.OreBytes());
        simulation.Step();
        Assert.NotEqual(before, simulation.World.Map.OreBytes());
    }

    [Fact]
    public void Ore_stops_at_the_last_stage_and_gems_never_change()
    {
        var world = Match().World;

        Grow(world, 300);

        Assert.Equal(new OreCell(OreKind.Ore, OreCell.MaxStage), world.Map.OreAt(new CellPos(11, 11)));
        Assert.Equal(new OreCell(OreKind.Gems, 7), world.Map.OreAt(new CellPos(20, 5)));
        Assert.Equal(new OreCell(OreKind.Gems, 11), world.Map.OreAt(new CellPos(21, 5)));
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                Assert.True(world.Map.OreAt(new CellPos(x, y)).Stage <= OreCell.MaxStage);
            }
        }
    }

    [Fact]
    public void Ore_spreads_only_on_clear_or_rough_land_with_nothing_on_it()
    {
        var world = Match().World;
        var reactor = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(15, 12), ready: true, null);

        Grow(world, 300);

        var (cells, _) = Count(world);
        Assert.True(cells > 30, $"only {cells} cells of ore after 300 rounds");
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                var cell = new CellPos(x, y);
                if (world.Map.OreAt(cell).Kind == OreKind.Ore)
                {
                    Assert.True(world.Map.LandAt(cell) is LandType.Clear or LandType.Rough, $"ore on {world.Map.LandAt(cell)} at {cell}");
                    Assert.False(reactor.Covers(cell), $"ore under the reactor at {cell}");
                    Assert.NotEqual(new CellPos(12, 14), cell);
                }
            }
        }
    }

    [Fact]
    public void Ore_takes_part_in_the_digest_the_copies_and_equality()
    {
        var map = FieldMap();
        var digest = map.Digest;
        var copy = map.Clone();

        Assert.True(copy.ContentEquals(map));
        Assert.Equal(map.OreBytes(), copy.OreBytes());

        copy.SetOre(new CellPos(1, 1), new OreCell(OreKind.Ore, 0));
        Assert.False(copy.ContentEquals(map));
        Assert.NotEqual(digest, copy.Digest);
        Assert.Equal(digest, map.Digest);
    }

    [Fact]
    public void Ore_survives_a_replay()
    {
        var replay = new Replay(SimulationVersion.Current, 0, new SimulationSettings(3, FieldMap()), 0, [], []);

        var read = ReplayFormat.Read(ReplayFormat.Write(replay));

        Assert.Equal(FieldMap().OreBytes(), read.Settings.Map!.OreBytes());
    }

    [Theory]
    [InlineData(0x1C)]
    [InlineData(0x0C)]
    [InlineData(0x30)]
    public void An_ore_byte_that_means_nothing_is_refused(int packed)
    {
        var ore = new byte[4];
        ore[2] = (byte)packed;

        Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(2, 2, new byte[] { 1, 1, 1, 1 }, new byte[4], default, null, null, null, ore));
    }

    [Fact]
    public void No_building_stands_on_ore_and_no_mcv_deploys_on_it()
    {
        var simulation = Match();
        var world = simulation.World;

        Assert.Equal(PlacementProblem.Ore, PlacementRules.Check(world, Player1, TestRules.BuildingDef("reactor"), new CellPos(13, 13), needsBase: false));

        // The yard around (14, 13) would cover the ore at (13, 12) and (13, 13).
        simulation.Enqueue(new SpawnTestUnitCommand(Player1, 0, 0, UnitType.Mcv, new CellPos(14, 13).Center));
        simulation.Step();
        var mcv = world.Entities.OfType<Unit>().Single();
        simulation.Enqueue(new DeployCommand(Player1, simulation.CurrentTick, 1, mcv.Id));
        simulation.Step();

        Assert.Contains(simulation.LastTickEvents, e => e is CannotDeploy { Problem: PlacementProblem.Ore });
    }

    [Fact]
    public void Units_drive_over_ore()
    {
        var simulation = Match();
        var world = simulation.World;
        Assert.True(world.Map.CanEnter(new CellPos(11, 11), Locomotor.Track));

        var tank = world.SpawnForTest(Player1, new CellPos(6, 11).Center, speed: 36, UnitType.HeavyTank);
        simulation.Enqueue(new MoveCommand(Player1, 0, 0, [tank.Id], new CellPos(18, 11).Center));
        var crossed = false;
        for (var i = 0; i < 200 && tank.Position.ToCell() != new CellPos(18, 11); i++)
        {
            simulation.Step();
            crossed |= !world.Map.OreAt(tank.Position.ToCell()).IsEmpty;
        }

        Assert.True(crossed);
        Assert.Equal(new CellPos(18, 11), tank.Position.ToCell());
    }
}
