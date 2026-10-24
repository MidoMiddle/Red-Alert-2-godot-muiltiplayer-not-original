namespace Rts.Client.Controls;

/// <summary>The cursors of the game (docs/implementation/M2_unit_control.md, 4.14; M3_base_economy.md, 4.17).</summary>
public enum CursorKind
{
    Normal,
    Select,
    Move,
    Forbidden,
    AttackMove,
    Waypoint,

    /// <summary>Over an enemy unit or building with a selection: an order attacks it (M4 step 7).</summary>
    Attack,

    /// <summary>Ctrl with a selection: an order forces an attack on what is under the pointer (M4 step 7).</summary>
    ForceAttack,

    /// <summary>The sell mode over an own building.</summary>
    Sell,

    /// <summary>The repair mode over an own damaged building.</summary>
    Repair,

    /// <summary>Over a selected MCV that can deploy where it stands.</summary>
    Deploy,

    /// <summary>Over ore while harvesters are selected: they go to work there.</summary>
    Harvest,

    /// <summary>
    /// Over a building a selected unit can go into: an own bunker with room for soldiers (stage 2e), a building for an engineer to take
    /// or mend, or for an infiltrator (stage 3b); or over an own transport a selected unit can board (stage 3d).
    /// </summary>
    Enter,
}

/// <summary>
/// What lies under the pointer beyond the unit picking of <see cref="PointerContext"/>, for the cursors of the base (M3 step 13).
/// </summary>
/// <param name="OwnBuilding">An own building of a player stands on the cell.</param>
/// <param name="Damaged">That building has lost hit points.</param>
/// <param name="Ore">The cell holds ore or gems.</param>
/// <param name="Harvesters">The selection holds a harvester.</param>
/// <param name="DeployFits">The MCV under the pointer (<see cref="PointerContext.TargetDeploys"/>) can deploy where it stands.</param>
/// <param name="Enter">A building one of the selection can go into (stage 2e, 3b): an order there sends it in.</param>
/// <param name="Bridge">A standing bridge segment one of the selection can blow up (the commando's C4): an order there plants it.</param>
/// <param name="Repairable">A tech building of the map the player took (an oil derrick…): the repair mode mends it, the sell mode not.</param>
public readonly record struct PointerSpot(
    bool OwnBuilding = false,
    bool Damaged = false,
    bool Ore = false,
    bool Harvesters = false,
    bool DeployFits = false,
    bool Enter = false,
    bool Bridge = false,
    bool Repairable = false);

/// <summary>Which cursor to show for what is under the pointer. Pure, tested in Rts.Simulation.Tests.</summary>
public static class CursorChoice
{
    /// <param name="context">The pointer context, as the interpreter would get it on a press.</param>
    /// <param name="anyCanEnter">Whether at least one selected unit can enter the cell under the pointer.</param>
    /// <param name="tool">The armed tool: sell and repair have their own cursors, a placement keeps the arrow (the preview shows it).</param>
    /// <param name="spot">What lies under the pointer for the cursors of the base.</param>
    public static CursorKind For(PointerContext context, bool anyCanEnter, PointerTool tool = PointerTool.None, PointerSpot spot = default)
    {
        switch (tool)
        {
            case PointerTool.Sell:
                return spot.OwnBuilding ? CursorKind.Sell : CursorKind.Forbidden;
            case PointerTool.Repair:
                return (spot.OwnBuilding || spot.Repairable) && spot.Damaged ? CursorKind.Repair : CursorKind.Forbidden;
            case PointerTool.Build:
                return CursorKind.Normal;
            case PointerTool.Superweapon:
                return context.Ground is null ? CursorKind.Forbidden : CursorKind.Attack;
        }

        var groundOrder = context.WaypointMode || (context.Ctrl && context.Shift);
        if (context.TargetDeploys && !groundOrder && !context.Shift && !context.Ctrl)
        {
            return spot.DeployFits ? CursorKind.Deploy : CursorKind.Forbidden;
        }

        if (context.HasSelection && context.Ctrl && !groundOrder
            && (context.Ground is not null || context.Target.Kind is PointerTargetKind.OwnUnit or PointerTargetKind.OtherUnit))
        {
            return CursorKind.ForceAttack;
        }

        if (context.HasSelection && spot.Enter && !groundOrder && !context.Ctrl && context.Target.Kind != PointerTargetKind.OtherUnit)
        {
            return CursorKind.Enter;
        }

        if (context.Target.Kind == PointerTargetKind.OwnUnit && !groundOrder)
        {
            return CursorKind.Select;
        }

        if (context.HasSelection && spot.Bridge && !groundOrder && !context.Ctrl && context.Target.Kind == PointerTargetKind.Ground)
        {
            return CursorKind.Attack;
        }

        if (context.HasSelection && !groundOrder && context.Target.Kind is PointerTargetKind.OtherUnit or PointerTargetKind.OtherBuilding)
        {
            return CursorKind.Attack;
        }

        if (!context.HasSelection)
        {
            return CursorKind.Normal;
        }

        if (context.Ground is null || !anyCanEnter)
        {
            return CursorKind.Forbidden;
        }

        if (context.WaypointMode)
        {
            return CursorKind.Waypoint;
        }

        if (context.Ctrl && context.Shift)
        {
            return CursorKind.AttackMove;
        }

        return spot.Ore && spot.Harvesters ? CursorKind.Harvest : CursorKind.Move;
    }
}
