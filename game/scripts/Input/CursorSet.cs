using System;
using System.Collections.Generic;
using Godot;

namespace Rts.Client.Controls;

/// <summary>
/// The cursor images (docs/implementation/M2_unit_control.md, 4.14), drawn in code and symbolic until the visuals stage
/// (docs/implementation/conventions.md, section 12): an arrow, green corners over an own unit, green arrows pointing in for a
/// move, a red crossed circle where nobody can go, a yellow and red sight for an attack-move, a green flag for a waypoint. For the
/// base (M3 step 13): a yellow dollar to sell, a green wrench to repair, green arrows pointing out to deploy an MCV, and a yellow
/// crystal between green arrows to send harvesters to ore. The cursors of RA2 (2026-10-25): orange arrows in for a forced move (Alt),
/// red treads to crush, a yellow house with a green flag for an engineer to take, a yellow wrench for an engineer to mend, blue
/// arrows in for a spy, a green cross to heal, a black bomb with a lit fuse to plant one; the sights of the superweapons — the
/// radiation sign of the missile, a lightning bolt of the storm, the blue ring of the phase gate, the red shield of the field
/// projector, a parachute of the paradrop.
/// </summary>
public static class CursorSet
{
    private const int Size = 32;
    private const int Middle = Size / 2;

    private static readonly Color Outline = new(0f, 0f, 0f);
    private static readonly Color White = new(1f, 1f, 1f);
    private static readonly Color Green = new(0.35f, 1f, 0.35f);
    private static readonly Color Red = new(1f, 0.2f, 0.15f);
    private static readonly Color Yellow = new(1f, 0.85f, 0.1f);
    private static readonly Color Orange = new(1f, 0.55f, 0.1f);
    private static readonly Color Blue = new(0.35f, 0.7f, 1f);
    private static readonly Color Dark = new(0.12f, 0.12f, 0.14f);

    private static readonly Dictionary<CursorKind, Image> Images = new();
    private static CursorKind? _shown;

    /// <summary>The cursor shown now, for the debug panel: a screenshot of the view does not hold the cursor.</summary>
    public static CursorKind? Shown => _shown;

    /// <summary>Every cursor side by side on a dark strip, in the order of <see cref="CursorKind"/>: the input script saves it to look at.</summary>
    public static Image Sheet()
    {
        var kinds = Enum.GetValues<CursorKind>();
        var sheet = Image.CreateEmpty(Size * kinds.Length, Size, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.25f, 0.27f, 0.3f));
        for (var i = 0; i < kinds.Length; i++)
        {
            var image = Draw(kinds[i]);
            sheet.BlendRect(image, new Rect2I(0, 0, Size, Size), new Vector2I(i * Size, 0));
        }

        return sheet;
    }

    /// <summary>Shows the cursor of <paramref name="kind"/>; nothing happens when it is already shown.</summary>
    public static void Show(CursorKind kind)
    {
        if (_shown == kind)
        {
            return;
        }

        _shown = kind;
        if (!Images.TryGetValue(kind, out var image))
        {
            image = Draw(kind);
            Images.Add(kind, image);
        }

        Input.SetCustomMouseCursor(image, Input.CursorShape.Arrow, Hotspot(kind));
    }

    private static Vector2 Hotspot(CursorKind kind) => kind switch
    {
        CursorKind.Normal => Vector2.Zero,
        CursorKind.Waypoint or CursorKind.Rally => new Vector2(8, 28),
        _ => new Vector2(Middle, Middle),
    };

    private static Image Draw(CursorKind kind)
    {
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        Func<float, float, Color?> paint = kind switch
        {
            CursorKind.Normal => Arrow,
            CursorKind.Select => Corners,
            CursorKind.Move => ArrowsIn,
            CursorKind.Forbidden => CrossedCircle,
            CursorKind.AttackMove => Sight,
            CursorKind.Attack => AttackSight,
            CursorKind.ForceAttack => ForceSight,
            CursorKind.Sell => Dollar,
            CursorKind.Repair => Wrench,
            CursorKind.Deploy => ArrowsOut,
            CursorKind.Harvest => Crystal,
            CursorKind.Enter => ArrowsIn,
            CursorKind.ForceMove => (x, y) => Recolor(ArrowsIn(x, y), Orange),
            CursorKind.Crush => Treads,
            CursorKind.Capture => House,
            CursorKind.EngineerRepair => (x, y) => Outlined(x, y, (px, py) => Wrench(px, py) is not null, Yellow),
            CursorKind.Infiltrate => (x, y) => Recolor(ArrowsIn(x, y), Blue),
            CursorKind.Heal => (x, y) => Outlined(x, y, (px, py) => (MathF.Abs(px - Middle) < 3.5f && MathF.Abs(py - Middle) < 11) || (MathF.Abs(py - Middle) < 3.5f && MathF.Abs(px - Middle) < 11), Green),
            CursorKind.Bomb => Bomb,
            CursorKind.Nuke => Radiation,
            CursorKind.Storm => (x, y) => Outlined(x, y, Lightning, Yellow),
            CursorKind.Chrono => ChronoRing,
            CursorKind.IronCurtain => (x, y) => Outlined(x, y, Shield, Red),
            CursorKind.Paradrop => Parachute,
            CursorKind.Rally => RallyFlag,
            _ => Flag,
        };
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                image.SetPixel(x, y, paint(x + 0.5f, y + 0.5f) ?? new Color(0, 0, 0, 0));
            }
        }

        return image;
    }

    /// <summary>A white arrow with a dark edge, its tip at the top-left corner.</summary>
    private static Color? Arrow(float x, float y)
    {
        var inHead = y <= 20 && x <= y * 0.62f;
        var inStem = y > 12 && y <= 26 && x >= y * 0.62f - 8 && x <= y * 0.62f - 3 && x >= 4;
        if (!inHead && !inStem)
        {
            return null;
        }

        var stemEdge = inStem && !inHead && (x < y * 0.62f - 7f || x > y * 0.62f - 4f);
        var edge = x < 1.5f || x > y * 0.62f - 1.5f || (y > 18.5f && inHead && !inStem) || y > 24.5f || stemEdge;
        return edge ? Outline : White;
    }

    /// <summary>Four green corner brackets around the middle.</summary>
    private static Color? Corners(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        var onSide = (dx is > 11 and < 14 && dy is > 5 and < 14) || (dy is > 11 and < 14 && dx is > 5 and < 14);
        return onSide ? Green : null;
    }

    /// <summary>Four green triangles pointing at the middle.</summary>
    private static Color? ArrowsIn(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        var vertical = dy is > 5 and < 14 && dx < (dy - 5) * 0.7f;
        var horizontal = dx is > 5 and < 14 && dy < (dx - 5) * 0.7f;
        return vertical || horizontal ? Green : null;
    }

    /// <summary>A red ring crossed from the top-left to the bottom-right.</summary>
    private static Color? CrossedCircle(float x, float y)
    {
        var distance = MathF.Sqrt((x - Middle) * (x - Middle) + (y - Middle) * (y - Middle));
        var ring = distance is > 9 and < 12.5f;
        var bar = distance < 10 && MathF.Abs(x - y) < 2.2f;
        return ring || bar ? Red : null;
    }

    /// <summary>A red ring with a yellow cross that leaves the middle open.</summary>
    private static Color? Sight(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance is > 8 and < 10.5f)
        {
            return Red;
        }

        var cross = (dx < 1.2f && dy is > 3 and < 15) || (dy < 1.2f && dx is > 3 and < 15);
        return cross ? Yellow : null;
    }

    /// <summary>The attack of RA2 (M4 step 13): a red ring with four red ticks pointing in, on a dark edge.</summary>
    private static Color? AttackSight(float x, float y) => Crosshair(x, y, Red, dot: false);

    /// <summary>The forced attack (Ctrl, M4 step 13): the same sight in orange, with a dot in the middle.</summary>
    private static Color? ForceSight(float x, float y) => Crosshair(x, y, Orange, dot: true);

    private static Color? Crosshair(float x, float y, Color color, bool dot)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var ring = distance is > 9 and < 11.5f;
        var ticks = (dx < 1.3f && dy is > 6 and < 14) || (dy < 1.3f && dx is > 6 and < 14);
        var centre = dot && distance < 2.2f;
        if (ring || ticks || centre)
        {
            return color;
        }

        var edge = distance is > 8 and < 12.5f || (dx < 2.3f && dy is > 5 and < 15) || (dy < 2.3f && dx is > 5 and < 15) || (dot && distance < 3.2f);
        return edge ? Outline : null;
    }

    /// <summary>A yellow dollar sign with a dark edge: an S of three bars and two sides, crossed by a stroke.</summary>
    private static Color? Dollar(float x, float y)
    {
        bool Inside(float px, float py)
        {
            var bar = px is > 9 and < 23 && (MathF.Abs(py - 8) < 1.8f || MathF.Abs(py - 16) < 1.8f || MathF.Abs(py - 24) < 1.8f);
            var left = px is > 9 and < 12.6f && py is > 8 and < 16;
            var right = px is > 19.4f and < 23 && py is > 16 and < 24;
            var stroke = MathF.Abs(px - Middle) < 1.4f && py is > 4 and < 28;
            return bar || left || right || stroke;
        }

        if (Inside(x, y))
        {
            return Yellow;
        }

        return Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1) ? Outline : null;
    }

    /// <summary>A green wrench: a handle from the bottom-left and an open ring at the top-right.</summary>
    private static Color? Wrench(float x, float y)
    {
        // Distance from the handle, the line x + y = 32 between (8, 24) and (19, 13).
        var across = MathF.Abs(x + y - 32) / MathF.Sqrt(2);
        var handle = across < 2.2f && x is > 6 and < 20 && y is > 12 and < 26;
        var dx = x - 21.5f;
        var dy = y - 10.5f;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        // The ring is open towards the top-right, along the handle.
        var ring = distance is > 3.2f and < 7 && !(dx > 0 && dy < 0 && MathF.Abs(dx + dy) < 3.5f);
        return handle || ring ? Green : null;
    }

    /// <summary>Four green triangles pointing away from the middle.</summary>
    private static Color? ArrowsOut(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        var vertical = dy is > 4 and < 14 && dx < (14 - dy) * 0.7f;
        var horizontal = dx is > 4 and < 14 && dy < (14 - dx) * 0.7f;
        return vertical || horizontal ? Green : null;
    }

    /// <summary>A yellow crystal in the middle between four green arrows pointing at it.</summary>
    private static Color? Crystal(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        var dy = MathF.Abs(y - Middle);
        if (dx + dy < 5f)
        {
            return Yellow;
        }

        return ArrowsIn(x, y) is { } arrow && dx + dy > 8f ? arrow : null;
    }

    private static Color? Recolor(Color? color, Color to) => color is null ? null : to;

    /// <summary>The shape in <paramref name="color"/> with a dark edge a pixel wide.</summary>
    private static Color? Outlined(float x, float y, Func<float, float, bool> inside, Color color)
    {
        if (inside(x, y))
        {
            return color;
        }

        return inside(x - 1, y) || inside(x + 1, y) || inside(x, y - 1) || inside(x, y + 1) ? Outline : null;
    }

    /// <summary>Two red tank treads, side by side, with dark links across them: drive over it.</summary>
    private static Color? Treads(float x, float y)
    {
        var tread = (x is > 7 and < 13 || x is > 19 and < 25) && y is > 5 and < 27;
        if (!tread)
        {
            return null;
        }

        return (int)y % 4 == 0 ? Outline : Red;
    }

    /// <summary>A yellow house — walls and a roof — with a green flag on it: take it.</summary>
    private static Color? House(float x, float y)
    {
        var flag = x >= 16 && x < 26 && y is > 2 and < 9 && x < 16 + 10 * (1 - MathF.Abs(y - 5.5f) / 3.5f);
        if (flag)
        {
            return Green;
        }

        if (x is > 15 and < 17 && y is > 2 and < 14)
        {
            return Outline;
        }

        return Outlined(x, y, (px, py) => (px is > 8 and < 24 && py is > 18 and < 28) || (py is > 11 and <= 18 && MathF.Abs(px - Middle) < (py - 11) * 1.15f), Yellow);
    }

    /// <summary>A black round bomb with a fuse lit at its end.</summary>
    private static Color? Bomb(float x, float y)
    {
        var dx = x - 14;
        var dy = y - 19;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance < 8)
        {
            return distance > 6.8f ? White : Dark;
        }

        var fuse = MathF.Abs((x - 18) - (11 - y)) < 1.4f && x is > 18 and < 25 && y is > 4 and < 12;
        if (fuse)
        {
            return Yellow;
        }

        var spark = MathF.Sqrt((x - 26) * (x - 26) + (y - 4) * (y - 4)) < 3f;
        return spark ? Orange : null;
    }

    /// <summary>The sign of radiation: a yellow disc with three dark blades round a dark middle.</summary>
    private static Color? Radiation(float x, float y)
    {
        var dx = x - Middle;
        var dy = y - Middle;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > 13)
        {
            return null;
        }

        if (distance > 12)
        {
            return Outline;
        }

        if (distance < 2.5f)
        {
            return Dark;
        }

        // Three blades, 60° wide, every 120°, from the top.
        var angle = (MathF.Atan2(dy, dx) * 180f / MathF.PI + 450f) % 120f;
        return distance is > 4 and < 11 && angle < 60f ? Dark : Yellow;
    }

    /// <summary>A lightning bolt from the top-right to the bottom-left.</summary>
    private static bool Lightning(float x, float y)
    {
        var upper = y is > 3 and < 17 && MathF.Abs(x - (23 - (y - 3) * 0.55f)) < 2.6f;
        var middle = y is > 14 and < 18 && x is > 11 and < 22;
        var lower = y is > 16 and < 30 && MathF.Abs(x - (20 - (y - 16) * 0.6f)) < 2.6f;
        return upper || middle || lower;
    }

    /// <summary>A blue ring with a light blue middle: the phase gate takes what stands here.</summary>
    private static Color? ChronoRing(float x, float y)
    {
        var distance = MathF.Sqrt((x - Middle) * (x - Middle) + (y - Middle) * (y - Middle));
        return distance switch
        {
            > 9 and < 13 => Blue,
            > 8 and < 14 => Outline,
            < 3 => White,
            _ => null,
        };
    }

    /// <summary>A shield: flat on top, pointed at the bottom.</summary>
    private static bool Shield(float x, float y)
    {
        var dx = MathF.Abs(x - Middle);
        return (y is > 4 and < 17 && dx < 10) || (y is >= 17 and < 29 && dx < 10 * (29 - y) / 12f);
    }

    /// <summary>A white parachute with its lines down to a dark load.</summary>
    private static Color? Parachute(float x, float y)
    {
        var dx = x - Middle;
        var dome = y < 14 && dx * dx / 144f + (y - 14) * (y - 14) / 100f < 1f && y > 4;
        if (dome)
        {
            return White;
        }

        var lines = y is >= 14 and < 24 && (MathF.Abs(dx - (-11 + (y - 14) * 1.0f)) < 0.8f || MathF.Abs(dx - (11 - (y - 14) * 1.0f)) < 0.8f);
        if (lines)
        {
            return Outline;
        }

        return MathF.Abs(dx) < 3 && y is >= 23 and < 29 ? Dark : null;
    }

    /// <summary>A dark pole with a green flag; the hotspot is the foot of the pole.</summary>
    /// <summary>The rally point: a yellow pennant on a pole planted in a white ring on the ground.</summary>
    private static Color? RallyFlag(float x, float y)
    {
        var (dx, dy) = ((x - 8.5f) / 7.5f, (y - 28f) / 3f);
        var ring = dx * dx + dy * dy;
        if (ring is > 0.55f and < 1f)
        {
            return Color.Color8(255, 255, 255);
        }

        if (x is > 7 and < 9.5f && y is > 3 and < 28)
        {
            return Outline;
        }

        return x >= 9.5f && y is > 4 and < 16 && x < 9.5f + 14 * (1 - MathF.Abs(y - 10) / 6) ? Color.Color8(255, 214, 64) : null;
    }

    private static Color? Flag(float x, float y)
    {
        if (x is > 7 and < 9.5f && y is > 3 and < 29)
        {
            return Outline;
        }

        var flag = x >= 9.5f && y is > 4 and < 16 && x < 9.5f + 14 * (1 - MathF.Abs(y - 10) / 6);
        return flag ? Green : null;
    }
}
