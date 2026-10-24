using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Models;

/// <summary>
/// One model of the table (docs/art/model_standards.md): what it draws (<see cref="Key"/>: <c>building/&lt;id&gt;</c>,
/// <c>unit/&lt;id&gt;</c>, <c>object/&lt;id&gt;</c>, <c>tree/&lt;shape&gt;</c>, <c>ore/&lt;kind&gt;</c>), its files under
/// <c>res://assets/models/</c> — one, or several kinds of it (five cacti) — and how it sits: scaled by <see cref="Scale"/>, turned by
/// <see cref="Yaw"/> degrees about the vertical, moved by <see cref="Offset"/> cells. <see cref="Jitter"/> varies the size of each
/// one by up to that share either way, <see cref="Spin"/> turns each one any way. <see cref="Height"/>, when above 0, is how high it
/// stands for the bars over it; <see cref="Climates"/>, when not empty, the climates it is drawn in (a snowdrift only in the snow).
/// </summary>
public sealed record ModelEntry(
    string Key, ImmutableArray<string> Files, float Scale, float Yaw, (float X, float Y, float Z) Offset, float Height, ImmutableArray<string> Climates,
    float Jitter = 0f, bool Spin = false)
{
    /// <summary>The first file: the one there is when there is one.</summary>
    public string File => Files[0];

    /// <summary>Whether it is drawn on a map of <paramref name="climate"/>.</summary>
    public bool DrawnIn(string climate) => Climates.IsDefaultOrEmpty || Climates.Contains(climate);

    /// <summary>
    /// The one standing on cell (<paramref name="x"/>, <paramref name="y"/>) out of <paramref name="count"/> files there: which file,
    /// how big, how turned. By the cell alone — the same tree is the same cactus in every session and replay, neighbours differ — and
    /// for the look only: the rules never see it.
    /// </summary>
    public ModelPick Pick(int x, int y, int count)
    {
        var hash = Mix(unchecked(((uint)x * 73856093u) ^ ((uint)y * 19349663u) ^ 0x9e3779b9u));
        var index = count <= 1 ? 0 : (int)(hash % (uint)count);
        var size = Jitter <= 0f ? Scale : Scale * (1f + Jitter * ((Mix(hash ^ 0x85ebca6bu) % 2001u / 1000f) - 1f));
        var yaw = Spin ? Yaw + (Mix(hash ^ 0xc2b2ae35u) % 360u) : Yaw;
        return new ModelPick(index, size, yaw);
    }

    // The finaliser of MurmurHash3: every bit of the input moves every bit of the output.
    private static uint Mix(uint h)
    {
        h ^= h >> 16;
        h = unchecked(h * 0x85ebca6bu);
        h ^= h >> 13;
        h = unchecked(h * 0xc2b2ae35u);
        h ^= h >> 16;
        return h;
    }
}

/// <summary>Which file of a <see cref="ModelEntry"/> stands on a cell, and its scale and its yaw in degrees.</summary>
public readonly record struct ModelPick(int Index, float Scale, float Yaw);

/// <summary>A table of models that cannot be read; <see cref="Line"/> and <see cref="Column"/> are 1-based.</summary>
public sealed class ModelTableException(int line, int column, string message) : Exception($"{line}:{column}: {message}")
{
    public int Line { get; } = line;

    public int Column { get; } = column;
}

/// <summary>
/// The table of models, <c>game/data/visuals/models.yaml</c>: every object of the game that has a model, and the file and the
/// placement of it. An object not in the table — or whose file is not there — is drawn with its placeholder. Read through the YAML
/// representation model, as the maps and the climates are: unknown keys are errors, each error names its line and column.
/// </summary>
public static class ModelTable
{
    public const string FormatVersion = "models/1";

    /// <summary>The most files — kinds of one model — an entry may list.</summary>
    public const int MaxFiles = 64;

    /// <summary>The kinds of keys: what the first part of a key may be.</summary>
    public static readonly ImmutableArray<string> Kinds = ["building", "unit", "object", "tree", "ore"];

    private static readonly Regex KeyPattern = new("^(building|unit|object|tree|ore)/[a-z0-9_]+$", RegexOptions.CultureInvariant);
    private static readonly Regex FilePattern = new("^[a-z0-9_]+(/[a-z0-9_]+)*\\.glb$", RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, ModelEntry> Read(string text)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text.Replace("\r\n", "\n")));
        }
        catch (YamlException e)
        {
            throw new ModelTableException((int)e.Start.Line, (int)e.Start.Column, $"not valid YAML: {e.Message}");
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ModelTableException(1, 1, "the table of models is one YAML mapping");
        }

        var values = Entries(root, ["format", "models"], "the table");
        if (!values.TryGetValue("format", out var format) || Text(format) != FormatVersion)
        {
            throw Error(values.GetValueOrDefault("format") ?? root, $"the format must be '{FormatVersion}'");
        }

        var table = new Dictionary<string, ModelEntry>(StringComparer.Ordinal);
        if (!values.TryGetValue("models", out var models) || models is YamlScalarNode { Value: null or "" })
        {
            return table;
        }

        if (models is not YamlMappingNode map)
        {
            throw Error(models, "models must be a mapping of keys to models");
        }

        foreach (var pair in map)
        {
            var key = Text(pair.Key);
            if (!KeyPattern.IsMatch(key))
            {
                throw Error(pair.Key, $"'{key}' is not a key such as building/<id>, unit/<id>, object/<id>, tree/<shape> or ore/<kind>");
            }

            if (table.ContainsKey(key))
            {
                throw Error(pair.Key, $"'{key}' repeats");
            }

            table[key] = Entry(key, pair.Value);
        }

        return table;
    }

    private static ModelEntry Entry(string key, YamlNode node)
    {
        if (node is not YamlMappingNode map)
        {
            throw Error(node, $"the model of '{key}' must be a mapping with a file");
        }

        var values = Entries(map, ["file", "files", "scale", "yaw", "offset", "height", "climates", "jitter", "spin"], $"the model of '{key}'");
        var files = ImmutableArray.CreateBuilder<YamlNode>();
        if (values.TryGetValue("file", out var fileNode) == values.TryGetValue("files", out var filesNode))
        {
            throw Error(node, $"the model of '{key}' needs a file, or files: a list of its kinds — one of the two");
        }

        if (fileNode is not null)
        {
            files.Add(fileNode);
        }
        else if (filesNode is YamlSequenceNode { Children.Count: > 0 and <= MaxFiles } list)
        {
            files.AddRange(list.Children);
        }
        else
        {
            throw Error(filesNode!, $"files must be a list of 1…{MaxFiles} .glb paths");
        }

        foreach (var one in files)
        {
            var file = Text(one);
            if (!FilePattern.IsMatch(file))
            {
                throw Error(one, $"'{file}' is not a .glb path under assets/models in lower case, such as map/ore_source/ore_source.glb");
            }
        }

        var scale = values.TryGetValue("scale", out var scaleNode) ? Number(scaleNode) : 1f;
        if (scale is < 0.01f or > 100f)
        {
            throw Error(scaleNode!, "scale must be 0.01…100");
        }

        var yaw = values.TryGetValue("yaw", out var yawNode) ? Number(yawNode) : 0f;
        if (yaw is < -360f or > 360f)
        {
            throw Error(yawNode!, "yaw must be -360…360 degrees");
        }

        var offset = (0f, 0f, 0f);
        if (values.TryGetValue("offset", out var offsetNode))
        {
            if (offsetNode is not YamlSequenceNode { Children.Count: 3 } xyz)
            {
                throw Error(offsetNode, "offset must be [x, y, z] in cells");
            }

            offset = (Number(xyz[0]), Number(xyz[1]), Number(xyz[2]));
        }

        var height = values.TryGetValue("height", out var heightNode) ? Number(heightNode) : 0f;
        if (height is < 0f or > 20f)
        {
            throw Error(heightNode!, "height must be 0…20 cells");
        }

        var climates = ImmutableArray<string>.Empty;
        if (values.TryGetValue("climates", out var climatesNode))
        {
            if (climatesNode is not YamlSequenceNode list || list.Children.Count == 0)
            {
                throw Error(climatesNode, "climates must be a list such as [winter]");
            }

            climates = [.. list.Children.Select(Text)];
        }

        var jitter = values.TryGetValue("jitter", out var jitterNode) ? Number(jitterNode) : 0f;
        if (jitter is < 0f or > 0.5f)
        {
            throw Error(jitterNode!, "jitter must be 0…0.5: the share the size varies either way");
        }

        var spin = values.TryGetValue("spin", out var spinNode) && Text(spinNode) switch
        {
            "true" => true,
            "false" => false,
            var other => throw Error(spinNode, $"spin must be true or false, not '{other}'"),
        };

        return new ModelEntry(key, [.. files.Select(Text)], scale, yaw, offset, height, climates, jitter, spin);
    }

    private static Dictionary<string, YamlNode> Entries(YamlMappingNode map, string[] allowed, string what)
    {
        var values = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var pair in map)
        {
            var name = Text(pair.Key);
            if (Array.IndexOf(allowed, name) < 0)
            {
                throw Error(pair.Key, $"unknown key '{name}' in {what}; the keys are {string.Join(", ", allowed)}");
            }

            values[name] = pair.Value;
        }

        return values;
    }

    private static string Text(YamlNode node) =>
        node is YamlScalarNode scalar ? scalar.Value ?? "" : throw Error(node, "a single value is expected here");

    private static float Number(YamlNode node) =>
        float.TryParse(Text(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value)
            ? value
            : throw Error(node, $"'{Text(node)}' is not a number");

    private static ModelTableException Error(YamlNode node, string message) =>
        new((int)node.Start.Line, (int)node.Start.Column, message);
}
