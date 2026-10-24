using Godot;

namespace Rts.Client.Climates;

/// <summary>
/// Lights a scene by a time of day of its climate (<see cref="LightPreset"/>; docs/map_elements/18_lighting/README.md): the
/// sun's direction, colour and energy, and a copy of the scene's environment with the preset's ambient light, background, fog,
/// exposure and saturation. The rest of the environment (SSAO, glow, tonemapper) stays as the scene has it. Visual only.
/// </summary>
public static class SceneLighting
{
    private const string CopyMark = "map_lighting";

    public static void Apply(LightPreset preset, DirectionalLight3D? sun, WorldEnvironment? world)
    {
        if (sun is not null)
        {
            var (x, y, z) = preset.SunDirection();
            sun.Basis = Basis.LookingAt(new Vector3(x, y, z), Vector3.Up);
            sun.LightColor = Color(preset.SunColor);
            sun.LightEnergy = preset.SunEnergy;
        }

        if (world?.Environment is { } shared)
        {
            // The scene's environment is a shared resource: the map's light goes on a copy, made once.
            var environment = shared.HasMeta(CopyMark) ? shared : (Environment)shared.Duplicate();
            environment.SetMeta(CopyMark, true);
            environment.BackgroundColor = Color(preset.Background);
            environment.AmbientLightColor = Color(preset.AmbientColor);
            environment.AmbientLightEnergy = preset.AmbientEnergy;
            environment.FogLightColor = Color(preset.FogColor);
            environment.FogDensity = preset.FogDensity;
            environment.TonemapExposure = preset.Exposure;
            environment.AdjustmentSaturation = preset.Saturation;
            world.Environment = environment;
        }
    }

    /// <summary>The direction the light of <paramref name="preset"/> goes in, for shaders.</summary>
    public static Vector3 SunDirection(LightPreset preset)
    {
        var (x, y, z) = preset.SunDirection();
        return new Vector3(x, y, z);
    }

    public static Color Color(LookColor color) => new(color.R, color.G, color.B);

    /// <summary>The text key of a time of day of <see cref="Rts.Data.Maps.MapLighting.Ids"/>, for the editor's lists.</summary>
    public static string LabelKey(string lighting) => lighting switch
    {
        "dawn" => "UI_LIGHTING_DAWN",
        "dusk" => "UI_LIGHTING_DUSK",
        "night" => "UI_LIGHTING_NIGHT",
        _ => "UI_LIGHTING_DAY",
    };
}
