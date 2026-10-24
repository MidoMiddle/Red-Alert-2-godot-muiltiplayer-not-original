using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Climates;

/// <summary>
/// What a map looks like beyond its rules (docs/implementation/M7_maps.md, 4.12): the climate, the time of day and the
/// <c>texture</c> layer with its legend. The simulation map drops all of it, so the view takes it from here. Godot-free.
/// A new object means the look changed.
/// </summary>
public sealed class MapLook
{
    private MapLook(string climate, string lighting, int width, int height, ImmutableArray<TextureEntry> legend, ImmutableArray<char> texture,
        MapArea? playable = null)
    {
        Playable = playable;
        Climate = climate;
        Lighting = lighting;
        Width = width;
        Height = height;
        Legend = legend;
        Texture = texture;
    }

    public string Climate { get; }

    /// <summary>The playable area, a rectangle on the screen the camera stops at (<see cref="MapArea"/>); null when the whole map plays.</summary>
    public MapArea? Playable { get; }

    /// <summary>One of <see cref="MapLighting.Ids"/>.</summary>
    public string Lighting { get; }

    public int Width { get; }

    public int Height { get; }

    public ImmutableArray<TextureEntry> Legend { get; }

    /// <summary>One code of <see cref="Legend"/> per cell, row by row from the north-west.</summary>
    public ImmutableArray<char> Texture { get; }

    public static MapLook FromDefinition(MapDefinition map) =>
        new(map.Climate, map.Lighting, map.Width, map.Height, map.TextureLegend, map.Texture, map.Playable);

    /// <summary>The map open in the editor as it is now.</summary>
    public static MapLook FromEditable(EditableMap map)
    {
        var texture = ImmutableArray.CreateBuilder<char>(map.Width * map.Height);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                texture.Add(map.TextureAt(new CellPos(x, y)));
            }
        }

        return new MapLook(map.Climate, map.Lighting, map.Width, map.Height, [.. map.Legend], texture.MoveToImmutable(), map.Playable);
    }

    /// <summary>
    /// A map without a file of its own (an ASCII test map, a replay of another map): the winter day, the texture the converted
    /// maps get by the land (<see cref="TestMapConverter.MaterialOf"/>).
    /// </summary>
    public static MapLook FromLand(MapGrid map)
    {
        var texture = ImmutableArray.CreateBuilder<char>(map.Width * map.Height);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var material = TestMapConverter.MaterialOf(map.LandAt(new CellPos(x, y)));
                texture.Add(TestMapConverter.Legend.First(entry => entry.Material == material).Code);
            }
        }

        return new MapLook("winter", MapLighting.Default, map.Width, map.Height, TestMapConverter.Legend, texture.MoveToImmutable());
    }

    /// <summary>The material of a cell by the legend, or null for a code the legend lacks.</summary>
    public string? MaterialAt(int index)
    {
        var code = Texture[index];
        foreach (var entry in Legend)
        {
            if (entry.Code == code)
            {
                return entry.Material;
            }
        }

        return null;
    }
}
