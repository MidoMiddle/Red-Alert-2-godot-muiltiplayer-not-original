using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Client.Units;

/// <summary>One clip of the people: whether it loops, its length in seconds and, for a walk or a run, how far it carries in a second.</summary>
public sealed record HumanClip(string Name, bool Loop, float Length, float? Stride);

/// <summary>A table of clips that cannot be read; <see cref="Line"/> and <see cref="Column"/> are 1-based.</summary>
public sealed class HumanClipTableException(int line, int column, string message) : Exception($"{line}:{column}: {message}")
{
    public int Line { get; } = line;

    public int Column { get; } = column;
}

/// <summary>
/// The table of the people's clips, <c>game/data/visuals/human_clips.yaml</c> (docs/art/mixamo.md), written by the Blender script that
/// makes the clips (tools/blender/units/civilians.py): every clip of <c>assets/models/unit/human_clips</c>, and the clips only some
/// people take (the catwalk is the women's). A clip's family is its name before the last <c>_</c> part: <c>idle_0</c>…<c>idle_4</c> are
/// the idles, <c>walk_woman_0</c> a walk. Read through the YAML representation model; unknown keys are errors.
/// </summary>
public sealed class HumanClipTable
{
    private HumanClipTable(ImmutableDictionary<string, HumanClip> clips, ImmutableDictionary<string, ImmutableArray<string>> only)
    {
        Clips = clips;
        Only = only;
    }

    public ImmutableDictionary<string, HumanClip> Clips { get; }

    /// <summary>The clips only these people take (unit ids); a clip not here is everyone's.</summary>
    public ImmutableDictionary<string, ImmutableArray<string>> Only { get; }

    /// <summary>The clips of a family (<c>idle</c>, <c>walk</c>, <c>run</c>, <c>die</c>) the unit <paramref name="unitId"/> takes, by name.</summary>
    public ImmutableArray<HumanClip> Family(string family, string unitId) =>
        [.. Clips.Values
            .Where(c => c.Name.StartsWith(family + "_", StringComparison.Ordinal) && EndsInNumber(c.Name))
            .Where(c => !Only.TryGetValue(c.Name, out var who) || who.Contains(unitId))
            .OrderBy(c => c.Name, StringComparer.Ordinal)];

    // idle_0 and walk_woman_0 are of their families; crouch_down is no crouch of a family "crouch" but a clip of its own.
    private static bool EndsInNumber(string name)
    {
        var last = name[(name.LastIndexOf('_') + 1)..];
        return last.Length > 0 && last.All(char.IsAsciiDigit);
    }

    public static HumanClipTable Read(string text)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text.Replace("\r\n", "\n")));
        }
        catch (YamlException e)
        {
            throw new HumanClipTableException((int)e.Start.Line, (int)e.Start.Column, $"not valid YAML: {e.Message}");
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new HumanClipTableException(1, 1, "the table of clips is one YAML mapping");
        }

        var values = Entries(root, ["clips", "only"], "the table");
        if (!values.TryGetValue("clips", out var clipsNode) || clipsNode is not YamlMappingNode clipsMap)
        {
            throw Error(clipsNode ?? root, "clips must be a mapping of names to clips");
        }

        var clips = ImmutableDictionary.CreateBuilder<string, HumanClip>(StringComparer.Ordinal);
        foreach (var pair in clipsMap)
        {
            var name = Text(pair.Key);
            if (clips.ContainsKey(name))
            {
                throw Error(pair.Key, $"'{name}' repeats");
            }

            if (pair.Value is not YamlMappingNode clip)
            {
                throw Error(pair.Value, $"{name}: a clip is a mapping of loop, length and stride");
            }

            var fields = Entries(clip, ["loop", "length", "stride"], name);
            var loop = fields.TryGetValue("loop", out var l) ? Text(l) switch
            {
                "true" => true,
                "false" => false,
                _ => throw Error(l, $"{name}: loop is true or false"),
            } : throw Error(clip, $"{name}: no loop");
            var length = fields.TryGetValue("length", out var len) ? Positive(len, name, "length") : throw Error(clip, $"{name}: no length");
            float? stride = fields.TryGetValue("stride", out var s) ? Positive(s, name, "stride") : null;
            clips.Add(name, new HumanClip(name, loop, length, stride));
        }

        var only = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(StringComparer.Ordinal);
        if (values.TryGetValue("only", out var onlyNode) && onlyNode is not YamlScalarNode { Value: null or "" })
        {
            if (onlyNode is not YamlMappingNode onlyMap)
            {
                throw Error(onlyNode, "only must be a mapping of clips to lists of unit ids");
            }

            foreach (var pair in onlyMap)
            {
                var name = Text(pair.Key);
                if (!clips.ContainsKey(name))
                {
                    throw Error(pair.Key, $"only: '{name}' is no clip of the table");
                }

                if (pair.Value is not YamlSequenceNode ids)
                {
                    throw Error(pair.Value, $"only: {name} takes a list of unit ids");
                }

                only.Add(name, [.. ids.Select(Text)]);
            }
        }

        return new HumanClipTable(clips.ToImmutable(), only.ToImmutable());
    }

    private static float Positive(YamlNode node, string name, string field) =>
        float.TryParse(Text(node), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0f
            ? value
            : throw Error(node, $"{name}: {field} is a number above 0");

    private static Dictionary<string, YamlNode> Entries(YamlMappingNode map, string[] known, string what)
    {
        var entries = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var pair in map)
        {
            var key = Text(pair.Key);
            if (!known.Contains(key))
            {
                throw Error(pair.Key, $"{what}: unknown key '{key}' (known: {string.Join(", ", known)})");
            }

            entries[key] = pair.Value;
        }

        return entries;
    }

    private static string Text(YamlNode node) => node is YamlScalarNode { Value: { } value } ? value : "";

    private static HumanClipTableException Error(YamlNode node, string message) =>
        new((int)node.Start.Line, (int)node.Start.Column, message);
}
