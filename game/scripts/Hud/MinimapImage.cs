using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Hud;

/// <summary>
/// The ground of the minimap (docs/implementation/M2_unit_control.md, 4.14): one pixel per cell in the colour of its land
/// (<see cref="LandColors"/>; a bridge in the colour of a road), trees dark green, everything a little darker so the unit dots stand out. Pure, tested in
/// Rts.Simulation.Tests.
/// </summary>
public static class MinimapImage
{
    /// <summary>How much of the land colour is kept.</summary>
    public const float Shade = 0.8f;

    public static readonly (float R, float G, float B) TreeColor = (0.12f, 0.3f, 0.12f);

    /// <summary>The colour of one cell, components 0…1.</summary>
    public static (float R, float G, float B) ColorOf(LandType land, bool blocked)
    {
        var (r, g, b) = blocked ? TreeColor : LandColors.Of(land);
        return (r * Shade, g * Shade, b * Shade);
    }

    /// <summary>RGB bytes, row by row from the north-west corner: the data of an RGB8 image of <c>Width × Height</c>.</summary>
    public static byte[] Pixels(MapGrid map)
    {
        var pixels = new byte[map.Width * map.Height * 3];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var (r, g, b) = ColorOf(map.SurfaceAt(cell), map.IsBlocked(cell));
                var index = (y * map.Width + x) * 3;
                pixels[index] = ToByte(r);
                pixels[index + 1] = ToByte(g);
                pixels[index + 2] = ToByte(b);
            }
        }

        return pixels;
    }

    public static byte ToByte(float component) => (byte)(component <= 0f ? 0 : component >= 1f ? 255 : (int)(component * 255f + 0.5f));
}
