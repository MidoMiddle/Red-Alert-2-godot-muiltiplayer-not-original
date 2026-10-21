using System;
using System.Globalization;

namespace Rts.Simulation.Math;

/// <summary>
/// Fixed-point number Q47.16 stored in a <see cref="long"/>: the value is <c>Raw / 65536</c>.
/// Replaces float/double in the simulation, so every platform computes bit-identical results.
/// Rounding rules are part of the simulation contract (docs/implementation/M0_foundation.md, 4.3):
/// multiplication rounds toward negative infinity, division rounds toward zero, square root rounds down.
/// </summary>
public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
{
    public const int FractionalBits = 16;
    public const long OneRaw = 1L << FractionalBits;

    private const long FractionMask = OneRaw - 1;

    public static readonly Fixed Zero = new(0);
    public static readonly Fixed One = new(OneRaw);
    public static readonly Fixed Half = new(OneRaw / 2);
    public static readonly Fixed Epsilon = new(1);
    public static readonly Fixed MinValue = new(long.MinValue);
    public static readonly Fixed MaxValue = new(long.MaxValue);

    /// <summary>Underlying value: the number multiplied by 65536.</summary>
    public readonly long Raw;

    private Fixed(long raw) => Raw = raw;

    public static Fixed FromRaw(long raw) => new(raw);

    public static Fixed FromInt(long value) => new(Narrow((Int128)value << FractionalBits));

    /// <summary><paramref name="numerator"/> / <paramref name="denominator"/>, rounded toward zero.</summary>
    public static Fixed FromRatio(long numerator, long denominator) =>
        new(Narrow(((Int128)numerator << FractionalBits) / denominator));

    public static Fixed operator +(Fixed a, Fixed b) => new(Add(a.Raw, b.Raw));

    public static Fixed operator -(Fixed a, Fixed b) => new(Subtract(a.Raw, b.Raw));

    public static Fixed operator -(Fixed value) => new(Subtract(0, value.Raw));

    /// <summary>Product rounded toward negative infinity (arithmetic shift).</summary>
    public static Fixed operator *(Fixed a, Fixed b) => new(Narrow(((Int128)a.Raw * b.Raw) >> FractionalBits));

    /// <summary>Quotient rounded toward zero. Division by zero throws: it is a logic error, not a game situation.</summary>
    public static Fixed operator /(Fixed a, Fixed b) => new(Narrow(((Int128)a.Raw << FractionalBits) / b.Raw));

    public static Fixed operator *(Fixed a, int b) => new(Narrow((Int128)a.Raw * b));

    public static Fixed operator *(int a, Fixed b) => b * a;

    /// <summary>Quotient rounded toward zero.</summary>
    public static Fixed operator /(Fixed a, int b) => new(a.Raw / b);

    public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;

    public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;

    public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;

    public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;

    public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;

    public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;

    public static Fixed Abs(Fixed value) => new(IntMath.Abs(value.Raw));

    public static Fixed Min(Fixed a, Fixed b) => a.Raw <= b.Raw ? a : b;

    public static Fixed Max(Fixed a, Fixed b) => a.Raw >= b.Raw ? a : b;

    public static Fixed Clamp(Fixed value, Fixed min, Fixed max) => new(IntMath.Clamp(value.Raw, min.Raw, max.Raw));

    public static int Sign(Fixed value) => IntMath.Sign(value.Raw);

    /// <summary>Largest whole number not greater than the value.</summary>
    public static Fixed Floor(Fixed value) => new(value.Raw & ~FractionMask);

    /// <summary>Smallest whole number not less than the value.</summary>
    public static Fixed Ceiling(Fixed value) => new(Add(value.Raw, FractionMask) & ~FractionMask);

    /// <summary>Square root rounded down. Throws on negative input.</summary>
    public static Fixed Sqrt(Fixed value)
    {
        if (value.Raw < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Square root of a negative number.");
        }

        // sqrt(raw * 2^16) = sqrt(value) * 2^16, which is exactly the raw form of the result.
        return new((long)IntMath.Sqrt((UInt128)(ulong)value.Raw << FractionalBits));
    }

    /// <summary>Whole part rounded toward negative infinity.</summary>
    public int ToIntFloor() => NarrowToInt(Raw >> FractionalBits);

    /// <summary>Whole part rounded toward zero.</summary>
    public int ToIntTruncate() => NarrowToInt(Raw / OneRaw);

    public bool Equals(Fixed other) => Raw == other.Raw;

    public override bool Equals(object? obj) => obj is Fixed other && Equals(other);

    public override int GetHashCode() => Raw.GetHashCode();

    public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

    /// <summary>
    /// Culture-independent decimal form with up to 5 fractional digits (truncated), e.g. "1.5", "-0.25".
    /// For logs and debugging only; never parse it back into the simulation.
    /// </summary>
    public override string ToString()
    {
        var negative = Raw < 0;
        var magnitude = negative ? (UInt128)(-(Int128)Raw) : (UInt128)(ulong)Raw;
        var whole = magnitude >> FractionalBits;
        var fractionDigits = (ulong)(((magnitude & FractionMask) * 100_000) >> FractionalBits);

        var text = whole.ToString(CultureInfo.InvariantCulture);
        if (fractionDigits != 0)
        {
            text += "." + fractionDigits.ToString("D5", CultureInfo.InvariantCulture).TrimEnd('0');
        }

        return negative ? "-" + text : text;
    }

    // Overflow is a bug. Debug builds (and the test suite) throw; Release keeps the hot path unchecked.
    private static long Narrow(Int128 value)
    {
#if DEBUG
        return checked((long)value);
#else
        return (long)value;
#endif
    }

    private static int NarrowToInt(long value)
    {
#if DEBUG
        return checked((int)value);
#else
        return (int)value;
#endif
    }

    private static long Add(long a, long b)
    {
#if DEBUG
        return checked(a + b);
#else
        return unchecked(a + b);
#endif
    }

    private static long Subtract(long a, long b)
    {
#if DEBUG
        return checked(a - b);
#else
        return unchecked(a - b);
#endif
    }
}
