using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Math;

public sealed class IntMathTests
{
    [Theory]
    [InlineData(7, 2, 3)]
    [InlineData(-7, 2, -4)]
    [InlineData(7, -2, -4)]
    [InlineData(-7, -2, 3)]
    [InlineData(-8, 2, -4)]
    [InlineData(8, 2, 4)]
    [InlineData(0, 5, 0)]
    [InlineData(-1, 256, -1)]
    [InlineData(-256, 256, -1)]
    [InlineData(-257, 256, -2)]
    public void DivFloor_rounds_toward_negative_infinity(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntMath.DivFloor(dividend, divisor));
    }

    [Theory]
    [InlineData(5, 2, 3)]
    [InlineData(-5, 2, -3)]
    [InlineData(5, -2, -3)]
    [InlineData(-5, -2, 3)]
    [InlineData(7, 3, 2)]
    [InlineData(8, 3, 3)]
    [InlineData(-7, 3, -2)]
    [InlineData(-8, 3, -3)]
    [InlineData(6, 3, 2)]
    [InlineData(0, 7, 0)]
    public void DivRound_rounds_halves_away_from_zero(long dividend, long divisor, long expected)
    {
        Assert.Equal(expected, IntMath.DivRound(dividend, divisor));
    }

    [Fact]
    public void DivFloor_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => IntMath.DivFloor(1, 0));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(3L, 1L)]
    [InlineData(4L, 2L)]
    [InlineData(15L, 3L)]
    [InlineData(16L, 4L)]
    [InlineData(1_000_000_000_000L, 1_000_000L)]
    [InlineData(999_999_999_999L, 999_999L)]
    [InlineData(long.MaxValue, 3_037_000_499L)]
    public void Sqrt_long_rounds_down(long value, long expected)
    {
        Assert.Equal(expected, IntMath.Sqrt(value));
    }

    [Fact]
    public void Sqrt_of_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IntMath.Sqrt(-1L));
    }

    [Fact]
    public void Sqrt_ulong_holds_floor_property_on_many_values()
    {
        var random = new TestRandom(12345);
        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextULong() >> random.NextInt(64);
            var root = IntMath.Sqrt(value);

            Assert.True((UInt128)root * root <= value, $"sqrt({value}) = {root} is too big");
            Assert.True((UInt128)(root + 1) * (root + 1) > value, $"sqrt({value}) = {root} is too small");
        }
    }

    [Fact]
    public void Sqrt_UInt128_matches_known_values()
    {
        Assert.Equal(UInt128.Zero, IntMath.Sqrt(UInt128.Zero));
        Assert.Equal((UInt128)ulong.MaxValue, IntMath.Sqrt(UInt128.MaxValue));
        Assert.Equal((UInt128)1L << 40, IntMath.Sqrt(UInt128.One << 80));
    }

    [Fact]
    public void Abs_of_min_value_throws()
    {
        Assert.Throws<OverflowException>(() => IntMath.Abs(long.MinValue));
        Assert.Throws<OverflowException>(() => IntMath.Abs(int.MinValue));
    }

    [Fact]
    public void Abs_Min_Max_Clamp_Sign_work()
    {
        Assert.Equal(5L, IntMath.Abs(-5L));
        Assert.Equal(5, IntMath.Abs(5));
        Assert.Equal(-3L, IntMath.Min(-3L, 2L));
        Assert.Equal(2, IntMath.Max(-3, 2));
        Assert.Equal(10L, IntMath.Clamp(15L, 0L, 10L));
        Assert.Equal(0, IntMath.Clamp(-15, 0, 10));
        Assert.Equal(7, IntMath.Clamp(7, 0, 10));
        Assert.Equal(-1, IntMath.Sign(-42));
        Assert.Equal(0, IntMath.Sign(0));
        Assert.Equal(1, IntMath.Sign(42));
        Assert.Throws<ArgumentException>(() => IntMath.Clamp(1, 10, 0));
    }
}
