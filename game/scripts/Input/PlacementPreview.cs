using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>How a cell of the placement preview is shown.</summary>
public enum PreviewState
{
    /// <summary>Green: the building or the segment goes there.</summary>
    Ok,

    /// <summary>Red: the rules refuse it.</summary>
    Refused,

    /// <summary>Grey: a wall segment the credits do not reach.</summary>
    Unpaid,
}

public readonly record struct PreviewCell(CellPos Cell, PreviewState State);

/// <summary>
/// The placement preview (docs/implementation/M3_base_economy.md, 4.17), without Godot: where the footprint under the pointer lies,
/// which of its cells the rules allow, and how a line of walls would go. It asks <see cref="PlacementRules"/>, as the simulation does,
/// so the colours and the decision cannot disagree.
/// </summary>
public static class PlacementPreview
{
    /// <summary>The north-west cell of a footprint whose centre is under <paramref name="position"/>.</summary>
    public static CellPos CornerOf(CellSize footprint, WorldPos position)
    {
        var centre = position.ToCell();
        return new CellPos(centre.X - (footprint.Width - 1) / 2, centre.Y - (footprint.Height - 1) / 2);
    }

    /// <summary>
    /// The cells of a wall line from <paramref name="from"/> (left out: it is built) to <paramref name="to"/>: straight along the longer
    /// axis, or a diagonal when both are equal, <see cref="PlaceWallsCommand.MaxCells"/> at most.
    /// </summary>
    public static ImmutableArray<CellPos> WallLine(CellPos from, CellPos to)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var diagonal = Math.Abs(dx) == Math.Abs(dy);
        var (stepX, stepY) = diagonal ? (Math.Sign(dx), Math.Sign(dy)) : Math.Abs(dx) > Math.Abs(dy) ? (Math.Sign(dx), 0) : (0, Math.Sign(dy));
        var length = Math.Min(Math.Max(Math.Abs(dx), Math.Abs(dy)), PlaceWallsCommand.MaxCells);
        var cells = ImmutableArray.CreateBuilder<CellPos>(length);
        for (var i = 1; i <= length; i++)
        {
            cells.Add(new CellPos(from.X + stepX * i, from.Y + stepY * i));
        }

        return cells.MoveToImmutable();
    }

    /// <summary>
    /// Every cell of the footprint at <paramref name="corner"/>: green when the building may stand there; otherwise each cell by its own
    /// problem, and the whole footprint red when it is too far from the base.
    /// </summary>
    public static IReadOnlyList<PreviewCell> Footprint(GameWorld world, PlayerId player, BuildingDef def, CellPos corner)
    {
        var whole = PlacementRules.Check(world, player, def, corner);
        var map = world.Map;
        var level = map.Contains(corner) ? map.Heights.CellLevel(corner) : 0;
        var cells = new List<PreviewCell>(def.Footprint.Width * def.Footprint.Height);
        for (var y = 0; y < def.Footprint.Height; y++)
        {
            for (var x = 0; x < def.Footprint.Width; x++)
            {
                var cell = new CellPos(corner.X + x, corner.Y + y);
                var refused = whole switch
                {
                    PlacementProblem.None => false,
                    PlacementProblem.TooFar => true,
                    _ => !map.Contains(cell) || !map.Contains(corner) || PlacementRules.CellProblem(world, cell, level) != PlacementProblem.None,
                };
                cells.Add(new PreviewCell(cell, refused ? PreviewState.Refused : PreviewState.Ok));
            }
        }

        return cells;
    }

    /// <summary>
    /// A line of walls as the command would place it: a refused cell is skipped, the first segment takes the ready wall, every next one
    /// is paid; once the credits end, the rest are unpaid. Segments placed earlier in the line are a base for the next ones.
    /// </summary>
    public static IReadOnlyList<PreviewCell> Wall(GameWorld world, PlayerId player, BuildingDef def, IReadOnlyList<CellPos> cells)
    {
        var credits = world.Player(player).Credits;
        var placed = new List<CellPos>();
        var result = new List<PreviewCell>(cells.Count);
        var broke = false;
        foreach (var cell in cells)
        {
            var problem = PlacementRules.Check(world, player, def, cell);
            if (problem == PlacementProblem.TooFar && placed.Exists(other => Gap(other, cell) <= def.Adjacent))
            {
                problem = PlacementProblem.None;
            }

            if (problem != PlacementProblem.None)
            {
                result.Add(new PreviewCell(cell, broke ? PreviewState.Unpaid : PreviewState.Refused));
                continue;
            }

            if (placed.Count > 0)
            {
                broke |= credits < def.Cost;
                credits -= broke ? 0 : def.Cost;
            }

            if (broke)
            {
                result.Add(new PreviewCell(cell, PreviewState.Unpaid));
                continue;
            }

            placed.Add(cell);
            result.Add(new PreviewCell(cell, PreviewState.Ok));
        }

        return result;
    }

    // Cells between two cells, Chebyshev: next to each other is 0.
    private static int Gap(CellPos a, CellPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) - 1;
}
