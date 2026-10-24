using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Maps;

/// <summary>A <c>.rtsmap</c> file that cannot be read; <see cref="Line"/> and <see cref="Column"/> are 1-based.</summary>
public sealed class RtsMapFormatException(int line, int column, string message) : Exception($"{line}:{column}: {message}")
{
    public int Line { get; } = line;

    public int Column { get; } = column;

    public string Reason { get; } = message;
}

/// <summary>
/// Reads a <c>.rtsmap</c> file (docs/implementation/M7_maps.md, 4.9) through the YAML representation model, so every error
/// names the line and column it comes from, inside the cell layers too. Unknown keys are errors: a typo in a map must not
/// be lost quietly. Numbers are integers only.
/// </summary>
public static class RtsMapReader
{
    private static readonly string[] RootKeys =
        ["format", "id", "name", "author", "description", "size", "playable", "climate", "lighting", "players", "starts", "legend", "layers", "objects", "bridges", "structures"];

    public static MapDefinition Read(string text)
    {
        text = text.Replace("\r\n", "\n");
        var lines = text.Split('\n');
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException e)
        {
            throw new RtsMapFormatException((int)e.Start.Line, (int)e.Start.Column, $"not valid YAML: {e.Message}");
        }
        catch (InvalidOperationException e)
        {
            // YamlDotNet's scanner gives up this way on some broken input (an unclosed bracket) without a position: report
            // where the last token it could read ended.
            var (line, column) = LastReadablePosition(text);
            throw new RtsMapFormatException(line, column, $"not valid YAML: {e.Message}");
        }

        if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new RtsMapFormatException(1, 1, "a map is one YAML mapping");
        }

        var values = Entries(root, RootKeys, "the map");
        var format = Text(Required(values, "format", root));
        if (format != MapDefinition.FormatVersion)
        {
            throw Error(values["format"], $"unknown format '{format}', expected '{MapDefinition.FormatVersion}'");
        }

        var id = Id(Required(values, "id", root));
        var name = Optional(values, "name") is { } nameNode ? Line(nameNode) : id;
        var author = Optional(values, "author") is { } authorNode ? Line(authorNode) : "";
        var description = Optional(values, "description") is { } descriptionNode ? Line(descriptionNode) : "";
        var sizeNode = Required(values, "size", root);
        var size = Pair(sizeNode, "size");
        if (size.X is < MapDefinition.MinSize or > MapDefinition.MaxSize || size.Y is < MapDefinition.MinSize or > MapDefinition.MaxSize)
        {
            throw Error(sizeNode, $"size {size.X}x{size.Y} is outside {MapDefinition.MinSize}…{MapDefinition.MaxSize}");
        }

        var (width, height) = (size.X, size.Y);
        var playable = Optional(values, "playable") is { } playableNode ? Playable(playableNode, width, height) : null;
        var climate = Optional(values, "climate") is { } climateNode ? Id(climateNode) : "winter";
        var lighting = Optional(values, "lighting") is { } lightingNode ? Lighting(lightingNode) : MapLighting.Default;
        var players = Int(Required(values, "players", root), 1, Rts.Simulation.World.PlayerId.MaxPlayers, "players");

        var starts = Optional(values, "starts") is { } startsNode ? Starts(startsNode, players, width, height) : [];
        var legend = Optional(values, "legend") is { } legendNode ? Legend(legendNode) : [new TextureEntry('a', "default")];

        var layersNode = Required(values, "layers", root);
        var layers = Entries(layersNode, ["height", "land", "texture", "ore"], "layers");
        var heightCodes = Layer<byte>(Required(layers, "height", layersNode), lines, width + 1, height + 1, "height",
            code => MapNames.HeightCodes.IndexOf(code) is var level and >= 0 ? (byte)level : null);
        var land = Layer<LandType>(Required(layers, "land", layersNode), lines, width, height, "land", code => MapNames.LandOf(code));
        var texture = Optional(layers, "texture") is { } textureNode
            ? Layer<char>(textureNode, lines, width, height, "texture", code => MapNames.IsLegendCode(code) && Contains(legend, code) ? code : null)
            : Filled(legend[0].Code, width * height);
        var ore = Optional(layers, "ore") is { } oreNode
            ? Layer<byte>(oreNode, lines, width, height, "ore", code => MapNames.OreCodes.IndexOf(code) is var value and >= 0 ? (byte)value : null)
            : Filled((byte)0, width * height);

        var objects = Optional(values, "objects") is { } objectsNode ? List(objectsNode, "objects", node => MapObject(node, width, height)) : [];
        var bridges = Optional(values, "bridges") is { } bridgesNode ? List(bridgesNode, "bridges", node => Bridge(node, width, height)) : [];
        var structures = Optional(values, "structures") is { } structuresNode
            ? List(structuresNode, "structures", node => Structure(node, players, width, height))
            : [];

        try
        {
            return new MapDefinition(id, name, author, description, width, height, climate, players, starts, legend, heightCodes, land, texture, ore,
                objects, bridges, structures, lighting, playable);
        }
        catch (ArgumentException e)
        {
            throw new RtsMapFormatException(1, 1, e.Message);
        }
    }

    /// <summary>The entries of a mapping by key, in file order; unknown and repeated keys are errors at the key.</summary>
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

    private static ImmutableArray<MapStart> Starts(YamlNode node, int players, int width, int height)
    {
        var starts = List(node, "starts", entry =>
        {
            var fields = Entries(entry, ["slot", "cell"], "a start");
            var slot = Int(Required(fields, "slot", entry), 1, players, "slot");
            return new MapStart(slot, Cell(Required(fields, "cell", entry), width, height));
        });
        var seen = new bool[players + 1];
        var items = ((YamlSequenceNode)node).Children;
        for (var i = 0; i < starts.Length; i++)
        {
            if (seen[starts[i].Slot])
            {
                throw Error(items[i], $"slot {starts[i].Slot} has two starts");
            }

            seen[starts[i].Slot] = true;
        }

        return starts;
    }

    private static ImmutableArray<TextureEntry> Legend(YamlNode node)
    {
        var legend = Entries(node, ["texture"], "legend");
        if (Optional(legend, "texture") is not YamlMappingNode textures || textures.Children.Count == 0)
        {
            throw Error(node, "legend needs a 'texture' mapping of codes to materials");
        }

        var entries = ImmutableArray.CreateBuilder<TextureEntry>();
        foreach (var pair in textures)
        {
            var code = Text(pair.Key);
            if (code.Length != 1 || !MapNames.IsLegendCode(code[0]))
            {
                throw Error(pair.Key, $"texture code '{code}' must be one letter or digit");
            }

            if (Contains(entries.ToImmutable(), code[0]))
            {
                throw Error(pair.Key, $"texture code '{code}' repeats");
            }

            entries.Add(new TextureEntry(code[0], Id(pair.Value)));
        }

        return entries.ToImmutable();
    }

    private static MapObjectPlacement MapObject(YamlNode node, int width, int height)
    {
        var fields = Entries(node, ["type", "cell", "variant"], "an object");
        var variant = Optional(fields, "variant") is { } variantNode ? Int(variantNode, 0, 255, "variant") : 0;
        return new MapObjectPlacement(Id(Required(fields, "type", node)), Cell(Required(fields, "cell", node), width, height), variant);
    }

    private static BridgePlacement Bridge(YamlNode node, int width, int height)
    {
        var fields = Entries(node, ["type", "from", "to"], "a bridge");
        var from = Cell(Required(fields, "from", node), width, height);
        var to = Cell(Required(fields, "to", node), width, height);
        if (from.X != to.X && from.Y != to.Y)
        {
            throw Error(fields["to"], $"a bridge runs along X or Y: {from.X},{from.Y} to {to.X},{to.Y} is neither");
        }

        return new BridgePlacement(Id(Required(fields, "type", node)), from, to);
    }

    private static StructurePlacement Structure(YamlNode node, int players, int width, int height)
    {
        var fields = Entries(node, ["type", "cell", "owner", "facing"], "a structure");
        var owner = 0;
        if (Optional(fields, "owner") is { } ownerNode && Text(ownerNode) != "neutral")
        {
            owner = Int(ownerNode, 1, players, "owner");
        }

        var facing = Optional(fields, "facing") is { } facingNode ? Int(facingNode, 0, 7, "facing") : 0;
        return new StructurePlacement(Id(Required(fields, "type", node)), Cell(Required(fields, "cell", node), width, height), owner, facing);
    }

    private static ImmutableArray<T> List<T>(YamlNode node, string what, Func<YamlNode, T> item)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw Error(node, $"{what} must be a list");
        }

        var items = ImmutableArray.CreateBuilder<T>(sequence.Children.Count);
        foreach (var child in sequence)
        {
            items.Add(item(child));
        }

        return items.MoveToImmutable();
    }

    /// <summary>
    /// A layer: a literal block (<c>|</c>) of <paramref name="rows"/> lines of <paramref name="columns"/> codes each. The rows
    /// start on the line after the key; their indentation is read from the file, so errors point at the very character.
    /// </summary>
    private static ImmutableArray<T> Layer<T>(YamlNode node, string[] lines, int columns, int rows, string what, Func<char, T?> decode)
        where T : struct
    {
        if (node is not YamlScalarNode { Style: ScalarStyle.Literal } scalar)
        {
            throw Error(node, $"layer '{what}' must be a literal block ('|') of {rows} lines");
        }

        var firstLine = (int)node.Start.Line + 1;
        var content = (scalar.Value ?? "").Split('\n');
        var count = content.Length > 0 && content[^1].Length == 0 ? content.Length - 1 : content.Length;
        if (count != rows)
        {
            throw new RtsMapFormatException(firstLine + IntMin(count, rows), 1, $"layer '{what}' has {count} lines, expected {rows}");
        }

        var cells = ImmutableArray.CreateBuilder<T>(columns * rows);
        for (var row = 0; row < rows; row++)
        {
            var lineNumber = firstLine + row;
            var raw = lineNumber - 1 < lines.Length ? lines[lineNumber - 1] : "";
            var indent = raw.Length - raw.TrimStart(' ').Length;
            var codes = content[row];
            if (codes.Length != columns)
            {
                throw new RtsMapFormatException(lineNumber, indent + IntMin(codes.Length, columns) + 1,
                    $"layer '{what}' line {row + 1} has {codes.Length} cells, expected {columns}");
            }

            for (var column = 0; column < columns; column++)
            {
                if (decode(codes[column]) is not { } value)
                {
                    throw new RtsMapFormatException(lineNumber, indent + column + 1, $"unknown code '{codes[column]}' in layer '{what}'");
                }

                cells.Add(value);
            }
        }

        return cells.MoveToImmutable();
    }

    private static (int X, int Y) Pair(YamlNode node, string what)
    {
        if (node is not YamlSequenceNode { Children.Count: 2 } pair)
        {
            throw Error(node, $"{what} must be [x, y]");
        }

        return (Int(pair.Children[0], int.MinValue, int.MaxValue, what), Int(pair.Children[1], int.MinValue, int.MaxValue, what));
    }

    private static CellPos Cell(YamlNode node, int width, int height)
    {
        var (x, y) = Pair(node, "a cell");
        return x >= 0 && y >= 0 && x < width && y < height ? new CellPos(x, y) : throw Error(node, $"cell {x},{y} is outside the {width}x{height} map");
    }

    private static int Int(YamlNode node, int min, int max, string what)
    {
        var text = Text(node);
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            throw Error(node, $"{what} '{text}' is not a whole number");
        }

        return value >= min && value <= max ? value : throw Error(node, $"{what} {value} is outside {min}…{max}");
    }

    private static string Id(YamlNode node)
    {
        var text = Text(node);
        return MapNames.IsId(text) ? text : throw Error(node, $"'{text}' must be lowercase letters, digits, '-' or '_'");
    }

    /// <summary>The playable area in the screen's axes of the cells (<see cref="MapArea"/>), within the map's own.</summary>
    private static MapArea Playable(YamlNode node, int width, int height)
    {
        var fields = Entries(node, ["left", "top", "right", "bottom"], "playable");
        var left = Int(Required(fields, "left", node), -(height - 1), width - 1, "left");
        var right = Int(Required(fields, "right", node), left, width - 1, "right");
        var top = Int(Required(fields, "top", node), 1, width + height - 1, "top");
        var bottom = Int(Required(fields, "bottom", node), top, width + height - 1, "bottom");
        return new MapArea(left, top, right, bottom);
    }

    private static string Lighting(YamlNode node)
    {
        var text = Text(node);
        return MapLighting.IsKnown(text) ? text : throw Error(node, $"lighting '{text}' is none of {string.Join(", ", MapLighting.Ids)}");
    }

    private static string Line(YamlNode node)
    {
        var text = Text(node);
        return MapNames.IsText(text) ? text : throw Error(node, "text must be a single line");
    }

    private static string Text(YamlNode node) =>
        node is YamlScalarNode { Value: { } value } ? value : throw Error(node, "expected a single value");

    private static bool Contains(ImmutableArray<TextureEntry> legend, char code)
    {
        foreach (var entry in legend)
        {
            if (entry.Code == code)
            {
                return true;
            }
        }

        return false;
    }

    private static int IntMin(int a, int b) => a < b ? a : b;

    private static (int Line, int Column) LastReadablePosition(string text)
    {
        var scanner = new Scanner(new StringReader(text));
        var line = 1;
        var column = 1;
        try
        {
            while (scanner.MoveNext())
            {
                line = (int)scanner.Current!.End.Line;
                column = (int)scanner.Current.End.Column;
            }
        }
        catch (Exception e) when (e is YamlException or InvalidOperationException)
        {
            // The position of the last token read stays.
        }

        return (line, column);
    }

    private static ImmutableArray<T> Filled<T>(T value, int count)
    {
        var items = new T[count];
        Array.Fill(items, value);
        return ImmutableArray.Create(items);
    }

    private static RtsMapFormatException Error(YamlNode node, string message) => new((int)node.Start.Line, (int)node.Start.Column, message);
}
