using System.Collections.Immutable;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Visibility;

/// <summary>
/// The cells within a sight radius, as offsets from the middle cell (docs/implementation/M4_combat.md, 4.14): every cell
/// <see cref="SightRules.InSight"/> keeps, row by row. Made once for the radii up to <see cref="Cached"/>; a larger one is made when asked.
/// </summary>
public static class SightCircles
{
    /// <summary>The largest radius made in advance.</summary>
    public const int Cached = 32;

    private static readonly ImmutableArray<CellPos>[] Circles = MakeAll();

    /// <summary>The offsets of the cells within <paramref name="radius"/>; none for a radius below 0.</summary>
    public static ImmutableArray<CellPos> Of(int radius) =>
        radius < 0 ? [] : radius <= Cached ? Circles[radius] : Make(radius);

    private static ImmutableArray<CellPos>[] MakeAll()
    {
        var circles = new ImmutableArray<CellPos>[Cached + 1];
        for (var radius = 0; radius <= Cached; radius++)
        {
            circles[radius] = Make(radius);
        }

        return circles;
    }

    private static ImmutableArray<CellPos> Make(int radius)
    {
        var cells = ImmutableArray.CreateBuilder<CellPos>();
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (SightRules.InSight(dx, dy, radius))
                {
                    cells.Add(new CellPos(dx, dy));
                }
            }
        }

        return cells.ToImmutable();
    }
}
