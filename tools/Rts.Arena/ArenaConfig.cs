using System.Collections.Immutable;
using System.Globalization;
using Rts.Data.Maps;
using Rts.MapGen;
using Rts.Simulation.Rules;
using YamlDotNet.RepresentationModel;

namespace Rts.Arena;

/// <summary>A pair of the matrix: the bots of the sides A and B.</summary>
public sealed record ArenaPair(BotProfile A, BotProfile B);

/// <summary>Which replays a run keeps, and checks: the draws, the upsets — a lower level beat a higher one — the longest N, or all.</summary>
public sealed record ReplaySelection(bool All, bool Draws, bool Upsets, int Longest)
{
    public static readonly ReplaySelection None = new(false, false, false, 0);

    public static readonly ReplaySelection Every = new(true, false, false, 0);
}

/// <summary>
/// A map of the matrix: a ready map of the data by its id, or <c>random:size=96,players=2</c> — a map of <see cref="MapGenerator"/>, made
/// anew for every match with its seed, so that every match plays back; the map goes into the replay.
/// </summary>
public sealed record MapSpec(string Text, MapDefinition? Ready, MapGenSettings? Random)
{
    /// <summary>The map of a match with <paramref name="seed"/>.</summary>
    public MapDefinition For(ulong seed) => Ready ?? MapGenerator.Generate(Random! with { Seed = (uint)seed }).Map;

    /// <summary>The map as a column of a CSV: no commas.</summary>
    public string Label => Text.Replace(',', ';');
}

/// <summary>
/// A matrix of the Arena (docs/implementation/M8_ai_difficulty.md, 4.10, step 9), YAML with the keys of 4.10:
/// <code>
/// games: 50                       # matches for every pair on every map
/// swapSides: true                 # every second match the sides swap starts
/// maxGameTime: 60m                # then a draw
/// baseSeed: 1000                  # the seed of the first match, the next ones +1
/// maps: [frozen-crossing, twin-rivers, random:size=96,players=2]
/// pairs:
///   - { a: hard:balanced, b: medium:balanced }
/// saveReplays: [draws, longest:3, upsets]
/// </code>
/// A mistake is told with the file, the line and the column, as the mistakes of the data of the game are.
/// </summary>
public sealed record ArenaConfig(
    int Games, bool SwapSides, int MaxTicks, ulong BaseSeed, ImmutableArray<MapSpec> Maps, ImmutableArray<ArenaPair> Pairs, ReplaySelection Save)
{
    private static readonly string[] Keys = ["games", "swapSides", "maxGameTime", "baseSeed", "maps", "pairs", "saveReplays"];

    /// <summary>The matches of the matrix: for every pair, every map, <see cref="Games"/> of them.</summary>
    public int Total => Pairs.Length * Maps.Length * Games;

    /// <summary>Reads the matrix of <paramref name="fileName"/>; the maps from the <c>maps</c> folder of <paramref name="dataFolder"/>.</summary>
    public static ArenaConfig Read(string fileName, string text, RulesDatabase rules, string dataFolder)
    {
        YamlMappingNode root;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            root = stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode mapping
                ? mapping
                : throw new ArenaException($"{fileName}:1:1: a matrix is one YAML mapping");
        }
        catch (YamlDotNet.Core.YamlException e)
        {
            throw new ArenaException($"{fileName}:{e.Start.Line}:{e.Start.Column}: not valid YAML: {e.Message}");
        }

        var fields = new Dictionary<string, (YamlNode Key, YamlNode Value)>(StringComparer.Ordinal);
        foreach (var (key, value) in root.Children)
        {
            var name = ((YamlScalarNode)key).Value ?? "";
            if (!Keys.Contains(name))
            {
                throw Error(fileName, key, $"unknown key '{name}'; a matrix has {string.Join(", ", Keys)}");
            }

            fields[name] = (key, value);
        }

        YamlNode Required(string key) => fields.TryGetValue(key, out var field)
            ? field.Value
            : throw new ArenaException($"{fileName}:1:1: the matrix has no '{key}'");

        var games = fields.TryGetValue("games", out var g) ? Number(fileName, g.Value, "games") : 1;
        var swap = !fields.TryGetValue("swapSides", out var s) || Flag(fileName, s.Value);
        var maxTicks = fields.TryGetValue("maxGameTime", out var t) ? Duration(fileName, t.Value) : 60 * 900;
        var seed = fields.TryGetValue("baseSeed", out var b) ? (ulong)Number(fileName, b.Value, "baseSeed", allowZero: true) : 1000UL;
        var maps = ImmutableArray.CreateBuilder<MapSpec>();
        foreach (var (node, written) in MapList(fileName, Required("maps")))
        {
            maps.Add(Map(fileName, node, written, dataFolder));
        }

        var pairs = ImmutableArray.CreateBuilder<ArenaPair>();
        foreach (var node in List(fileName, Required("pairs"), "pairs"))
        {
            pairs.Add(Pair(fileName, node, rules));
        }

        var save = fields.TryGetValue("saveReplays", out var r) ? Selection(fileName, r.Value) : ReplaySelection.None;
        return new ArenaConfig(games, swap, maxTicks, seed, maps.ToImmutable(), pairs.ToImmutable(), save);
    }

    /// <summary>
    /// The maps of the list as written. In a list in brackets a comma parts items, so <c>random:size=96,players=2</c> comes as
    /// <c>random:size=96</c> and <c>players=2</c>: a setting with no map before it joins the random map before it.
    /// </summary>
    private static List<(YamlNode Node, string Text)> MapList(string fileName, YamlNode node)
    {
        var maps = new List<(YamlNode Node, string Text)>();
        foreach (var item in List(fileName, node, "maps"))
        {
            var text = Scalar(fileName, item);
            if (maps.Count > 0 && maps[^1].Text.StartsWith("random:", StringComparison.Ordinal) && !text.Contains(':') && text.Contains('='))
            {
                maps[^1] = (maps[^1].Node, $"{maps[^1].Text},{text}");
            }
            else
            {
                maps.Add((item, text));
            }
        }

        return maps;
    }

    /// <summary>A map of the data by its id, or a random one by its settings; <paramref name="node"/> is where it is written.</summary>
    public static MapSpec Map(string fileName, YamlNode node, string text, string dataFolder)
    {
        if (text.StartsWith("random:", StringComparison.Ordinal))
        {
            return new MapSpec(text, null, RandomSettings(fileName, node, text["random:".Length..]));
        }

        var maps = Path.Combine(dataFolder, "maps");
        var file = Path.Combine(maps, text + ".rtsmap");
        if (!File.Exists(file))
        {
            var known = Directory.GetFiles(maps, "*.rtsmap").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal);
            throw Error(fileName, node, $"no map '{text}': one of {string.Join(", ", known)}, or random:size=96,players=2");
        }

        try
        {
            return new MapSpec(text, RtsMapReader.Read(File.ReadAllText(file)), null);
        }
        catch (RtsMapFormatException e)
        {
            throw Error(fileName, node, $"the map '{text}' cannot be read: {e.Message}");
        }
    }

    private static MapGenSettings RandomSettings(string fileName, YamlNode node, string settings)
    {
        var map = new MapGenSettings();
        foreach (var part in settings.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var (key, value) = part.IndexOf('=') is var at and > 0 ? (part[..at].Trim(), part[(at + 1)..].Trim()) : (part.Trim(), "");
            map = key switch
            {
                "size" when int.TryParse(value, CultureInfo.InvariantCulture, out var size) && MapGenSettings.Sizes.Contains(size) => map with { Size = size },
                "players" when value is "2" or "4" => map with { Players = int.Parse(value, CultureInfo.InvariantCulture) },
                "relief" when Enum.TryParse<ReliefKind>(value, true, out var relief) => map with { Relief = relief },
                "water" when Enum.TryParse<WaterKind>(value, true, out var water) => map with { Water = water },
                "forest" when Enum.TryParse<Amount>(value, true, out var forest) => map with { Forest = forest },
                "resources" when Enum.TryParse<Amount>(value, true, out var resources) => map with { Resources = resources },
                _ => throw Error(fileName, node,
                    $"'{part}' is not a setting of a random map: size={string.Join("|", MapGenSettings.Sizes)}, players=2|4, relief, water, forest, resources"),
            };
        }

        return map with { Symmetry = map.Players == 4 ? MapSymmetry.Rotate90 : MapSymmetry.Mirror };
    }

    private static ArenaPair Pair(string fileName, YamlNode node, RulesDatabase rules)
    {
        if (node is not YamlMappingNode mapping)
        {
            throw Error(fileName, node, "a pair is { a: profile, b: profile }");
        }

        BotProfile? a = null, b = null;
        foreach (var (key, value) in mapping.Children)
        {
            var side = ((YamlScalarNode)key).Value;
            var profile = Profile(fileName, value, rules);
            (a, b) = side switch
            {
                "a" => (profile, b),
                "b" => (a, profile),
                _ => throw Error(fileName, key, $"unknown key '{side}' in a pair; it has a and b"),
            };
        }

        return a is not null && b is not null ? new ArenaPair(a, b) : throw Error(fileName, node, "a pair needs both a and b");
    }

    private static BotProfile Profile(string fileName, YamlNode node, RulesDatabase rules)
    {
        try
        {
            return BotProfile.Parse(Scalar(fileName, node), rules);
        }
        catch (ArenaException e)
        {
            throw Error(fileName, node, e.Message);
        }
    }

    private static ReplaySelection Selection(string fileName, YamlNode node)
    {
        var selection = ReplaySelection.None;
        foreach (var item in List(fileName, node, "saveReplays"))
        {
            var text = Scalar(fileName, item);
            selection = text switch
            {
                "all" => selection with { All = true },
                "draws" => selection with { Draws = true },
                "upsets" => selection with { Upsets = true },
                _ when text.StartsWith("longest:", StringComparison.Ordinal)
                       && int.TryParse(text["longest:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0 =>
                    selection with { Longest = count },
                _ => throw Error(fileName, item, $"'{text}' is not a choice of replays: all, draws, upsets or longest:N"),
            };
        }

        return selection;
    }

    private static IEnumerable<YamlNode> List(string fileName, YamlNode node, string key) =>
        node is YamlSequenceNode { Children.Count: > 0 } sequence ? sequence.Children : throw Error(fileName, node, $"'{key}' is a list of one or more");

    private static string Scalar(string fileName, YamlNode node) =>
        node is YamlScalarNode { Value: { } text } ? text : throw Error(fileName, node, "expected a single value");

    private static int Number(string fileName, YamlNode node, string key, bool allowZero = false)
    {
        var text = Scalar(fileName, node);
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && (allowZero || value > 0)
            ? value
            : throw Error(fileName, node, $"'{key}' must be a whole number{(allowZero ? "" : " above zero")}, not '{text}'");
    }

    private static bool Flag(string fileName, YamlNode node) => Scalar(fileName, node) switch
    {
        "true" => true,
        "false" => false,
        var text => throw Error(fileName, node, $"'{text}' must be true or false"),
    };

    /// <summary><c>60m</c> or <c>90s</c>, in ticks.</summary>
    private static int Duration(string fileName, YamlNode node)
    {
        var text = Scalar(fileName, node);
        var (number, unit) = text.Length > 1 ? (text[..^1], text[^1]) : ("", ' ');
        return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 && unit is 'm' or 's'
            ? value * (unit == 'm' ? 900 : 15)
            : throw Error(fileName, node, $"'{text}' must be a duration such as 60m or 90s");
    }

    private static ArenaException Error(string fileName, YamlNode node, string reason) =>
        new($"{fileName}:{node.Start.Line}:{node.Start.Column}: {reason}");
}
