using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Data.Maps;

/// <summary>
/// Checks of single map elements for the editor and <see cref="MapValidator"/> (docs/implementation/M7_maps.md, 4.13): a
/// bridge or a building against the ground alone, with the rules the simulation applies when it loads the map
/// (<see cref="MapGrid.FromLayers"/>), so one broken element never hides whether another is fine.
/// </summary>
public static class MapChecks
{
    /// <summary>The land, heights and map objects of a known type of <paramref name="map"/>, without bridges and buildings.</summary>
    public static MapGrid BaseGrid(MapDefinition map)
    {
        var land = new byte[map.Width * map.Height];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)map.Land[i];
        }

        var objects = new List<MapObjectState>();
        foreach (var placement in map.Objects)
        {
            if (MapObjectTypes.TryGetById(placement.Type, out var type))
            {
                objects.Add(MapObjectState.New(type.Kind, placement.Cell, placement.Variant));
            }
        }

        return MapGrid.FromLayers(map.Width, map.Height, land, new byte[land.Length], map.Heights.AsSpan(), objects);
    }

    /// <summary>The cells of a bridge, from <see cref="BridgePlacement.From"/> to <see cref="BridgePlacement.To"/>; none when it is not straight.</summary>
    public static List<CellPos> BridgeCells(BridgePlacement bridge)
    {
        var cells = new List<CellPos>();
        if (bridge.From.X != bridge.To.X && bridge.From.Y != bridge.To.Y)
        {
            return cells;
        }

        var (dx, dy) = (IntMath.Sign(bridge.To.X - bridge.From.X), IntMath.Sign(bridge.To.Y - bridge.From.Y));
        for (var cell = bridge.From; ; cell = new CellPos(cell.X + dx, cell.Y + dy))
        {
            cells.Add(cell);
            if (cell == bridge.To)
            {
                return cells;
            }
        }
    }

    /// <summary>The cells a structure of a known type takes; none for an unknown type.</summary>
    public static List<CellPos> Footprint(StructurePlacement structure)
    {
        var cells = new List<CellPos>();
        if (BuildingTypes.TryGetById(structure.Type, out var type))
        {
            for (var y = structure.Cell.Y; y < structure.Cell.Y + type.Height; y++)
            {
                for (var x = structure.Cell.X; x < structure.Cell.X + type.Width; x++)
                {
                    cells.Add(new CellPos(x, y));
                }
            }
        }

        return cells;
    }

    /// <summary>What is wrong with the bridge on this ground beside <paramref name="others"/>, or null when it stands.</summary>
    public static string? BridgeProblem(MapGrid ground, BridgePlacement bridge, IEnumerable<BridgePlacement> others)
    {
        if (!BridgeTypes.TryGetById(bridge.Type, out var type))
        {
            return $"unknown bridge '{bridge.Type}'";
        }

        var taken = new HashSet<CellPos>();
        foreach (var other in others)
        {
            taken.UnionWith(BridgeCells(other));
        }

        foreach (var cell in BridgeCells(bridge))
        {
            if (taken.Contains(cell))
            {
                return $"cell {cell.X},{cell.Y} already has a bridge";
            }
        }

        return Problem(() => FromGround(ground, bridges: [BridgeState.New(type.Kind, bridge.From, bridge.To)]));
    }

    /// <summary>What is wrong with the building on this ground beside <paramref name="others"/>, or null when it stands.</summary>
    public static string? BuildingProblem(MapGrid ground, StructurePlacement structure, IEnumerable<StructurePlacement> others)
    {
        if (!BuildingTypes.TryGetById(structure.Type, out var type))
        {
            return $"unknown building '{structure.Type}'";
        }

        var taken = new HashSet<CellPos>();
        foreach (var other in others)
        {
            taken.UnionWith(Footprint(other));
        }

        foreach (var cell in Footprint(structure))
        {
            if (taken.Contains(cell))
            {
                return $"cell {cell.X},{cell.Y} is taken";
            }
        }

        return Problem(() => FromGround(ground, buildings: [new BuildingState(type.Kind, structure.Cell, new PlayerId(structure.Owner), structure.Facing)]));
    }

    private static MapGrid FromGround(MapGrid ground, IReadOnlyList<BridgeState>? bridges = null, IReadOnlyList<BuildingState>? buildings = null) =>
        MapGrid.FromLayers(ground.Width, ground.Height, ground.LandBytes(), ground.BlockedBytes(), ground.Heights.LevelBytes(), ground.ObjectStates(), bridges, buildings);

    // "Bridge 1 is not valid: no shore at 9,4, a bridge runs from shore to shore." → "no shore at 9,4, a bridge runs from shore to shore"
    private static string? Problem(Func<MapGrid> build)
    {
        try
        {
            build();
            return null;
        }
        catch (ArgumentException error)
        {
            var message = error.Message;
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            return (colon >= 0 ? message[(colon + 2)..] : message).TrimEnd('.');
        }
    }
}
