using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The land types of the simulation map as a texture, one pixel per cell, for the ground shader; cliff cells in the cliff colour
/// (<see cref="LandColors.Of(LandType, CellShape)"/>).
/// </summary>
public static class LandLayer
{
    public static ImageTexture CreateTexture(MapGrid map) => ImageTexture.CreateFromImage(CreateImage(map));

    /// <summary>Paints the map into an existing texture of its size (the map changed).</summary>
    public static void Update(ImageTexture texture, MapGrid map) => texture.Update(CreateImage(map));

    /// <summary>The texture index of every cell (<see cref="TerrainTextures.IndexOf"/>) in the red channel, for the ground shader.</summary>
    public static ImageTexture CreateTypeTexture(MapGrid map) => ImageTexture.CreateFromImage(CreateTypeImage(map));

    /// <summary>Paints the texture indices into an existing type texture of its size (the map changed).</summary>
    public static void UpdateType(ImageTexture texture, MapGrid map) => texture.Update(CreateTypeImage(map));

    private static Image CreateTypeImage(MapGrid map)
    {
        var image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgb8);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var index = TerrainTextures.IndexOf(map.LandAt(cell), map.Heights.ShapeAt(cell));
                image.SetPixel(x, y, new Color(index / 255f, 0f, 0f));
            }
        }

        return image;
    }

    private static Image CreateImage(MapGrid map)
    {
        var image = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.Rgb8);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var (r, g, b) = LandColors.Of(map.LandAt(cell), map.Heights.ShapeAt(cell));
                image.SetPixel(x, y, new Color(r, g, b));
            }
        }

        return image;
    }
}
