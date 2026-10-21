using System;

namespace Rts.Simulation.Math;

/// <summary>
/// Trigonometry of the simulation on integers only (docs/implementation/M2_unit_control.md, 4.1).
/// Values come from the committed tables in <see cref="TrigTables"/> with integer linear interpolation,
/// so every platform gets bit-identical results. Map directions: north is −Y, east is +X.
/// </summary>
public static class FixedMath
{
    private const int SinStepBits = 2;
    private const int SinStepMask = (1 << SinStepBits) - 1;
    private const int AtanSteps = 1024;
    private const int AtanFractionBits = 16;
    private const long AtanFractionMask = (1L << AtanFractionBits) - 1;

    /// <summary>Sine; exactly 0 and ±1 at the four cardinal directions.</summary>
    public static Fixed Sin(BAngle angle)
    {
        var quadrant = angle.Value / BAngle.QuarterTurn;
        var offset = angle.Value % BAngle.QuarterTurn;
        // sin(90° + x) = sin(90° − x), sin(180° + x) = −sin(x).
        var value = quadrant % 2 == 0 ? QuarterSin(offset) : QuarterSin(BAngle.QuarterTurn - offset);
        return Fixed.FromRaw(quadrant < 2 ? value : -value);
    }

    public static Fixed Cos(BAngle angle) => Sin(angle + BAngle.East);

    /// <summary>
    /// Compass direction of the vector (<paramref name="dx"/>, <paramref name="dy"/>) on the map: (0, −1) is north,
    /// (1, 0) is east. The zero vector gives north.
    /// </summary>
    public static BAngle Angle(long dx, long dy)
    {
        var ax = IntMath.Abs(dx);
        var ay = IntMath.Abs(dy);
        if (ax == 0 && ay == 0)
        {
            return BAngle.North;
        }

        // Angle from north inside the north-east quadrant, using the table on the smaller-to-larger ratio.
        var fromNorth = ax <= ay ? AtanOfRatio(ax, ay) : BAngle.QuarterTurn - AtanOfRatio(ay, ax);
        long angle = (dx >= 0, dy <= 0) switch
        {
            (true, true) => fromNorth,
            (true, false) => BAngle.HalfTurn - fromNorth,
            (false, false) => BAngle.HalfTurn + fromNorth,
            (false, true) => BAngle.FullTurn - fromNorth,
        };
        return BAngle.FromUnits(angle);
    }

    /// <summary>Unit vector pointing at <paramref name="angle"/>: north is (0, −1), east is (1, 0).</summary>
    public static (Fixed X, Fixed Y) Direction(BAngle angle) => (Sin(angle), -Cos(angle));

    /// <summary>Length of the vector, rounded down. Exact for any coordinates of a map.</summary>
    public static long Length(long dx, long dy)
    {
        var ax = (UInt128)(ulong)IntMath.Abs(dx);
        var ay = (UInt128)(ulong)IntMath.Abs(dy);
        return checked((long)IntMath.Sqrt(ax * ax + ay * ay));
    }

    /// <summary>
    /// Vector of length <see cref="Fixed.One"/> along (<paramref name="dx"/>, <paramref name="dy"/>); the zero vector stays zero.
    /// Coordinates must fit the integer part of <see cref="Fixed"/> (below 2⁴⁷ in absolute value).
    /// </summary>
    public static (Fixed X, Fixed Y) Normalize(long dx, long dy)
    {
        const long Limit = 1L << 47;
        if (IntMath.Abs(dx) >= Limit || IntMath.Abs(dy) >= Limit)
        {
            throw new ArgumentOutOfRangeException(nameof(dx), "Vector too long to normalize.");
        }

        if (dx == 0 && dy == 0)
        {
            return (Fixed.Zero, Fixed.Zero);
        }

        // Length with 16 fraction bits: rounding the length itself down would make short vectors visibly longer than one.
        var ax = (UInt128)(ulong)IntMath.Abs(dx);
        var ay = (UInt128)(ulong)IntMath.Abs(dy);
        var scaledLength = (Int128)IntMath.Sqrt((ax * ax + ay * ay) << (2 * Fixed.FractionalBits));
        return (Fixed.FromRaw((long)(((Int128)dx << (2 * Fixed.FractionalBits)) / scaledLength)),
            Fixed.FromRaw((long)(((Int128)dy << (2 * Fixed.FractionalBits)) / scaledLength)));
    }

    /// <summary>sin of an offset 0…16384 inside the first quadrant, as Fixed.Raw.</summary>
    private static long QuarterSin(int offset)
    {
        var index = offset >> SinStepBits;
        long low = TrigTables.SinQuarter[index];
        var fraction = offset & SinStepMask;
        if (fraction == 0)
        {
            return low;
        }

        long high = TrigTables.SinQuarter[index + 1];
        // The table grows on the quarter, so the step is never negative and the shift rounds half up.
        return low + (((high - low) * fraction + (1 << (SinStepBits - 1))) >> SinStepBits);
    }

    /// <summary>atan(small / large) in BAngle units, 0…8192, for 0 ≤ small ≤ large, large &gt; 0.</summary>
    private static long AtanOfRatio(long small, long large)
    {
        // Position in the table with 16 fraction bits; Int128 keeps any map coordinate exact.
        var position = (long)(((Int128)small * AtanSteps << AtanFractionBits) / large);
        var index = (int)(position >> AtanFractionBits);
        long low = TrigTables.Atan[index];
        var fraction = position & AtanFractionMask;
        if (fraction == 0)
        {
            return low;
        }

        long high = TrigTables.Atan[index + 1];
        return low + (((high - low) * fraction + (1L << (AtanFractionBits - 1))) >> AtanFractionBits);
    }
}
