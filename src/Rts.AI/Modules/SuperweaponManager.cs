using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The superweapons of a bot (M11 step 3, the bot that knows every weapon): each of its own that may fire (<see cref="AiWorldView.ReadySuperweapon"/>)
/// fires where it pays, once there is such a place; a superweapon fires at a cell, in the fog too.
/// <list type="bullet">
/// <item><b>The missile and the storm</b>: at the remembered enemy building round which the most is lost to the blast — the costs of the
/// enemy buildings the bot remembers and of the enemy units in sight within <see cref="BlastCells"/> of it — at least
/// <see cref="MinStrikeWorth"/>, with nothing of the bot's or its allies' within the blast and a cell more. The storm not while one
/// rages.</item>
/// <item><b>The curtain</b>: on the 3 × 3 cells of a squad that fights with the most of its vehicles in them, at least
/// <see cref="MinGroupWorth"/>, and none of its soldiers — the curtain kills them.</item>
/// <item><b>The gate</b>: the 3 × 3 cells of the vehicles of a squad that is out, <see cref="FarCells"/> or more from its target and with
/// none of its soldiers in them, are moved <see cref="GateShortCells"/> short of the target, onto land a tank can stand on.</item>
/// <item><b>The paradrop</b>: on the enemy refinery the bot remembers nearest its start, else on its nearest enemy building.</item>
/// </list>
/// </summary>
public sealed class SuperweaponManager : IAiModule
{
    /// <summary>The fewest credits a missile or a storm must take, in the costs of what it hits.</summary>
    public const int MinStrikeWorth = 3000;

    /// <summary>The fewest credits of vehicles the curtain or the gate is spent on.</summary>
    public const int MinGroupWorth = 2000;

    /// <summary>A squad this far from its target, in cells, is moved by the gate.</summary>
    public const int FarCells = 24;

    /// <summary>How far short of its target the gate puts a squad, in cells.</summary>
    public const int GateShortCells = 6;

    /// <summary>The reach of a missile or a storm without a warhead to tell it, in cells (RA2: 10 for both).</summary>
    public const int BlastCells = SuperweaponSystem.StormSpread;

    public AiModuleKind Kind => AiModuleKind.Superweapons;

    public void Update(AiContext context)
    {
        var view = context.View;
        foreach (var building in view.OwnBuildings)
        {
            var kind = view.ReadySuperweapon(building);
            (CellPos Target, CellPos Second)? shot = kind switch
            {
                SuperweaponKind.Missile => Strike(context, Blast(view, building)),
                SuperweaponKind.Storm when !view.StormRaging() => Strike(context, Blast(view, building)),
                SuperweaponKind.Curtain => Curtain(context),
                SuperweaponKind.Gate => Gate(context),
                SuperweaponKind.Paradrop => Paradrop(context),
                _ => null,
            };
            if (shot is { } fire)
            {
                context.Issue(new SuperweaponCommand(view.Me, context.ExecuteTick, context.NextSequence(), building.Id, fire.Target, fire.Second));
                context.Log?.Add(context.Tick, context.Module, $"fires {kind} at {fire.Target.X},{fire.Target.Y}");
            }
        }
    }

    /// <summary>The reach of the blast of <paramref name="building"/>'s weapon: its warhead's spread, else <see cref="BlastCells"/>.</summary>
    private static int Blast(AiWorldView view, Building building)
    {
        var database = view.Rules.Database;
        if (building.Def is { } def && database.IndexOfWeapon(def.SuperweaponWeapon) is >= 0 and var weapon
            && database.IndexOfWarhead(database.Weapons[weapon].Warhead) is >= 0 and var warhead && database.Warheads[warhead].Spread > Fixed.Zero)
        {
            return database.Warheads[warhead].Spread.ToIntFloor();
        }

        return BlastCells;
    }

    /// <summary>The remembered enemy building round which a blast of <paramref name="reach"/> takes the most; null below the least worth it.</summary>
    internal static (CellPos, CellPos)? Strike(AiContext context, int reach)
    {
        var view = context.View;
        var database = view.Rules.Database;
        CellPos? best = null;
        var bestWorth = MinStrikeWorth - 1;
        foreach (var centre in context.Memory.Buildings)
        {
            var at = new CellPos((centre.Cell.X + centre.FarCell.X) / 2, (centre.Cell.Y + centre.FarCell.Y) / 2);
            if (Friends(view, at, reach + 1))
            {
                continue;
            }

            var worth = 0;
            foreach (var building in context.Memory.Buildings)
            {
                worth += Distance(building.Cell, at) <= reach && building.DefIndex >= 0 ? database.Buildings[building.DefIndex].Cost : 0;
            }

            foreach (var enemy in view.VisibleEnemies)
            {
                worth += enemy is Unit unit && Distance(unit.Position.ToCell(), at) <= reach ? database.Units[database.IndexOfUnit(unit.Archetype.Id)].Cost : 0;
            }

            if (worth > bestWorth)
            {
                (best, bestWorth) = (at, worth);
            }
        }

        return best is { } target ? (target, target) : null;
    }

    /// <summary>Whether a unit or a building of the bot or an ally stands within <paramref name="reach"/> of <paramref name="cell"/>.</summary>
    private static bool Friends(AiWorldView view, CellPos cell, int reach)
    {
        foreach (var unit in view.OwnUnits)
        {
            if (Distance(unit.Position.ToCell(), cell) <= reach)
            {
                return true;
            }
        }

        foreach (var building in view.OwnBuildings)
        {
            if (Distance(Nearest(building.Cell, building.FarCell, cell), cell) <= reach)
            {
                return true;
            }
        }

        foreach (var ally in view.Allied)
        {
            var at = ally is Building building ? Nearest(building.Cell, building.FarCell, cell) : ((Unit)ally).Position.ToCell();
            if (Distance(at, cell) <= reach)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The 3 × 3 cells of a fighting squad worth the most in vehicles, none of its soldiers in them.</summary>
    private static (CellPos, CellPos)? Curtain(AiContext context)
    {
        foreach (var squad in context.Plan.Squads)
        {
            if (squad.State == SquadState.Fighting && Group(context, squad) is { } group)
            {
                return (group, group);
            }
        }

        return null;
    }

    /// <summary>The vehicles of a squad far out on its way, moved short of its target onto land a tank stands on.</summary>
    private static (CellPos, CellPos)? Gate(AiContext context)
    {
        var view = context.View;
        foreach (var squad in context.Plan.Squads)
        {
            if (squad.State != SquadState.Moving || TargetOf(context, squad) is not { } target || Distance(squad.Centre, target) < FarCells
                || Group(context, squad) is not { } group)
            {
                continue;
            }

            // Short of the target on the way from the squad: the first cell back from there a tank can stand on, with its 3 x 3 too.
            var (dx, dy) = (squad.Centre.X - target.X, squad.Centre.Y - target.Y);
            var length = IntMath.Max(IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)), 1);
            for (var back = GateShortCells; back < length; back++)
            {
                var cell = new CellPos(target.X + dx * back / length, target.Y + dy * back / length);
                if (Stands(view, cell))
                {
                    return (group, cell);
                }
            }
        }

        return null;
    }

    /// <summary>Where <paramref name="squad"/> goes: the building it attacks as the bot remembers it, else its cell; null for neither.</summary>
    private static CellPos? TargetOf(AiContext context, Squad squad)
    {
        foreach (var building in context.Memory.Buildings)
        {
            if (building.Id == squad.TargetBuilding)
            {
                return building.Cell;
            }
        }

        return squad.TargetCell;
    }

    /// <summary>The middle of the 3 × 3 cells of <paramref name="squad"/> worth the most in vehicles, none of its soldiers in them.</summary>
    private static CellPos? Group(AiContext context, Squad squad)
    {
        var view = context.View;
        var database = view.Rules.Database;
        var members = new List<Unit>();
        foreach (var unit in view.OwnUnits)
        {
            foreach (var id in squad.Units)
            {
                if (id == unit.Id)
                {
                    members.Add(unit);
                }
            }
        }

        CellPos? best = null;
        var bestWorth = MinGroupWorth - 1;
        foreach (var middle in members)
        {
            var at = middle.Position.ToCell();
            var (worth, soldiers) = (0, false);
            foreach (var unit in view.OwnUnits)
            {
                if (Distance(unit.Position.ToCell(), at) <= 1)
                {
                    soldiers |= unit.Archetype.Category == UnitCategory.Infantry;
                    worth += unit.Archetype.Category is UnitCategory.Vehicle or UnitCategory.Naval
                        ? database.Units[database.IndexOfUnit(unit.Archetype.Id)].Cost : 0;
                }
            }

            if (!soldiers && worth > bestWorth)
            {
                (best, bestWorth) = (at, worth);
            }
        }

        return best;
    }

    /// <summary>Whether every cell of the 3 × 3 round <paramref name="middle"/> is land a tank stands on, as the map shows it.</summary>
    private static bool Stands(AiWorldView view, CellPos middle)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!view.CanEnterOnPreview(new CellPos(middle.X + dx, middle.Y + dy), Locomotor.Track))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The enemy refinery nearest the start, else the nearest enemy building the bot remembers; null for none.</summary>
    private static (CellPos, CellPos)? Paradrop(AiContext context)
    {
        var view = context.View;
        var from = view.Start ?? new CellPos(view.Width / 2, view.Height / 2);
        RememberedBuilding? best = null;
        var (bestRefinery, bestDistance) = (false, int.MaxValue);
        foreach (var building in context.Memory.Buildings)
        {
            var refinery = building.DefIndex >= 0 && view.Rules.Database.Buildings[building.DefIndex].DockCell is not null;
            var distance = Distance(building.Cell, from);
            if ((refinery && !bestRefinery) || (refinery == bestRefinery && distance < bestDistance))
            {
                (best, bestRefinery, bestDistance) = (building, refinery, distance);
            }
        }

        return best is { } target ? (target.Cell, target.Cell) : null;
    }

    private static CellPos Nearest(CellPos from, CellPos to, CellPos cell) =>
        new(IntMath.Clamp(cell.X, from.X, to.X), IntMath.Clamp(cell.Y, from.Y, to.Y));

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
