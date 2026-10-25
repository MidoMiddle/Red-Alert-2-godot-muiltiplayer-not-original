using System.Collections.Generic;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Data.Maps;

public enum ProblemLevel
{
    Error,
    Warning,
}

/// <summary>Something wrong with a map: an error stops «Play», a warning does not; <see cref="Cell"/> is where to look, when there is a place.</summary>
public sealed record MapProblem(ProblemLevel Level, string Message, CellPos? Cell);

/// <summary>
/// The checks of the map editor (docs/implementation/M7_maps.md, 4.13): starts — as many as players, where tracks can stand,
/// at least <see cref="EdgeMargin"/> cells from the edge, reachable from each other (by tracks, or else over the water by an amphibious
/// transport — a map of islands: a warning); every bridge and building where the
/// rules allow it; ore within <see cref="OreRadius"/> cells of every start and about as much at each (within
/// <see cref="BalancePercent"/> %) and as many drills of each kind (M8 step 10b); with three players or more, about the same distance from every start to its nearest
/// opponent. Errors keep a map out of play; it can still be saved.
/// </summary>
public static class MapValidator
{
    public const int EdgeMargin = 3;
    public const int OreRadius = 12;
    public const int BalancePercent = 15;

    public static List<MapProblem> Check(MapDefinition map)
    {
        var problems = new List<MapProblem>();
        var ground = MapChecks.BaseGrid(map);
        foreach (var placement in map.Objects)
        {
            if (!MapObjectTypes.TryGetById(placement.Type, out var type))
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Unknown map object '{placement.Type}'", placement.Cell));
            }
            else if (type.Kind is MapObjectKind.OreSource or MapObjectKind.GemSource && map.Contains(placement.Cell)
                     && map.Ore[placement.Cell.Y * map.Width + placement.Cell.X] != 0)
            {
                // No harvester reaches it under the drill, and the source never throws onto its own cell.
                problems.Add(new MapProblem(ProblemLevel.Warning, $"Ore under the {placement.Type}", placement.Cell));
            }
        }

        // Bridges and buildings one by one, each against the ground and those before it that stand.
        var bridges = new List<BridgePlacement>();
        var bridgeStates = new List<BridgeState>();
        foreach (var bridge in map.Bridges)
        {
            if (MapChecks.BridgeProblem(ground, bridge, bridges) is { } problem)
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Bridge at {Text(bridge.From)}: {problem}", bridge.From));
                continue;
            }

            bridges.Add(bridge);
            BridgeTypes.TryGetById(bridge.Type, out var type);
            bridgeStates.Add(BridgeState.New(type.Kind, bridge.From, bridge.To));
        }

        var structures = new List<StructurePlacement>();
        var buildings = new List<BuildingState>();
        foreach (var structure in map.Structures)
        {
            if (MapChecks.BuildingProblem(ground, structure, structures) is { } problem)
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Building {structure.Type} at {Text(structure.Cell)}: {problem}", structure.Cell));
                continue;
            }

            structures.Add(structure);
            BuildingTypes.TryGetById(structure.Type, out var type);
            buildings.Add(new BuildingState(type.Kind, structure.Cell, new PlayerId(structure.Owner), structure.Facing));
        }

        var grid = MapGrid.FromLayers(
            ground.Width, ground.Height, ground.LandBytes(), ground.BlockedBytes(), ground.Heights.LevelBytes(), ground.ObjectStates(), bridgeStates, buildings);
        CheckArea(map, problems);
        CheckStarts(map, grid, problems);
        CheckOre(map, problems);
        CheckDrills(map, problems);
        CheckDistances(map, problems);
        return problems;
    }

    /// <summary>Buildings, bridges and ore outside the playable area: nobody could reach them (map objects there are scenery).</summary>
    private static void CheckArea(MapDefinition map, List<MapProblem> problems)
    {
        if (map.Playable is null)
        {
            return;
        }

        foreach (var structure in map.Structures)
        {
            if (!MapChecks.Footprint(structure).All(map.Plays))
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Building {structure.Type} at {Text(structure.Cell)} is outside the playable area", structure.Cell));
            }
        }

        foreach (var bridge in map.Bridges)
        {
            if (!map.Plays(bridge.From) || !map.Plays(bridge.To))
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Bridge at {Text(bridge.From)} is outside the playable area", bridge.From));
            }
        }

        for (var i = 0; i < map.Ore.Length; i++)
        {
            var cell = new CellPos(i % map.Width, i / map.Width);
            if (map.Ore[i] != 0 && !map.Plays(cell))
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Ore at {Text(cell)} is outside the playable area", cell));
                break;
            }
        }
    }

    public static bool HasErrors(IReadOnlyList<MapProblem> problems)
    {
        foreach (var problem in problems)
        {
            if (problem.Level == ProblemLevel.Error)
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckStarts(MapDefinition map, MapGrid grid, List<MapProblem> problems)
    {
        if (map.Starts.Length != map.Players)
        {
            problems.Add(new MapProblem(ProblemLevel.Error, $"{map.Starts.Length} starts for {map.Players} players", null));
        }

        foreach (var start in map.Starts)
        {
            var cell = start.Cell;
            if (!grid.CanEnter(cell, Locomotor.Track))
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Start {start.Slot} at {Text(cell)} is on ground tracks cannot stand on", cell));
            }
            else if (cell.X < EdgeMargin || cell.Y < EdgeMargin || cell.X >= map.Width - EdgeMargin || cell.Y >= map.Height - EdgeMargin
                     || map.Playable?.Margin(cell) < EdgeMargin)
            {
                problems.Add(new MapProblem(ProblemLevel.Error, $"Start {start.Slot} at {Text(cell)} is closer than {EdgeMargin} cells to the edge", cell));
            }
        }

        if (map.Starts.Length < 2 || !grid.CanEnter(map.Starts[0].Cell, Locomotor.Track))
        {
            return;
        }

        var reached = Reachable(grid, map.Starts[0].Cell, Locomotor.Track);
        bool[]? sailed = null;
        for (var i = 1; i < map.Starts.Length; i++)
        {
            var cell = map.Starts[i].Cell;
            if (!grid.CanEnter(cell, Locomotor.Track) || reached[cell.Y * grid.Width + cell.X])
            {
                continue;
            }

            sailed ??= Reachable(grid, map.Starts[0].Cell, Locomotor.Amphibious);
            problems.Add(sailed[cell.Y * grid.Width + cell.X]
                ? new MapProblem(ProblemLevel.Warning, $"Start {map.Starts[i].Slot} at {Text(cell)} is reached from start {map.Starts[0].Slot} only over the water", cell)
                : new MapProblem(ProblemLevel.Error, $"Start {map.Starts[i].Slot} at {Text(cell)} cannot be reached from start {map.Starts[0].Slot} by tracks", cell));
        }
    }

    /// <summary>Every cell <paramref name="locomotor"/> reaches from <paramref name="from"/>: eight directions, no corner cutting, as the path search moves.</summary>
    private static bool[] Reachable(MapGrid grid, CellPos from, Locomotor locomotor)
    {
        var reached = new bool[grid.Width * grid.Height];
        var queue = new Queue<CellPos>();
        reached[from.Y * grid.Width + from.X] = true;
        queue.Enqueue(from);
        while (queue.TryDequeue(out var cell))
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = new CellPos(cell.X + dx, cell.Y + dy);
                    if ((dx == 0 && dy == 0) || !grid.CanEnter(next, locomotor) || reached[next.Y * grid.Width + next.X])
                    {
                        continue;
                    }

                    if (dx != 0 && dy != 0
                        && (!grid.CanEnter(new CellPos(cell.X + dx, cell.Y), locomotor) || !grid.CanEnter(new CellPos(cell.X, cell.Y + dy), locomotor)))
                    {
                        continue;
                    }

                    reached[next.Y * grid.Width + next.X] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }

    private static void CheckOre(MapDefinition map, List<MapProblem> problems)
    {
        var amounts = new List<int>();
        foreach (var start in map.Starts)
        {
            var amount = 0;
            for (var y = start.Cell.Y - OreRadius; y <= start.Cell.Y + OreRadius; y++)
            {
                for (var x = start.Cell.X - OreRadius; x <= start.Cell.X + OreRadius; x++)
                {
                    var (dx, dy) = (x - start.Cell.X, y - start.Cell.Y);
                    if (map.Contains(new CellPos(x, y)) && dx * dx + dy * dy <= OreRadius * OreRadius)
                    {
                        amount += map.Ore[y * map.Width + x];
                    }
                }
            }

            amounts.Add(amount);
            if (amount == 0)
            {
                problems.Add(new MapProblem(ProblemLevel.Warning, $"No ore within {OreRadius} cells of start {start.Slot}", start.Cell));
            }
        }

        if (amounts.Count >= 2 && Spread(amounts) is var (min, max) && max > 0 && (max - min) * 100 > BalancePercent * max)
        {
            problems.Add(new MapProblem(ProblemLevel.Warning, $"Ore near the starts differs by more than {BalancePercent} %: {min} to {max}", null));
        }
    }

    // M8 step 10b: as many drills of each kind within OreRadius of every start — the fields that grow back are fair too.
    private static void CheckDrills(MapDefinition map, List<MapProblem> problems)
    {
        foreach (var kind in new[] { MapObjectKind.OreSource, MapObjectKind.GemSource })
        {
            var counts = new List<int>();
            foreach (var start in map.Starts)
            {
                counts.Add(map.Objects.Count(placement =>
                    MapObjectTypes.TryGetById(placement.Type, out var type) && type.Kind == kind
                    && (placement.Cell.X - start.Cell.X) * (placement.Cell.X - start.Cell.X) + (placement.Cell.Y - start.Cell.Y) * (placement.Cell.Y - start.Cell.Y) <= OreRadius * OreRadius));
            }

            if (counts.Count >= 2 && Spread(counts) is var (min, max) && min != max)
            {
                var name = kind == MapObjectKind.OreSource ? "ore drills" : "gem drills";
                problems.Add(new MapProblem(ProblemLevel.Warning, $"The starts have {min} to {max} {name} within {OreRadius} cells", null));
            }
        }
    }

    private static void CheckDistances(MapDefinition map, List<MapProblem> problems)
    {
        if (map.Starts.Length < 3)
        {
            return;
        }

        // Squared distances: d_min < (1 − 15 %) · d_max ⇔ d_min² · 100² < (100 − 15)² · d_max².
        var nearest = new List<int>();
        foreach (var start in map.Starts)
        {
            var best = int.MaxValue;
            foreach (var other in map.Starts)
            {
                if (other.Slot != start.Slot)
                {
                    var (dx, dy) = (other.Cell.X - start.Cell.X, other.Cell.Y - start.Cell.Y);
                    best = IntMath.Min(best, dx * dx + dy * dy);
                }
            }

            nearest.Add(best);
        }

        var (min, max) = Spread(nearest);
        if ((long)min * 100 * 100 < (long)(100 - BalancePercent) * (100 - BalancePercent) * max)
        {
            problems.Add(new MapProblem(ProblemLevel.Warning, $"The starts are not about as far from their nearest opponents (within {BalancePercent} %)", null));
        }
    }

    private static (int Min, int Max) Spread(List<int> values)
    {
        var (min, max) = (int.MaxValue, int.MinValue);
        foreach (var value in values)
        {
            (min, max) = (IntMath.Min(min, value), IntMath.Max(max, value));
        }

        return (min, max);
    }

    private static string Text(CellPos cell) => $"{cell.X},{cell.Y}";
}
