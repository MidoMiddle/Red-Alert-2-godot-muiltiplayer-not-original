using Rts.Data.Maps;
using Rts.Simulation.Geometry;

namespace Rts.MapGen.Tests;

/// <summary>
/// Random maps play on a rectangle of the screen, as in RA2 (docs/implementation/M7_maps.md, 4.14; <see cref="MapArea"/>): about as
/// many cells as the square maps of that size had, the same under every symmetry, everything that matters inside it.
/// </summary>
public sealed class PlayableAreaTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(96)]
    [InlineData(128)]
    [InlineData(160)]
    public void The_area_of_a_size_has_about_as_many_cells_as_the_square_map_had(int size)
    {
        var n = MapGenSettings.GridSize(size);
        var area = MapArea.Inscribed(n);
        var cells = Enumerable.Range(0, n * n).Count(i => area.Contains(new CellPos(i % n, i / n)));

        Assert.Equal(0, n % 2);
        Assert.InRange(cells, size * size * 97 / 100, size * size * 103 / 100);
    }

    [Theory]
    [InlineData(MapSymmetry.Mirror)]
    [InlineData(MapSymmetry.Rotate180)]
    [InlineData(MapSymmetry.Rotate90)]
    public void The_area_is_its_own_image_under_every_symmetry(MapSymmetry kind)
    {
        const int n = 90;
        var (area, symmetry) = (MapArea.Inscribed(n), new Symmetry(kind, n));

        for (var i = 0; i < n * n; i++)
        {
            var cell = new CellPos(i % n, i / n);
            for (var k = 1; k < symmetry.Order; k++)
            {
                Assert.Equal(area.Contains(cell), area.Contains(symmetry.Cell(k, cell)));
            }
        }
    }

    [Theory]
    [InlineData(MapSymmetry.Mirror, 2)]
    [InlineData(MapSymmetry.Rotate180, 2)]
    [InlineData(MapSymmetry.Rotate90, 4)]
    public void Starts_ore_bridges_and_buildings_are_inside_and_the_scenery_is_blocked(MapSymmetry kind, int players)
    {
        foreach (var seed in new uint[] { 1, 2, 3 })
        {
            var (map, _) = MapGenerator.Generate(new MapGenSettings { Seed = seed, Symmetry = kind, Players = players });
            var area = map.Playable!;
            var grid = map.ToMapGrid();

            Assert.All(map.Starts, start => Assert.True(area.Contains(start.Cell), $"seed {seed}: start {start.Slot}"));
            Assert.All(map.Bridges, bridge => Assert.True(area.Contains(bridge.From) && area.Contains(bridge.To), $"seed {seed}: bridge {bridge.From}"));
            Assert.All(map.Structures, building => Assert.True(area.Contains(building.Cell), $"seed {seed}: {building.Type}"));
            for (var i = 0; i < map.Ore.Length; i++)
            {
                var cell = new CellPos(i % map.Width, i / map.Width);
                Assert.True(map.Ore[i] == 0 || area.Contains(cell), $"seed {seed}: ore at {cell}");
                Assert.Equal(!area.Contains(cell), grid.IsBlocked(cell) && !area.Contains(cell));
            }
        }
    }
}
