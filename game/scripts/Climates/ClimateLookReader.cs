using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Rts.Data.Maps;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Climates;

/// <summary>A climate file that cannot be read; <see cref="Line"/> and <see cref="Column"/> are 1-based.</summary>
public sealed class ClimateFormatException(int line, int column, string message) : Exception($"{line}:{column}: {message}")
{
    public int Line { get; } = line;

    public int Column { get; } = column;

    public string Reason { get; } = message;
}

/// <summary>
/// Reads a climate's <c>look.yaml</c> (<see cref="ClimateLook"/>) the way <see cref="RtsMapReader"/> reads a map: through the YAML
/// representation model, unknown keys are errors, every error names its line and column. Numbers may have decimals, colours
/// are <c>"#rrggbb"</c>.
/// </summary>
public static class ClimateLookReader
{
    public static ClimateLook Read(string text)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text.Replace("\r\n", "\n")));
        }
        catch (YamlException e)
        {
            throw new ClimateFormatException((int)e.Start.Line, (int)e.Start.Column, $"not valid YAML: {e.Message}");
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ClimateFormatException(1, 1, "a climate is one YAML mapping");
        }

        var values = Entries(root, ["format", "id", "materials", "cliff", "water_normal", "deep_water", "lighting", "trees", "stones"], "the climate");
        var format = Text(Required(values, "format", root));
        if (format != ClimateLook.FormatVersion)
        {
            throw Error(values["format"], $"unknown format '{format}', expected '{ClimateLook.FormatVersion}'");
        }

        var id = Id(Required(values, "id", root));
        var materialsNode = Required(values, "materials", root);
        if (materialsNode is not YamlMappingNode materialMap || materialMap.Children.Count is 0 or > ClimateLook.MaxMaterials)
        {
            throw Error(materialsNode, $"materials must be a mapping of 1…{ClimateLook.MaxMaterials} materials");
        }

        var materials = ImmutableArray.CreateBuilder<ClimateMaterial>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in materialMap)
        {
            var material = Id(pair.Key);
            if (!seen.Add(material))
            {
                throw Error(pair.Key, $"material '{material}' repeats");
            }

            materials.Add(Material(material, pair.Value));
        }

        var cliffNode = Required(values, "cliff", root);
        var cliff = Id(cliffNode);
        if (!seen.Contains(cliff))
        {
            throw Error(cliffNode, $"the cliff material '{cliff}' is none of the materials");
        }

        var lightingNode = Required(values, "lighting", root);
        var lightingEntries = Entries(lightingNode, [.. MapLighting.Ids], "lighting");
        var lighting = ImmutableArray.CreateBuilder<LightPreset>();
        foreach (var preset in MapLighting.Ids)
        {
            lighting.Add(Light(preset, Required(lightingEntries, preset, lightingNode)));
        }

        return new ClimateLook(
            id,
            materials.ToImmutable(),
            cliff,
            File(Required(values, "water_normal", root)),
            Color(Required(values, "deep_water", root)),
            lighting.ToImmutable(),
            values.TryGetValue("trees", out var trees) ? TreesOf(trees) : TreeKind.Pine,
            values.TryGetValue("stones", out var stones) ? Number(stones, 0f, 0.2f, "stones") : 0f);
    }

    private static TreeKind TreesOf(YamlNode node) => Text(node) switch
    {
        "pine" => TreeKind.Pine,
        "broadleaf" => TreeKind.Broadleaf,
        "desert" => TreeKind.Desert,
        "coast" => TreeKind.Coast,
        var other => throw Error(node, $"trees '{other}' is not pine, broadleaf, desert or coast"),
    };

    private static ClimateMaterial Material(string id, YamlNode node)
    {
        var fields = Entries(node, ["albedo", "tiling", "roughness", "tint", "frost", "water", "color", "patches", "patches_share"], $"material '{id}'");
        var color = Optional(fields, "color") is { } painted ? Color(painted) : (LookColor?)null;
        if (color is null && (Optional(fields, "patches") ?? Optional(fields, "patches_share")) is { } lonely)
        {
            throw Error(lonely, "patches need a color: they are a second colour of a painted ground");
        }

        return new ClimateMaterial(
            id,
            File(Required(fields, "albedo", node)),
            Number(Required(fields, "tiling", node), 0.5f, 64f, "tiling"),
            Optional(fields, "roughness") is { } roughness ? Number(roughness, 0f, 1f, "roughness") : 0.9f,
            Optional(fields, "tint") is { } tint ? Color(tint) : LookColor.White,
            Optional(fields, "frost") is { } frost ? Number(frost, 0f, 1f, "frost") : 0f,
            Optional(fields, "water") is { } water && Bool(water),
            color,
            Optional(fields, "patches") is { } patches ? Color(patches) : color,
            Optional(fields, "patches_share") is { } share ? Number(share, 0f, 1f, "patches_share") : 0.5f);
    }

    private static LightPreset Light(string id, YamlNode node)
    {
        var fields = Entries(node, ["sun", "ambient", "background", "fog", "exposure", "saturation"], $"lighting '{id}'");
        var sunNode = Required(fields, "sun", node);
        var sun = Entries(sunNode, ["azimuth", "elevation", "color", "energy"], "the sun");
        var ambientNode = Required(fields, "ambient", node);
        var ambient = Entries(ambientNode, ["color", "energy"], "the ambient light");
        var fogNode = Required(fields, "fog", node);
        var fog = Entries(fogNode, ["color", "density"], "the fog");
        return new LightPreset(
            id,
            Number(Required(sun, "azimuth", sunNode), -360f, 360f, "azimuth"),
            Number(Required(sun, "elevation", sunNode), 1f, 89f, "elevation"),
            Color(Required(sun, "color", sunNode)),
            Number(Required(sun, "energy", sunNode), 0f, 8f, "energy"),
            Color(Required(ambient, "color", ambientNode)),
            Number(Required(ambient, "energy", ambientNode), 0f, 8f, "energy"),
            Color(Required(fields, "background", node)),
            Color(Required(fog, "color", fogNode)),
            Number(Required(fog, "density", fogNode), 0f, 1f, "density"),
            Optional(fields, "exposure") is { } exposure ? Number(exposure, 0.1f, 4f, "exposure") : 1f,
            Optional(fields, "saturation") is { } saturation ? Number(saturation, 0f, 2f, "saturation") : 1f);
    }

    /// <summary>The entries of a mapping by key; unknown and repeated keys are errors at the key.</summary>
    private static SortedDictionary<string, YamlNode> Entries(YamlNode node, string[] allowed, string what)
    {
        if (node is not YamlMappingNode mapping)
        {
            throw Error(node, $"{what} must be a mapping");
        }

        var entries = new SortedDictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var pair in mapping)
        {
            var key = Text(pair.Key);
            if (Array.IndexOf(allowed, key) < 0)
            {
                throw Error(pair.Key, $"unknown key '{key}' in {what}");
            }

            if (!entries.TryAdd(key, pair.Value))
            {
                throw Error(pair.Key, $"key '{key}' repeats in {what}");
            }
        }

        return entries;
    }

    private static YamlNode Required(SortedDictionary<string, YamlNode> entries, string key, YamlNode owner) =>
        entries.TryGetValue(key, out var node) ? node : throw Error(owner, $"missing key '{key}'");

    private static YamlNode? Optional(SortedDictionary<string, YamlNode> entries, string key) =>
        entries.TryGetValue(key, out var node) ? node : null;

    private static string Id(YamlNode node)
    {
        var text = Text(node);
        return MapNames.IsId(text) ? text : throw Error(node, $"'{text}' must be lowercase letters, digits, '-' or '_'");
    }

    /// <summary>A file name in the folder of the terrain textures: no folders, no way out of it.</summary>
    private static string File(YamlNode node)
    {
        var text = Text(node);
        return text.Length > 0 && text.IndexOfAny(['/', '\\']) < 0 && text != ".." && text.EndsWith(".png", StringComparison.Ordinal)
            ? text
            : throw Error(node, $"'{text}' must be a .png file name without folders");
    }

    private static float Number(YamlNode node, float min, float max, string what)
    {
        var text = Text(node);
        if (!float.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw Error(node, $"{what} '{text}' is not a number");
        }

        return value >= min && value <= max ? value : throw Error(node, $"{what} {text} is outside {min}…{max}");
    }

    private static bool Bool(YamlNode node) => Text(node) switch
    {
        "true" => true,
        "false" => false,
        var text => throw Error(node, $"'{text}' must be true or false"),
    };

    private static LookColor Color(YamlNode node)
    {
        var text = Text(node);
        if (text.Length != 7 || text[0] != '#'
            || !int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            throw Error(node, $"colour '{text}' must be \"#rrggbb\"");
        }

        return new LookColor(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }

    private static string Text(YamlNode node) =>
        node is YamlScalarNode scalar ? scalar.Value ?? "" : throw Error(node, "expected a single value");

    private static ClimateFormatException Error(YamlNode node, string message) => new((int)node.Start.Line, (int)node.Start.Column, message);
}
