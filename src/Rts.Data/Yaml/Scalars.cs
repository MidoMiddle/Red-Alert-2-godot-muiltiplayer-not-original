using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// Converters of the scalars of data files (docs/implementation/M3_base_economy.md, 4.1, 4.2). Each takes the file name and a node, and
/// returns the value or throws a <see cref="RulesFormatException"/> at the node. Numbers go through <see cref="FixedParser"/>: YAML never
/// converts them, so <c>1e3</c> reaches the parser as the text it is.
/// </summary>
internal static class Scalars
{
    /// <summary>A text of one line, without control characters.</summary>
    public static string Text(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        foreach (var c in text)
        {
            if (char.IsControl(c))
            {
                throw RulesFormatException.At(fileName, node, "a text must be one line");
            }
        }

        return text;
    }

    /// <summary>A rule id in snake_case: a lowercase letter, then lowercase letters, digits and '_'.</summary>
    public static string Id(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        if (text.Length == 0 || !IsLower(text[0]) || !AllChars(text, c => IsLower(c) || IsDigit(c) || c == '_'))
        {
            throw RulesFormatException.At(fileName, node, $"'{text}' must be an id: lowercase letters, digits and '_', starting with a letter");
        }

        return text;
    }

    /// <summary>A path of an asset, relative to the assets folder: lowercase letters, digits, '_' and '/'.</summary>
    public static string Path(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        if (!AllChars(text, c => IsLower(c) || IsDigit(c) || c is '_' or '/'))
        {
            throw RulesFormatException.At(fileName, node, $"'{text}' must be a path: lowercase letters, digits, '_' and '/'");
        }

        return text;
    }

    /// <summary><c>true</c> or <c>false</c>, nothing else.</summary>
    public static bool Flag(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        return text switch
        {
            "true" => true,
            "false" => false,
            _ => throw RulesFormatException.At(fileName, node, $"'{text}' must be true or false"),
        };
    }

    /// <summary>A whole number, such as <c>30</c> or <c>-5</c>.</summary>
    public static int Int(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        return FixedParser.TryParseInt(text, out var value)
            ? value
            : throw RulesFormatException.At(fileName, node, $"'{text}' must be a whole number such as 30");
    }

    /// <summary>A number with fractions, such as <c>1.25</c>.</summary>
    public static Fixed FixedNumber(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        return FixedParser.TryParseFixed(text, out var value)
            ? value
            : throw RulesFormatException.At(fileName, node, $"'{text}' must be a number such as 1.25");
    }

    /// <summary>A percentage, such as <c>125%</c>, as a fraction: 1.25.</summary>
    public static Fixed Percent(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        return FixedParser.TryParsePercent(text, out var value)
            ? value
            : throw RulesFormatException.At(fileName, node, $"'{text}' must be a percentage such as 125%");
    }

    /// <summary>A duration in seconds, such as <c>2s</c> or <c>1.5s</c>, as ticks at the simulation rate.</summary>
    public static int Ticks(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        return FixedParser.TryParseTicks(text, SimulationSettings.DefaultTicksPerSecond, out var ticks)
            ? ticks
            : throw RulesFormatException.At(fileName, node, $"'{text}' must be a duration such as 2s");
    }

    /// <summary>A footprint, <c>[2, 2]</c>.</summary>
    public static CellSize Size(string fileName, YamlNode node)
    {
        var pair = Pair(fileName, node, "[width, height], such as [2, 2]");
        return new CellSize(Int(fileName, pair[0]), Int(fileName, pair[1]));
    }

    /// <summary>A cell, <c>[x, y]</c>.</summary>
    public static CellPos Cell(string fileName, YamlNode node)
    {
        var pair = Pair(fileName, node, "[x, y], such as [3, 4]");
        return new CellPos(Int(fileName, pair[0]), Int(fileName, pair[1]));
    }

    /// <summary>One printable character, such as <c>.</c> or <c>~</c>: the land code of a map file.</summary>
    public static char Code(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        if (text.Length != 1 || text[0] is <= ' ' or >= '\u007f')
        {
            throw RulesFormatException.At(fileName, node, $"'{text}' must be one printable character");
        }

        return text[0];
    }

    /// <summary>A list of ids, <c>[war_factory, reactor]</c>.</summary>
    public static ImmutableArray<string> IdList(string fileName, YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw RulesFormatException.At(fileName, node, "expected a list of ids such as [war_factory]");
        }

        var ids = ImmutableArray.CreateBuilder<string>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            ids.Add(Id(fileName, child));
        }

        return ids.MoveToImmutable();
    }

    /// <summary>The speed of each locomotor on a land type: a mapping such as <c>foot: 90%</c>, sorted by locomotor id.</summary>
    public static ImmutableArray<LocomotorSpeed> Speeds(string fileName, YamlNode node)
    {
        if (node is not YamlMappingNode mapping)
        {
            throw RulesFormatException.At(fileName, node, "expected a mapping from locomotors to percentages such as foot: 90%");
        }

        var speeds = new SortedDictionary<string, LocomotorSpeed>(StringComparer.Ordinal);
        foreach (var entry in mapping)
        {
            var locomotor = Id(fileName, entry.Key);
            if (!speeds.TryAdd(locomotor, new LocomotorSpeed(locomotor, Percent(fileName, entry.Value))))
            {
                throw RulesFormatException.At(fileName, entry.Key, $"locomotor '{locomotor}' repeats");
            }
        }

        return ImmutableArray.CreateRange(speeds.Values);
    }

    /// <summary>One of the given words, such as <c>infantry</c> or <c>vehicle</c>.</summary>
    public static string Choice(string fileName, YamlNode node, params string[] options)
    {
        var text = Scalar(fileName, node);
        if (Array.IndexOf(options, text) < 0)
        {
            throw RulesFormatException.At(fileName, node, $"'{text}' must be one of {string.Join(", ", options)}");
        }

        return text;
    }

    private static string Scalar(string fileName, YamlNode node) =>
        node is YamlScalarNode { Value: { } value } ? value : throw RulesFormatException.At(fileName, node, "expected a single value");

    private static YamlNode[] Pair(string fileName, YamlNode node, string expected)
    {
        if (node is not YamlSequenceNode { Children.Count: 2 } sequence)
        {
            throw RulesFormatException.At(fileName, node, $"expected {expected}");
        }

        return [sequence.Children[0], sequence.Children[1]];
    }

    private static bool IsLower(char c) => c is >= 'a' and <= 'z';

    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    /// <summary>True for a non-empty text whose every character is allowed.</summary>
    private static bool AllChars(string text, Func<char, bool> allowed)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!allowed(c))
            {
                return false;
            }
        }

        return true;
    }
}
