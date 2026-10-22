using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Data.Maps;

/// <summary>
/// The temporary ASCII maps of M2 (<see cref="TestMapParser"/>) as <c>.rtsmap</c> maps: flat (every corner at level 0), the
/// same land, the same map objects (trees and the <c>object</c> lines, M7 step 5) and bridges (step 6), a winter texture picked by the land. The
/// units of the old format have no place in a map file (starts and units come with M6) and are left out; the players are
/// the owners found there.
/// </summary>
public static class TestMapConverter
{
    public const string PineTree = "pine_tree";

    /// <summary>Texture codes of the converted maps, by land: snow on clear land, dirt on rough, asphalt on roads, and so on.</summary>
    public static readonly ImmutableArray<TextureEntry> Legend =
    [
        new('a', "asphalt"),
        new('b', "shore"),
        new('d', "dirt"),
        new('i', "ice"),
        new('k', "rock"),
        new('s', "snow"),
        new('w', "water"),
    ];

    public static MapDefinition Convert(TestMap test, string id, string name)
    {
        var grid = test.Grid;
        var land = ImmutableArray.CreateBuilder<LandType>(grid.Width * grid.Height);
        var texture = ImmutableArray.CreateBuilder<char>(grid.Width * grid.Height);
        var objects = ImmutableArray.CreateBuilder<MapObjectPlacement>();
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var cell = new CellPos(x, y);
                land.Add(grid.LandAt(cell));
                texture.Add(TextureOf(grid.LandAt(cell)));
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
            Legend,
            heights: ImmutableArray.Create(new byte[(grid.Width + 1) * (grid.Height + 1)]),
            land.MoveToImmutable(),
            texture.MoveToImmutable(),
            ore: ImmutableArray.Create(new byte[grid.Width * grid.Height]),
            objects.ToImmutable(),
            bridges.ToImmutable(),
            structures: []);
    }

    private static char TextureOf(LandType land) => land switch
    {
        LandType.Rough => 'd',
        LandType.Road => 'a',
        LandType.Beach => 'b',
        LandType.Shallow or LandType.Deep => 'w',
        LandType.Rock => 'k',
        LandType.Ice => 'i',
        _ => 's',
    };
}
