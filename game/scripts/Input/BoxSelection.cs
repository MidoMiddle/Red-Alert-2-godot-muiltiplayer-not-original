using System;
using System.Collections.Generic;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>A rectangle of the view in design pixels: the field, a selection box, or the part of the view a unit is on.</summary>
public readonly record struct ScreenRect(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Whether a point lies inside the rectangle, edges included.</summary>
    public bool Contains(ScreenPoint point) => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
}

/// <summary>Which units a selection box or a type select covers. Pure screen geometry, tested in Rts.Simulation.Tests.</summary>
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

    /// <summary>
    /// The corners of a box cut to <paramref name="field"/>: a box dragged over the sidebar or the command bar does not reach what
    /// is hidden under them.
    /// </summary>
    public static (ScreenPoint From, ScreenPoint To) ClipToField(ScreenPoint from, ScreenPoint to, ScreenRect field) =>
        (ClipPoint(from, field), ClipPoint(to, field));

    private static ScreenPoint ClipPoint(ScreenPoint point, ScreenRect field) =>
        new(Math.Clamp(point.X, field.Left, field.Right), Math.Clamp(point.Y, field.Top, field.Bottom));

    /// <summary>
    /// Ids of the units of <paramref name="type"/> whose screen point lies inside <paramref name="field"/>, in input order: a
    /// double click on a unit, or the type select key. Points behind the sidebar or the command bar are outside the field. The
    /// caller passes only the player's own living units.
    /// </summary>
    public static List<EntityId> SameTypeInField(IEnumerable<(EntityId Unit, UnitType Type, ScreenPoint Point)> units, UnitType type, ScreenRect field)
    {
        var picked = new List<EntityId>();
        foreach (var (unit, unitType, point) in units)
        {
            if (unitType == type && field.Contains(point))
            {
                picked.Add(unit);
            }
        }

        return picked;
    }

    /// <summary>
    /// Ids of the units of <paramref name="type"/> anywhere on the map, in input order, at most <paramref name="limit"/>. The
    /// whole-map select keeps the cap of one command (docs/implementation/open_questions.md). The caller passes the player's own
    /// living units in id order.
    /// </summary>
    public static List<EntityId> SameTypeOnMap(IEnumerable<(EntityId Unit, UnitType Type)> units, UnitType type, int limit)
    {
        var picked = new List<EntityId>();
        foreach (var (unit, unitType) in units)
        {
            if (picked.Count >= limit)
            {
                break;
            }

            if (unitType == type)
            {
                picked.Add(unit);
            }
        }

        return picked;
    }
}
