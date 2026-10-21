using System;

namespace Rts.Client.Hud;

/// <summary>
/// Maps between map coordinates (in cells) and the pixels of the minimap, a square of <c>size</c> pixels that shows the whole map.
/// Each axis is scaled on its own, so a non-square map still fills the square.
/// </summary>
public static class MinimapMath
{
    /// <summary>The minimap pixel of a point on the map.</summary>
    public static (float X, float Y) ToMinimap(float cellX, float cellY, float mapWidth, float mapHeight, float size) =>
        (cellX / mapWidth * size, cellY / mapHeight * size);

    /// <summary>The map point, in cells, under a minimap pixel. Pixels outside the minimap are clamped onto the map.</summary>
    public static (float X, float Y) FromMinimap(float x, float y, float mapWidth, float mapHeight, float size) =>
        (Math.Clamp(x / size * mapWidth, 0f, mapWidth), Math.Clamp(y / size * mapHeight, 0f, mapHeight));
}
