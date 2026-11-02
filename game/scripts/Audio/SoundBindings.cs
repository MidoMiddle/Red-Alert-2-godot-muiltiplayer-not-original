using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Audio;

/// <summary>The sounds of a bridge that falls: <paramref name="Span"/> for one segment in a tick, <paramref name="Whole"/> for more.</summary>
public sealed record BridgeSounds(string Span, string Whole);

/// <summary>What a unit says when it is selected, sent somewhere, sent to attack; empty when it has nothing to say to it.</summary>
public sealed record VoiceSounds(string Select, string Move, string Attack);

/// <summary>The sounds of a unit deploying where it stands (<paramref name="On"/>: the Rifleman digging in) and getting up again.</summary>
public sealed record DeploySounds(string On, string Off);

/// <summary>
/// Which sound plays for what (<c>game/data/audio/bindings.yaml</c>): the shot of each weapon, the fall of each building and of each
/// bridge, the loop of each unit on the move (a rotor, an engine, footsteps), the death of each unit, what each says and how it deploys,
/// all by their ids in the rules. Only the client reads it.
/// </summary>
public sealed record SoundBindings(
    IReadOnlyDictionary<string, string> Weapons,
    IReadOnlyDictionary<string, string> Buildings,
    string BuildingDefault,
    IReadOnlyDictionary<string, BridgeSounds> Bridges,
    IReadOnlyDictionary<string, string> Movement,
    IReadOnlyDictionary<string, string> Deaths,
    string DeathDefault,
    IReadOnlyDictionary<string, VoiceSounds> Voices,
    IReadOnlyDictionary<string, DeploySounds> Deploys)
{
    /// <summary>What a weapon not in the file fires.</summary>
    public const string ShotDefault = "snd_shot";

    /// <summary>The key of <c>buildings</c> and <c>deaths</c> for every building or unit not named there.</summary>
    public const string DefaultKey = "default";

    private static readonly string[] Sections = ["weapons", "buildings", "bridges", "movement", "deaths", "voices", "deploys"];

    public static readonly SoundBindings Empty = new(new Dictionary<string, string>(), new Dictionary<string, string>(), string.Empty,
        new Dictionary<string, BridgeSounds>(), new Dictionary<string, string>(), new Dictionary<string, string>(), string.Empty,
        new Dictionary<string, VoiceSounds>(), new Dictionary<string, DeploySounds>());

    /// <summary>The sound of a shot of the weapon <paramref name="id"/>.</summary>
    public string ShotOf(string id) => Weapons.TryGetValue(id, out var sound) ? sound : ShotDefault;

    /// <summary>The sound of the fall of the building <paramref name="id"/>; empty when there is none.</summary>
    public string FallOf(string id) => Buildings.TryGetValue(id, out var sound) ? sound : BuildingDefault;

    /// <summary>The sound of the death of the unit <paramref name="id"/>; empty when there is none.</summary>
    public string DeathOf(string id) => Deaths.TryGetValue(id, out var sound) ? sound : DeathDefault;

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

        var deaths = Names(root, "deaths", fileName, errors);
        var deathDefault = deaths.Remove(DefaultKey, out var fallen) ? fallen : string.Empty;
        var voices = Tables(root, "voices", ["select", "move", "attack"], fileName, errors)
            .ToDictionary(pair => pair.Key, pair => new VoiceSounds(pair.Value["select"], pair.Value["move"], pair.Value["attack"]));
        var deploys = Tables(root, "deploys", ["on", "off"], fileName, errors)
            .ToDictionary(pair => pair.Key, pair => new DeploySounds(pair.Value["on"], pair.Value["off"]));
        return new SoundBindings(
            weapons, buildings, buildingDefault, bridges, Names(root, "movement", fileName, errors), deaths, deathDefault, voices, deploys);
    }

    /// <summary>A section of ids, each with a table of some of <paramref name="keys"/>, each the name of a sound; a key left out is empty.</summary>
    private static Dictionary<string, Dictionary<string, string>> Tables(
        YamlMappingNode root, string section, string[] keys, string fileName, List<string> errors)
    {
        var tables = new Dictionary<string, Dictionary<string, string>>();
        if (Child(root, section) is not { } node)
        {
            return tables;
        }

        if (node is not YamlMappingNode table)
        {
            errors.Add($"{fileName}: {section} must be a table of ids");
            return tables;
        }

        foreach (var pair in table.Children)
        {
            var id = (pair.Key as YamlScalarNode)?.Value ?? "?";
            if (pair.Value is YamlMappingNode fields && fields.Children.Count > 0
                && fields.Children.All(field => keys.Contains((field.Key as YamlScalarNode)?.Value) && field.Value is YamlScalarNode))
            {
                tables[id] = keys.ToDictionary(key => key, key => (Child(fields, key) as YamlScalarNode)?.Value ?? string.Empty);
            }
            else
            {
                errors.Add($"{fileName}: {section}.{id} must be {{{string.Join(", ", keys.Select(key => $"{key}: <sound>"))}}}, one or more of them");
            }
        }

        return tables;
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
