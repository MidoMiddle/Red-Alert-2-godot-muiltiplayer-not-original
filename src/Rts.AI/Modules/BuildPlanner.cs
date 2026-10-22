using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The base of a bot, as far as step 4 needs it (docs/implementation/M5_first_bot.md, 4.6): it deploys the MCV — where it stands, or
/// first on the nearest cell within <see cref="DeployReach"/> where the yard fits as far as the bot has seen; with none seen it drives
/// <see cref="ScoutStep"/> cells toward the middle of the map to see more — then builds what <see cref="AiPlan.Wants"/> asks for:
/// with the building queue empty it orders the first wanted building it may build and can pay for, and it places a ready one where the
/// end-to-end player of M3 would (<c>EconomyRun.Spot</c>): near the yard, where the rules allow and a ring of cells stays free, a refinery
/// as near the explored ore as it can. It gives no command it knows to be refused: no place, no order. Step 5 brings the build order
/// and the placement that weighs the passages.
/// </summary>
public sealed class BuildPlanner : IAiModule
{
    /// <summary>How far, in cells, the MCV may go to find flat ground for its yard.</summary>
    public const int DeployReach = 6;

    /// <summary>How far the MCV drives to see more ground when it found none for its yard.</summary>
    public const int ScoutStep = 4;

    /// <summary>How far from the middle of the yard, in cells, buildings go.</summary>
    public const int BaseReach = 14;

    public AiModuleKind Kind => AiModuleKind.BuildPlanner;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var yardIndex = rules.IndexOfBuilding(view.Player.Faction.ConstructionYard);
        Building? yard = null;
        foreach (var building in view.OwnBuildings)
        {
            if (building.DefIndex == yardIndex)
            {
                yard = building;
                break;
            }
        }

        if (yard is null)
        {
            Deploy(context, rules.Buildings[yardIndex]);
            return;
        }

        var queue = view.Player.Queue(ProductionCategory.Building);
        if (queue is [{ Ready: true } ready, ..])
        {
            if (Spot(view, yard, rules.Buildings[ready.DefIndex]) is { } spot)
            {
                context.Issue(new PlaceBuildingCommand(view.Me, context.ExecuteTick, context.NextSequence(), ready.DefIndex, spot));
            }

            return;
        }

        if (queue.Count > 0)
        {
            return;
        }

        foreach (var wanted in context.Plan.Wants)
        {
            var def = rules.Buildings[wanted];
            if (def.Category == ProductionCategory.Building && view.Player.CanBuildBuilding(wanted) && def.Cost <= view.Player.Credits)
            {
                context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Building, wanted, 1));
                return;
            }
        }
    }

    /// <summary>The MCV deploys where its yard fits, or drives to the nearest cell where it does; it waits while it drives.</summary>
    private static void Deploy(AiContext context, BuildingDef yard)
    {
        var view = context.View;
        Unit? mcv = null;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Archetype.Id == view.Player.Faction.Mcv)
            {
                mcv = unit;
                break;
            }
        }

        if (mcv is null || mcv.MoveTarget is not null)
        {
            return;
        }

        var here = mcv.Position.ToCell();
        if (view.CanPlace(yard, Corner(here, yard), needsBase: false, ignoreUnit: mcv.Id))
        {
            context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), mcv.Id));
            return;
        }

        for (var ring = 1; ring <= DeployReach; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(here.X + dx, here.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.CanEnter(cell, mcv.Archetype.Locomotor)
                        && view.CanPlace(yard, Corner(cell, yard), needsBase: false, ignoreUnit: mcv.Id))
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }

        // Nothing seen will do: a few cells toward the middle of the map, to the nearest cell it can enter there, to see more ground.
        var middle = new CellPos(view.Width / 2, view.Height / 2);
        var ahead = new CellPos(
            here.X + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.X - here.X)),
            here.Y + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.Y - here.Y)));
        for (var ring = 0; ring <= 2; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(ahead.X + dx, ahead.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && cell != here && view.CanEnter(cell, mcv.Archetype.Locomotor))
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }
    }

    /// <summary>The north-west cell of <paramref name="def"/> centred on <paramref name="centre"/>, as a deploy places the yard.</summary>
    private static CellPos Corner(CellPos centre, BuildingDef def) =>
        new(centre.X - (def.Footprint.Width - 1) / 2, centre.Y - (def.Footprint.Height - 1) / 2);

    /// <summary>
    /// The north-west cell for <paramref name="def"/> near <paramref name="yard"/> where the rules allow it and no own building stands in the
    /// ring round it; nearest the yard, a refinery nearest the explored ore; equal ones go to the smaller (Y, X). Null when there is none.
    /// </summary>
    private static CellPos? Spot(AiWorldView view, Building yard, BuildingDef def)
    {
        var refinery = def.DockCell is not null;
        var ore = refinery ? OreCells(view) : [];
        var centre = new CellPos(yard.Cell.X + yard.Width / 2, yard.Cell.Y + yard.Height / 2);
        CellPos? best = null;
        var bestScore = long.MaxValue;
        for (var y = centre.Y - BaseReach; y <= centre.Y + BaseReach; y++)
        {
            for (var x = centre.X - BaseReach; x <= centre.X + BaseReach; x++)
            {
                var corner = new CellPos(x, y);
                if (!Clear(view, corner, def) || !view.CanPlace(def, corner))
                {
                    continue;
                }

                var middleX = x + def.Footprint.Width / 2;
                var middleY = y + def.Footprint.Height / 2;
                long score;
                if (refinery && ore.Count > 0)
                {
                    score = long.MaxValue;
                    foreach (var cell in ore)
                    {
                        var distance = Square(cell.X - middleX) + Square(cell.Y - middleY);
                        score = distance < score ? distance : score;
                    }
                }
                else
                {
                    score = Square(middleX - centre.X) + Square(middleY - centre.Y);
                }

                score = score * 10_000 + y * 100 + x;
                if (score < bestScore)
                {
                    (best, bestScore) = (corner, score);
                }
            }
        }

        return best;
    }

    /// <summary>No own building in the ring of cells round the footprint, so that exits and docks are not walled in.</summary>
    private static bool Clear(AiWorldView view, CellPos corner, BuildingDef def)
    {
        var (left, top) = (corner.X - 1, corner.Y - 1);
        var (right, bottom) = (corner.X + def.Footprint.Width, corner.Y + def.Footprint.Height);
        foreach (var building in view.OwnBuildings)
        {
            if (building.Cell.X <= right && building.FarCell.X >= left && building.Cell.Y <= bottom && building.FarCell.Y >= top)
            {
                return false;
            }
        }

        return true;
    }

    private static List<CellPos> OreCells(AiWorldView view)
    {
        var cells = new List<CellPos>();
        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                if (!view.OreAt(new CellPos(x, y)).IsEmpty)
                {
                    cells.Add(new CellPos(x, y));
                }
            }
        }

        return cells;
    }

    private static long Square(int value) => (long)value * value;
}
