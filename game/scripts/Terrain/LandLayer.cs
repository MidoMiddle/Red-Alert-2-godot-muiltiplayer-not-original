using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>The land types of the simulation map as a texture, one pixel per cell, for the ground shader.</summary>
public static class LandLayer
{
    public static ImageTexture CreateTexture(MapGrid map)
    {
        var image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgb8);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var (r, g, b) = LandColors.Of(map.LandAt(new CellPos(x, y)));
                image.SetPixel(x, y, new Color(r, g, b));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
