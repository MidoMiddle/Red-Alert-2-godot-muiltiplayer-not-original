using System;
using System.Collections.Generic;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>Which units a selection box covers. Pure screen geometry, tested in Rts.Simulation.Tests.</summary>
public static class BoxSelection
{
    /// <summary>Ids of the units whose screen point lies inside the box from <paramref name="a"/> to <paramref name="b"/> (edges included, corners in any order), in input order.</summary>
    public static List<EntityId> Pick(IEnumerable<(EntityId Unit, ScreenPoint Point)> units, ScreenPoint a, ScreenPoint b)
    {
        var left = MathF.Min(a.X, b.X);
        var right = MathF.Max(a.X, b.X);
        var top = MathF.Min(a.Y, b.Y);
        var bottom = MathF.Max(a.Y, b.Y);
        var picked = new List<EntityId>();
        foreach (var (unit, point) in units)
        {
            if (point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom)
            {
                picked.Add(unit);
            }
        }

        return picked;
    }
}
