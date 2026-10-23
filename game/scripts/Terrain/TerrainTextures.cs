using Godot;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The ground textures (docs/map_elements/02_land_types/prompts.md, 03_water/prompts.md). For now these are the placeholders of
/// assets_src/textures (see its README). Every cell has a texture index (<see cref="IndexOf"/>) in the type map, and the ground
/// shader picks the texture by it. A cliff is rock whatever its land. Without all the textures the ground is drawn flat.
/// </summary>
public static class TerrainTextures
{
    private const string Folder = "res://assets/textures/terrain/";

    /// <summary>The index of the rock texture: cliff cells use it too.</summary>
    public const int Rock = 6;

    /// <summary>
    /// The texture index of a cell: 0 clear, 1 rough, 2 road, 3 beach, 4 shallow, 5 deep, 6 rock, 7 ice. The same order as the
    /// branches of grid.gdshader.
    /// </summary>
    public static int IndexOf(LandType land, CellShape shape) =>
        shape == CellShape.Cliff ? Rock : (land switch
        {
            LandType.Clear => 0,
            LandType.Rough => 1,
            LandType.Road => 2,
            LandType.Beach => 3,
            LandType.Shallow => 4,
            LandType.Deep => 5,
            LandType.Rock => Rock,
            LandType.Ice => 7,
            _ => throw new System.ArgumentOutOfRangeException(nameof(land), land, "Unknown land type."),
        });

    /// <summary>The textures for the ground shader, or null when one of the files is missing.</summary>
    public static GroundTextures? Load()
    {
        var snow = GD.Load<Texture2D>(Folder + "snow_clean.png");
        var grass = GD.Load<Texture2D>(Folder + "frozen_grass.png");
        var gravel = GD.Load<Texture2D>(Folder + "beach_gravel.png");
        var rock = GD.Load<Texture2D>(Folder + "rock_field.png");
        var ice = GD.Load<Texture2D>(Folder + "frozen_water.png");
        var normal = GD.Load<Texture2D>(Folder + "water_normal.png");
        if (snow is null || grass is null || gravel is null || rock is null || ice is null || normal is null)
        {
            GD.PushWarning("Ground textures are missing in " + Folder + ": the ground is drawn flat.");
            return null;
        }

        return new GroundTextures(snow, grass, gravel, rock, ice, normal);
    }
}

/// <summary>The textures the ground shader samples (see <see cref="TerrainTextures.Load"/>).</summary>
public sealed record GroundTextures(Texture2D Snow, Texture2D Grass, Texture2D Gravel, Texture2D Rock, Texture2D Ice, Texture2D WaterNormal);
