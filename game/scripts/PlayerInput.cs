using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Orders;
using Rts.Client.UI;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Player input of the debug game (docs/implementation/M6_menus_skirmish.md, 4.6). Mouse events go through the
/// <see cref="InputInterpreter"/> of the chosen scheme and come back as intents: select, order, cancel, camera drag. Keys are
/// actions of the key bindings, so the player can change them in the settings. M2 step 13 adds the RA2 controls
/// (docs/implementation/M2_unit_control.md, 4.14): control groups on the digits, S stop, X scatter, Z waypoints, the cursors,
/// the hover glow, order markers and a debug log of the unit "voices".
/// </summary>
public partial class PlayerInput : Node
{
    /// <summary>Screen distance in pixels within which a click picks a unit.</summary>
    private const float PickRadius = 22f;

    /// <summary>Height of the unit cubes: a click picks them at their middle.</summary>
    private const float UnitHeight = 0.25f;

    private SimulationHost _host = null!;
    private RtsCamera _camera = null!;
    private InputInterpreter _interpreter = null!;
    private PauseMenu _pauseMenu = null!;
    private OrderMarkers? _markers;

    /// <summary>How far from the ground point under the pointer a unit can be and still be picked: 2 cells covers a unit's height on screen.</summary>
    private const int PickSearchLeptons = 2 * Rts.Simulation.Geometry.WorldPos.LeptonsPerCell;

    private readonly List<EntityId> _candidates = new();

    /// <summary>The selection box on screen: a thin green frame, symbolic (docs/implementation/conventions.md, section 12).</summary>
    private readonly ReferenceRect _box = new()
    {
        EditorOnly = false,
        BorderColor = new Color(0.4f, 1f, 0.4f),
        BorderWidth = 1.5f,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false,
    };

    /// <summary>The units the player has selected. The unit renderer shows them.</summary>
    public SelectionSet Selection { get; } = new();

    public ControlGroups Groups { get; } = new();

    /// <summary>Waypoints laid out with Z and not sent yet. The route lines show them.</summary>
    public WaypointDraft Waypoints { get; } = new();

    /// <summary>The unit under the pointer, if any. The unit renderer makes it glow faintly.</summary>
    public EntityId? Hovered { get; private set; }

    /// <summary>Map cell under the mouse pointer, or null off the map. Shown on the debug panel.</summary>
    public CellPos? CellUnderPointer { get; private set; }

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _camera = GetNode<RtsCamera>("../CameraRig");
        _pauseMenu = GetNode<PauseMenu>("../PauseMenu");
        var settings = AppSettings.Instance?.Values ?? SettingsValues.Defaults;
        _interpreter = new InputInterpreter(settings.Scheme, settings.CameraDrag);
        var boxLayer = new CanvasLayer { Name = "SelectionBox", Layer = 5 };
        boxLayer.AddChild(_box);
        AddChild(boxLayer);
    }

    public override void _Process(double delta)
    {
        // A unit that died leaves the selection.
        Selection.RemoveWhere(unit => !_host.Simulation.World.TryGet(unit, out var entity) || !entity.IsAlive);
        // Every frame, not on mouse motion: the camera can move under a still pointer.
        var at = ToScreen(GetViewport().GetMousePosition());
        var context = Context(
            at,
            Input.IsKeyPressed(Key.Shift),
            Input.IsKeyPressed(Key.Alt),
            Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Meta));
        CellUnderPointer = context.Ground?.ToCell();
        Hovered = context.Target.Kind == PointerTargetKind.Ground ? null : context.Target.Unit;
        // Over the sidebar and the menus the plain arrow.
        CursorSet.Show(GetViewport().GuiGetHoveredControl() is null ? CursorChoice.For(context, AnyCanEnter(context.Ground)) : CursorKind.Normal);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle } mouse:
                HandleButton(mouse);
                break;
            case InputEventMouseMotion motion:
                Apply(_interpreter.Move(ToScreen(motion.Position)));
                break;
            case InputEventKey key:
                HandleKey(key);
                break;
        }
    }

    private void HandleButton(InputEventMouseButton mouse)
    {
        var pointer = ToPointerButton(mouse.ButtonIndex);
        var at = ToScreen(mouse.Position);
        var intents = mouse.Pressed
            ? _interpreter.Press(
                pointer,
                at,
                Context(at, mouse.ShiftPressed, mouse.AltPressed, mouse.CtrlPressed || mouse.MetaPressed) with { DoubleClick = mouse.DoubleClick })
            : _interpreter.Release(pointer);
        Apply(intents);
    }

    private void HandleKey(InputEventKey key)
    {
        if (key is { Pressed: true, Echo: false } && key.Keycode is >= Key.Key0 and <= Key.Key9)
        {
            HandleGroupKey((int)(key.Keycode - Key.Key0), key.CtrlPressed || key.MetaPressed, key.ShiftPressed);
        }
        else if (key.IsActionPressed("cancel_selection"))
        {
            Apply(_interpreter.EscapePressed(!Selection.IsEmpty, !Waypoints.IsEmpty));
            // The pause menu must not also see this Escape.
            GetViewport().SetInputAsHandled();
        }
        else if (key.IsActionPressed("pause"))
        {
            _host.Paused = !_host.Paused;
        }
        else if (key.IsActionPressed("step") && _host.Paused)
        {
            _host.StepOnce();
        }
        else if (key.IsActionPressed("speed_up"))
        {
            _host.ChangeSpeed(+1);
        }
        else if (key.IsActionPressed("speed_down"))
        {
            _host.ChangeSpeed(-1);
        }
        else if (key.IsActionPressed("save_replay"))
        {
            _host.SaveReplay();
        }
        else if (key.IsActionPressed("load_replay"))
        {
            _host.StartPlayback();
        }
        else if (key.IsActionPressed("new_session"))
        {
            _host.StartLiveSession();
        }
        else if (key.IsActionPressed("spawn_type"))
        {
            _host.NextSpawnType();
        }
        else if (key.IsActionPressed("select_all"))
        {
            // Debug: every living unit of the player, to try group orders.
            ChangeSelection(() => Selection.ReplaceAll(_host.Simulation.World.Entities
                .Where(e => e is Unit { IsAlive: true } && e.Owner == SimulationHost.LocalPlayer)
                .Select(e => e.Id)));
        }
        else if (key.IsActionPressed("stop") && !Selection.IsEmpty)
        {
            Voice("stopping");
            _host.IssueStop(Selection.Units);
        }
        else if (key.IsActionPressed("scatter") && !Selection.IsEmpty)
        {
            Voice("scattering");
            _host.IssueScatter(Selection.Units);
        }
    }

    /// <summary>Ctrl + digit assigns the group, Shift + digit adds it, the digit selects it; twice quickly also moves the camera there.</summary>
    private void HandleGroupKey(int digit, bool ctrl, bool shift)
    {
        if (ctrl)
        {
            Groups.Assign(digit, Selection.Units);
            GD.Print($"[input] group {digit} = {Selection.Units.Count} units");
            return;
        }

        var members = Groups.Members(digit, IsOwnAlive);
        if (shift)
        {
            ChangeSelection(() => Selection.AddAll(members));
            return;
        }

        if (members.Count == 0)
        {
            return;
        }

        ChangeSelection(() => Selection.ReplaceAll(members));
        if (Groups.Tap(digit, Time.GetTicksMsec()))
        {
            _camera.CenterOn(WorldCoords.ToGodotOnGround(CentreOf(members)));
        }
    }

    /// <summary>Carries out the intents: the selection changes here, orders and spawns go to the host, drags to the camera.</summary>
    private void Apply(IEnumerable<Intent> intents)
    {
        foreach (var intent in intents)
        {
            if (intent is not CameraDragMoved)
            {
                GD.Print($"[input] {intent}");
            }

            switch (intent)
            {
                case SelectUnit select:
                    ChangeSelection(() => Selection.Replace(select.Unit));
                    break;
                case ToggleUnit toggle:
                    ChangeSelection(() => Selection.Toggle(toggle.Unit));
                    break;
                case SelectSameType same:
                    SelectSameTypeOnScreen(same);
                    break;
                case BoxStarted:
                    _box.Visible = true;
                    break;
                case BoxMoved box:
                    _box.Position = new Vector2(MathF.Min(box.From.X, box.To.X), MathF.Min(box.From.Y, box.To.Y));
                    _box.Size = new Vector2(MathF.Abs(box.To.X - box.From.X), MathF.Abs(box.To.Y - box.From.Y));
                    break;
                case BoxSelect box:
                    _box.Visible = false;
                    var inside = BoxSelection.Pick(OwnUnitsOnScreen().Select(u => (u.Unit, u.Point)), box.From, box.To);
                    ChangeSelection(() =>
                    {
                        if (box.Add)
                        {
                            Selection.AddAll(inside);
                        }
                        else
                        {
                            Selection.ReplaceAll(inside);
                        }
                    });
                    break;
                case BoxCancelled:
                    _box.Visible = false;
                    break;
                case ClearSelection:
                    ChangeSelection(Selection.Clear);
                    break;
                case OrderMove order when CanOrder(order.Target):
                    Voice(order.Queue ? "moving (queued)" : "moving");
                    _markers?.Add(order.Target, MarkerKind.Move);
                    _host.IssueMove(Selection.Units, order.Target, order.Queue);
                    break;
                case OrderAttackMove order when CanOrder(order.Target):
                    Voice("attack-moving");
                    _markers?.Add(order.Target, MarkerKind.AttackMove);
                    _host.IssueAttackMove(Selection.Units, order.Target);
                    break;
                case AddWaypoint waypoint when CanOrder(waypoint.Point):
                    Waypoints.Add(waypoint.Point);
                    _markers?.Add(waypoint.Point, MarkerKind.Move);
                    break;
                case FinishWaypoints finish when !_host.IsPlayback:
                    // A last point nobody can reach is left out; the points laid out so far still go.
                    var (points, loop) = Waypoints.Finish(CanOrder(finish.Point) ? finish.Point : null);
                    Voice(loop ? $"on patrol, {points.Length} points" : $"following {points.Length} waypoints");
                    _host.IssueWaypoints(Selection.Units, points, loop);
                    break;
                case CancelWaypoints:
                    Waypoints.Clear();
                    break;
                case SpawnUnit spawn:
                    if (!_host.IsPlayback)
                    {
                        _host.IssueSpawn(spawn.Position);
                    }

                    break;
                case CameraDragStarted started:
                    _camera.BeginDrag(ToVector(started.From));
                    break;
                case CameraDragMoved moved:
                    _camera.DragTo(ToVector(moved.To));
                    break;
                case CameraDragEnded:
                    _camera.EndDrag();
                    break;
                case OpenMenu:
                    _pauseMenu.Open();
                    break;
            }
        }
    }

    /// <summary>
    /// Whether an order to <paramref name="target"/> goes out: not in playback, and at least one selected unit can enter that
    /// cell. Otherwise a red cross shows where the order was refused, as RA2 does under the "no" cursor.
    /// </summary>
    private bool CanOrder(WorldPos target)
    {
        if (_host.IsPlayback)
        {
            return false;
        }

        _markers ??= GetNodeOrNull<OrderMarkers>("../OrderMarkers");
        if (AnyCanEnter(target))
        {
            return true;
        }

        _markers?.Add(target, MarkerKind.Forbidden);
        return false;
    }

    /// <summary>Whether at least one selected unit can enter the cell of <paramref name="point"/>.</summary>
    private bool AnyCanEnter(WorldPos? point)
    {
        if (point is not { } ground)
        {
            return false;
        }

        var world = _host.Simulation.World;
        var cell = ground.ToCell();
        foreach (var id in Selection.Units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit && world.Map.CanEnter(cell, unit.Archetype.Locomotor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Changes the selection; laid-out waypoints belong to the old selection and are dropped.</summary>
    private void ChangeSelection(Action change)
    {
        change();
        Waypoints.Clear();
    }

    /// <summary>A double click: every own unit of the clicked unit's type on screen.</summary>
    private void SelectSameTypeOnScreen(SelectSameType same)
    {
        if (!_host.Simulation.World.TryGet(same.Unit, out var entity) || entity is not Unit unit)
        {
            return;
        }

        var size = GetViewport().GetVisibleRect().Size;
        var picked = BoxSelection.SameTypeOnScreen(OwnUnitsOnScreen(), unit.Type, new ScreenPoint(size.X, size.Y));
        ChangeSelection(() =>
        {
            if (same.Add)
            {
                Selection.AddAll(picked);
            }
            else
            {
                Selection.ReplaceAll(picked);
            }
        });
    }

    /// <summary>
    /// Debug log of the unit "voices" (real voices come in M6): the type of the first selected unit and what it answers.
    /// </summary>
    private void Voice(string answer)
    {
        if (Selection.Units.Count > 0 && _host.Simulation.World.TryGet(Selection.Units[0], out var entity) && entity is Unit unit)
        {
            GD.Print($"[voice] {unit.Archetype.Id}: {answer}");
        }
    }

    private bool IsOwnAlive(EntityId id) =>
        _host.Simulation.World.TryGet(id, out var entity) && entity is Unit { IsAlive: true } && entity.Owner == SimulationHost.LocalPlayer;

    /// <summary>The middle of the units' positions.</summary>
    private WorldPos CentreOf(IReadOnlyList<EntityId> units)
    {
        long x = 0;
        long y = 0;
        var count = 0;
        foreach (var id in units)
        {
            if (_host.Simulation.World.TryGet(id, out var entity) && entity is Unit unit)
            {
                x += unit.Position.X;
                y += unit.Position.Y;
                count++;
            }
        }

        return count == 0 ? default : new WorldPos((int)(x / count), (int)(y / count));
    }

    /// <summary>Living units of the player with their type and the screen point the picking uses, in id order.</summary>
    private IEnumerable<(EntityId Unit, UnitType Type, ScreenPoint Point)> OwnUnitsOnScreen()
    {
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && unit.Owner == SimulationHost.LocalPlayer)
            {
                var screen = _camera.UnprojectPosition(WorldCoords.ToGodotOnGround(unit.Position, UnitHeight));
                yield return (unit.Id, unit.Type, new ScreenPoint(screen.X, screen.Y));
            }
        }
    }

    private PointerContext Context(ScreenPoint at, bool shift, bool alt, bool ctrl) =>
        new(
            GroundAt(at),
            PickTarget(at),
            shift,
            !Selection.IsEmpty,
            alt,
            ctrl,
            WaypointMode: Input.IsActionPressed("waypoint_mode"),
            HasWaypoints: !Waypoints.IsEmpty);

    /// <summary>The unit nearest to the pointer within <see cref="PickRadius"/>, or the ground when none is that close.</summary>
    /// <remarks>
    /// Candidates come from the spatial index around the ground point under the pointer (M2 step 11); off the map, from all units.
    /// </remarks>
    private PointerTarget PickTarget(ScreenPoint at)
    {
        PointerTarget? nearest = null;
        var nearestDistance = PickRadius;
        var world = _host.Simulation.World;
        _candidates.Clear();
        if (GroundAt(at) is { } ground)
        {
            world.Spatial.QueryRadius(world, ground, PickSearchLeptons, _candidates);
        }
        else
        {
            _candidates.AddRange(world.Entities.Select(e => e.Id));
        }

        foreach (var id in _candidates)
        {
            if (!world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            var screen = _camera.UnprojectPosition(WorldCoords.ToGodotOnGround(unit.Position, UnitHeight));
            var distance = at.DistanceTo(new ScreenPoint(screen.X, screen.Y));
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = new PointerTarget(
                    unit.Owner == SimulationHost.LocalPlayer ? PointerTargetKind.OwnUnit : PointerTargetKind.OtherUnit,
                    unit.Id);
            }
        }

        return nearest ?? new PointerTarget(PointerTargetKind.Ground);
    }

    /// <summary>The point on the ground under the pointer, or null when the pointer is off the map.</summary>
    private WorldPos? GroundAt(ScreenPoint at)
    {
        if (!_camera.TryGroundAt(ToVector(at), out var hit))
        {
            return null;
        }

        var point = WorldCoords.FromGodot(hit);
        return _host.Simulation.World.Contains(point) ? point : null;
    }

    private static PointerButton ToPointerButton(MouseButton button) => button switch
    {
        MouseButton.Right => PointerButton.Right,
        MouseButton.Middle => PointerButton.Middle,
        _ => PointerButton.Left,
    };

    private static ScreenPoint ToScreen(Vector2 point) => new(point.X, point.Y);

    private static Vector2 ToVector(ScreenPoint point) => new(point.X, point.Y);
}
