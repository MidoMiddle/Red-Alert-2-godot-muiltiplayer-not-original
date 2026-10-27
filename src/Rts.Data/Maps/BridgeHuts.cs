using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;

namespace Rts.Data.Maps;

/// <summary>
/// The bridge repair huts of a map (loose ends, stage 2): one by every bridge that has none in reach
/// (<see cref="BridgeRepair.HutReach"/>), on the first free cell off ore by its first end (<see cref="MapChecks.HutCandidates"/>) whose
/// images (the generator's symmetry, given as a function) are free too, so the huts stay as fair as the map — or alone where no cell
/// has free images. The generator places
/// them after the derricks; the map tool adds them to the maps made before (<c>add-huts</c>), and the ready maps take them as they
/// are built.
/// </summary>
public static class BridgeHuts
{
    public const string Hut = "tech_bridge_repair_hut";

    /// <summary>The huts the map lacks, each with its <paramref name="images"/> (the cell itself among them), or alone without them.</summary>
    public static List<StructurePlacement> For(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null)
    {
        var ground = MapChecks.BaseGrid(map, types);
        var placed = new List<StructurePlacement>(map.Structures);
        var huts = new List<StructurePlacement>();
        foreach (var bridge in map.Bridges)
        {
            if (placed.Exists(structure => structure.Type == Hut && Near(bridge, structure.Cell)))
            {
                continue;
            }

            // With its images if they are free, or alone when no cell has free images (a thick forest).
            var cells = Place(map, ground, bridge, placed, images) ?? Place(map, ground, bridge, placed, null);
            if (cells is not null)
            {
                placed.AddRange(cells);
                huts.AddRange(cells);
            }
        }

        return huts;
    }

    // The first candidate of the bridge free with its images, and the images; null without one.
    private static List<StructurePlacement>? Place(
        MapDefinition map, Rts.Simulation.Map.MapGrid ground, BridgePlacement bridge, List<StructurePlacement> placed, Func<CellPos, IEnumerable<CellPos>>? images)
    {
        foreach (var candidate in MapChecks.HutCandidates(bridge))
        {
            var cells = new List<StructurePlacement>();
            foreach (var cell in images?.Invoke(candidate) ?? [candidate])
            {
                var image = new StructurePlacement(Hut, cell, 0, 0);
                if (!cells.Contains(image))
                {
                    cells.Add(image);
                }
            }

            var trial = new List<StructurePlacement>(placed);
            var fits = true;
            foreach (var image in cells)
            {
                fits &= map.Contains(image.Cell) && map.Ore[(image.Cell.Y * map.Width) + image.Cell.X] == 0
                    && MapChecks.BuildingProblem(ground, image, trial) is null;
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
    public static MapDefinition AddTo(MapDefinition map, MapTypes types, Func<CellPos, IEnumerable<CellPos>>? images = null)
    {
        var huts = For(map, types, images);
        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, map.Objects, map.Bridges, [.. map.Structures, .. huts],
            map.Lighting, map.Playable, map.Origin);
    }

    // Whether the cell is within the reach of a hut from the bridge.
    private static bool Near(BridgePlacement bridge, CellPos cell) =>
        MapChecks.BridgeCells(bridge).Exists(segment =>
            IntMath.Max(IntMath.Abs(segment.X - cell.X), IntMath.Abs(segment.Y - cell.Y)) <= BridgeRepair.HutReach);
}
