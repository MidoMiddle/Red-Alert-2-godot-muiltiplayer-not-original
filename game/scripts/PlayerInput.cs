using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Hud;
using Rts.Client.Orders;
using Rts.Client.Terrain;
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
    /// <summary>Hit points one press of the debug damage key takes off a map object, a bridge segment or a building: a tree falls on the second press, a segment on the third, a house on the fourth.</summary>
    private const int DebugDamage = 200;

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
    // Four cells: a flier is drawn high, so on screen it stands a few cells north of its ground point (stage 2b).
    private const int PickSearchLeptons = 4 * Rts.Simulation.Geometry.WorldPos.LeptonsPerCell;

    private readonly List<EntityId> _candidates = new();

    /// <summary>The double tap of the type select key: a second T within the window takes the whole map.</summary>
    private readonly DoubleTap _typeTaps = new();

    /// <summary>The number the double tap counts the type select key by.</summary>
    private const int TypeSelectKey = 1;

    // The key of P for the double tap.
    private const int CombatSelectKey = 2;

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

    public CameraBookmarks Bookmarks { get; } = new();

    /// <summary>Waypoints laid out with Z or the waypoints button and not sent yet. The route lines show them.</summary>
    public WaypointDraft Waypoints { get; } = new();

    /// <summary>
    /// Whether the waypoints mode is on: the flag of the command bar. Clicks on the field lay out points until the flag is pressed
    /// again or Escape is pressed.
    /// </summary>
    public bool WaypointArmed { get; private set; }

    /// <summary>The unit under the pointer, if any. The unit renderer makes it glow faintly.</summary>
    public EntityId? Hovered { get; private set; }

    /// <summary>Map cell under the mouse pointer, or null off the map. Shown on the debug panel.</summary>
    public CellPos? CellUnderPointer { get; private set; }

    /// <summary>The point of the ground under the mouse pointer, or null off the map: the placement preview follows it.</summary>
    public WorldPos? PointerGround { get; private set; }

    /// <summary>Whether a building is being placed (V or the sidebar): a click places the host's building type.</summary>
    public bool BuildArmed => _interpreter.BuildArmed;

    /// <summary>What a left click on the field does now: select and order, or the armed tool.</summary>
    public PointerTool Tool => _interpreter.Tool;

    /// <summary>The sidebar (M3 step 12): a click on a ready square places that building.</summary>
    public void ArmPlacement(int defIndex)
    {
        _host.ArmPlacement(defIndex);
        _interpreter.ArmBuild();
    }

    // The superweapon armed (stage 2d), and the Phase Gate's first cell once chosen.
    private EntityId _superweapon;
    private CellPos? _gateFrom;

    /// <summary>The superweapon armed, and the Phase Gate's first cell when it waits for the second.</summary>
    public (EntityId Building, CellPos? GateFrom) Superweapon => (_superweapon, _gateFrom);

    /// <summary>A superweapon button: arms it, or lets it be when it is armed.</summary>
    public void ToggleSuperweapon(EntityId building)
    {
        if (Tool == PointerTool.Superweapon && _superweapon == building)
        {
            _interpreter.Disarm();
            return;
        }

        _superweapon = building;
        _gateFrom = null;
        _interpreter.ArmSuperweapon();
    }

    /// <summary>The Sell button: turns the sell mode on, or off when it is on.</summary>
    public void ToggleSell()
    {
        if (Tool == PointerTool.Sell)
        {
            _interpreter.Disarm();
        }
        else
        {
            _interpreter.ArmSell();
        }
    }

    /// <summary>The Repair button: turns the repair mode on, or off when it is on.</summary>
    public void ToggleRepair()
    {
        if (Tool == PointerTool.Repair)
        {
            _interpreter.Disarm();
        }
        else
        {
            _interpreter.ArmRepair();
        }
    }

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _host.EventRaised += Remember;
        _camera = GetNode<RtsCamera>("../CameraRig");
        _pauseMenu = GetNode<PauseMenu>("../PauseMenu");
        var settings = AppSettings.Instance?.Values ?? SettingsValues.Defaults;
        _interpreter = new InputInterpreter(settings.Scheme, settings.CameraDrag);
        AddChild(new PlacementGhost { Name = "PlacementGhost" });
        var boxLayer = new CanvasLayer { Name = "SelectionBox", Layer = 5 };
        boxLayer.AddChild(_box);
        AddChild(boxLayer);
    }

    public override void _Process(double delta)
    {
        // The placement mode of the sidebar ends with its ready building: placed, cancelled or lost.
        if (BuildArmed && !_host.DebugBuild && !_host.PlacementReady)
        {
            _interpreter.Disarm();
        }

        // A unit that died leaves the selection.
        Selection.RemoveWhere(unit => !_host.Simulation.World.TryGet(unit, out var entity) || !entity.IsAlive);
        // The waypoints mode lays out orders for units: with none selected it ends.
        if (Selection.IsEmpty)
        {
            WaypointArmed = false;
        }
        // A gesture whose release never arrived (a control took it) ends here, as the release would have ended it.
        Apply(_interpreter.SyncButtons(
            Input.IsMouseButtonPressed(MouseButton.Left),
            Input.IsMouseButtonPressed(MouseButton.Right),
            Input.IsMouseButtonPressed(MouseButton.Middle)));
        // Every frame, not on mouse motion: the camera can move under a still pointer.
        var at = ToScreen(GetViewport().GetMousePosition());
        var context = Context(
            at,
            Input.IsKeyPressed(Key.Shift),
            Input.IsKeyPressed(Key.Alt),
            Input.IsKeyPressed(Key.Ctrl) || Input.IsKeyPressed(Key.Meta));
        CellUnderPointer = context.Ground?.ToCell();
        PointerGround = context.Ground;
        Hovered = context.Target.Kind == PointerTargetKind.Ground ? null : context.Target.Unit;
        // Over the sidebar and the menus the plain arrow.
        CursorSet.Show(GetViewport().GuiGetHoveredControl() is null
            ? CursorChoice.For(context, AnyCanEnter(context.Ground), Tool, SpotOf(context))
            : CursorKind.Normal);
    }

    /// <summary>
    /// Releases and motion come here, before the GUI, so a release is seen even when a control took the press. Presses stay in
    /// <see cref="_UnhandledInput"/>: a press on the sidebar or the command bar never starts a gesture on the field.
    /// </summary>
    public override void _Input(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle } release:
                Apply(_interpreter.Release(ToPointerButton(release.ButtonIndex)));
                break;
            case InputEventMouseMotion motion:
                Apply(_interpreter.Move(ToScreen(motion.Position)));
                break;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle } press:
                HandlePress(press);
                break;
            case InputEventKey key:
                HandleKey(key);
                break;
        }
    }

    private void HandlePress(InputEventMouseButton mouse)
    {
        var at = ToScreen(mouse.Position);
        var context = Context(at, mouse.ShiftPressed, mouse.AltPressed, mouse.CtrlPressed || mouse.MetaPressed) with { DoubleClick = mouse.DoubleClick };
        Apply(_interpreter.Press(ToPointerButton(mouse.ButtonIndex), at, context));
    }

    private void HandleKey(InputEventKey key)
    {
        if (key is { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            // Space (RA2): the camera goes to the last event of the radar.
            if (_lastEvent is { } point)
            {
                _camera.CenterOn(WorldCoords.ToGodotOnGround(point));
            }
        }
        else if (key is { Pressed: true, Echo: false } && key.Keycode is >= Key.F1 and <= Key.F4)
        {
            HandleBookmarkKey((int)(key.Keycode - Key.F1) + 1, key.CtrlPressed || key.MetaPressed);
        }
        else if (key is { Pressed: true, Echo: false } && key.Keycode is >= Key.Key0 and <= Key.Key9)
        {
            HandleGroupKey((int)(key.Keycode - Key.Key0), key.CtrlPressed || key.MetaPressed, key.ShiftPressed);
        }
        else if (key.IsActionPressed("cancel_selection", false, true))
        {
            Apply(_interpreter.EscapePressed(!Selection.IsEmpty, WaypointArmed || !Waypoints.IsEmpty));
            // The pause menu must not also see this Escape.
            GetViewport().SetInputAsHandled();
        }
        else if (key.IsActionPressed("pause", false, true))
        {
            _host.Paused = !_host.Paused;
        }
        else if (key.IsActionPressed("step", false, true) && _host.Paused)
        {
            _host.StepOnce();
        }
        else if (SpeedKey(key, "speed_up"))
        {
            _host.ChangeSpeed(+1);
        }
        else if (SpeedKey(key, "speed_down"))
        {
            _host.ChangeSpeed(-1);
        }
        else if (key.IsActionPressed("save_replay", false, true))
        {
            _host.SaveReplay();
        }
        else if (key.IsActionPressed("load_replay", false, true))
        {
            _host.StartPlayback();
        }
        else if (key.IsActionPressed("new_session", false, true))
        {
            _host.StartLiveSession();
        }
        else if (key.IsActionPressed("spawn", false, true))
        {
            // B arms the debug spawn; B again while it is armed changes the unit type.
            if (_interpreter.SpawnArmed)
            {
                _host.NextSpawnType();
            }
            else
            {
                _interpreter.ArmSpawn();
            }
        }
        else if (key.IsActionPressed("type_select", false, true) && !Selection.IsEmpty)
        {
            // T: the units of the type of the first selected unit on the field; a second T within 400 ms takes the whole map.
            SelectSameType(Selection.Units[0], false, _typeTaps.Press(TypeSelectKey, Time.GetTicksMsec()));
        }
        else if (key.IsActionPressed("debug_build", false, true))
        {
            // V arms the debug build (M3 steps 6, 8) with the reactor and orders it; V again while it is armed takes and orders the next
            // building. A click places the building once it is produced.
            if (!_interpreter.BuildArmed && _host.BuildIndex >= 0)
            {
                _host.ResumeDebugBuild();
                _interpreter.ArmBuild();
            }
            else if (!_host.IsPlayback)
            {
                _interpreter.ArmBuild();
                _host.ChooseBuild();
            }
        }
        else if (key.IsActionPressed("debug_produce", false, true) && !_host.IsPlayback)
        {
            // U (M3 step 8): one unit of the B type from the barracks or the factory; Shift + U, five.
            _host.IssueProduceUnits(key.ShiftPressed ? ProductionRules.MaxOrder : 1);
        }
        else if (key.IsActionPressed("deploy", false, true) && !_host.IsPlayback && OccupiedUnderPointer() is { } occupied)
        {
            // D over an own building with soldiers inside: they come out (stage 2e).
            _host.IssueEvacuate(occupied);
        }
        else if (key.IsActionPressed("deploy", false, true) && !Selection.IsEmpty && !_host.IsPlayback)
        {
            // D: the selected MCVs turn into construction yards (M3 step 7), transports unload, soldiers dig in or get up.
            _host.IssueDeploy(Selection.Units);
        }
        else if (key.IsActionPressed("deploy", false, true) && SelectedFactory is { } yard && IsYard(yard) && !_host.IsPlayback)
        {
            // D on the selected construction yard: it packs up into its MCV (RA2).
            _host.IssueDeploy([yard]);
            ChangeSelection(Selection.Clear);
        }
        else if (TabKey(key) is { } tab)
        {
            // Q / W / E / R: the tabs of the sidebar (RA2).
            TabRequested?.Invoke(tab);
        }
        else if (key.IsActionPressed("next_unit", false, true))
        {
            NextUnit(+1);
        }
        else if (key.IsActionPressed("previous_unit", false, true))
        {
            NextUnit(-1);
        }
        else if (key.IsActionPressed("home", false, true))
        {
            CenterOnBase();
        }
        else if (key.IsActionPressed("repair_mode", false, true))
        {
            ToggleRepair();
        }
        else if (key.IsActionPressed("sell_mode", false, true))
        {
            ToggleSell();
        }
        else if (key.IsActionPressed("debug_damage", false, true) && CellUnderPointer is { } damaged)
        {
            // Debug (M7 steps 5–7): a blow at the map object, bridge segment or building under the cursor, until the weapons of M4.
            _host.IssueDamage(damaged, DebugDamage);
        }
        else if (key.IsActionPressed("select_all", false, true))
        {
            // P (RA2): the combat units on the field; a second P within 400 ms takes those of the whole map.
            SelectCombatUnits(_typeTaps.Press(CombatSelectKey, Time.GetTicksMsec()));
        }
        else if (key.IsActionPressed("stop", false, true) && !Selection.IsEmpty)
        {
            StopSelection();
        }
        else if (key.IsActionPressed("scatter", false, true) && !Selection.IsEmpty)
        {
            ScatterSelection();
        }
        else if (key.IsActionPressed("debug_switch_player", false, true) && OS.IsDebugBuild())
        {
            // M4 step 13: play the other side until the bot comes. The selection and the waypoints belong to the old player; its
            // groups stay and come back with it, as the groups keep only own units.
            _host.SwitchPlayer();
            ChangeSelection(Selection.Clear);
            WaypointArmed = false;
            Waypoints.Clear();
        }
        else if (key.IsActionPressed("guard", false, true) && !Selection.IsEmpty)
        {
            NextStance();
        }
    }

    /// <summary>Runs a command of the command bar: the one its key runs. The buttons that wait for a milestone do nothing.</summary>
    public void RunCommand(CommandBarButton button)
    {
        if (!CommandBarLayout.Enabled(button, !Selection.IsEmpty))
        {
            return;
        }

        switch (button)
        {
            case CommandBarButton.TypeOnScreen:
                SelectSameType(Selection.Units[0], false, wholeMap: false);
                break;
            case CommandBarButton.TypeOnMap:
                SelectSameType(Selection.Units[0], false, wholeMap: true);
                break;
            case CommandBarButton.Scatter:
                ScatterSelection();
                break;
            case CommandBarButton.Stop:
                StopSelection();
                break;
            case CommandBarButton.Guard:
                NextStance();
                break;
            case CommandBarButton.Waypoints:
                ToggleWaypointMode();
                break;
        }
    }

    /// <summary>The selected units stop: the S key, or the Stop button.</summary>
    private void StopSelection()
    {
        Voice("stopping");
        _host.IssueStop(Selection.Units);
    }

    /// <summary>
    /// The G key or the shield button (M4 step 7): the next stance after the one of the first armed selected unit — aggressive, guard,
    /// hold position, round again — for the whole selection.
    /// </summary>
    private void NextStance()
    {
        if (_host.IsPlayback)
        {
            return;
        }

        var world = _host.Simulation.World;
        var first = Selection.Units
            .Select(id => world.TryGet(id, out var entity) ? entity as Unit : null)
            .FirstOrDefault(unit => unit is { IsAlive: true, Weapons.Length: > 0, Harvester: null });
        if (first is null)
        {
            return;
        }

        var next = first.Stance switch
        {
            Stance.Aggressive => Stance.Guard,
            Stance.Guard => Stance.HoldPosition,
            _ => Stance.Aggressive,
        };
        Voice(next switch
        {
            Stance.Guard => "stance guard",
            Stance.HoldPosition => "stance hold position",
            _ => "stance aggressive",
        });
        _host.IssueStance(Selection.Units, next);
    }

    /// <summary>The selected units scatter: the X key, or the Scatter button.</summary>
    private void ScatterSelection()
    {
        Voice("scattering");
        _host.IssueScatter(Selection.Units);
    }

    /// <summary>
    /// The waypoints button. The first press turns the mode on, and clicks on the field lay out points. The next press sends the
    /// points laid out so far and turns the mode off. Escape turns it off and drops them.
    /// </summary>
    private void ToggleWaypointMode()
    {
        if (!WaypointArmed)
        {
            // Playback only shows recorded orders, so there is nothing to lay out.
            WaypointArmed = !_host.IsPlayback;
            return;
        }

        if (!Waypoints.IsEmpty && !_host.IsPlayback)
        {
            SendWaypoints(null);
        }

        WaypointArmed = false;
        Waypoints.Clear();
    }

    /// <summary>Sends the laid-out waypoints to the selected units, ending at <paramref name="last"/> when there is one.</summary>
    private void SendWaypoints(WorldPos? last)
    {
        var (points, loop) = Waypoints.Finish(last);
        Voice(loop ? $"on patrol, {points.Length} points" : $"following {points.Length} waypoints");
        _host.IssueWaypoints(Selection.Units, points, loop);
    }

    /// <summary>A speed key. Shift is allowed, because the + sign is Shift with the equal key; Ctrl, Alt and Meta make another key.</summary>
    private static bool SpeedKey(InputEventKey key, string action) =>
        key.IsActionPressed(action) && !key.CtrlPressed && !key.AltPressed && !key.MetaPressed;

    /// <summary>Ctrl + F1…F4 remembers the middle of the view, F1…F4 moves the camera back there (M2 step 14).</summary>
    private void HandleBookmarkKey(int slot, bool ctrl)
    {
        if (ctrl)
        {
            Bookmarks.Save(slot, _camera.Position.X, _camera.Position.Z);
            GD.Print($"[input] bookmark F{slot} = {_camera.Position.X:0.#}, {_camera.Position.Z:0.#}");
        }
        else if (Bookmarks.TryGet(slot, out var point))
        {
            _camera.CenterOn(new Vector3(point.X, 0f, point.Z));
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
                // A building that produces selected: an order on the ground is its rally point; nothing else is ordered.
                case SelectBuilding select:
                    ChangeSelection(Selection.Clear);
                    _factory = select.Building;
                    break;
                case OrderMove order when SelectedFactory is { } factory:
                    SetRallyPoint(factory, order.Target);
                    break;
                case OrderAttackMove order when SelectedFactory is { } factory:
                    SetRallyPoint(factory, order.Target);
                    break;
                case OrderAttack or OrderForceAttack or AddWaypoint or FinishWaypoints when SelectedFactory is not null:
                    break;
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
                    // The frame is drawn over the field only: a box dragged onto the sidebar stops at its edge.
                    var (shownFrom, shownTo) = BoxSelection.ClipToField(box.From, box.To, FieldScreenRect());
                    _box.Position = new Vector2(MathF.Min(shownFrom.X, shownTo.X), MathF.Min(shownFrom.Y, shownTo.Y));
                    _box.Size = new Vector2(MathF.Abs(shownTo.X - shownFrom.X), MathF.Abs(shownTo.Y - shownFrom.Y));
                    break;
                case BoxSelect box:
                    _box.Visible = false;
                    var (clippedFrom, clippedTo) = BoxSelection.ClipToField(box.From, box.To, FieldScreenRect());
                    var inside = BoxSelection.Pick(OwnUnitsOnScreen().Select(u => (u.Unit, u.Point)), clippedFrom, clippedTo);
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
                case OrderMove order when !_host.IsPlayback && EnterAt(order.Target) is { } bunker:
                    Voice("entering");
                    _host.IssueEnter(Selection.Units, bunker);
                    break;
                case OrderAttack attack when !_host.IsPlayback && EnterAt(attack.Target) is { } entered:
                    // Stage 3b: engineers and infiltrators go into the enemy building, the others attack it.
                    Voice("entering");
                    if (_host.IssueEnter(Selection.Units, entered) is { Count: > 0 } others)
                    {
                        _host.IssueAttack(others, entered);
                    }

                    break;
                case OrderMove order when !_host.IsPlayback && !order.Queue && BridgeSappers(order.Target) is { Count: > 0 } sappers:
                    // The commando's C4 on the bridge; the rest of the selection goes there.
                    Voice("attacking (forced)");
                    _markers ??= GetNodeOrNull<OrderMarkers>("../OrderMarkers");
                    _markers?.Add(order.Target, MarkerKind.Attack);
                    _host.IssueForceAttack(sappers, null, order.Target.ToCell().Center);
                    if (Selection.Units.Where(id => !sappers.Contains(id)).ToList() is { Count: > 0 } companions)
                    {
                        _host.IssueMove(companions, order.Target, false);
                    }

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
                case OrderAttack attack when !_host.IsPlayback:
                    if (_host.Simulation.World.TryGet(attack.Target, out var enemy) && !enemy.Owner.IsNeutral)
                    {
                        Voice("attacking");
                        _markers ??= GetNodeOrNull<OrderMarkers>("../OrderMarkers");
                        _markers?.Add(PointOf(enemy), MarkerKind.Attack);
                        _host.IssueAttack(Selection.Units, attack.Target);
                    }

                    break;
                case OrderForceAttack force when !_host.IsPlayback:
                    Voice("attacking (forced)");
                    _markers ??= GetNodeOrNull<OrderMarkers>("../OrderMarkers");
                    _markers?.Add(force.Target is { } forced && _host.Simulation.World.TryGet(forced, out var victim) ? PointOf(victim) : force.Point, MarkerKind.Attack);
                    _host.IssueForceAttack(Selection.Units, force.Target, force.Point);
                    break;
                case AddWaypoint waypoint when CanOrder(waypoint.Point):
                    Waypoints.Add(waypoint.Point);
                    _markers?.Add(waypoint.Point, MarkerKind.Move);
                    break;
                case FinishWaypoints finish when !_host.IsPlayback:
                    // A last point nobody can reach is left out; the points laid out so far still go.
                    SendWaypoints(CanOrder(finish.Point) ? finish.Point : null);
                    break;
                case CancelWaypoints:
                    Waypoints.Clear();
                    WaypointArmed = false;
                    break;
                case SpawnUnit spawn:
                    if (!_host.IsPlayback)
                    {
                        _host.IssueSpawn(spawn.Position);
                    }

                    break;
                case PlaceBuilding place:
                    if (!_host.IsPlayback)
                    {
                        _host.IssuePlaceBuilding(place.Position, place.Line);
                    }

                    break;
                case DeployUnit deploy when !_host.IsPlayback:
                    _host.IssueDeploy([deploy.Unit]);
                    break;
                case AimSuperweapon aim when !_host.IsPlayback:
                    // The Phase Gate takes two clicks: what to move, then where.
                    if (_host.Simulation.World.TryGet(_superweapon, out var armed) && armed is Building { Def.Superweapon: Rts.Simulation.Rules.SuperweaponKind.Gate }
                        && _gateFrom is null)
                    {
                        _gateFrom = aim.Position.ToCell();
                        break;
                    }

                    _host.IssueSuperweapon(_superweapon, _gateFrom ?? aim.Position.ToCell(), aim.Position.ToCell());
                    _gateFrom = null;
                    _interpreter.Disarm();
                    break;
                case SellBuilding sell when !_host.IsPlayback:
                    _host.IssueSell(sell.Position.ToCell());
                    break;
                case RepairBuilding repair when !_host.IsPlayback:
                    _host.IssueToggleRepair(repair.Position.ToCell());
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
    /// <summary>
    /// The building at <paramref name="point"/> one of the selection can go into (stage 2e, 3b: a bunker of the player, a building to take,
    /// mend or infiltrate): an order there sends it in.
    /// </summary>
    private EntityId? EnterAt(WorldPos point) =>
        Hovered is { } hovered && EnterAt(hovered) is { } transport ? transport
        : _host.Simulation.World.BuildingAt(point.ToCell()) is { } building ? EnterAt(building.Id) : null;

    private EntityId? EnterAt(EntityId id)
    {
        var world = _host.Simulation.World;
        // A building, or an own transport under the pointer (stage 3d).
        if (!world.TryGet(id, out var entity) || entity is not (Building or Unit) || !Terrain.FogOfWar.Shows(world, entity))
        {
            return null;
        }

        foreach (var selected in Selection.Units)
        {
            if (world.TryGet(selected, out var unit) && unit is Unit soldier && soldier.Owner == SimulationHost.LocalPlayer
                && Rts.Simulation.Systems.EntrySystem.ActionOf(world, soldier, entity) != Rts.Simulation.Systems.EntryAction.None)
            {
                return entity.Id;
            }
        }

        return null;
    }

    /// <summary>The selected units that can plant a bomb on the standing bridge at <paramref name="point"/> (the commando's C4).</summary>
    private List<EntityId> BridgeSappers(WorldPos point)
    {
        var world = _host.Simulation.World;
        var cell = point.ToCell();
        var sappers = new List<EntityId>();
        foreach (var id in Selection.Units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit unit && unit.Owner == SimulationHost.LocalPlayer
                && Rts.Simulation.Systems.BridgeDemolition.CanBlow(world, unit, cell))
            {
                sappers.Add(id);
            }
        }

        return sappers;
    }

    /// <summary>The player's building under the pointer with soldiers inside (stage 2e), for D to let them out.</summary>
    private EntityId? OccupiedUnderPointer() =>
        CellUnderPointer is { } cell && _host.Simulation.World.BuildingAt(cell) is { IsAlive: true, Occupants.Count: > 0 } building
            && building.Owner == SimulationHost.LocalPlayer
            ? building.Id
            : null;

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
            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit
                && (unit.Archetype.Flies || world.Map.CanEnter(cell, unit.Archetype.Locomotor)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Changes the selection; laid-out waypoints belong to the old selection and are dropped.</summary>
    private void ChangeSelection(Action change)
    {
        _factory = null;
        change();
        Waypoints.Clear();
    }

    /// <summary>A double click on a unit: every own unit of its type on the field; with Shift they are added.</summary>
    private void SelectSameTypeOnScreen(SelectSameType same) => SelectSameType(same.Unit, same.Add, wholeMap: false);

    /// <summary>
    /// Selects the own living units of the type of <paramref name="reference"/>: on the field, or on the whole map (at most one
    /// command's worth of units). They replace the selection, or are added to it. Without any of the type on the field, the selection
    /// stays, so the second press can still take the whole map.
    /// </summary>
    private void SelectSameType(EntityId reference, bool add, bool wholeMap)
    {
        if (!_host.Simulation.World.TryGet(reference, out var entity) || entity is not Unit unit)
        {
            return;
        }

        var picked = wholeMap
            ? BoxSelection.SameTypeOnMap(OwnLivingUnits(), unit.Type, Rts.Simulation.Commands.UnitsCommand.MaxUnits)
            : BoxSelection.SameTypeInField(OwnUnitsOnScreen(), unit.Type, FieldScreenRect());
        if (!wholeMap && picked.Count == 0)
        {
            return;
        }

        ChangeSelection(() =>
        {
            if (add)
            {
                Selection.AddAll(picked);
            }
            else
            {
                Selection.ReplaceAll(picked);
            }
        });
    }

    /// <summary>Living units of the player with their type, in id order: the whole-map type select reads them.</summary>
    private IEnumerable<(EntityId Unit, UnitType Type)> OwnLivingUnits()
    {
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && unit.Owner == SimulationHost.LocalPlayer)
            {
                yield return (unit.Id, unit.Type);
            }
        }
    }

    /// <summary>The field in the view's design pixels: what a selection on screen may reach, without the sidebar and the bar.</summary>
    private ScreenRect FieldScreenRect()
    {
        var field = _camera.FieldRect();
        return new ScreenRect(field.Position.X, field.Position.Y, field.End.X, field.End.Y);
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

    /// <summary>Where a target stands for its marker: a unit's position, the middle of a building.</summary>
    private static WorldPos PointOf(Entity entity) => entity switch
    {
        Building building => Rts.Simulation.Combat.Aim.CentreOf(building),
        Unit unit => unit.Position,
        _ => WorldPos.Zero,
    };

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
                var screen = _camera.UnprojectPosition(WorldCoords.ToGodotOnGround(unit.Position, UnitHeight + Units.UnitAltitude.Of(unit.Archetype)));
                yield return (unit.Id, unit.Type, new ScreenPoint(screen.X, screen.Y));
            }
        }
    }

    private PointerContext Context(ScreenPoint at, bool shift, bool alt, bool ctrl)
    {
        var target = PickTarget(at);
        var ground = GroundAt(at);
        return new(
            ground,
            target,
            shift,
            !Selection.IsEmpty || SelectedFactory is not null,
            alt,
            ctrl,
            WaypointMode: WaypointArmed || WaypointHeld(),
            HasWaypoints: !Waypoints.IsEmpty,
            TargetDeploys: target.Kind == PointerTargetKind.OwnUnit && Selection.Contains(target.Unit) && Deploys(target.Unit),
            OwnFactory: ground is { } point ? FactoryAt(point) : null);
    }

    // The building that produces selected instead of units: an order on the ground sets its rally point.
    private EntityId? _factory;

    // Where the last event of the radar happened (Space), and the unit N / M picked last.
    private WorldPos? _lastEvent;
    private EntityId _cycled;

    /// <summary>Q / W / E / R: the sidebar shows the tab of the number (the order of <c>BuildGroup</c>).</summary>
    public event Action<int>? TabRequested;

    /// <summary>The tab of a tab key, or null.</summary>
    private static int? TabKey(InputEventKey key) =>
        key.IsActionPressed("tab_buildings", false, true) ? 0
        : key.IsActionPressed("tab_defense", false, true) ? 1
        : key.IsActionPressed("tab_infantry", false, true) ? 2
        : key.IsActionPressed("tab_vehicles", false, true) ? 3
        : null;

    /// <summary>Whether the building is the player's construction yard.</summary>
    private bool IsYard(EntityId id) =>
        _host.Simulation.World.TryGet(id, out var entity) && entity is Building { Def: { } def }
        && def.Id == _host.Simulation.World.Player(SimulationHost.LocalPlayer).Faction.ConstructionYard;

    /// <summary>
    /// P (RA2): the player's combat units — armed, not a harvester nor an engineer, nor a plane that is not the player's to order — on
    /// the field, or on the whole map (at most one command's worth).
    /// </summary>
    private void SelectCombatUnits(bool wholeMap)
    {
        var field = FieldScreenRect();
        var world = _host.Simulation.World;
        var picked = OwnUnitsOnScreen()
            .Where(entry => wholeMap || field.Contains(entry.Point))
            .Select(entry => entry.Unit)
            .Where(id => world.TryGet(id, out var entity) && entity is Unit
            {
                Archetype.Armed: true, Harvester: null, Drop: null, Mother: null,
            } unit && !unit.Archetype.Has(UnitTalents.Engineer))
            .Take(Rts.Simulation.Commands.UnitsCommand.MaxUnits)
            .ToList();
        if (picked.Count > 0)
        {
            ChangeSelection(() => Selection.ReplaceAll(picked));
        }
    }

    /// <summary>N / M (RA2): selects the next or the previous own unit without orders, by id, and the camera goes to it.</summary>
    private void NextUnit(int step)
    {
        var units = _host.Simulation.World.Entities
            .OfType<Unit>()
            .Where(unit => unit.IsAlive && unit.Owner == SimulationHost.LocalPlayer && unit.Drop is null && unit.Mother is null
                && unit.MoveTarget is null && !unit.PathPending && unit.Target is null && unit.Harvester is null)
            .ToList();
        if (units.Count == 0)
        {
            return;
        }

        var at = units.FindIndex(unit => unit.Id == _cycled);
        var next = units[at < 0 ? (step > 0 ? 0 : units.Count - 1) : (at + step + units.Count) % units.Count];
        _cycled = next.Id;
        ChangeSelection(() => Selection.Replace(next.Id));
        _camera.CenterOn(WorldCoords.ToGodotOnGround(next.Position));
    }

    /// <summary>H (RA2): the camera goes to the construction yard, or the first building, or the MCV.</summary>
    private void CenterOnBase()
    {
        var world = _host.Simulation.World;
        var player = SimulationHost.LocalPlayer;
        var own = world.Entities.Where(entity => entity.IsAlive && entity.Owner == player).ToList();
        var yard = own.OfType<Building>().FirstOrDefault(building => IsYard(building.Id));
        var any = own.OfType<Building>().FirstOrDefault(building => building.Def is not null);
        var mcv = own.OfType<Unit>().FirstOrDefault(unit => IsMcv(unit.Id, out _));
        WorldPos? point = (yard ?? any) is { } building ? Rts.Simulation.Combat.Aim.CentreOf(building) : mcv?.Position;
        if (point is { } centre)
        {
            _camera.CenterOn(WorldCoords.ToGodotOnGround(centre));
        }
    }

    /// <summary>Remembers where an event of the radar for the local player happened, for Space.</summary>
    private void Remember(Rts.Simulation.Events.SimEvent simEvent)
    {
        var world = _host.Simulation.World;
        var player = SimulationHost.LocalPlayer;
        WorldPos? point = simEvent switch
        {
            Rts.Simulation.Events.UnderAttack attack when attack.Player == player => attack.Point,
            Rts.Simulation.Events.BuildingCaptured captured when (captured.To == player || captured.From == player)
                && world.TryGet(captured.Building, out var taken) && taken is Building building => Rts.Simulation.Combat.Aim.CentreOf(building),
            Rts.Simulation.Events.SuperweaponFired fired => fired.Target.Center,
            Rts.Simulation.Events.UnitPromoted promoted when promoted.Owner == player && world.TryGet(promoted.Unit, out var veteran) && veteran is Unit unit
                => unit.Position,
            _ => null,
        };
        if (point is not null)
        {
            _lastEvent = point;
        }
    }

    /// <summary>The player's own building that produces, selected; null when none is, or it is gone or taken.</summary>
    public EntityId? SelectedFactory =>
        _factory is { } id && _host.Simulation.World.TryGet(id, out var entity)
        && entity is Building { IsAlive: true, Def.Produces.Length: > 0 } building && building.Owner == SimulationHost.LocalPlayer
            ? id
            : null;

    /// <summary>The player's own ready building that produces at <paramref name="point"/> — the construction yard among them — if any.</summary>
    private EntityId? FactoryAt(WorldPos point) =>
        _host.Simulation.World.BuildingAt(point.ToCell()) is { IsAlive: true, IsReady: true, Def.Produces.Length: > 0 } building
        && building.Owner == SimulationHost.LocalPlayer
            ? building.Id
            : null;

    /// <summary>The rally point of the selected factory: its units go there once they are out.</summary>
    private void SetRallyPoint(EntityId factory, WorldPos point)
    {
        if (_host.IsPlayback || !_host.Simulation.World.Contains(point))
        {
            return;
        }

        Voice("rally point");
        _markers?.Add(point, MarkerKind.Move);
        _host.IssueRallyPoint(factory, point);
    }

    /// <summary>Whether the unit is an MCV of the local player's faction.</summary>
    /// <summary>
    /// Whether a click on this selected unit of the player deploys it, as D does: an MCV, a unit with a deploy weapon (the toxin
    /// trooper), one that digs in (the GI), a transport with passengers.
    /// </summary>
    private bool Deploys(EntityId id)
    {
        var world = _host.Simulation.World;
        return IsMcv(id, out _) || (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit
            && (unit.Archetype.DeployWeapon >= 0 || unit.Archetype.DeployedWeapon >= 0 || unit.Passengers.Count > 0));
    }

    private bool IsMcv(EntityId id, out Unit mcv)
    {
        var world = _host.Simulation.World;
        if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit
            && unit.Archetype.Id == world.Player(SimulationHost.LocalPlayer).Faction.Mcv)
        {
            mcv = unit;
            return true;
        }

        mcv = null!;
        return false;
    }

    /// <summary>What lies under the pointer for the cursors of the base (M3 step 13).</summary>
    private PointerSpot SpotOf(PointerContext context)
    {
        var world = _host.Simulation.World;
        var player = SimulationHost.LocalPlayer;
        var building = context.Ground is { } ground ? world.BuildingAt(ground.ToCell()) : null;
        var own = building is { IsAlive: true, Def: not null } && building.Owner == player;
        // A tech building he took: repaired, never sold.
        var tech = building is { IsAlive: true } && building.Owner == player && Rts.Simulation.Systems.RepairSystem.Repairable(building);
        // An MCV deploys where its yard fits; any other unit that deploys (a GI digging in, a toxin trooper, a full transport) always.
        var fits = context.TargetDeploys;
        if (context.TargetDeploys && IsMcv(context.Target.Unit, out var mcv)
            && world.Rules.Database.TryGetBuilding(world.Player(player).Faction.ConstructionYard, out var yard))
        {
            var corner = PlacementPreview.CornerOf(yard.Footprint, mcv.Position);
            fits = PlacementRules.Check(world, player, yard, corner, needsBase: false, ignoreUnit: mcv.Id) == PlacementProblem.None;
        }

        // What lies under the pointer for a unit to act on: the unit there, else the building.
        Entity? target = null;
        if (context.Target.Kind is PointerTargetKind.OtherUnit or PointerTargetKind.OwnUnit && world.TryGet(context.Target.Unit, out var hovered))
        {
            target = hovered;
        }
        else if (building is { IsAlive: true })
        {
            target = building;
        }

        return new PointerSpot(
            own,
            (own || tech) && building!.Hp < building.MaxHp,
            context.Ground is { } at && !world.Map.OreAt(at.ToCell()).IsEmpty,
            Selection.Units.Any(id => world.TryGet(id, out var entity) && entity is Unit { Harvester: not null }),
            fits,
            context.Ground is { } door && EnterAt(door) is not null,
            context.Ground is { } bridge && BridgeSappers(bridge).Count > 0,
            tech,
            target is null ? Rts.Simulation.Systems.EntryAction.None : EntryOf(target),
            target is not null && Bombs(target),
            context.Alt && target is Unit { Archetype.Crushable: true } soldier && !soldier.Owner.IsNeutral
                && !world.AreAllies(soldier.Owner, player) && Selection.Units.Any(id => world.TryGet(id, out var tank) && tank is Unit { Archetype.Crusher: true }),
            Tool == PointerTool.Superweapon && world.TryGet(_superweapon, out var armed) && armed is Building weapon
                ? weapon.Def?.Superweapon ?? (BuildingTypes.IsKnown(weapon.Kind) ? weapon.Type.Superweapon : Rts.Simulation.Rules.SuperweaponKind.None)
                : Rts.Simulation.Rules.SuperweaponKind.None);
    }

    /// <summary>What the first selected unit of the player that can go into <paramref name="target"/> would do there.</summary>
    private Rts.Simulation.Systems.EntryAction EntryOf(Entity target)
    {
        var world = _host.Simulation.World;
        foreach (var id in Selection.Units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit unit && unit.Owner == SimulationHost.LocalPlayer
                && Rts.Simulation.Systems.EntrySystem.ActionOf(world, unit, target) is var action and not Rts.Simulation.Systems.EntryAction.None)
            {
                return action;
            }
        }

        return Rts.Simulation.Systems.EntryAction.None;
    }

    /// <summary>
    /// Whether a selected unit of the player would plant a bomb on <paramref name="target"/>: one of its weapons plants bombs and has a
    /// share against the target's armor (Ivan's bomb on anything, the commando's C4 on a building but not on a soldier).
    /// </summary>
    private bool Bombs(Entity target)
    {
        var world = _host.Simulation.World;
        var rules = world.Rules;
        var armor = target switch
        {
            Unit unit => unit.Archetype.Armor,
            Building { DefIndex: >= 0 } building => rules.ArmorOfBuilding(building.DefIndex),
            _ => 0,
        };
        foreach (var id in Selection.Units)
        {
            if (!world.TryGet(id, out var entity) || entity is not Unit unit || unit.Owner != SimulationHost.LocalPlayer)
            {
                continue;
            }

            foreach (var weapon in rules.WeaponsOf(unit))
            {
                if (weapon < 0)
                {
                    continue;
                }

                var warhead = rules.Database.IndexOfWarhead(rules.Database.Weapons[weapon].Warhead);
                if (rules.Database.Warheads[warhead].Bomb.Length > 0 && rules.Verses(warhead, armor) > Rts.Simulation.Math.Fixed.Zero)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Whether Z is held on its own: with Ctrl, Shift, Alt or Meta it is not the waypoint key.</summary>
    private static bool WaypointHeld() =>
        Input.IsActionPressed("waypoint_mode") && !Input.IsKeyPressed(Key.Shift) && !Input.IsKeyPressed(Key.Ctrl)
        && !Input.IsKeyPressed(Key.Alt) && !Input.IsKeyPressed(Key.Meta);

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
            // A unit hidden by the fog (M4 step 12) cannot be pointed at; an ally's is no target either (M6, A6): an order on it is a move
            // to the ground under it, as an attack on an ally is refused.
            // An enemy holo tank standing still (stage 3c) is a tree to the player: nothing to point at.
            if (!world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit || !Terrain.FogOfWar.Shows(world, unit)
                || (unit.Owner != SimulationHost.LocalPlayer && world.AreAllies(SimulationHost.LocalPlayer, unit.Owner))
                || Units.Disguises.LooksLikeTree(world, unit))
            {
                continue;
            }

            var screen = _camera.UnprojectPosition(WorldCoords.ToGodotOnGround(unit.Position, UnitHeight + Units.UnitAltitude.Of(unit.Archetype)));
            var distance = at.DistanceTo(new ScreenPoint(screen.X, screen.Y));
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = new PointerTarget(
                    unit.Owner == SimulationHost.LocalPlayer ? PointerTargetKind.OwnUnit : PointerTargetKind.OtherUnit,
                    unit.Id);
            }
        }

        if (nearest is { } picked)
        {
            return picked;
        }

        // M4 step 7: the building on the cell is a target, for an attack when it belongs to a player not of the team (M6,
        // A6) and is no wall; one the player does not see now — in the shroud, or a ghost in the fog (step 12) — is only ground.
        if (GroundAt(at) is { } point && world.BuildingAt(point.ToCell()) is { IsAlive: true } building && Terrain.FogOfWar.Shows(world, building))
        {
            var enemy = !world.AreAllies(SimulationHost.LocalPlayer, building.Owner) && !building.Owner.IsNeutral && building.Def is not { Wall: true };
            return new PointerTarget(enemy ? PointerTargetKind.OtherBuilding : PointerTargetKind.Ground, Building: building.Id);
        }

        return new PointerTarget(PointerTargetKind.Ground);
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
