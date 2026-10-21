namespace Rts.Client.Controls;

/// <summary>The cursors of the game (docs/implementation/M2_unit_control.md, 4.14).</summary>
public enum CursorKind
{
    Normal,
    Select,
    Move,
    Forbidden,
    AttackMove,
    Waypoint,
}

/// <summary>Which cursor to show for what is under the pointer. Pure, tested in Rts.Simulation.Tests.</summary>
public static class CursorChoice
{
    /// <param name="context">The pointer context, as the interpreter would get it on a press.</param>
    /// <param name="anyCanEnter">Whether at least one selected unit can enter the cell under the pointer.</param>
    public static CursorKind For(PointerContext context, bool anyCanEnter)
    {
        if (context.Target.Kind == PointerTargetKind.OwnUnit && !context.WaypointMode && !(context.Ctrl && context.Shift))
        {
            return CursorKind.Select;
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

        return context.Ctrl && context.Shift ? CursorKind.AttackMove : CursorKind.Move;
    }
}
