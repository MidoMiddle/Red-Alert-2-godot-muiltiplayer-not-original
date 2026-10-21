using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Math;

/// <summary>
/// Accuracy of the table trigonometry against System.Math (double is fine in tests, never in the simulation),
/// plus the exact values the movement code relies on (docs/implementation/M2_unit_control.md, 4.1).
/// </summary>
public sealed class FixedMathTests
{
    private const int MaxSinError = 2;
    private const int MaxAngleError = 8;

    private static double Radians(BAngle angle) => angle.Value * 2.0 * System.Math.PI / BAngle.FullTurn;

    /// <summary>Compass angle of a map vector in units, 0…65536, with double precision.</summary>
    private static double ExpectedAngle(long dx, long dy)
    {
        var angle = System.Math.Atan2(dx, -dy) / (2.0 * System.Math.PI) * BAngle.FullTurn;
        return angle < 0 ? angle + BAngle.FullTurn : angle;
    }

    private static double AngleError(BAngle actual, double expected)
    {
        var error = System.Math.Abs(actual.Value - expected);
        return System.Math.Min(error, BAngle.FullTurn - error);
    }

    [Fact]
    public void Sin_and_cos_are_exact_at_cardinal_directions()
    {
        Assert.Equal(0, FixedMath.Sin(BAngle.North).Raw);
        Assert.Equal(Fixed.OneRaw, FixedMath.Sin(BAngle.East).Raw);
        Assert.Equal(0, FixedMath.Sin(BAngle.South).Raw);
        Assert.Equal(-Fixed.OneRaw, FixedMath.Sin(BAngle.West).Raw);

        Assert.Equal(Fixed.OneRaw, FixedMath.Cos(BAngle.North).Raw);
        Assert.Equal(0, FixedMath.Cos(BAngle.East).Raw);
        Assert.Equal(-Fixed.OneRaw, FixedMath.Cos(BAngle.South).Raw);
        Assert.Equal(0, FixedMath.Cos(BAngle.West).Raw);
    }

    [Fact]
    public void Sin_and_cos_match_double_precision()
    {
        var random = new TestRandom(1);
        long worst = 0;
        for (var i = 0; i < 10_000; i++)
        {
            var angle = BAngle.FromUnits(random.NextInt(65536));

            var sinError = (long)System.Math.Abs(FixedMath.Sin(angle).Raw - System.Math.Sin(Radians(angle)) * Fixed.OneRaw);
            var cosError = (long)System.Math.Abs(FixedMath.Cos(angle).Raw - System.Math.Cos(Radians(angle)) * Fixed.OneRaw);
            worst = IntMath.Max(worst, IntMath.Max(sinError, cosError));
        }

        Assert.True(worst <= MaxSinError, $"Worst sin/cos error {worst} raw units.");
    }

    [Fact]
    public void Sin_is_odd_and_symmetric_around_east()
    {
        for (var units = 0; units < BAngle.FullTurn; units += 7)
        {
            var angle = BAngle.FromUnits(units);
            Assert.Equal(-FixedMath.Sin(angle).Raw, FixedMath.Sin(BAngle.North - angle).Raw);
            Assert.Equal(FixedMath.Sin(angle).Raw, FixedMath.Sin(BAngle.South - angle).Raw);
        }
    }

    [Theory]
    [InlineData(0, -1, 0)]
    [InlineData(1, -1, 8192)]
    [InlineData(1, 0, 16384)]
    [InlineData(1, 1, 24576)]
    [InlineData(0, 1, 32768)]
    [InlineData(-1, 1, 40960)]
    [InlineData(-1, 0, 49152)]
    [InlineData(-1, -1, 57344)]
    [InlineData(0, -1_000_000, 0)]
    [InlineData(-51_200, 0, 49152)]
    [InlineData(0, 0, 0)]
    public void Angle_of_axes_and_diagonals_is_exact(long dx, long dy, int expected)
    {
        Assert.Equal(expected, FixedMath.Angle(dx, dy).Value);
    }

    [Fact]
    public void Angle_matches_double_precision_in_every_octant()
    {
        var random = new TestRandom(2);
        double worst = 0;
        for (var i = 0; i < 10_000; i++)
        {
            // Map vectors up to 200 cells of 256 leptons, plus a share of short ones where rounding matters most.
            var range = i % 4 == 0 ? 16 : 51_200;
            long dx = random.NextInt(2 * range + 1) - range;
            long dy = random.NextInt(2 * range + 1) - range;
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            worst = System.Math.Max(worst, AngleError(FixedMath.Angle(dx, dy), ExpectedAngle(dx, dy)));
        }

        Assert.True(worst <= MaxAngleError, $"Worst angle error {worst} units.");
    }

    [Fact]
    public void Angle_handles_extreme_coordinates()
    {
        Assert.Equal(BAngle.East, FixedMath.Angle(long.MaxValue, 0));
        Assert.Equal(8192, FixedMath.Angle(long.MaxValue, -long.MaxValue).Value);
        Assert.True(AngleError(FixedMath.Angle(long.MaxValue, -1), BAngle.QuarterTurn) <= 1);
    }

    [Fact]
    public void Direction_points_at_the_compass_angle()
    {
        Assert.Equal((Fixed.Zero, -Fixed.One), FixedMath.Direction(BAngle.North));
        Assert.Equal((Fixed.One, Fixed.Zero), FixedMath.Direction(BAngle.East));
        Assert.Equal((Fixed.Zero, Fixed.One), FixedMath.Direction(BAngle.South));
        Assert.Equal((-Fixed.One, Fixed.Zero), FixedMath.Direction(BAngle.West));
    }

    [Fact]
    public void Angle_of_direction_returns_the_angle()
    {
        long worst = 0;
        for (var units = 0; units < BAngle.FullTurn; units += 3)
        {
            var angle = BAngle.FromUnits(units);
            var (x, y) = FixedMath.Direction(angle);

            worst = IntMath.Max(worst, IntMath.Abs(BAngle.Difference(angle, FixedMath.Angle(x.Raw, y.Raw))));
        }

        Assert.True(worst <= MaxAngleError, $"Worst round-trip error {worst} units.");
    }

    [Fact]
    public void Direction_has_unit_length()
    {
        for (var units = 0; units < BAngle.FullTurn; units += 5)
        {
            var (x, y) = FixedMath.Direction(BAngle.FromUnits(units));

            Assert.InRange(FixedMath.Length(x.Raw, y.Raw), Fixed.OneRaw - 2, Fixed.OneRaw + 2);
        }
    }

    [Fact]
    public void Normalize_gives_unit_vectors_along_the_input()
    {
        Assert.Equal((Fixed.FromRatio(3, 5), Fixed.FromRatio(-4, 5)), FixedMath.Normalize(3, -4));
        Assert.Equal((Fixed.Zero, -Fixed.One), FixedMath.Normalize(0, -51_200));
        Assert.Equal((Fixed.Zero, Fixed.Zero), FixedMath.Normalize(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedMath.Normalize(1L << 47, 0));

        var random = new TestRandom(4);
        for (var i = 0; i < 10_000; i++)
        {
            // Short vectors too: there the rounding of the length matters most.
            var range = i % 4 == 0 ? 3 : 51_200;
            long dx = random.NextInt(2 * range + 1) - range;
            long dy = random.NextInt(2 * range + 1) - range;
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            var (x, y) = FixedMath.Normalize(dx, dy);
            Assert.InRange(FixedMath.Length(x.Raw, y.Raw), Fixed.OneRaw - 2, Fixed.OneRaw + 2);
            Assert.True(IntMath.Abs(BAngle.Difference(FixedMath.Angle(dx, dy), FixedMath.Angle(x.Raw, y.Raw))) <= MaxAngleError);
        }
    }

    [Theory]
    [InlineData(3, 4, 5)]
    [InlineData(-3, 4, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(51_200, 51_200, 72_407)]
    [InlineData(long.MaxValue, 0, long.MaxValue)]
    public void Length_rounds_down(long dx, long dy, long expected)
    {
        Assert.Equal(expected, FixedMath.Length(dx, dy));
    }

    [Fact]
    public void Length_matches_double_precision_on_map_vectors()
    {
        var random = new TestRandom(3);
        for (var i = 0; i < 10_000; i++)
        {
            long dx = random.NextInt(102_401) - 51_200;
            long dy = random.NextInt(102_401) - 51_200;

            Assert.Equal((long)System.Math.Floor(System.Math.Sqrt((double)(dx * dx + dy * dy))), FixedMath.Length(dx, dy));
        }
    }
}
