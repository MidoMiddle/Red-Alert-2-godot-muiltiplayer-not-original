using System.Numerics;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Client;

/// <summary>The land beyond the edge of a map (docs/implementation/M7_maps.md, 4.12, «Край карты»): it goes on from the edge, no void, no hill in front of the map.</summary>
public sealed class MapSkirtTests
{
    private const int Size = 64;
    private const byte Water = 7;
    private const byte Pines = 9;
    private static readonly SkirtSlots Slots = new(0, 1, 2, 3, Water);

    // Direction of the RA2 camera's view, as in TerrainHeightsTests.
    private static readonly Vector3 CameraDirection = Vector3.Normalize(new Vector3(
        -MathF.Sin(MathF.PI / 4f) * MathF.Cos(MathF.PI / 6f),
        -MathF.Sin(MathF.PI / 6f),
        -MathF.Cos(MathF.PI / 4f) * MathF.Cos(MathF.PI / 6f)));

    /// <summary>
    /// The relief of <see cref="TerrainHeightsTests.Relief"/> with a deep river along rows 30–33 that leaves the map to the west
    /// and to the east, and pines along the north edge between x = 40 and 50.
    /// </summary>
    private static (MapGrid Map, byte[] Cells) Sample()
    {
        var heights = TerrainHeightsTests.Relief();
        var levels = new byte[(Size + 1) * (Size + 1)];
        for (var z = 0; z <= Size; z++)
        {
            for (var x = 0; x <= Size; x++)
            {
                levels[z * (Size + 1) + x] = (byte)heights.CornerLevel(x, z);
            }
        }

        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var cells = new byte[Size * Size * 2];
        for (var z = 30; z <= 33; z++)
        {
            for (var x = 0; x < Size; x++)
            {
                land[z * Size + x] = (byte)LandType.Deep;
                (cells[(z * Size + x) * 2], cells[(z * Size + x) * 2 + 1]) = (Water, 2);
            }
        }

        var objects = new List<MapObjectState>();
        for (var x = 40; x <= 50; x += 2)
        {
            objects.Add(MapObjectState.New(MapObjectKind.PineTree, new CellPos(x, 0), 0));
        }

        cells[(5 * Size + 5) * 2] = Pines; // a material of the map's own, inside: the skirt copies the map unchanged
        return (MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], levels, objects), cells);
    }

    private static MapSkirt Build() => Build(out _);

    private static MapSkirt Build(out MapGrid map)
    {
        (map, var cells) = Sample();
        return MapSkirt.Build(map, cells, Slots);
    }

    private static int CellAt(MapSkirt skirt, int x, int z) => ((z + skirt.Skirt) * skirt.CellsX + x + skirt.Skirt) * 2;

    [Fact]
    public void The_map_inside_stays_the_rules_and_the_skirt_meets_its_edge_without_a_seam()
    {
        var skirt = Build(out var map);
        var terrain = TerrainHeights.FromRules(map.Heights, skirt);
        var rules = TerrainHeights.FromRules(map.Heights);

        Assert.Equal(MapSkirt.WidthFor(Size, Size), skirt.Skirt);
        for (var z = 0; z <= Size; z++)
        {
            for (var x = 0; x <= Size; x++)
            {
                Assert.Equal(rules.VertexHeight(x, z), terrain.VertexHeight(x, z));
            }
        }

        // The first corners beyond the edge go on from it: no step at the border.
        for (var i = 0; i <= Size; i++)
        {
            Assert.InRange(MathF.Abs(skirt.LevelAt(-1, i) - map.Heights.CornerLevel(0, i)), 0f, 0.05f);
            Assert.InRange(MathF.Abs(skirt.LevelAt(i, Size + 1) - map.Heights.CornerLevel(i, Size)), 0f, 0.05f);
        }

        Assert.Equal(Pines, skirt.Cells[CellAt(skirt, 5, 5)]);
        Assert.Equal(Size, terrain.WidthCells);
    }

    [Fact]
    public void The_land_in_front_of_the_map_never_rises_above_the_edge_so_the_map_stays_in_sight()
    {
        var skirt = Build(out var map);
        var terrain = TerrainHeights.FromRules(map.Heights, skirt);

        // East and south of the map, within the band the camera looks through at the map, from 6 cells past the edge on.
        for (var a = -skirt.Skirt; a <= Size + skirt.Skirt; a++)
        {
            for (var b = Size + 6; b <= Size + skirt.Skirt; b++)
            {
                if (b - a <= Size && b - a >= -Size)
                {
                    Assert.True(skirt.LevelAt(b, a) <= map.Heights.CornerLevel(Size, System.Math.Clamp(a, 0, Size)) + 0.001f, $"east {b},{a}");
                }

                if (a - b <= Size && a - b >= -Size)
                {
                    Assert.True(skirt.LevelAt(a, b) <= map.Heights.CornerLevel(System.Math.Clamp(a, 0, Size), Size) + 0.001f, $"south {a},{b}");
                }
            }
        }

        // The camera finds the cells along the east and the south edge, over the skirt in front of them.
        for (var i = 0; i < Size; i++)
        {
            foreach (var (x, z) in new[] { (Size - 0.5f, i + 0.5f), (i + 0.5f, Size - 0.5f) })
            {
                var target = new Vector3(x, terrain.HeightAt(x, z), z);
                Assert.True(terrain.TryIntersectRay(target - CameraDirection * 50f, CameraDirection, out var hit));
                Assert.True(Vector3.Distance(target, hit) < 0.01f, $"hit {hit}, expected {target}");
            }
        }
    }

    [Fact]
    public void Far_from_the_edge_the_skirt_is_gentle_hills_without_cliffs()
    {
        var skirt = Build();

        for (var z = -skirt.Skirt; z < Size + skirt.Skirt; z++)
        {
            for (var x = -skirt.Skirt; x < Size + skirt.Skirt; x++)
            {
                var outside = System.Math.Max(System.Math.Max(-x, x - Size), System.Math.Max(-z, z - Size));
                if (outside < 16)
                {
                    continue;
                }

                Assert.True(MathF.Abs(skirt.LevelAt(x + 1, z) - skirt.LevelAt(x, z)) < 2f, $"{x},{z}");
                Assert.True(MathF.Abs(skirt.LevelAt(x, z + 1) - skirt.LevelAt(x, z)) < 2f, $"{x},{z}");
                Assert.InRange(skirt.LevelAt(x, z), 0f, 6f);
            }
        }
    }

    [Fact]
    public void A_river_goes_on_beyond_the_edge_and_ends_in_ground()
    {
        var skirt = Build();

        for (var z = 30; z <= 33; z++)
        {
            for (var x = 1; x <= 18; x++)
            {
                Assert.Equal((Water, 2), (skirt.Cells[CellAt(skirt, -x, z)], skirt.Cells[CellAt(skirt, -x, z) + 1]));
                Assert.Equal((Water, 2), (skirt.Cells[CellAt(skirt, Size - 1 + x, z)], skirt.Cells[CellAt(skirt, Size - 1 + x, z) + 1]));
            }

            Assert.NotEqual(Water, skirt.Cells[CellAt(skirt, -30, z)]);
            Assert.Equal(0, skirt.Cells[CellAt(skirt, -30, z) + 1]);
        }

        // Away from the river only the materials of the climate's ground are painted.
        Assert.All(Enumerable.Range(0, skirt.CellsX), x => Assert.Contains(skirt.Cells[CellAt(skirt, x - skirt.Skirt, -skirt.Skirt)], new byte[] { 0, 1, 2 }));
    }

    [Fact]
    public void Pines_stand_thick_where_the_forest_meets_the_edge_never_on_water_or_on_the_map()
    {
        var skirt = Build();

        Assert.All(skirt.Trees, tree =>
        {
            var (x, z) = ((int)MathF.Floor(tree.X), (int)MathF.Floor(tree.Z));
            Assert.False(x >= 0 && z >= 0 && x < Size && z < Size, $"a pine on the map at {tree}");
            Assert.Equal(0, skirt.Cells[CellAt(skirt, x, z) + 1]);
            Assert.NotEqual(Water, skirt.Cells[CellAt(skirt, x, z)]);
        });
        var byTheForest = skirt.Trees.Count(t => t.X >= 40 && t.X < 51 && t.Z < 0 && t.Z >= -6);
        var elsewhere = skirt.Trees.Count(t => t.X >= 10 && t.X < 21 && t.Z < 0 && t.Z >= -6);
        Assert.True(byTheForest > 3 * System.Math.Max(elsewhere, 1), $"{byTheForest} by the forest, {elsewhere} elsewhere");
    }

    [Fact]
    public void The_same_map_gives_the_same_skirt_and_a_change_inside_keeps_its_edge_signature()
    {
        var (map, cells) = Sample();
        var first = MapSkirt.Build(map, cells, Slots);
        var second = MapSkirt.Build(map, cells, Slots);

        Assert.Equal(first.Cells, second.Cells);
        Assert.Equal(first.Trees, second.Trees);
        Assert.Equal(first.LevelAt(-20, -20), second.LevelAt(-20, -20));

        var painted = (byte[])cells.Clone();
        painted[(20 * Size + 20) * 2] = 4;
        Assert.Equal(MapSkirt.EdgeSignature(map, cells), MapSkirt.EdgeSignature(map, painted));
        Assert.Equal(4, first.WithMapCells(painted)[CellAt(first, 20, 20)]);
        painted[(0 * Size + 20) * 2] = 4;
        Assert.NotEqual(MapSkirt.EdgeSignature(map, cells), MapSkirt.EdgeSignature(map, painted));
    }

    [Fact]
    public void A_map_smaller_than_the_view_gets_a_skirt_to_the_corners_of_the_view()
    {
        Assert.Equal(44, MapSkirt.WidthFor(64, 64));
        Assert.Equal(92, MapSkirt.WidthFor(160, 160));
        Assert.True(MapSkirt.WidthFor(24, 24) >= 60);
    }
}
