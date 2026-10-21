using System;

namespace Rts.Simulation.Math;

/// <summary>
/// Integer helpers for simulation code, where <c>System.Math</c> is banned (it works with floating point).
/// All results are exact or explicitly rounded; nothing depends on the CPU.
/// </summary>
public static class IntMath
{
    /// <summary>Absolute value. Throws on <see cref="int.MinValue"/>, which has no positive counterpart.</summary>
    public static int Abs(int value) => value < 0 ? checked(-value) : value;

    /// <summary>Absolute value. Throws on <see cref="long.MinValue"/>, which has no positive counterpart.</summary>
    public static long Abs(long value) => value < 0 ? checked(-value) : value;

    public static int Min(int a, int b) => a <= b ? a : b;

    public static long Min(long a, long b) => a <= b ? a : b;

    public static int Max(int a, int b) => a >= b ? a : b;

    public static long Max(long a, long b) => a >= b ? a : b;

    public static int Clamp(int value, int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentException($"min ({min}) is greater than max ({max}).");
        }

        return value < min ? min : value > max ? max : value;
    }

    public static long Clamp(long value, long min, long max)
    {
        if (min > max)
        {
            throw new ArgumentException($"min ({min}) is greater than max ({max}).");
        }

        return value < min ? min : value > max ? max : value;
    }

    public static int Sign(long value) => value > 0 ? 1 : value < 0 ? -1 : 0;

    /// <summary>
    /// Division rounded toward negative infinity (C# <c>/</c> rounds toward zero).
    /// Needed wherever negative coordinates map to cells: -1 lepton is in cell -1, not cell 0.
    /// </summary>
    public static long DivFloor(long dividend, long divisor)
    {
        var quotient = dividend / divisor;
        var hasRemainder = quotient * divisor != dividend;
        var signsDiffer = (dividend < 0) != (divisor < 0);
        return hasRemainder && signsDiffer ? quotient - 1 : quotient;
    }

    /// <summary>Integer square root rounded down. Throws on negative input.</summary>
    public static long Sqrt(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Square root of a negative number.");
        }

        return (long)Sqrt((ulong)value);
    }

    /// <summary>Integer square root rounded down (digit-by-digit, exact on every platform).</summary>
    public static ulong Sqrt(ulong value)
    {
        ulong result = 0;
        ulong bit = 1UL << 62;
        while (bit > value)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (value >= result + bit)
            {
                value -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return result;
    }

    /// <summary>Integer square root rounded down (digit-by-digit, exact on every platform).</summary>
    public static UInt128 Sqrt(UInt128 value)
    {
        UInt128 result = 0;
        UInt128 bit = UInt128.One << 126;
        while (bit > value)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (value >= result + bit)
            {
                value -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return result;
    }
}
