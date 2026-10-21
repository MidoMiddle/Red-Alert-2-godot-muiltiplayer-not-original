using System;
using System.Globalization;

namespace Rts.Simulation.Math;

/// <summary>
/// Angle as a 16-bit binary fraction of a full turn: 65536 units = 360°, so one unit ≈ 0.0055°.
/// 0 is north (−Y on the map), angles grow clockwise: east is a quarter turn (docs/implementation/M2_unit_control.md, 4.1).
/// Adding and subtracting wrap around 0° by themselves, like the ushort they are stored in.
/// </summary>
public readonly struct BAngle : IEquatable<BAngle>
{
    public const int FullTurn = 65536;
    public const int HalfTurn = FullTurn / 2;
    public const int QuarterTurn = FullTurn / 4;

    public static readonly BAngle North = new(0);
    public static readonly BAngle East = new(QuarterTurn);
    public static readonly BAngle South = new(HalfTurn);
    public static readonly BAngle West = new(HalfTurn + QuarterTurn);

    /// <summary>Underlying value: 0…65535.</summary>
    public readonly ushort Value;

    public BAngle(ushort value) => Value = value;

    /// <summary>Any whole number of units, wrapped into one turn (−1 is 65535).</summary>
    public static BAngle FromUnits(long units) => new(unchecked((ushort)units));

    /// <summary>Whole degrees, rounded to the nearest unit (data files and tests speak degrees).</summary>
    public static BAngle FromDegrees(long degrees) => FromUnits(IntMath.DivRound(degrees * FullTurn, 360));

    public static BAngle operator +(BAngle a, BAngle b) => FromUnits(a.Value + b.Value);

    public static BAngle operator -(BAngle a, BAngle b) => FromUnits(a.Value - b.Value);

    public static bool operator ==(BAngle a, BAngle b) => a.Value == b.Value;

    public static bool operator !=(BAngle a, BAngle b) => a.Value != b.Value;

    /// <summary>
    /// Shortest signed turn from <paramref name="from"/> to <paramref name="to"/>: −32768…32767 units,
    /// positive is clockwise. From 350° to 10° is +20°, from 10° to 350° is −20°; exactly half a turn is −32768.
    /// </summary>
    public static short Difference(BAngle from, BAngle to) => unchecked((short)(to.Value - from.Value));

    /// <summary>Turns from <paramref name="current"/> toward <paramref name="target"/> the short way, by at most <paramref name="maxStep"/> units.</summary>
    public static BAngle RotateTowards(BAngle current, BAngle target, int maxStep)
    {
        if (maxStep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStep), maxStep, "The turn step cannot be negative.");
        }

        int difference = Difference(current, target);
        if (IntMath.Abs(difference) <= maxStep)
        {
            return target;
        }

        return FromUnits(current.Value + (difference > 0 ? maxStep : -maxStep));
    }

    public bool Equals(BAngle other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is BAngle other && Equals(other);

    public override int GetHashCode() => Value;

    /// <summary>Units and whole degrees (rounded down), for logs and test messages.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Value} ({Value * 360 / FullTurn}°)");
}
