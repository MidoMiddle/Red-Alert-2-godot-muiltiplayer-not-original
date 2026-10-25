using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rts.Net.Desync;

/// <summary>One field two dumps disagree on: its path, and its value in each (null where the dump has no such line).</summary>
public sealed record DumpDifference(string Path, string? First, string? Second)
{
    /// <summary>What the field belongs to: <c>entity[12]</c>, <c>player[2]</c>, <c>map</c>, <c>world</c>.</summary>
    public string Owner => Path.IndexOfAny(['.', '[']) is var cut and > 0 && Path[cut] == '[' ? Path[..(Path.IndexOf(']') + 1)] : Path.Split('.')[0];

    /// <summary>The field within its owner: <c>Hp</c>, <c>Position</c>.</summary>
    public string Field => Path.Length > Owner.Length + 1 ? Path[(Owner.Length + 1)..] : Path;

    public override string ToString() => $"{Path}: {First ?? "(none)"} ≠ {Second ?? "(none)"}";
}

/// <summary>
/// Two dumps of one tick compared line by line (docs/implementation/M9_network.md, 4.10, step 6): the fields they disagree on, in
/// the order of the first dump, and what each holds there. The first one is where to look; the owners say which entities went apart.
/// </summary>
public static class DumpDiff
{
    /// <summary>The fields <paramref name="first"/> and <paramref name="second"/> disagree on, in the order of the first, then those only the second has.</summary>
    public static ImmutableArray<DumpDifference> Compare(string first, string second)
    {
        var a = Parse(first);
        var b = Parse(second);
        var lookup = b.ToDictionary(line => line.Path, line => line.Value, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var differences = ImmutableArray.CreateBuilder<DumpDifference>();
        foreach (var (path, value) in a)
        {
            seen.Add(path);
            if (!lookup.TryGetValue(path, out var other) || other != value)
            {
                differences.Add(new DumpDifference(path, value, lookup.TryGetValue(path, out var found) ? found : null));
            }
        }

        foreach (var (path, value) in b)
        {
            if (!seen.Contains(path))
            {
                differences.Add(new DumpDifference(path, null, value));
            }
        }

        return differences.ToImmutable();
    }

    /// <summary>The tick of a dump, from its first line.</summary>
    public static int? TickOf(string dump) =>
        Parse(dump).FirstOrDefault(line => line.Path == "tick") is { Value: { } value } && int.TryParse(value, out var tick) ? tick : null;

    /// <summary>A report for a person: the tick, how many fields, the first ones, and the entities and players they belong to.</summary>
    public static string Report(ImmutableArray<DumpDifference> differences, int shown = 20)
    {
        if (differences.IsEmpty)
        {
            return "The dumps are the same.";
        }

        var owners = differences.GroupBy(difference => difference.Owner).Select(group => $"{group.Key} ({group.Count()})");
        return $"{differences.Length} fields differ; the first is {differences[0]}.\n"
               + $"Of: {string.Join(", ", owners)}.\n"
               + string.Join('\n', differences.Take(shown).Select(difference => "  " + difference))
               + (differences.Length > shown ? $"\n  … and {differences.Length - shown} more." : "");
    }

    private static List<(string Path, string Value)> Parse(string dump)
    {
        var lines = new List<(string, string)>();
        foreach (var line in dump.Split('\n'))
        {
            var cut = line.IndexOf(" = ", StringComparison.Ordinal);
            if (cut > 0)
            {
                lines.Add((line[..cut], line[(cut + 3)..]));
            }
        }

        return lines;
    }
}
