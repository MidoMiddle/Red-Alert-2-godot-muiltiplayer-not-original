using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Simulation.Combat;

/// <summary>
/// The damage of one hit (docs/implementation/M4_combat.md, 4.5), in whole numbers so every platform gets the same: first the share
/// against the armor (<c>verses</c>), then the fall-off over the area, each step rounded down.
/// <code>
/// against armor = damage × verses
/// fall-off      = 1 − (1 − percent_at_max) × d / spread      (d ≤ spread, otherwise nothing)
/// result        = against armor × fall-off
/// </code>
/// The target the shot was aimed at and hit takes the whole share against its armor, at least 1 while the share is above zero; others in
/// the area take the fall-off by their distance from the centre, and nothing outside it.
/// </summary>
public static class DamageCalculator
{
    /// <summary>The damage against an armor: <paramref name="damage"/> × <paramref name="verses"/>, rounded down.</summary>
    public static int AgainstArmor(int damage, Fixed verses) =>
        damage <= 0 || verses <= Fixed.Zero ? 0 : (int)(((long)damage * verses.Raw) >> Fixed.FractionalBits);

    /// <summary>
    /// The share of damage at <paramref name="distance"/> leptons from the centre of an area of <paramref name="spread"/> cells:
    /// 1 at the centre, <paramref name="percentAtMax"/> at the edge, in a straight line between them, 0 beyond. An area of no size
    /// has no fall-off: nothing is hit but the target.
    /// </summary>
    public static Fixed FallOff(Fixed spread, Fixed percentAtMax, int distance)
    {
        var radius = (spread.Raw * WorldPos.LeptonsPerCell) >> Fixed.FractionalBits;
        if (radius <= 0 || distance > radius)
        {
            return Fixed.Zero;
        }

        var lost = ((Fixed.OneRaw - percentAtMax.Raw) * distance) / radius;
        return Fixed.FromRaw(Fixed.OneRaw - lost);
    }

    /// <summary>
    /// The damage the target of a shot takes: the whole share against its armor, never less than 1 while that share is above zero (a
    /// weapon that can hurt the armor always does).
    /// </summary>
    public static int Direct(int damage, Fixed verses)
    {
        if (damage <= 0 || verses <= Fixed.Zero)
        {
            return 0;
        }

        return IntMath.Max(1, AgainstArmor(damage, verses));
    }

    /// <summary>The damage something else in the area takes, <paramref name="distance"/> leptons from its centre.</summary>
    public static int InArea(int damage, Fixed verses, Fixed spread, Fixed percentAtMax, int distance)
    {
        var againstArmor = AgainstArmor(damage, verses);
        var fallOff = FallOff(spread, percentAtMax, distance);
        return (int)(((long)againstArmor * fallOff.Raw) >> Fixed.FractionalBits);
    }

    /// <summary>The straight distance between two points in leptons, rounded down.</summary>
    public static int Distance(WorldPos a, WorldPos b)
    {
        long dx = a.X - b.X;
        long dy = a.Y - b.Y;
        return (int)IntMath.Sqrt((dx * dx) + (dy * dy));
    }
}
