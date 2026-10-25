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
    /// Over a building a selected unit can go into to sit in it — an own bunker, a civilian building (stage 2e, garrisons) — or over an own
    /// transport a selected unit can board (stage 3d): green arrows pointing in.
    /// </summary>
    Enter,

    /// <summary>Alt with a selection (RA2 force move): an order moves there, even over an enemy.</summary>
    ForceMove,

    /// <summary>Alt over an enemy soldier a selected tank can crush (RA2): an order drives over it.</summary>
    Crush,

    /// <summary>An engineer over a building to take: an enemy's, or a tech building of the neutral side.</summary>
    Capture,

    /// <summary>An engineer over a building to mend: an own damaged one, a damaged civilian one, a bridge repair hut.</summary>
    EngineerRepair,

    /// <summary>An infiltrator over an enemy building to go into (stage 3b).</summary>
    Infiltrate,

    /// <summary>A hurt soldier over its own hospital (the tech hospital): it goes in and comes out whole.</summary>
    Heal,

    /// <summary>
    /// A selected unit that plants a bomb over a target it would bomb: Ivan's bomb on anything, the commando's C4 on a building or a
    /// bridge.
    /// </summary>
    Bomb,

    /// <summary>The armed missile silo over the ground.</summary>
    Nuke,

    /// <summary>The armed storm generator over the ground.</summary>
    Storm,

    /// <summary>The armed phase gate (RA2 Chronosphere) over the ground: what to move, then where.</summary>
    Chrono,

    /// <summary>The armed field projector (RA2 Iron Curtain) over the ground.</summary>
    IronCurtain,

    /// <summary>An armed paradrop over the ground.</summary>
    Paradrop,

    /// <summary>
    /// A building that produces selected, over the ground: a click sets its rally point, where its units go (the owner, 2026-10-25:
    /// the cursor must change while the point is chosen).
    /// </summary>
    Rally,
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
/// <param name="Entry">What the first selected unit that can go into the building or transport under the pointer would do there.</param>
/// <param name="Bomb">A selected unit plants a bomb on the target under the pointer (Ivan, the commando's C4).</param>
/// <param name="Crush">A selected tank can crush the enemy soldier under the pointer.</param>
/// <param name="Superweapon">The kind of the superweapon armed, for its sight.</param>
/// <param name="Rally">A building that produces is selected (no units): an order on the ground sets its rally point.</param>
public readonly record struct PointerSpot(
    bool OwnBuilding = false,
    bool Damaged = false,
    bool Ore = false,
    bool Harvesters = false,
    bool DeployFits = false,
    bool Enter = false,
    bool Bridge = false,
    bool Repairable = false,
    Rts.Simulation.Systems.EntryAction Entry = Rts.Simulation.Systems.EntryAction.None,
    bool Bomb = false,
    bool Crush = false,
    Rts.Simulation.Rules.SuperweaponKind Superweapon = Rts.Simulation.Rules.SuperweaponKind.None,
    bool Rally = false);

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
                // Each superweapon its own sight (RA2).
                return context.Ground is null ? CursorKind.Forbidden : spot.Superweapon switch
                {
                    Rts.Simulation.Rules.SuperweaponKind.Missile => CursorKind.Nuke,
                    Rts.Simulation.Rules.SuperweaponKind.Storm => CursorKind.Storm,
                    Rts.Simulation.Rules.SuperweaponKind.Gate => CursorKind.Chrono,
                    Rts.Simulation.Rules.SuperweaponKind.Curtain => CursorKind.IronCurtain,
                    Rts.Simulation.Rules.SuperweaponKind.Paradrop => CursorKind.Paradrop,
                    _ => CursorKind.Attack,
                };
        }

        if (spot.Rally)
        {
            // An own unit or another of the buildings that produce is picked by a click; an enemy takes no order of a building.
            return context.Target.Kind == PointerTargetKind.OwnUnit || context.OwnFactory is not null ? CursorKind.Select
                : context.Ground is null || context.Target.Kind is PointerTargetKind.OtherUnit or PointerTargetKind.OtherBuilding
                    ? CursorKind.Forbidden
                    : CursorKind.Rally;
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

        if (context.HasSelection && context.Alt && !context.Ctrl && !groundOrder)
        {
            // A forced move (RA2): over an enemy soldier a tank crushes, anywhere a unit can go.
            return spot.Crush ? CursorKind.Crush : context.Ground is not null && anyCanEnter ? CursorKind.ForceMove : CursorKind.Forbidden;
        }

        if (context.HasSelection && spot.Enter && !groundOrder && !context.Ctrl && context.Target.Kind != PointerTargetKind.OtherUnit)
        {
            return spot.Entry switch
            {
                Rts.Simulation.Systems.EntryAction.Capture => CursorKind.Capture,
                Rts.Simulation.Systems.EntryAction.Mend or Rts.Simulation.Systems.EntryAction.RepairBridge => CursorKind.EngineerRepair,
                Rts.Simulation.Systems.EntryAction.Infiltrate => CursorKind.Infiltrate,
                Rts.Simulation.Systems.EntryAction.Heal => CursorKind.Heal,
                Rts.Simulation.Systems.EntryAction.BlowBridge => CursorKind.Bomb,
                _ => CursorKind.Enter,
            };
        }

        if (context.Target.Kind == PointerTargetKind.OwnUnit && !groundOrder)
        {
            return CursorKind.Select;
        }

        if (context.HasSelection && spot.Bridge && !groundOrder && !context.Ctrl && context.Target.Kind == PointerTargetKind.Ground)
        {
            return CursorKind.Bomb;
        }

        if (context.HasSelection && !groundOrder && context.Target.Kind is PointerTargetKind.OtherUnit or PointerTargetKind.OtherBuilding)
        {
            return spot.Bomb ? CursorKind.Bomb : CursorKind.Attack;
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
