using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Audio;

/// <summary>
/// One sound of <c>sounds.yaml</c>: its files (relative to <c>assets/audio</c>), its bus, its volume, the spread of its pitch, its
/// priority and its limits (docs/implementation/M6_menus_skirmish.md, 4.8).
/// </summary>
public sealed record SoundDefinition(
    string Name,
    IReadOnlyList<string> Files,
    string Bus,
    float VolumeDb,
    float PitchMin,
    float PitchMax,
    int Priority,
    int MaxConcurrent,
    int CooldownMs,
    bool Positional);

/// <summary>
/// Reads <c>game/data/audio/sounds.yaml</c>. Every mistake is reported with its sound and its field, and the sound it is in is left
/// out, so the rest of the game still plays. Pure, so the tests read the real file.
/// </summary>
public static class SoundLibrary
{
    /// <summary>The buses a sound can play on: the ones of <c>game/default_bus_layout.tres</c>.</summary>
    public static readonly IReadOnlyList<string> Buses = ["Master", "Music", "SFX", "Voice", "UI", "EVA"];

    /// <summary>The fields of a sound; any other is a mistake, so that a typo does not pass silently.</summary>
    public static readonly IReadOnlyList<string> Fields =
        ["files", "bus", "volume", "pitch", "priority", "maxConcurrent", "cooldownMs", "positional"];

    private const string SoundsKey = "sounds";
    private const int MaxPriority = 100;

    /// <summary>The sounds of <paramref name="text"/> by name; the mistakes are added to <paramref name="errors"/>.</summary>
    public static IReadOnlyDictionary<string, SoundDefinition> Parse(string text, string fileName, List<string> errors)
    {
        var sounds = new Dictionary<string, SoundDefinition>();
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException error)
        {
            errors.Add($"{fileName}: {error.Message}");
            return sounds;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root
            || Child(root, SoundsKey) is not YamlMappingNode table)
        {
            errors.Add($"{fileName}: there is no '{SoundsKey}' table");
            return sounds;
        }

        foreach (var pair in table.Children)
        {
            if (pair.Key is not YamlScalarNode { Value: { } name } || pair.Value is not YamlMappingNode fields)
            {
                errors.Add($"{fileName}: every sound is a name with a table of its fields");
                continue;
            }

            if (Definition(name, fields, fileName, errors) is { } definition)
            {
                sounds[name] = definition;
            }
        }

        return sounds;
    }

    /// <summary>One sound, or null when a field is missing or wrong; the reasons go to <paramref name="errors"/>.</summary>
    private static SoundDefinition? Definition(string name, YamlMappingNode fields, string fileName, List<string> errors)
    {
        var valid = true;
        void Problem(string field, string message)
        {
            errors.Add($"{fileName}: {name}.{field} {message}");
            valid = false;
        }

        foreach (var pair in fields.Children)
        {
            var key = (pair.Key as YamlScalarNode)?.Value;
            if (key is null || !Fields.Contains(key))
            {
                Problem(key ?? "?", "is not a field of a sound");
            }
        }

        var files = new List<string>();
        if (Child(fields, "files") is YamlSequenceNode { Children.Count: > 0 } list)
        {
            foreach (var item in list.Children)
            {
                if (item is YamlScalarNode { Value: { Length: > 0 } file })
                {
                    files.Add(file);
                }
                else
                {
                    Problem("files", "must be file names");
                }
            }
        }
        else
        {
            Problem("files", "must list at least one file");
        }

        var bus = (Child(fields, "bus") as YamlScalarNode)?.Value ?? string.Empty;
        if (!Buses.Contains(bus))
        {
            Problem("bus", $"must be one of {string.Join(", ", Buses)}");
        }

        var volume = Number("volume", 0f);
        var pitchMin = 1f;
        var pitchMax = 1f;
        if (Child(fields, "pitch") is { } pitch)
        {
            if (pitch is YamlSequenceNode { Children.Count: 2 } spread && TryNumber(spread.Children[0], out var low)
                && TryNumber(spread.Children[1], out var high) && low > 0f && low <= high)
            {
                pitchMin = low;
                pitchMax = high;
            }
            else
            {
                Problem("pitch", "must be two numbers above zero, the lower first");
            }
        }

        var priority = Whole("priority", null, 0, MaxPriority);
        var maxConcurrent = Whole("maxConcurrent", null, 1, int.MaxValue);
        var cooldownMs = Whole("cooldownMs", 0, 0, int.MaxValue);
        var positional = Flag("positional", false);

        return valid
            ? new SoundDefinition(name, files, bus, volume, pitchMin, pitchMax, priority, maxConcurrent, cooldownMs, positional)
            : null;

        float Number(string field, float fallback)
        {
            var node = Child(fields, field);
            if (node is null)
            {
                return fallback;
            }

            if (TryNumber(node, out var value))
            {
                return value;
            }

            Problem(field, "must be a number");
            return fallback;
        }

        int Whole(string field, int? fallback, int min, int max)
        {
            var node = Child(fields, field);
            if (node is null)
            {
                if (fallback is null)
                {
                    Problem(field, "is missing");
                }

                return fallback ?? min;
            }

            if (node is YamlScalarNode { Value: { } text } && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value >= min && value <= max)
            {
                return value;
            }

            Problem(field, $"must be a whole number from {min} to {max}");
            return fallback ?? min;
        }

        bool Flag(string field, bool fallback)
        {
            var node = Child(fields, field);
            if (node is null)
            {
                return fallback;
            }

            if (node is YamlScalarNode { Value: { } text } && bool.TryParse(text, out var value))
            {
                return value;
            }

            Problem(field, "must be true or false");
            return fallback;
        }
    }

    private static YamlNode? Child(YamlMappingNode node, string key)
    {
        foreach (var pair in node.Children)
        {
            if (pair.Key is YamlScalarNode { Value: var text } && text == key)
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static bool TryNumber(YamlNode node, out float value)
    {
        value = 0f;
        return node is YamlScalarNode { Value: { } text } && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
