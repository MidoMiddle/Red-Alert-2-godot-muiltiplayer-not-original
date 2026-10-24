using System.Collections.Generic;
using Godot;

namespace Rts.Client.Climates;

/// <summary>
/// The climates of <c>res://data/climates/&lt;id&gt;/look.yaml</c> (<see cref="ClimateLook"/>), read once each. A map of a climate
/// that is missing or broken is drawn in winter, with a warning: the look never stops a match.
/// </summary>
public static class ClimateLooks
{
    public const string Default = "winter";
    public const string TextureFolder = "res://assets/textures/terrain/";

    private static readonly Dictionary<string, ClimateLook?> Loaded = new();

    /// <summary>The look of a climate, or of winter when it cannot be read; null only when winter cannot be read either.</summary>
    public static ClimateLook? Get(string id)
    {
        if (TryRead(id) is { } look)
        {
            return look;
        }

        if (id != Default)
        {
            GD.PushWarning($"[climate] '{id}' cannot be read: the map is drawn in {Default}.");
        }

        return TryRead(Default);
    }

    private static ClimateLook? TryRead(string id)
    {
        if (Loaded.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var path = $"res://data/climates/{id}/look.yaml";
        ClimateLook? look = null;
        if (Godot.FileAccess.FileExists(path))
        {
            try
            {
                look = ClimateLookReader.Read(Godot.FileAccess.GetFileAsString(path));
            }
            catch (ClimateFormatException e)
            {
                GD.PushError($"[climate] {path}:{e.Message}");
            }
        }

        Loaded[id] = look;
        return look;
    }
}
