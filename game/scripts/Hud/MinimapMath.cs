using System;

namespace Rts.Client.Hud;

/// <summary>
/// Maps between map coordinates (in cells) and the pixels of the minimap, a square of <c>size</c> pixels that shows the whole map.
/// Each axis is scaled on its own, so a non-square map still fills the square. A map with a playable area (a rectangle on the
/// screen, <c>Rts.Data.Maps.MapArea</c>) is shown as the screen shows it, as in RA2: that rectangle, its width twice its height
/// in the screen's axes <c>v = x − z</c> and <c>u = x + z</c>, fitted into the square and centred (<see cref="For"/>).
/// </summary>
public static class MinimapMath
{
    /// <summary>The minimap pixel of a point on the map.</summary>
    public static (float X, float Y) ToMinimap(float cellX, float cellY, float mapWidth, float mapHeight, float size) =>
        (cellX / mapWidth * size, cellY / mapHeight * size);

    /// <summary>The map point, in cells, under a minimap pixel. Pixels outside the minimap are clamped onto the map.</summary>
    public static (float X, float Y) FromMinimap(float x, float y, float mapWidth, float mapHeight, float size) =>
        (Math.Clamp(x / size * mapWidth, 0f, mapWidth), Math.Clamp(y / size * mapHeight, 0f, mapHeight));

    /// <summary>
    /// The minimap of a map: without a playable area the square of <see cref="ToMinimap"/>; with one (its left, top, right and
    /// bottom in the screen's axes) the rectangle of the area.
    /// </summary>
    public static MinimapTransform For(float mapWidth, float mapHeight, (int Left, int Top, int Right, int Bottom)? area, float size)
    {
        if (area is not { } a)
        {
            return new MinimapTransform(size / mapWidth, 0f, 0f, size / mapHeight, 0f, 0f, (0f, 0f, mapWidth, mapHeight), (0f, 0f, size, size));
        }

        // A step of v is 2 pixels of the screen for every pixel a step of u is: fit 2·Δv × Δu into the square.
        var (width, height) = (2f * (a.Right - a.Left), (float)(a.Bottom - a.Top));
        var scale = size / Math.Max(width, height);
        var (left, top) = ((size - width * scale) / 2f, (size - height * scale) / 2f);
        // px = left + 2s (v − Left), py = top + s (u − Top), with v = x − z and u = x + z.
        return new MinimapTransform(2f * scale, scale, -2f * scale, scale, left - 2f * scale * a.Left, top - scale * a.Top, (0f, 0f, mapWidth, mapHeight),
            (left, top, width * scale, height * scale));
    }
}

/// <summary>
/// An affine map from the map (x, z in cells) to minimap pixels: <c>px = Ox + Xx·x + Zx·z</c>, <c>py = Oy + Xy·x + Zy·z</c>.
/// <see cref="FromMinimap"/> goes back and clamps onto the map (<paramref name="Bounds"/>). <paramref name="Frame"/> is the part of the
/// minimap the map fills: all of it, or the rectangle of the playable area.
/// </summary>
public readonly record struct MinimapTransform(
    float Xx, float Xy, float Zx, float Zy, float Ox, float Oy, (float X0, float Z0, float X1, float Z1) Bounds, (float X, float Y, float Width, float Height) Frame)
{
    public (float X, float Y) ToMinimap(float x, float z) => (Ox + Xx * x + Zx * z, Oy + Xy * x + Zy * z);

    public (float X, float Z) FromMinimap(float px, float py)
    {
        var (dx, dy) = (px - Ox, py - Oy);
        var determinant = Xx * Zy - Zx * Xy;
        var x = (dx * Zy - Zx * dy) / determinant;
        var z = (Xx * dy - Xy * dx) / determinant;
        return (Math.Clamp(x, Bounds.X0, Bounds.X1), Math.Clamp(z, Bounds.Z0, Bounds.Z1));
    }
}
