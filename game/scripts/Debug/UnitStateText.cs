using Rts.Simulation.Map;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>Debug panel line of the selected unit's movement state (M2 step 8). Pure text, tested in Rts.Simulation.Tests.</summary>
public static class UnitStateText
{
    public static string Describe(Unit? unit)
    {
        if (unit is null)
        {
            return "unit -";
        }

        if (unit.PathPending)
        {
            return "unit waiting for path";
        }

        if (unit.ResumeTarget is not null)
        {
            return unit.HoldTicks > 0 ? $"unit giving way · hold {unit.HoldTicks}" : "unit giving way";
        }

        return unit.MoveTarget is null ? "unit idle" : $"unit moving · wait {unit.WaitTicks} · replans {unit.Replans}" + Route(unit);
    }

    /// <summary>
    /// <c>aggressive</c>, <c>guard · attacking #16</c>, <c>hold · forced on #3</c>, <c>aggressive · forced on the ground</c>: the stance
    /// of an armed unit and its target, and whether the player ordered it (M4 step 7); empty for an unarmed one.
    /// </summary>
    public static string DescribeFight(Unit unit)
    {
        if (unit.Weapons.Length == 0)
        {
            return "";
        }

        var stance = unit.Stance switch
        {
            Stance.Guard => "guard",
            Stance.HoldPosition => "hold",
            _ => "aggressive",
        };
        var target = unit.Target is { } id ? $"#{id.Value}" : "the ground";
        return unit.TargetOrder switch
        {
            TargetOrder.Attack => $"{stance} · attacking {target}",
            TargetOrder.Force => $"{stance} · forced on {target}",
            _ when unit.Target is not null => $"{stance} · target {target}",
            _ => stance,
        };
    }

    /// <summary><c>sight 8 +2 height</c>: the base sight of the unit's type and what the height of its cell adds (M7 step 8).</summary>
    public static string DescribeSight(Unit unit, MapGrid map)
    {
        var bonus = SightRules.SightOf(unit, map) - unit.Archetype.Sight;
        return bonus == 0 ? $"sight {unit.Archetype.Sight}" : $"sight {unit.Archetype.Sight} +{bonus} height";
    }

    /// <summary><c> · order 2/3 · patrol · attack-move</c> for a unit with a route of several legs; empty for a single order.</summary>
    private static string Route(Unit unit)
    {
        var text = unit.Orders.Length > 1 ? $" · order {unit.OrderIndex + 1}/{unit.Orders.Length}" : "";
        text += unit.LoopOrders ? " · patrol" : "";
        return text + (unit.IsAttackMoving ? " · attack-move" : "");
    }
}
