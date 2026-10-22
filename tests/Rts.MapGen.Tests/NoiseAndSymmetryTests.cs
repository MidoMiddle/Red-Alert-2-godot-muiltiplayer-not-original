using Rts.Simulation.Geometry;

namespace Rts.MapGen.Tests;

/// <summary>The integer noise and the symmetry of the generator (docs/implementation/M7_maps.md, 4.14).</summary>
public sealed class NoiseAndSymmetryTests
{
    [Fact]
    public void Noise_is_in_range_repeats_for_a_seed_and_differs_between_seeds()
    {
        var values = new List<int>();
        for (var y = -20; y < 80; y++)
        {
            for (var x = -20; x < 80; x++)
            {
                var value = IntNoise.At(x, y, 42, 1);
                Assert.InRange(value, 0, IntNoise.Max);
                Assert.Equal(value, IntNoise.At(x, y, 42, 1));
                values.Add(value);
            }
        }

        Assert.True(values.Min() < IntNoise.Max / 4 && values.Max() > IntNoise.Max * 3 / 4, $"{values.Min()}…{values.Max()}");
        Assert.NotEqual(values, Enumerable.Range(0, 10_000).Select(i => IntNoise.At(i % 100 - 20, i / 100 - 20, 43, 1)));
        Assert.NotEqual(values, Enumerable.Range(0, 10_000).Select(i => IntNoise.At(i % 100 - 20, i / 100 - 20, 42, 2)));
    }

    [Fact]
    public void Noise_changes_little_from_one_cell_to_the_next()
    {
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                Assert.True(Math.Abs(IntNoise.At(x + 1, y, 7, 1) - IntNoise.At(x, y, 7, 1)) <= 12_000);
                Assert.True(Math.Abs(IntNoise.At(x, y + 1, 7, 1) - IntNoise.At(x, y, 7, 1)) <= 12_000);
            }
        }
    }

    [Fact]
    public void Golden_noise_values_are_the_same_on_every_system()
    {
        // Golden values: integers only, so every system must give exactly these.
        Assert.Equal(47212, IntNoise.At(0, 0, 1, 1));
        Assert.Equal(36152, IntNoise.At(17, 5, 99, 3));
        Assert.Equal(50115, IntNoise.At(-3, 40, 123456, 8));
        Assert.Equal(3824762690u, IntNoise.Hash(5, -7, 3, 2));
    }

    [Theory]
    [InlineData(MapSymmetry.Mirror, 2)]
    [InlineData(MapSymmetry.Rotate180, 2)]
    [InlineData(MapSymmetry.Rotate90, 4)]
    public void Images_go_round_their_group_and_the_canonical_point_stands_for_the_orbit(MapSymmetry kind, int order)
    {
        var symmetry = new Symmetry(kind, 64);
        Assert.Equal(order, symmetry.Order);
        for (var y = 0; y < 64; y += 7)
        {
            for (var x = 0; x < 64; x += 5)
            {
                var cell = new CellPos(x, y);
                var orbit = Enumerable.Range(0, order).Select(k => symmetry.Cell(k, cell)).ToList();
                Assert.All(orbit, image => Assert.InRange(image.X, 0, 63));
                Assert.Equal(cell, symmetry.Cell(1, orbit[order - 1]));
                Assert.All(orbit, image => Assert.Equal(symmetry.CanonicalCell(cell), symmetry.CanonicalCell(image)));
                Assert.Contains(symmetry.CanonicalCell(cell), orbit);

                var corners = Enumerable.Range(0, order).Select(k => symmetry.Corner(k, x, y)).ToList();
                Assert.All(corners, c => Assert.Equal(symmetry.CanonicalCorner(x, y), symmetry.CanonicalCorner(c.X, c.Y)));
            }
        }

        // A cell centre and its corners turn together: the image of the cell's corners are the corners of its image.
        var turned = symmetry.Cell(1, new CellPos(3, 9));
        var imageCorners = new[] { (3, 9), (4, 9), (3, 10), (4, 10) }.Select(c => symmetry.Corner(1, c.Item1, c.Item2)).OrderBy(c => c).ToList();
        Assert.Equal(new[] { (turned.X, turned.Y), (turned.X + 1, turned.Y), (turned.X, turned.Y + 1), (turned.X + 1, turned.Y + 1) }.OrderBy(c => c).ToList(), imageCorners);
    }
}
