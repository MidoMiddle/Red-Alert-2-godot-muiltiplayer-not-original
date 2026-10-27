using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Random;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Visibility;

/// <summary>
/// The fog of war of the simulation (docs/implementation/M4_combat.md, 4.14): the circles, the counts, shroud, fog and visible; the
/// incremental update against a count from scratch; who gives sight and who does not.
/// </summary>
public sealed class VisibilityTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld World() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    /// <summary>The counts of a player's grid made from scratch out of what stands now.</summary>
    private static ushort[] FromScratch(GameWorld world, PlayerId player)
    {
        var counts = new ushort[world.WidthCells * world.HeightCells];
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Owner != player || entity is not (Unit or Building))
            {
                continue;
            }

            if (VisibilitySystem.SightOf(world, entity) is not ({ } centre, var radius))
            {
                continue;
            }

            foreach (var offset in SightCircles.Of(radius))
            {
                var (x, y) = (centre.X + offset.X, centre.Y + offset.Y);
                if (x >= 0 && y >= 0 && x < world.WidthCells && y < world.HeightCells)
                {
                    counts[y * world.WidthCells + x]++;
                }
            }
        }

        return counts;
    }

    [Fact]
    public void A_circle_of_eight_holds_197_cells_and_is_cut_at_the_edge()
    {
        Assert.Equal(197, SightCircles.Of(8).Length);
        Assert.Equal(new CellPos(0, 0), Assert.Single(SightCircles.Of(0)));
        Assert.Empty(SightCircles.Of(-1));
        // Beyond the cached radii the circle is made when asked, the same way.
        Assert.Contains(new CellPos(33, 0), SightCircles.Of(33));

        var world = World();
        world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(20, 20).Center);
        var corner = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(0, 0).Center);
        VisibilitySystem.Update(world);

        Assert.Equal(197, world.VisionOf(Player1).Counts.ToArray().Count(count => count > 0));
        // In the corner a quarter of the circle and its two edges are left: (8 × 8 quarter with the axes) = 1 + 8 + 8 + 41.
        Assert.Equal(58, world.VisionOf(Player2).Counts.ToArray().Count(count => count > 0));
        Assert.True(corner.IsAlive);
    }

    [Fact]
    public void Two_sources_count_two_and_a_cell_left_behind_is_fog_not_shroud()
    {
        var world = World();
        var first = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(12, 10).Center);
        VisibilitySystem.Update(world);
        var grid = world.VisionOf(Player1);

        Assert.Equal(2, grid.CountAt(new CellPos(11, 10)));
        Assert.Equal(CellVisibility.Shroud, grid.StateAt(new CellPos(30, 30)));

        first.Position = new CellPos(30, 30).Center;
        VisibilitySystem.Update(world);

        Assert.Equal(1, grid.CountAt(new CellPos(11, 10)));
        Assert.Equal(CellVisibility.Fog, grid.StateAt(new CellPos(3, 10)));             // seen from 10,10 only
        Assert.Equal(CellVisibility.Visible, grid.StateAt(new CellPos(30, 30)));
        Assert.Equal(FromScratch(world, Player1), grid.Counts.ToArray());
    }

    [Fact]
    public void A_unit_on_a_hill_sees_a_larger_circle()
    {
        var heights = new byte[21 * 21];
        for (var y = 6; y <= 14; y++)
        {
            for (var x = 6; x <= 14; x++)
            {
                heights[y * 21 + x] = 4;
            }
        }

        var map = MapGrid.FromLayers(20, 20, Enumerable.Repeat((byte)LandType.Clear, 400).ToArray(), new byte[400], heights, types: TestRules.Standard.MapTypes);
        var world = new GameWorld(seed: 1, map, TestRules.Standard);
        world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        VisibilitySystem.Update(world);

        // Sight 8 + 2 for level 4: the cell 10 cells off along the row is seen.
        Assert.True(world.VisionOf(Player1).IsVisible(new CellPos(0, 10)));
    }

    [Fact]
    public void A_building_sees_from_the_middle_of_its_footprint_and_the_dead_see_nothing()
    {
        var world = World();
        var yard = world.CreateBuilding(Player1, TestRules.BuildingDef("construction_yard"), new CellPos(10, 10), ready: false, null);
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(30, 30).Center);
        VisibilitySystem.Update(world);
        var grid = world.VisionOf(Player1);

        // A growing yard of 3 × 3 sees 8 cells from its middle cell, 11,11.
        Assert.True(grid.IsVisible(new CellPos(19, 11)));
        Assert.False(grid.IsVisible(new CellPos(20, 11)));

        world.DestroyBuilding(yard, null);
        DeathSystem.Kill(world, tank, EntityId.None, PlayerId.Neutral, Events.DeathCause.Weapon, -1, null);
        VisibilitySystem.Update(world);

        Assert.All(grid.Counts.ToArray(), count => Assert.Equal(0, count));
        Assert.Equal(CellVisibility.Fog, grid.StateAt(new CellPos(11, 11)));
    }

    [Fact]
    public void Neutral_entities_and_shots_give_no_sight()
    {
        var world = World();
        var neutral = world.SpawnUnit(PlayerId.Neutral, UnitType.HeavyTank, new CellPos(10, 10).Center);
        world.Launch(Player1, neutral.Id, 0, null, new CellPos(20, 20).Center, new CellPos(21, 20).Center);
        VisibilitySystem.Update(world);

        for (var player = 1; player <= PlayerId.MaxPlayers; player++)
        {
            Assert.All(world.VisionOf(new PlayerId(player)).Counts.ToArray(), count => Assert.Equal(0, count));
        }
    }

    [Fact]
    public void The_incremental_grids_match_a_count_from_scratch_through_a_battle()
    {
        var simulation = new GameSimulation(new SimulationSettings(9) { MapWidthCells = 48, MapHeightCells = 48 }, TestRules.Standard);
        var world = simulation.World;
        var random = new DeterministicRandom(17);
        UnitType[] types = [UnitType.HeavyTank, UnitType.Conscript, UnitType.Rocketeer, UnitType.SupportVehicle, UnitType.Engineer];
        var buildings = new List<Building>();
        foreach (var (player, x) in new[] { (Player1, 2), (Player2, 40) })
        {
            buildings.Add(world.CreateBuilding(player, TestRules.BuildingDef("construction_yard"), new CellPos(x, 2), ready: true, null));
            buildings.Add(world.CreateBuilding(player, TestRules.BuildingDef("reactor"), new CellPos(x, 40), ready: true, null));
            for (var i = 0; i < 15; i++)
            {
                world.SpawnUnit(player, types[i % types.Length], new CellPos(x + random.NextInt(5), 10 + 2 * i).Center);
            }
        }

        uint sequence = 0;
        for (var tick = 0; tick < 1000; tick++)
        {
            if (tick % 40 == 0)
            {
                foreach (var player in new[] { Player1, Player2 })
                {
                    var units = world.Entities.OfType<Unit>().Where(u => u.IsAlive && u.Owner == player).Select(u => u.Id).ToArray();
                    var point = new CellPos(random.NextInt(48), random.NextInt(48)).Center;
                    simulation.Enqueue(random.NextInt(2) == 0
                        ? new MoveCommand(player, tick, sequence++, [.. units], point)
                        : new AttackMoveCommand(player, tick, sequence++, [.. units], point));
                }
            }

            if (tick is 300 or 600)
            {
                var building = buildings[tick / 300];
                simulation.Enqueue(new DamageMapObjectCommand(building.Owner, tick, sequence++, building.Cell, DamageMapObjectCommand.MaxAmount));
            }

            simulation.Step();
            if (tick % 50 == 49)
            {
                foreach (var player in new[] { Player1, Player2 })
                {
                    var grid = world.VisionOf(player);
                    Assert.Equal(FromScratch(world, player), grid.Counts.ToArray());
                    for (var y = 0; y < 48; y++)
                    {
                        for (var x = 0; x < 48; x++)
                        {
                            Assert.True(!grid.IsVisible(new CellPos(x, y)) || grid.IsExplored(new CellPos(x, y)));
                        }
                    }
                }
            }
        }

        Assert.Contains(simulation.ExecutedCommands, c => c is DamageMapObjectCommand);
        Assert.True(world.Entities.OfType<Unit>().Count(u => u.Owner == Player1) < 15 || world.Entities.OfType<Unit>().Count(u => u.Owner == Player2) < 15);
    }

    [Fact]
    public void A_change_of_sight_changes_the_hash()
    {
        var world = World();
        world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var before = StateHasher.Compute(world);

        VisibilitySystem.Update(world);

        Assert.NotEqual(before, StateHasher.Compute(world));
    }

    [Fact]
    public void Exploring_the_whole_map_marks_every_cell_and_nothing_past_the_last()
    {
        // 90 x 90 = 8100 cells: the last word of the marks holds 36 of them (stage 2d, the orbital uplink).
        var world = new GameWorld(seed: 1, widthCells: 90, heightCells: 90, TestRules.Standard);
        var grid = world.VisionOf(Player1);

        grid.ExploreAll();

        Assert.True(grid.IsExplored(new CellPos(0, 0)));
        Assert.True(grid.IsExplored(new CellPos(89, 89)));
        Assert.False(grid.IsVisible(new CellPos(45, 45)));
        Assert.Equal((1UL << 36) - 1, grid.Explored[^1]);
    }

    [Fact]
    public void A_veil_counts_like_sight_and_goes_when_taken_away()
    {
        var grid = World().VisionOf(Player1);

        grid.Veil(new CellPos(20, 20), 3, 1);
        grid.Veil(new CellPos(22, 20), 3, 1);
        Assert.True(grid.IsVeiled(new CellPos(17, 20)));
        Assert.Equal(2, grid.Veils[20 * 40 + 21]);

        grid.Veil(new CellPos(20, 20), 3, -1);
        Assert.False(grid.IsVeiled(new CellPos(17, 20)));
        Assert.True(grid.IsVeiled(new CellPos(25, 20)));
        Assert.False(grid.IsExplored(new CellPos(20, 20)), "a veil explores nothing");
    }
}
