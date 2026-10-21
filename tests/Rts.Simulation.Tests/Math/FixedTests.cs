using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Math;

/// <summary>
/// Exact raw values pin down the rounding rules from docs/implementation/M0_foundation.md, section 4.3.
/// If one of these changes, simulation results change on every platform.
/// </summary>
public sealed class FixedTests
{
    private static Fixed F(long whole) => Fixed.FromInt(whole);

    private static Fixed Ratio(long numerator, long denominator) => Fixed.FromRatio(numerator, denominator);

    [Fact]
    public void Constants_have_expected_raw_values()
    {
        Assert.Equal(65536, Fixed.One.Raw);
        Assert.Equal(32768, Fixed.Half.Raw);
        Assert.Equal(1, Fixed.Epsilon.Raw);
        Assert.Equal(0, Fixed.Zero.Raw);
    }

    [Theory]
    [InlineData(3, 2, 98304)]
    [InlineData(1, 3, 21845)]
    [InlineData(-1, 3, -21845)]
    [InlineData(2, 3, 43690)]
    [InlineData(-2, 3, -43690)]
    [InlineData(1, 65536, 1)]
    [InlineData(1, 131072, 0)]
    public void FromRatio_rounds_toward_zero(long numerator, long denominator, long expectedRaw)
    {
        Assert.Equal(expectedRaw, Ratio(numerator, denominator).Raw);
    }

    [Fact]
    public void FromInt_scales_by_65536()
    {
        Assert.Equal(5 * 65536, F(5).Raw);
        Assert.Equal(-7 * 65536, F(-7).Raw);
    }

    [Fact]
    public void Addition_and_subtraction_are_exact()
    {
        Assert.Equal(Ratio(7, 4), Ratio(3, 2) + Ratio(1, 4));
        Assert.Equal(Ratio(-5, 4), Ratio(1, 4) - Ratio(3, 2));
        Assert.Equal(Ratio(-3, 2), -Ratio(3, 2));
    }

    [Fact]
    public void Multiplication_rounds_toward_negative_infinity()
    {
        Assert.Equal(0, (Fixed.Epsilon * Fixed.Epsilon).Raw);
        Assert.Equal(-1, (-Fixed.Epsilon * Fixed.Epsilon).Raw);
        Assert.Equal(Ratio(-3, 4), Ratio(3, 2) * -Fixed.Half);
        Assert.Equal(Ratio(9, 4), Ratio(3, 2) * Ratio(3, 2));
        // 1/3 * 3: 21845 * 196608 >> 16 = 65535, one epsilon below 1 — the cost of rounding 1/3 first.
        Assert.Equal(65535, (Ratio(1, 3) * F(3)).Raw);
    }

    [Fact]
    public void Multiplication_of_large_values_does_not_overflow_the_intermediate()
    {
        // The raw product is ~4.3e21 and needs 128 bits before the shift.
        Assert.Equal(F(1_000_000_000_000), F(1_000_000) * F(1_000_000));
    }

    [Fact]
    public void Division_rounds_toward_zero()
    {
        Assert.Equal(21845, (F(1) / F(3)).Raw);
        Assert.Equal(-21845, (F(-1) / F(3)).Raw);
        Assert.Equal(-21845, (F(1) / F(-3)).Raw);
        Assert.Equal(Ratio(3, 2), F(3) / F(2));
    }

    [Fact]
    public void Division_by_zero_throws()
    {
        Assert.Throws<DivideByZeroException>(() => F(1) / Fixed.Zero);
        Assert.Throws<DivideByZeroException>(() => Fixed.FromRatio(1, 0));
    }

    [Fact]
    public void Mixed_operations_with_int()
    {
        Assert.Equal(Ratio(9, 2), Ratio(3, 2) * 3);
        Assert.Equal(Ratio(9, 2), 3 * Ratio(3, 2));
        Assert.Equal(21845, (F(1) / 3).Raw);
        Assert.Equal(-21845, (F(-1) / 3).Raw);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4 * 65536, 2 * 65536)]
    [InlineData(147456, 98304)] // 2.25 -> 1.5
    [InlineData(2 * 65536, 92681)] // sqrt(2) = 1.41421... -> 92681.9 rounded down
    [InlineData(1, 256)] // sqrt(1/65536) = 1/256 exactly
    public void Sqrt_rounds_down(long valueRaw, long expectedRaw)
    {
        Assert.Equal(expectedRaw, Fixed.Sqrt(Fixed.FromRaw(valueRaw)).Raw);
    }

    [Fact]
    public void Sqrt_holds_floor_property_on_many_values()
    {
        var random = new TestRandom(2026);
        for (var i = 0; i < 10_000; i++)
        {
            var raw = (long)(random.NextULong() >> 1) >> random.NextInt(63);
            var root = Fixed.Sqrt(Fixed.FromRaw(raw)).Raw;
            var target = (UInt128)(ulong)raw << Fixed.FractionalBits;

            Assert.True((UInt128)(ulong)root * (ulong)root <= target, $"sqrt(raw {raw}) = raw {root} is too big");
            Assert.True((UInt128)(ulong)(root + 1) * (ulong)(root + 1) > target, $"sqrt(raw {raw}) = raw {root} is too small");
        }
    }

    [Fact]
    public void Sqrt_of_negative_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Sqrt(-Fixed.Epsilon));
    }

    [Fact]
    public void Comparisons_follow_raw_values()
    {
        Assert.True(Ratio(1, 3) < Fixed.Half);
        Assert.True(Fixed.Half > Ratio(1, 3));
        Assert.True(Fixed.Half <= Ratio(1, 2));
        Assert.True(Fixed.Half >= Ratio(1, 2));
        Assert.True(F(2) == Ratio(4, 2));
        Assert.True(F(2) != F(3));
        Assert.True(F(-1).CompareTo(F(1)) < 0);
        Assert.True(F(2).Equals((object)F(2)));
        Assert.Equal(F(2).GetHashCode(), Ratio(4, 2).GetHashCode());
    }

    [Fact]
    public void Abs_Min_Max_Clamp_Sign()
    {
        Assert.Equal(Ratio(3, 2), Fixed.Abs(Ratio(-3, 2)));
        Assert.Equal(F(-2), Fixed.Min(F(-2), F(1)));
        Assert.Equal(F(1), Fixed.Max(F(-2), F(1)));
        Assert.Equal(F(10), Fixed.Clamp(F(15), Fixed.Zero, F(10)));
        Assert.Equal(-1, Fixed.Sign(-Fixed.Epsilon));
        Assert.Equal(0, Fixed.Sign(Fixed.Zero));
        Assert.Equal(1, Fixed.Sign(Fixed.Epsilon));
    }

    [Theory]
    [InlineData(3, 2, 1, 2)]
    [InlineData(-3, 2, -2, -1)]
    [InlineData(2, 1, 2, 2)]
    [InlineData(-2, 1, -2, -2)]
    [InlineData(1, 65536, 0, 1)]
    [InlineData(-1, 65536, -1, 0)]
    public void Floor_and_Ceiling(long numerator, long denominator, long expectedFloor, long expectedCeiling)
    {
        var value = Ratio(numerator, denominator);

        Assert.Equal(F(expectedFloor), Fixed.Floor(value));
        Assert.Equal(F(expectedCeiling), Fixed.Ceiling(value));
    }

    [Theory]
    [InlineData(3, 2, 1, 1)]
    [InlineData(-3, 2, -2, -1)]
    [InlineData(7, 1, 7, 7)]
    [InlineData(-1, 65536, -1, 0)]
    public void ToInt_conversions(long numerator, long denominator, int expectedFloor, int expectedTruncate)
    {
        var value = Ratio(numerator, denominator);

        Assert.Equal(expectedFloor, value.ToIntFloor());
        Assert.Equal(expectedTruncate, value.ToIntTruncate());
    }

    [Theory]
    [InlineData(0, 1, "0")]
    [InlineData(3, 2, "1.5")]
    [InlineData(-1, 4, "-0.25")]
    [InlineData(5, 1, "5")]
    [InlineData(-7, 1, "-7")]
    [InlineData(1, 3, "0.33332")] // raw 21845 = 0.333328..., digits are truncated, not rounded
    [InlineData(1, 65536, "0.00001")]
    [InlineData(-1, 65536, "-0.00001")]
    public void ToString_is_culture_independent(long numerator, long denominator, string expected)
    {
        Assert.Equal(expected, Ratio(numerator, denominator).ToString());
    }

#if DEBUG
    [Fact]
    public void Overflow_throws_in_debug_builds()
    {
        Assert.Throws<OverflowException>(() => Fixed.MaxValue + Fixed.Epsilon);
        Assert.Throws<OverflowException>(() => Fixed.MinValue - Fixed.Epsilon);
        Assert.Throws<OverflowException>(() => -Fixed.MinValue);
        Assert.Throws<OverflowException>(() => Fixed.MaxValue * F(2));
        Assert.Throws<OverflowException>(() => Fixed.FromInt(long.MaxValue));
        Assert.Throws<OverflowException>(() => F(int.MaxValue + 1L).ToIntFloor());
    }
#endif
}
