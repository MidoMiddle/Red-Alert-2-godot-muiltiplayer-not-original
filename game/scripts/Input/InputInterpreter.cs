using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>The mouse buttons the game reads.</summary>
public enum PointerButton
{
    Left,
    Right,
    Middle,
}

/// <summary>What a left click on the field does while a tool is armed instead of selecting and ordering.</summary>
public enum PointerTool
{
    None,

    /// <summary>The debug spawn (B).</summary>
    Spawn,

    /// <summary>Placing a building (V, or a ready square of the sidebar).</summary>
    Build,

    /// <summary>The Sell button of the sidebar.</summary>
    Sell,

    /// <summary>The Repair button of the sidebar.</summary>
    Repair,

    /// <summary>A superweapon button of the sidebar (stage 2d): a left click on the map aims it.</summary>
    Superweapon,
}

/// <summary>What is under the cursor when a button goes down.</summary>
public enum PointerTargetKind
{
    Ground,
    OwnUnit,
    OtherUnit,

    /// <summary>A building of another player, not a wall: a click with a selection attacks it (M4 step 7).</summary>
    OtherBuilding,
}

/// <summary>
/// What is under the cursor. <see cref="Unit"/> means something only for the unit kinds; <see cref="Building"/> is the building on the
/// cell under the cursor whoever owns it, walls too, for an attack or a forced attack (M4 step 7).
/// </summary>
public readonly record struct PointerTarget(PointerTargetKind Kind, EntityId Unit = default, EntityId? Building = null);

/// <summary>A point on the screen, in pixels.</summary>
public readonly record struct ScreenPoint(float X, float Y)
{
    public float DistanceTo(ScreenPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>What the interpreter knows about a press besides the button.</summary>
/// <param name="Ground">The point on the ground under the cursor, or null when the cursor is off the map.</param>
/// <param name="Target">What is under the cursor.</param>
/// <param name="Shift">Whether Shift is held: Shift and a left click add a unit to the selection or take it out, Shift and a box add.</param>
/// <param name="HasSelection">Whether any unit is selected.</param>
/// <param name="Alt">Whether Alt is held: Alt and a left click on the ground spawn a test unit (debug).</param>
/// <param name="Ctrl">Whether Ctrl (or Command) is held: Ctrl + Shift and an order give an attack-move, Ctrl alone a forced attack.</param>
/// <param name="DoubleClick">Whether this press is the second one of a double click.</param>
/// <param name="WaypointMode">Whether the waypoints mode is on (the command bar's flag, or Z held): an order adds a waypoint instead.</param>
/// <param name="HasWaypoints">Whether waypoints are laid out and not sent yet: an order without Z sends them.</param>
/// <param name="TargetDeploys">Whether the unit under the pointer is an own MCV already selected: a click deploys it (M3 step 13).</param>
public readonly record struct PointerContext(
    WorldPos? Ground,
    PointerTarget Target,
    bool Shift,
    bool HasSelection,
    bool Alt = false,
    bool Ctrl = false,
    bool DoubleClick = false,
    bool WaypointMode = false,
    bool HasWaypoints = false,
    bool TargetDeploys = false);

/// <summary>What the player wants. The game applies it; the interpreter knows nothing about units or the camera.</summary>
public abstract record Intent;

public sealed record SelectUnit(EntityId Unit) : Intent;

/// <summary>Add the unit to the selection, or take it out if it is selected (Shift + click).</summary>
public sealed record ToggleUnit(EntityId Unit) : Intent;

/// <summary>Deploy the MCV: a click on an own MCV that is already selected, as in RA2 (M3 step 13).</summary>
public sealed record DeployUnit(EntityId Unit) : Intent;

/// <summary>A selection box started at <paramref name="From"/>: the left button was dragged past <see cref="InputInterpreter.ClickSlop"/>.</summary>
public sealed record BoxStarted(ScreenPoint From) : Intent;

/// <summary>The selection box now spans from <paramref name="From"/> to <paramref name="To"/>.</summary>
public sealed record BoxMoved(ScreenPoint From, ScreenPoint To) : Intent;

/// <summary>The box was released: select own units inside it, or add them to the selection when <paramref name="Add"/> (Shift).</summary>
public sealed record BoxSelect(ScreenPoint From, ScreenPoint To, bool Add) : Intent;

/// <summary>The box was given up (Escape).</summary>
public sealed record BoxCancelled : Intent;

public sealed record ClearSelection : Intent;

/// <summary>Select every own unit of the clicked unit's type on screen (double click); add them when <paramref name="Add"/> (Shift).</summary>
public sealed record SelectSameType(EntityId Unit, bool Add) : Intent;

/// <summary>Move the selection; <paramref name="Queue"/> (Shift) adds the point to the end of the route.</summary>
public sealed record OrderMove(WorldPos Target, bool Queue = false) : Intent;

/// <summary>Attack-move the selection (Ctrl + Shift and an order).</summary>
public sealed record OrderAttackMove(WorldPos Target) : Intent;

/// <summary>Attack the enemy unit or building (an order on it, M4 step 7).</summary>
public sealed record OrderAttack(EntityId Target) : Intent;

/// <summary>Attack anything (Ctrl and an order, M4 step 7): <paramref name="Target"/>, or the <paramref name="Point"/> on the ground without one.</summary>
public sealed record OrderForceAttack(EntityId? Target, WorldPos Point) : Intent;

/// <summary>Lay out one more waypoint (an order with Z held); nothing is sent yet.</summary>
public sealed record AddWaypoint(WorldPos Point) : Intent;

/// <summary>The last waypoint (an order without Z after waypoints): send the route.</summary>
public sealed record FinishWaypoints(WorldPos Point) : Intent;

/// <summary>Leave the waypoints mode and drop the waypoints laid out so far (Escape).</summary>
public sealed record CancelWaypoints : Intent;

public sealed record SpawnUnit(WorldPos Position) : Intent;

/// <summary>
/// Debug (M3 steps 6, 11): place the armed building with its centre on <paramref name="Position"/>; with <paramref name="Line"/>
/// (Shift) a wall line from the last wall.
/// </summary>
public sealed record PlaceBuilding(WorldPos Position, bool Line = false) : Intent;

/// <summary>The sell mode (the Sell button, M3 step 12): sell the player's building at <paramref name="Position"/>.</summary>
public sealed record SellBuilding(WorldPos Position) : Intent;

/// <summary>A left click with a superweapon armed (stage 2d): its target, or the Phase Gate's first or second cell.</summary>
public sealed record AimSuperweapon(WorldPos Position) : Intent;

/// <summary>The repair mode (the Repair button, M3 step 12): turn the repair of the player's building at <paramref name="Position"/> on or off.</summary>
public sealed record RepairBuilding(WorldPos Position) : Intent;

public sealed record CameraDragStarted(ScreenPoint From) : Intent;

public sealed record CameraDragMoved(ScreenPoint To) : Intent;

public sealed record CameraDragEnded : Intent;

public sealed record OpenMenu : Intent;

/// <summary>
/// Turns mouse events into intents for the chosen scheme (docs/implementation/M6_menus_skirmish.md, 4.6). The game reports
/// presses, releases and pointer moves with what is under the cursor, and the interpreter answers with intents. The same
/// events give different intents in Classic and Modern.
/// The left button is followed until it is released (M2 step 12): released in place it is a click (select, Shift + click to
/// add or remove, order or cancel on the ground); dragged past <see cref="ClickSlop"/> it draws a selection box.
/// Orders follow RA2 in both schemes (M2 step 13, docs/implementation/M2_unit_control.md, 4.14): Shift queues a move,
/// Ctrl + Shift attack-moves, Z lays out waypoints and the next order without Z sends them; a double click selects the type.
/// An order on an enemy attacks it, Ctrl and an order force an attack on anything, the ground too (M4 step 7).
/// </summary>
public sealed class InputInterpreter
{
    /// <summary>Pixels the pointer may move before a press counts as a drag instead of a click.</summary>
    public const float ClickSlop = 6f;

    private readonly ControlScheme _scheme;
    private readonly bool _cameraDrag;

    /// <summary>The button whose press is followed until its release: the camera button, or the right button in Classic.</summary>
    private PointerButton? _tracked;
    private ScreenPoint _pressedAt;
    private ScreenPoint _lastAt;
    private bool _moved;
    private bool _dragging;
    private bool _boxing;
    private PointerContext _leftContext;

    public InputInterpreter(ControlScheme scheme, bool cameraDrag)
    {
        _scheme = scheme;
        _cameraDrag = cameraDrag;
    }

    /// <summary>The button that moves the camera: right in Classic, middle in Modern.</summary>
    private PointerButton CameraButton => _scheme == ControlScheme.Classic ? PointerButton.Right : PointerButton.Middle;

    /// <summary>A button went down at <paramref name="at"/>.</summary>
    public IReadOnlyList<Intent> Press(PointerButton button, ScreenPoint at, PointerContext context)
    {
        if (_tracked == button)
        {
            // The same button went down again: its release never came (a control took it). The old gesture ends here,
            // and this press starts over.
            var intents = new List<Intent>(EndGesture());
            intents.AddRange(Start(button, at, context));
            return intents;
        }

        return _tracked is null ? Start(button, at, context) : Array.Empty<Intent>();
    }

    /// <summary>A gesture whose release was lost ends: a box is cancelled, a camera drag stops. The state is cleared.</summary>
    private IReadOnlyList<Intent> EndGesture()
    {
        var intents = new List<Intent>();
        if (_boxing)
        {
            intents.Add(new BoxCancelled());
        }
        else if (_dragging)
        {
            intents.Add(new CameraDragEnded());
        }

        _tracked = null;
        _dragging = false;
        _boxing = false;
        _moved = false;
        return intents;
    }

    /// <summary>
    /// The start of a press: a building, a sale or a repair while that tool is armed (a right click leaves the tool), a spawn while the
    /// debug spawn is armed or Alt is held, else the button's own gesture.
    /// </summary>
    private IReadOnlyList<Intent> Start(PointerButton button, ScreenPoint at, PointerContext context)
    {
        if (Tool is PointerTool.Build or PointerTool.Sell or PointerTool.Repair or PointerTool.Superweapon)
        {
            if (button == PointerButton.Right)
            {
                Tool = PointerTool.None;
                return Array.Empty<Intent>();
            }

            if (button == PointerButton.Left)
            {
                return context.Ground is not { } ground ? Array.Empty<Intent>() : Tool switch
                {
                    PointerTool.Build => new Intent[] { new PlaceBuilding(ground, context.Shift) },
                    PointerTool.Sell => new Intent[] { new SellBuilding(ground) },
                    PointerTool.Superweapon => new Intent[] { new AimSuperweapon(ground) },
                    _ => new Intent[] { new RepairBuilding(ground) },
                };
            }
        }

        if ((context.Alt || SpawnArmed) && button == PointerButton.Left)
        {
            return context.Ground is { } spawnAt && context.Target.Kind == PointerTargetKind.Ground
                ? new Intent[] { new SpawnUnit(spawnAt) }
                : Array.Empty<Intent>();
        }

        switch (button)
        {
            case PointerButton.Left:
                // Click or box: decided when the button is released or the pointer moves away.
                _tracked = button;
                _pressedAt = at;
                _lastAt = at;
                _moved = false;
                _boxing = false;
                _leftContext = context;
                return Array.Empty<Intent>();
            case PointerButton.Right when _scheme == ControlScheme.Classic:
                return Track(button, at);
            case PointerButton.Right:
                return Order(context);
            case PointerButton.Middle when _scheme == ControlScheme.Modern:
                return Track(button, at);
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>
    /// A button went up. The left button finishes its click or box; the camera button ends a drag, or cancels the selection
    /// after a right click in Classic.
    /// </summary>
    public IReadOnlyList<Intent> Release(PointerButton button)
    {
        if (_tracked != button)
        {
            return Array.Empty<Intent>();
        }

        var dragging = _dragging;
        var boxing = _boxing;
        var click = !_moved;
        _tracked = null;
        _dragging = false;
        _boxing = false;
        _moved = false;

        if (button == PointerButton.Left)
        {
            return boxing
                ? new Intent[] { new BoxSelect(_pressedAt, _lastAt, _leftContext.Shift) }
                : ClickLeft(_leftContext);
        }

        if (dragging)
        {
            return new Intent[] { new CameraDragEnded() };
        }

        return click && _scheme == ControlScheme.Classic && button == PointerButton.Right
            ? new Intent[] { new ClearSelection() }
            : Array.Empty<Intent>();
    }

    /// <summary>The pointer moved to <paramref name="at"/>.</summary>
    public IReadOnlyList<Intent> Move(ScreenPoint at)
    {
        if (_tracked is null)
        {
            return Array.Empty<Intent>();
        }

        if (_pressedAt.DistanceTo(at) > ClickSlop)
        {
            _moved = true;
        }

        if (_tracked == PointerButton.Left)
        {
            _lastAt = at;
            if (_boxing)
            {
                return new Intent[] { new BoxMoved(_pressedAt, at) };
            }

            if (!_moved)
            {
                return Array.Empty<Intent>();
            }

            _boxing = true;
            return new Intent[] { new BoxStarted(_pressedAt), new BoxMoved(_pressedAt, at) };
        }

        if (_dragging)
        {
            return new Intent[] { new CameraDragMoved(at) };
        }

        if (_moved && _cameraDrag && _tracked == CameraButton)
        {
            _dragging = true;
            return new Intent[] { new CameraDragStarted(_pressedAt), new CameraDragMoved(at) };
        }

        return Array.Empty<Intent>();
    }

    /// <summary>
    /// The buttons held right now, read once per frame. A gesture whose button is up here lost its release (a control took it),
    /// so it ends now, as its release would have ended it.
    /// </summary>
    public IReadOnlyList<Intent> SyncButtons(bool leftHeld, bool rightHeld, bool middleHeld)
    {
        if (_tracked is not { } tracked)
        {
            return Array.Empty<Intent>();
        }

        var held = tracked switch
        {
            PointerButton.Left => leftHeld,
            PointerButton.Right => rightHeld,
            _ => middleHeld,
        };
        return held ? Array.Empty<Intent>() : Release(tracked);
    }

    /// <summary>What a left click on the field does instead of selecting and ordering; one at a time.</summary>
    public PointerTool Tool { get; private set; }

    /// <summary>Whether the debug spawn is armed (B): a left click on the ground places a test unit until Escape.</summary>
    public bool SpawnArmed => Tool == PointerTool.Spawn;

    /// <summary>
    /// Whether a building is being placed (V, M3 step 6; a ready square of the sidebar, step 12): a left click places it, a right
    /// click or Escape leaves the mode and the building stays ready.
    /// </summary>
    public bool BuildArmed => Tool == PointerTool.Build;

    /// <summary>Arms the debug spawn (B). PlayerInput cycles the unit type when B is pressed again.</summary>
    public void ArmSpawn() => Tool = PointerTool.Spawn;

    /// <summary>Arms the placement of a building (V or the sidebar).</summary>
    public void ArmBuild() => Tool = PointerTool.Build;

    /// <summary>Arms the sell mode (the Sell button): every left click sells, until a right click or Escape.</summary>
    public void ArmSell() => Tool = PointerTool.Sell;

    /// <summary>Arms the repair mode (the Repair button): every left click turns a repair on or off, until a right click or Escape.</summary>
    public void ArmRepair() => Tool = PointerTool.Repair;

    /// <summary>Arms a superweapon (stage 2d): a left click aims it, a right click or Escape lets it be.</summary>
    public void ArmSuperweapon() => Tool = PointerTool.Superweapon;

    /// <summary>Back to selecting and ordering.</summary>
    public void Disarm() => Tool = PointerTool.None;

    /// <summary>
    /// The cancel key (Escape) was pressed. An armed spawn or build is disarmed first and nothing else happens. Otherwise, with units
    /// selected it clears the selection. Without them it opens the pause menu, which takes all input, so a button still held down
    /// is never released in the game: the press is dropped here.
    /// </summary>
    public IReadOnlyList<Intent> EscapePressed(bool hasSelection, bool hasWaypoints = false)
    {
        if (Tool != PointerTool.None)
        {
            Tool = PointerTool.None;
            return Array.Empty<Intent>();
        }

        if (hasWaypoints && _tracked is null)
        {
            // Escape first leaves the waypoints mode and drops the waypoints laid out so far; the selection stays.
            return new Intent[] { new CancelWaypoints() };
        }

        if (_tracked == PointerButton.Left)
        {
            // Escape gives up a pressed left button; a box being drawn disappears.
            var boxing = _boxing;
            _tracked = null;
            _boxing = false;
            _moved = false;
            if (boxing)
            {
                return new Intent[] { new BoxCancelled() };
            }
        }

        if (hasSelection)
        {
            return new Intent[] { new ClearSelection() };
        }

        var intents = new List<Intent>();
        if (_dragging)
        {
            intents.Add(new CameraDragEnded());
        }

        _tracked = null;
        _dragging = false;
        _moved = false;
        intents.Add(new OpenMenu());
        return intents;
    }

    /// <summary>A left click released in place.</summary>
    private IReadOnlyList<Intent> ClickLeft(PointerContext context)
    {
        if (_scheme == ControlScheme.Classic && IsGroundOrder(context))
        {
            // Z or Ctrl + Shift give a point on the ground even over a unit.
            return Order(context);
        }

        if (_scheme == ControlScheme.Classic && AttackOf(context) is { } attack)
        {
            return new Intent[] { attack };
        }

        switch (context.Target.Kind)
        {
            case PointerTargetKind.OwnUnit when context.DoubleClick:
                return new Intent[] { new SelectSameType(context.Target.Unit, context.Shift) };
            case PointerTargetKind.OwnUnit when context.Shift:
                return new Intent[] { new ToggleUnit(context.Target.Unit) };
            case PointerTargetKind.OwnUnit when context.TargetDeploys:
                return new Intent[] { new DeployUnit(context.Target.Unit) };
            case PointerTargetKind.OwnUnit:
                return new Intent[] { new SelectUnit(context.Target.Unit) };
            case PointerTargetKind.Ground when _scheme == ControlScheme.Modern:
                return context.Ground is null ? Array.Empty<Intent>() : new Intent[] { new ClearSelection() };
            case PointerTargetKind.Ground:
                return Order(context);
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>The order modifiers that make a click a point on the ground whatever is under the cursor.</summary>
    private static bool IsGroundOrder(PointerContext context) =>
        context.HasSelection && (context.WaypointMode || (context.Ctrl && context.Shift));

    /// <summary>
    /// An order with the scheme's order button: waypoints, attack-move, a forced attack (Ctrl), an attack on an enemy, a queued or a
    /// plain move.
    /// </summary>
    private static IReadOnlyList<Intent> Order(PointerContext context)
    {
        if (!context.HasSelection)
        {
            return Array.Empty<Intent>();
        }

        if (!context.WaypointMode && !context.HasWaypoints && AttackOf(context) is { } attack)
        {
            return new Intent[] { attack };
        }

        if (context.Ground is not { } point)
        {
            return Array.Empty<Intent>();
        }

        Intent intent = context switch
        {
            { WaypointMode: true } => new AddWaypoint(point),
            { HasWaypoints: true } => new FinishWaypoints(point),
            { Ctrl: true, Shift: true } => new OrderAttackMove(point),
            _ => new OrderMove(point, context.Shift),
        };
        return new Intent[] { intent };
    }

    /// <summary>
    /// The attack an order gives, if any: with Ctrl alone a forced attack on the unit or building under the cursor, or on the ground;
    /// on an enemy unit or building an attack on it. Never in the waypoints mode or with Ctrl + Shift.
    /// </summary>
    private static Intent? AttackOf(PointerContext context)
    {
        if (!context.HasSelection || context.WaypointMode || (context.Ctrl && context.Shift))
        {
            return null;
        }

        if (context.Ctrl)
        {
            EntityId? target = context.Target.Kind is PointerTargetKind.OwnUnit or PointerTargetKind.OtherUnit
                ? context.Target.Unit
                : context.Target.Building;
            return target is not null || context.Ground is not null
                ? new OrderForceAttack(target, context.Ground ?? default)
                : null;
        }

        return context.Target.Kind switch
        {
            PointerTargetKind.OtherUnit => new OrderAttack(context.Target.Unit),
            PointerTargetKind.OtherBuilding when context.Target.Building is { } building => new OrderAttack(building),
            _ => null,
        };
    }

    /// <summary>The camera button went down: follow it until it is released. Modern starts the drag at once, Classic after a move.</summary>
    private IReadOnlyList<Intent> Track(PointerButton button, ScreenPoint at)
    {
        _tracked = button;
        _pressedAt = at;
        _moved = false;
        _dragging = _scheme == ControlScheme.Modern && _cameraDrag;
        return _dragging ? new Intent[] { new CameraDragStarted(at) } : Array.Empty<Intent>();
    }
}
