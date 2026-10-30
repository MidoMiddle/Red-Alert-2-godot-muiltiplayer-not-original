using System;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;

namespace Rts.Client.Climates;

/// <summary>A colour of the look, 0…1 per channel, written <c>"#rrggbb"</c> in the climate file.</summary>
public readonly record struct LookColor(float R, float G, float B)
{
    public static readonly LookColor White = new(1f, 1f, 1f);
}

/// <summary>
/// A ground material of a climate (docs/implementation/M7_maps.md, 4.12): the texture the codes of a map's <c>texture</c> layer
/// stand for. <paramref name="Albedo"/> is a file of <c>res://assets/textures/terrain/</c>; <paramref name="Tiling"/> is how many
/// cells one copy of it covers; <paramref name="Tint"/> multiplies it; <paramref name="Frost"/> (0…1) lays the climate's frost
/// over it in patches, so a photograph of a summer ground looks wintry; <paramref name="Water"/> gets the ripples and the depth of
/// the water's land types. With <paramref name="Color"/> the shader paints the ground itself (the owner, 2026-10-30: the light grass
/// of RA2): the colour is <paramref name="Color"/>, in large soft patches of <paramref name="Patches"/> over
/// <paramref name="PatchesShare"/> of the ground (0…1, half by default), and the photograph gives only its light and shade, its own
/// colour left out; the tint is not used then. Snowy grass is snow with patches of green over a third of it, as in RA2.
/// </summary>
public sealed record ClimateMaterial(
    string Id,
    string Albedo,
    float Tiling,
    float Roughness,
    LookColor Tint,
    float Frost,
    bool Water,
    LookColor? Color = null,
    LookColor? Patches = null,
    float PatchesShare = 0.5f);

/// <summary>
/// A time of day of a climate (docs/map_elements/18_lighting/README.md): the sun (or the moon) and the environment. The sun's
/// <paramref name="SunAzimuth"/> is the direction its light goes in, degrees around the vertical from the map's +X (east) towards
/// +Y (south); <paramref name="SunElevation"/> is how far above the horizon it stands, degrees.
/// </summary>
public sealed record LightPreset(
    string Id,
    float SunAzimuth,
    float SunElevation,
    LookColor SunColor,
    float SunEnergy,
    LookColor AmbientColor,
    float AmbientEnergy,
    LookColor Background,
    LookColor FogColor,
    float FogDensity,
    float Exposure,
    float Saturation)
{
    /// <summary>The direction the light goes in, in Godot axes (X east, Y up, Z south), of length 1.</summary>
    public (float X, float Y, float Z) SunDirection()
    {
        var azimuth = SunAzimuth * MathF.PI / 180f;
        var elevation = SunElevation * MathF.PI / 180f;
        var flat = MathF.Cos(elevation);
        return (flat * MathF.Cos(azimuth), -MathF.Sin(elevation), flat * MathF.Sin(azimuth));
    }
}

/// <summary>The trees of a climate: pines (winter), broadleaf trees (summer, the town), palms and cacti (the desert), palms among a few small broadleaf trees (the coast).</summary>
public enum TreeKind
{
    Pine,
    Broadleaf,
    Desert,
    Coast,
}

/// <summary>
/// The look of a climate (docs/implementation/M7_maps.md, 4.12; <c>game/data/climates/&lt;id&gt;/look.yaml</c>): the ground materials,
/// the material of cliffs, the water's normal map and depth colour, and the light of every time of day of
/// <see cref="MapLighting.Ids"/>. Visual only: the simulation never reads it and the data hash leaves it out.
/// </summary>
public sealed class ClimateLook
{
    public const string FormatVersion = "climate/1";

    /// <summary>The ground shader holds this many materials at most.</summary>
    public const int MaxMaterials = 16;

    public ClimateLook(
        string id,
        ImmutableArray<ClimateMaterial> materials,
        string cliff,
        string waterNormal,
        LookColor deepWater,
        ImmutableArray<LightPreset> lighting,
        TreeKind trees = TreeKind.Pine,
        float stones = 0f)
    {
        Require(!materials.IsEmpty && materials.Length <= MaxMaterials, $"a climate has 1…{MaxMaterials} materials");
        Require(materials.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() == materials.Length, "every material once");
        Require(materials.Any(m => m.Id == cliff), $"the cliff material '{cliff}' is none of the materials");
        Require(MapLighting.Ids.All(preset => lighting.Count(l => l.Id == preset) == 1), "every time of day once");
        Id = id;
        Materials = materials;
        Cliff = cliff;
        WaterNormal = waterNormal;
        DeepWater = deepWater;
        Lighting = [.. MapLighting.Ids.Select(preset => lighting.First(l => l.Id == preset))];
        Trees = trees;
        Stones = stones;
    }

    /// <summary>
    /// The share of the free level ground that has a few stones lying on it, 0…1 (key <c>stones</c>; none by default): the rocks
    /// among RA2's snow (the owner, 2026-10-30). Only a picture.
    /// </summary>
    public float Stones { get; }

    /// <summary>How the trees of the map and the holo tank's disguise look in the climate (the rules have one kind of tree).</summary>
    public TreeKind Trees { get; }

    public string Id { get; }

    /// <summary>In file order: the slot of a material in the ground shader is its index here.</summary>
    public ImmutableArray<ClimateMaterial> Materials { get; }

    /// <summary>The material of the cliffs' walls: the ground shader draws it on every steep face, from the side.</summary>
    public string Cliff { get; }

    public string WaterNormal { get; }

    /// <summary>The colour deep water fades to.</summary>
    public LookColor DeepWater { get; }

    /// <summary>In the order of <see cref="MapLighting.Ids"/>.</summary>
    public ImmutableArray<LightPreset> Lighting { get; }

    /// <summary>The slot of a material, or -1 for a material the climate does not have.</summary>
    public int SlotOf(string material)
    {
        for (var i = 0; i < Materials.Length; i++)
        {
            if (Materials[i].Id == material)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The light of a time of day; a time the climate does not have gets the day.</summary>
    public LightPreset Light(string lighting) =>
        Lighting.FirstOrDefault(l => l.Id == lighting) ?? Lighting.First(l => l.Id == MapLighting.Default);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException($"Invalid climate: {message}.");
        }
    }
}
