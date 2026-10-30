using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;

namespace Rts.Data.Maps;

/// <summary>
/// The bridge repair huts of a map (loose ends, stage 2): one by every bridge that has none, by one of its ends (the owner, 2026-10-30:
/// one hut a bridge, never one at each end). A hut belongs to the bridge nearest it (<see cref="BridgeRepair.BridgeOf(MapGrid, CellPos)"/>);
/// it goes on the first free cell off ore by the bridge's first end (<see cref="MapChecks.HutCandidates"/>) that belongs to that bridge,
/// with its images (the generator's symmetry, given as a function) by the images of the bridge — so the huts stay as fair as the map —
/// or alone where no cell has free images. An image that falls by the same bridge (a bridge its own image, across a river between
/// two players) or by one that has its hut is left out. The generator places them after the derricks; the map tool adds them to the
/// maps made before (<c>add-huts</c>) and takes the second huts off (<c>tidy</c>, <see cref="Trim"/>); the ready maps take them as
/// they are built.
/// </summary>
public static class BridgeHuts
{
    public const string Hut = "tech_bridge_repair_hut";

    /// <summary>The huts the map lacks, each with its <paramref name="images"/> (the cell itself among them), or alone without them.</summary>
    public static List<StructurePlacement> For(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null)
    {
        var ground = MapChecks.BaseGrid(map, types);
        var owners = new Owners(map, types, ground);
        var placed = new List<StructurePlacement>(map.Structures);
        var hutted = new HashSet<int>();
        foreach (var structure in map.Structures)
        {
            if (structure.Type == Hut && owners.Of(structure.Cell) is { } owner)
            {
                hutted.Add(owner);
            }
        }

        var huts = new List<StructurePlacement>();
        for (var i = 0; i < map.Bridges.Length; i++)
        {
            if (hutted.Contains(i))
            {
                continue;
            }

            // With its images if they are free, or alone when no cell has free images (a thick forest).
            var cells = Place(map, ground, owners, i, hutted, placed, images) ?? Place(map, ground, owners, i, hutted, placed, null);
            if (cells is not null)
            {
                foreach (var cell in cells)
                {
                    hutted.Add(owners.Of(cell.Cell)!.Value);
                }

                placed.AddRange(cells);
                huts.AddRange(cells);
            }
        }

        return huts;
    }

    // The first candidate of the bridge that belongs to it and is free with its images, and the images; null without one.
    private static List<StructurePlacement>? Place(
        MapDefinition map, MapGrid ground, Owners owners, int bridge, HashSet<int> hutted, List<StructurePlacement> placed,
        Func<CellPos, IEnumerable<CellPos>>? images)
    {
        foreach (var candidate in MapChecks.HutCandidates(map.Bridges[bridge]))
        {
            if (!map.Contains(candidate) || owners.Of(candidate) != bridge)
            {
                continue;
            }

            var cells = new List<StructurePlacement> { new(Hut, candidate, 0, 0) };
            var taken = new HashSet<int> { bridge };
            foreach (var cell in images?.Invoke(candidate) ?? [])
            {
                if (map.Contains(cell) && owners.Of(cell) is { } owner && !hutted.Contains(owner) && taken.Add(owner))
                {
                    cells.Add(new StructurePlacement(Hut, cell, 0, 0));
                }
            }

            var trial = new List<StructurePlacement>(placed);
            var fits = true;
            foreach (var image in cells)
            {
                fits &= map.Ore[(image.Cell.Y * map.Width) + image.Cell.X] == 0 && MapChecks.BuildingProblem(ground, image, trial) is null;
                trial.Add(image);
            }

            if (fits)
            {
                return cells;
            }
        }

        return null;
    }

    /// <summary>The map with the huts it lacks (see <see cref="For"/>).</summary>
    public static MapDefinition AddTo(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null) =>
        WithStructures(map, [.. map.Structures, .. For(map, types, images)]);

    /// <summary>
    /// The map with one hut a bridge: of the huts of one bridge the one farthest from the nearest start stays (the most neutral; the
    /// first of the map on a tie), the others go, and so does a hut no bridge is near: it would mend nothing.
    /// </summary>
    public static MapDefinition Trim(MapDefinition map, MapTypes types)
    {
        var owners = new Owners(map, types, MapChecks.BaseGrid(map, types));
        var kept = new Dictionary<int, StructurePlacement>();
        foreach (var structure in map.Structures)
        {
            if (structure.Type == Hut && owners.Of(structure.Cell) is { } owner
                && (!kept.TryGetValue(owner, out var other) || FromStarts(map, structure.Cell) > FromStarts(map, other.Cell)))
            {
                kept[owner] = structure;
            }
        }

        var structures = new List<StructurePlacement>();
        foreach (var structure in map.Structures)
        {
            if (structure.Type != Hut || (owners.Of(structure.Cell) is { } owner && kept[owner] == structure))
            {
                structures.Add(structure);
            }
        }

        return structures.Count == map.Structures.Length ? map : WithStructures(map, structures);
    }

    private static MapDefinition WithStructures(MapDefinition map, IEnumerable<StructurePlacement> structures) =>
        new(map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, map.Objects, map.Bridges, [.. structures],
            map.Lighting, map.Playable, map.Origin);

    // The squared distance from the cell to the nearest start.
    private static long FromStarts(MapDefinition map, CellPos cell)
    {
        var nearest = long.MaxValue;
        foreach (var start in map.Starts)
        {
            var (dx, dy) = ((long)(start.Cell.X - cell.X), (long)(start.Cell.Y - cell.Y));
            nearest = IntMath.Min(nearest, (dx * dx) + (dy * dy));
        }

        return nearest;
    }

    // Which bridge of the map (its index in the list) a hut on a cell belongs to, by the simulation's own rule: the bridges loaded as
    // the match loads them, those the checks let stand.
    private sealed class Owners
    {
        private readonly MapGrid _grid;
        private readonly List<int> _index = [];

        public Owners(MapDefinition map, MapTypes types, MapGrid ground)
        {
            var placed = new List<BridgePlacement>();
            var states = new List<BridgeState>();
            for (var i = 0; i < map.Bridges.Length; i++)
            {
                var bridge = map.Bridges[i];
                if (MapChecks.BridgeProblem(ground, bridge, placed) is null && types.TryGetBridge(bridge.Type, out var type))
                {
                    placed.Add(bridge);
                    states.Add(BridgeState.New(type, bridge.From, bridge.To));
                    _index.Add(i);
                }
            }

            _grid = MapGrid.FromLayers(
                ground.Width, ground.Height, ground.LandBytes(), ground.BlockedBytes(), ground.Heights.LevelBytes(), ground.ObjectStates(), states,
                types: types);
        }

        public int? Of(CellPos cell) => BridgeRepair.BridgeOf(_grid, cell) is { } bridge ? _index[bridge.Id - 1] : null;
    }
}
