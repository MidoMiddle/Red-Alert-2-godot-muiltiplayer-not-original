using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Maps.Editing;

/// <summary>
/// A map open in the editor (docs/implementation/M7_maps.md, 4.13): a changeable copy of a <see cref="MapDefinition"/>.
/// Heights per corner, land, texture and ore per cell; objects, bridges, structures and starts as the file has them (their
/// tools come in step 12). <see cref="ToDefinition"/> turns it back into a checked definition; nothing is lost on the way.
/// </summary>
public sealed class EditableMap
{
    private const string LegendCodes = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly byte[] _heights;
    private readonly LandType[] _land;
    private readonly char[] _texture;
    private readonly byte[] _ore;
    private readonly List<TextureEntry> _legend;

    private EditableMap(MapDefinition map)
    {
        Id = map.Id;
        Name = map.Name;
        Author = map.Author;
        Description = map.Description;
        Width = map.Width;
        Height = map.Height;
        Climate = map.Climate;
        Players = map.Players;
        Starts = map.Starts;
        Objects = map.Objects;
        Bridges = map.Bridges;
        Structures = map.Structures;
        _legend = [.. map.TextureLegend];
        _heights = [.. map.Heights];
        _land = [.. map.Land];
        _texture = [.. map.Texture];
        _ore = [.. map.Ore];
    }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Author { get; set; }

    public string Description { get; set; }

    public int Width { get; }

    public int Height { get; }

    public string Climate { get; }

    public int Players { get; }

    public ImmutableArray<MapStart> Starts { get; }

    public ImmutableArray<MapObjectPlacement> Objects { get; }

    public ImmutableArray<BridgePlacement> Bridges { get; }

    public ImmutableArray<StructurePlacement> Structures { get; }

    public IReadOnlyList<TextureEntry> Legend => _legend;

    public static EditableMap FromDefinition(MapDefinition map) => new(map);

    /// <summary>A new map: flat at level 0, clear land under snow, the texture legend of the converted maps, no starts yet.</summary>
    public static EditableMap New(int width, int height, int players)
    {
        var snow = 's';
        var map = new MapDefinition(
            "new-map", "New map", "", "", width, height, "winter", players, [], TestMapConverter.Legend,
            ImmutableArray.Create(new byte[(width + 1) * (height + 1)]),
            [.. Repeat(LandType.Clear, width * height)],
            [.. Repeat(snow, width * height)],
            ImmutableArray.Create(new byte[width * height]),
            [], [], []);
        return new EditableMap(map);
    }

    /// <summary>The map as a checked definition; throws <see cref="ArgumentException"/> when a field does not fit the format.</summary>
    public MapDefinition ToDefinition() => new(
        Id, Name, Author, Description, Width, Height, Climate, Players, Starts, [.. _legend],
        [.. _heights], [.. _land], [.. _texture], [.. _ore], Objects, Bridges, Structures);

    public bool ContainsCorner(int x, int y) => x >= 0 && y >= 0 && x <= Width && y <= Height;

    public bool ContainsCell(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    public int CornerLevel(int x, int y) => _heights[CornerIndex(x, y)];

    public LandType LandAt(CellPos cell) => _land[CellIndex(cell)];

    public char TextureAt(CellPos cell) => _texture[CellIndex(cell)];

    public int CornerIndex(int x, int y) => y * (Width + 1) + x;

    public int CellIndex(CellPos cell) => cell.Y * Width + cell.X;

    /// <summary>The texture code of a material, added to the legend with a free code when the map has none.</summary>
    public char CodeFor(string material)
    {
        foreach (var entry in _legend)
        {
            if (entry.Material == material)
            {
                return entry.Code;
            }
        }

        foreach (var code in LegendCodes)
        {
            if (!_legend.Exists(entry => entry.Code == code))
            {
                _legend.Add(new TextureEntry(code, material));
                return code;
            }
        }

        throw new InvalidOperationException("The texture legend is full.");
    }

    internal byte RawValue(EditLayer layer, int index) => layer switch
    {
        EditLayer.Height => _heights[index],
        EditLayer.Land => (byte)_land[index],
        _ => (byte)_texture[index],
    };

    internal void SetRaw(EditLayer layer, int index, byte value)
    {
        switch (layer)
        {
            case EditLayer.Height:
                _heights[index] = value;
                break;
            case EditLayer.Land:
                _land[index] = (LandType)value;
                break;
            default:
                _texture[index] = (char)value;
                break;
        }
    }

    private static T[] Repeat<T>(T value, int count)
    {
        var array = new T[count];
        Array.Fill(array, value);
        return array;
    }
}
