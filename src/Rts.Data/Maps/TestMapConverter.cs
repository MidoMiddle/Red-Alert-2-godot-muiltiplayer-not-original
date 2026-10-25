using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Data.Maps;

/// <summary>
/// The temporary ASCII maps of M2 (<see cref="TestMapParser"/>) as <c>.rtsmap</c> maps: flat (every corner at level 0), the
/// same land, the same map objects (trees and the <c>object</c> lines, M7 step 5), bridges (step 6) and buildings (step 7), a winter texture picked by the land. The
/// units of the old format have no place in a map file (starts and units come with M6) and are left out; the players are
/// the owners found there. So are the <c>ore</c> and <c>gems</c> lines (M3): the densities of the map file hold only some of the stages.
/// </summary>
public static class TestMapConverter
{
    public const string PineTree = "pine_tree";

    /// <summary>
    /// Texture codes of the converted and new maps: snow on clear land, dirt on rough, asphalt on roads, and so on; grass
    /// under snow belongs to no land type and is only painted (the editor's texture brush, the generator's patches).
    /// </summary>
    public static readonly ImmutableArray<TextureEntry> Legend =
    [
        new('a', "asphalt"),
        new('b', "shore"),
        new('d', "dirt"),
        new('g', "grass"),
        new('i', "ice"),
        new('k', "rock"),
        new('s', "snow"),
        new('w', "water"),
    ];

    /// <summary>The legend of a map: the converter's, with the codes of the unpaved road and the footpath when the map has them.</summary>
    public static ImmutableArray<TextureEntry> LegendFor(bool roadKinds) =>
        roadKinds ? [.. Legend, new TextureEntry('u', "track"), new TextureEntry('t', "path")] : Legend;

    public static MapDefinition Convert(TestMap test, string id, string name)
    {
        var grid = test.Grid;
        // The unpaved road and the footpath get their codes only in maps that have them, so the other maps keep their legend.
        var roads = false;
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                roads |= grid.LandAt(new CellPos(x, y)) is LandType.Track or LandType.Path;
            }
        }

        var legend = LegendFor(roads);
        var land = ImmutableArray.CreateBuilder<LandType>(grid.Width * grid.Height);
        var texture = ImmutableArray.CreateBuilder<char>(grid.Width * grid.Height);
        var objects = ImmutableArray.CreateBuilder<MapObjectPlacement>();
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new CellPos(x, y);
                land.Add(grid.LandAt(cell));
                texture.Add(TextureOf(grid.LandAt(cell), legend));
            }
        }

        foreach (var mapObject in grid.Objects)
        {
            objects.Add(new MapObjectPlacement(mapObject.Type.Id, mapObject.Cell, mapObject.Variant));
        }

        var bridges = ImmutableArray.CreateBuilder<BridgePlacement>();
        foreach (var bridge in grid.Bridges)
        {
            bridges.Add(new BridgePlacement(bridge.Type.Id, bridge.From, bridge.To));
        }

        var structures = ImmutableArray.CreateBuilder<StructurePlacement>();
        foreach (var building in grid.StartBuildings)
        {
            structures.Add(new StructurePlacement(BuildingTypes.Get(building.Kind).Id, building.Cell, building.Owner.Value, building.Facing));
        }

        var players = test.Units.Count == 0 ? 2 : IntMath.Max(2, test.Units.Max(u => u.Owner.Value));
        return new MapDefinition(
            id,
            name,
            author: "M2 test map",
            description: "Converted from the ASCII test map of M2.",
            grid.Width,
            grid.Height,
            climate: "winter",
            players,
            starts: [],
            legend,
            heights: ImmutableArray.Create(new byte[(grid.Width + 1) * (grid.Height + 1)]),
            land.MoveToImmutable(),
            texture.MoveToImmutable(),
            ore: ImmutableArray.Create(new byte[grid.Width * grid.Height]),
            objects.ToImmutable(),
            bridges.ToImmutable(),
            structures.ToImmutable(),
            origin: MapOrigin.Test);
    }

    /// <summary>The texture material that goes with a land type: snow on clear land, dirt on rough, asphalt on roads, and so on.</summary>
    public static string MaterialOf(LandType land) => land switch
    {
        LandType.Rough => "dirt",
        LandType.Road => "asphalt",
        LandType.Beach => "shore",
        LandType.Shallow or LandType.Deep => "water",
        LandType.Rock => "rock",
        LandType.Ice => "ice",
        LandType.Track => "track",
        LandType.Path => "path",
        _ => "snow",
    };

    private static char TextureOf(LandType land, ImmutableArray<TextureEntry> legend)
    {
        var material = MaterialOf(land);
        foreach (var entry in legend)
        {
            if (entry.Material == material)
            {
                return entry.Code;
            }
        }

        throw new System.InvalidOperationException($"No legend code for {material}.");
    }
}
