using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Audio;

/// <summary>The sounds of a bridge that falls: <paramref name="Span"/> for one segment in a tick, <paramref name="Whole"/> for more.</summary>
public sealed record BridgeSounds(string Span, string Whole);

/// <summary>
/// Which sound plays for what (<c>game/data/audio/bindings.yaml</c>): the shot of each weapon, the fall of each building and of each
/// bridge, the loop of each flier (its rotor, its engine), all by their ids in the rules. Only the client reads it.
/// </summary>
public sealed record SoundBindings(
    IReadOnlyDictionary<string, string> Weapons,
    IReadOnlyDictionary<string, string> Buildings,
    string BuildingDefault,
    IReadOnlyDictionary<string, BridgeSounds> Bridges,
    IReadOnlyDictionary<string, string> Movement)
{
    /// <summary>What a weapon not in the file fires.</summary>
    public const string ShotDefault = "snd_shot";

    /// <summary>The key of <c>buildings</c> for every building not named there.</summary>
    public const string DefaultKey = "default";

    private static readonly string[] Sections = ["weapons", "buildings", "bridges", "movement"];

    public static readonly SoundBindings Empty = new(new Dictionary<string, string>(), new Dictionary<string, string>(), string.Empty,
        new Dictionary<string, BridgeSounds>(), new Dictionary<string, string>());

    /// <summary>The sound of a shot of the weapon <paramref name="id"/>.</summary>
    public string ShotOf(string id) => Weapons.TryGetValue(id, out var sound) ? sound : ShotDefault;

    /// <summary>The sound of the fall of the building <paramref name="id"/>; empty when there is none.</summary>
    public string FallOf(string id) => Buildings.TryGetValue(id, out var sound) ? sound : BuildingDefault;

    /// <summary>
    /// Reads <paramref name="text"/>. Every mistake goes to <paramref name="errors"/> with its section and its key, and only that line is
    /// left out, so the rest still sounds. Pure, so the tests read the real file.
    /// </summary>
    public static SoundBindings Parse(string text, string fileName, List<string> errors)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException error)
        {
            errors.Add($"{fileName}: {error.Message}");
            return Empty;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            errors.Add($"{fileName}: the file is not a table of {string.Join(", ", Sections)}");
            return Empty;
        }

        foreach (var pair in root.Children)
        {
            if (pair.Key is not YamlScalarNode { Value: { } section } || System.Array.IndexOf(Sections, section) < 0)
            {
                errors.Add($"{fileName}: {(pair.Key as YamlScalarNode)?.Value ?? "?"} is not a section; the sections are {string.Join(", ", Sections)}");
            }
        }

        var weapons = Names(root, "weapons", fileName, errors);
        var buildings = Names(root, "buildings", fileName, errors);
        var buildingDefault = buildings.Remove(DefaultKey, out var fallback) ? fallback : string.Empty;
        var bridges = new Dictionary<string, BridgeSounds>();
        if (Child(root, "bridges") is { } bridgeNode)
        {
            if (bridgeNode is not YamlMappingNode table)
            {
                errors.Add($"{fileName}: bridges must be a table of ids");
            }
            else
            {
                foreach (var pair in table.Children)
                {
                    var id = (pair.Key as YamlScalarNode)?.Value ?? "?";
                    if (pair.Value is YamlMappingNode fields && fields.Children.Count == 2
                        && (Child(fields, "span") as YamlScalarNode)?.Value is { Length: > 0 } span
                        && (Child(fields, "whole") as YamlScalarNode)?.Value is { Length: > 0 } whole)
                    {
                        bridges[id] = new BridgeSounds(span, whole);
                    }
                    else
                    {
                        errors.Add($"{fileName}: bridges.{id} must be {{span: <sound>, whole: <sound>}}");
                    }
                }
            }
        }

        return new SoundBindings(weapons, buildings, buildingDefault, bridges, Names(root, "movement", fileName, errors));
    }

    /// <summary>A section of ids, each with the name of a sound.</summary>
    private static Dictionary<string, string> Names(YamlMappingNode root, string section, string fileName, List<string> errors)
    {
        var names = new Dictionary<string, string>();
        if (Child(root, section) is not { } node)
        {
            return names;
        }

        if (node is not YamlMappingNode table)
        {
            errors.Add($"{fileName}: {section} must be a table of ids");
            return names;
        }

        foreach (var pair in table.Children)
        {
            if (pair.Key is YamlScalarNode { Value: { Length: > 0 } id } && pair.Value is YamlScalarNode { Value: { Length: > 0 } sound })
            {
                names[id] = sound;
            }
            else
            {
                errors.Add($"{fileName}: {section}.{(pair.Key as YamlScalarNode)?.Value ?? "?"} must be the name of a sound");
            }
        }

        return names;
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
}
