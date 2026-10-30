using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;

namespace Rts.Data.Maps;

/// <summary>
/// The bridge repair huts of a map (loose ends, stage 2): two by every bridge, one by each of its ends, so each bank has its own (the
/// owner, 2026-10-30). A hut belongs to the bridge nearest it (<see cref="BridgeRepair.BridgeOf(MapGrid, CellPos)"/>) and to the end of
/// it nearer to it (<see cref="EndOf"/>); it goes on the first free cell off ore by that end (<see cref="MapChecks.HutCandidates(BridgePlacement, int)"/>)
/// that belongs to that bridge and end, with its images (the generator's symmetry, given as a function) by the images of the bridge —
/// so the huts stay as fair as the map — or alone where no cell has free images. A bridge its own image (across a river between two
/// players) gets the hut of its other end as the image of the first. The generator places them after the derricks; the map tool adds
/// them to the maps made before (<c>add-huts</c>) and takes the second huts of an end off (<c>tidy</c>, <see cref="Trim"/>); the ready
/// maps take them as they are built.
/// </summary>
public static class BridgeHuts
{
    public const string Hut = "tech_bridge_repair_hut";

    /// <summary>
    /// The end of a bridge from <paramref name="from"/> to <paramref name="to"/> a hut on <paramref name="hut"/> stands by, its bank:
    /// 0 the first, 1 the other — the nearer of the two. A bridge of one cell has its banks on the side the hut lies farther off it.
    /// </summary>
    public static int EndOf(CellPos from, CellPos to, CellPos hut)
    {
        if (from == to)
        {
            var (dx, dy) = (hut.X - from.X, hut.Y - from.Y);
            return (IntMath.Abs(dx) >= IntMath.Abs(dy) ? dx : dy) < 0 ? 0 : 1;
        }

        long Squared(CellPos end) => ((long)(end.X - hut.X) * (end.X - hut.X)) + ((long)(end.Y - hut.Y) * (end.Y - hut.Y));
        return Squared(to) < Squared(from) ? 1 : 0;
    }

    /// <summary>The huts the map lacks, each with its <paramref name="images"/> (the cell itself among them), or alone without them.</summary>
    public static List<StructurePlacement> For(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null)
    {
        var ground = MapChecks.BaseGrid(map, types);
        var owners = new Owners(map, types, ground);
        var placed = new List<StructurePlacement>(map.Structures);
        var hutted = new HashSet<(int Bridge, int End)>();
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
            for (var end = 0; end < 2; end++)
            {
                if (hutted.Contains((i, end)))
                {
                    continue;
                }

                // With its images if they are free, or alone when no cell has free images (a thick forest).
                var cells = Place(map, ground, owners, (i, end), hutted, placed, images) ?? Place(map, ground, owners, (i, end), hutted, placed, null);
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
        }

        return huts;
    }

    // The first candidate of the end of the bridge that belongs to it and is free with its images, and the images; null without one.
    private static List<StructurePlacement>? Place(
        MapDefinition map, MapGrid ground, Owners owners, (int Bridge, int End) end, HashSet<(int Bridge, int End)> hutted,
        List<StructurePlacement> placed, Func<CellPos, IEnumerable<CellPos>>? images)
    {
        foreach (var candidate in MapChecks.HutCandidates(map.Bridges[end.Bridge], end.End))
        {
            if (!map.Contains(candidate) || owners.Of(candidate) != end || owners.Bridged(candidate))
            {
                continue;
            }

            var cells = new List<StructurePlacement> { new(Hut, candidate, 0, 0) };
            var taken = new HashSet<(int Bridge, int End)> { end };
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

    /// <summary>
    /// The map with the huts it lacks (see <see cref="For"/>). An end of a bridge with no free cell by it (water, slopes and woods all
    /// round) gets one where a tree or another thing of the map stands, felled for it, with its images.
    /// </summary>
    public static MapDefinition AddTo(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null)
    {
        var hutted = WithStructures(map, [.. map.Structures, .. For(map, types, images)]);
        while (Felled(hutted, types, images) is { } felled)
        {
            hutted = felled;
        }

        return hutted;
    }

    // The map with a hut more by an end that lacks one, on the first cell by that end where a thing of the map stands that, felled with
    // the things on the images of the cell, leaves room for it (see For); null when no end can have one so.
    private static MapDefinition? Felled(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images)
    {
        var owners = new Owners(map, types, MapChecks.BaseGrid(map, types));
        var hutted = new HashSet<(int Bridge, int End)>();
        foreach (var structure in map.Structures)
        {
            if (structure.Type == Hut && owners.Of(structure.Cell) is { } owner)
            {
                hutted.Add(owner);
            }
        }

        for (var i = 0; i < map.Bridges.Length; i++)
        {
            for (var end = 0; end < 2; end++)
            {
                if (hutted.Contains((i, end)))
                {
                    continue;
                }

                foreach (var candidate in MapChecks.HutCandidates(map.Bridges[i], end))
                {
                    if (!map.Contains(candidate) || owners.Of(candidate) != (i, end) || owners.Bridged(candidate)
                        || map.Ore[(candidate.Y * map.Width) + candidate.X] != 0 || !map.Objects.Any(o => o.Cell == candidate))
                    {
                        continue;
                    }

                    var cells = new HashSet<CellPos>(images?.Invoke(candidate) ?? []) { candidate };
                    var cleared = new MapDefinition(
                        map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
                        map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, [.. map.Objects.Where(o => !cells.Contains(o.Cell))],
                        map.Bridges, map.Structures, map.Lighting, map.Playable, map.Origin);
                    var huts = For(cleared, types, images);
                    if (huts.Exists(hut => hut.Cell == candidate))
                    {
                        // Only the things where the huts went are felled.
                        var taken = huts.Select(hut => hut.Cell).ToHashSet();
                        return new MapDefinition(
                            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
                            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, [.. map.Objects.Where(o => !taken.Contains(o.Cell))],
                            map.Bridges, [.. map.Structures, .. huts], map.Lighting, map.Playable, map.Origin);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The map with one hut an end of a bridge: of the huts by one end the one farthest from the nearest start stays (the most neutral;
    /// the first of the map on a tie), the others go, and so does a hut no bridge is near: it would mend nothing.
    /// </summary>
    public static MapDefinition Trim(MapDefinition map, MapTypes types)
    {
        var owners = new Owners(map, types, MapChecks.BaseGrid(map, types));
        var kept = new Dictionary<(int Bridge, int End), StructurePlacement>();
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

    // Which bridge of the map (its index in the list) a hut on a cell belongs to, by the simulation's own rule (the bridges loaded as
    // the match loads them, those the checks let stand), and by which of its ends it stands.
    private sealed class Owners
    {
        private readonly MapDefinition _map;
        private readonly MapGrid _grid;
        private readonly List<int> _index = [];

        public Owners(MapDefinition map, MapTypes types, MapGrid ground)
        {
            _map = map;
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

        // Whether a bridge takes the cell (the deck of a high one over land): no hut stands there.
        public bool Bridged(CellPos cell) => _grid.BridgeAt(cell) is not null;

        public (int Bridge, int End)? Of(CellPos cell)
        {
            if (BridgeRepair.BridgeOf(_grid, cell) is not { } bridge)
            {
                return null;
            }

            var index = _index[bridge.Id - 1];
            return (index, EndOf(_map.Bridges[index].From, _map.Bridges[index].To, cell));
        }
    }
}
