using System.Collections.Generic;
using Godot;
using Rts.Client.Climates;

namespace Rts.Client.Terrain;

/// <summary>
/// The ground textures of a climate for the ground shader (docs/implementation/M7_maps.md, 4.12): the albedo of every material
/// in one <see cref="Texture2DArray"/>, layer = slot (<see cref="ClimateLook.Materials"/>), with mipmaps, and the water's normal
/// map. Built once per climate. Without one of the files the ground is drawn flat (the land colours).
/// </summary>
public static class TerrainTextures
{
    /// <summary>The size every layer is scaled to.</summary>
    private const int LayerSize = 1024;

    private static readonly Dictionary<string, GroundTextures?> Built = new();

    public static GroundTextures? Load(ClimateLook climate)
    {
        if (Built.TryGetValue(climate.Id, out var cached))
        {
            return cached;
        }

        Godot.Collections.Array<Image>? layers = new();
        GroundTextures? textures = null;
        foreach (var material in climate.Materials)
        {
            if (LoadImage(material.Albedo) is not { } image)
            {
                layers = null;
                break;
            }

            layers.Add(image);
        }

        var normal = GD.Load<Texture2D>(ClimateLooks.TextureFolder + climate.WaterNormal);
        if (layers is not null && normal is not null)
        {
            var albedo = new Texture2DArray();
            albedo.CreateFromImages(layers);
            textures = new GroundTextures(albedo, normal);
        }
        else
        {
            GD.PushWarning($"[climate] ground textures of '{climate.Id}' are missing in {ClimateLooks.TextureFolder}: the ground is drawn flat.");
        }

        Built[climate.Id] = textures;
        return textures;
    }

    // Every layer of a Texture2DArray has one size and one format.
    private static Image? LoadImage(string file)
    {
        if (GD.Load<Texture2D>(ClimateLooks.TextureFolder + file)?.GetImage() is not { } image)
        {
            return null;
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.ClearMipmaps();
        image.Convert(Image.Format.Rgb8);
        if (image.GetWidth() != LayerSize || image.GetHeight() != LayerSize)
        {
            image.Resize(LayerSize, LayerSize, Image.Interpolation.Lanczos);
        }

        image.GenerateMipmaps();
        return image;
    }
}

/// <summary>The textures the ground shader samples (see <see cref="TerrainTextures.Load"/>).</summary>
public sealed record GroundTextures(Texture2DArray Albedo, Texture2D WaterNormal);
