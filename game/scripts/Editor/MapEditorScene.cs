using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;
using Rts.Client.Terrain;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Editor;

/// <summary>
/// The map editor (docs/implementation/M7_maps.md, 4.13, steps 11–12): the RA2 camera over the terrain of the map being
/// edited, a cursor for the tool, the marks only the editor shows (<see cref="EditorMarks"/>), and a panel on the right —
/// new map, open, name and save; fourteen tools with the settings of the chosen one; brush size and shape; undo and redo;
/// the problems of <see cref="MapValidator"/> (a click on one moves the camera there) and «Play». The left button paints a
/// stroke while held (click tools act on letting go), the right one drags the camera. The editing itself is
/// <see cref="MapEditSession"/> (Rts.Data, tested without Godot); this scene turns the pointer into dabs and shows the
/// result. The terrain and the camera read the map through <see cref="IMapSource"/>. Maps are saved in
/// <see cref="UserMapPaths.UserDirectory"/>; the maps of the game are only read, so a changed one is saved as a copy there.
/// «Play» opens the map in the debug match (squads at the starts until the bot of M5); leaving the match comes back here
/// with the same session (<see cref="Kept"/>).
/// </summary>
public partial class MapEditorScene : Node3D, IMapSource
{
    /// <summary>The scene router argument a match started by «Play» carries, so leaving it returns to the editor.</summary>
    public const string FromEditorArgument = "from_editor";

    private const string MatchScene = "res://scenes/Main.tscn";
    private const string PlayPath = "user://cache/editor_play.rtsmap";
    private const float PanelWidth = 380f;
    private const float FieldHeight = 40f;
    private const float CursorLift = 0.04f;
    private const float CrossHalf = 0.12f;
    private static readonly int[] Sizes = [32, 64, 96, 128];
    private static readonly Color CursorColor = new(1f, 0.85f, 0.2f);

    private static readonly (EditTool Tool, string Key)[] Tools =
    [
        (EditTool.Raise, "UI_EDITOR_RAISE"), (EditTool.Lower, "UI_EDITOR_LOWER"), (EditTool.Flatten, "UI_EDITOR_FLATTEN"),
        (EditTool.Smooth, "UI_EDITOR_SMOOTH"), (EditTool.Cliff, "UI_EDITOR_CLIFF"), (EditTool.Land, "UI_EDITOR_LAND"),
        (EditTool.Texture, "UI_EDITOR_TEXTURE"), (EditTool.Water, "UI_EDITOR_WATER"), (EditTool.Road, "UI_EDITOR_ROAD"), (EditTool.Ore, "UI_EDITOR_ORE"),
        (EditTool.Objects, "UI_EDITOR_OBJECTS"), (EditTool.Bridge, "UI_EDITOR_BRIDGE"), (EditTool.Building, "UI_EDITOR_BUILDING"),
        (EditTool.Start, "UI_EDITOR_START"), (EditTool.Eraser, "UI_EDITOR_ERASER"),
    ];

    /// <summary>The session of the editor, kept while a match started by «Play» runs, so the editor comes back as it was.</summary>
    private static MapEditSession? Kept;

    private readonly ImmediateMesh _cursorMesh = new();
    private readonly Dictionary<EditTool, Button> _toolButtons = new();
    private MapEditSession _session = null!;
    private MapGrid _view = null!;
    private int _viewVersion = -1;
    private int _checkedVersion = -1;
    private List<MapProblem> _problems = [];
    private RtsCamera _camera = null!;
    private EditorMarks _marks = null!;
    private EditTool _tool = EditTool.Raise;
    private Brush _brush = new(BrushShape.Circle, 3);
    private EditOptions _options = new() { Land = LandType.Road };
    private bool _painting;
    private bool _dragging;
    private (int X, int Y, CellPos Cell)? _pointer;
    private CellPos? _pressedCell;
    private string _message = "";

    // «--look-at X Y»: the cell the camera shows over the first frames, for a screenshot of one place of the map.
    private Vector2? _lookAt;
    private int _lookFrames;
    private (EditTool, Brush, (int, int, CellPos)?, CellPos?, int, EditOptions) _drawnCursor;
    private List<MapEntry> _maps = [];

    private Label _status = null!;
    private LineEdit _name = null!;
    private OptionButton _openList = null!;
    private OptionButton _openFilter = null!;
    private List<MapEntry> _openListed = [];
    private OptionButton _lighting = null!;
    private Rts.Client.Climates.MapLook? _look;
    private int _lookVersion = -1;
    private OptionButton _sizeList = null!;
    private SpinBox _players = null!;
    private HSlider _sizeSlider = null!;
    private VBoxContainer _toolOptions = null!;
    private ItemList _problemList = null!;
    private Button _play = null!;

    public MapGrid CurrentMap
    {
        get
        {
            if (_viewVersion != _session.Version)
            {
                _view = _session.ViewGrid();
                _viewVersion = _session.Version;
            }

            return _view;
        }
    }

    public Rts.Client.Climates.MapLook CurrentLook
    {
        get
        {
            if (_look is null || _lookVersion != _session.Version)
            {
                _look = Rts.Client.Climates.MapLook.FromEditable(_session.Map);
                _lookVersion = _session.Version;
            }

            return _look;
        }
    }

    public override void _EnterTree()
    {
        // Before the terrain and the camera are ready: they ask for the map at once. «-- --ui-editor --open NAME» opens a map of
        // res://data/maps (to look at it, a screenshot of every climate); «--look-at X Y» shows one cell of it.
        Load(Kept ?? OpenedFromArguments() ?? new MapEditSession(EditableMap.NewWithArea(64, 2), DataFiles.Current.Rules.MapTypes));
    }

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);
        var args = OS.GetCmdlineUserArgs();
        var lookIndex = Array.IndexOf(args, "--look-at");
        if (lookIndex >= 0 && lookIndex + 2 < args.Length && int.TryParse(args[lookIndex + 1], out var lookX)
            && int.TryParse(args[lookIndex + 2], out var lookY))
        {
            _lookAt = new Vector2(lookX + 0.5f, lookY + 0.5f);
        }

        _camera = GetNode<RtsCamera>("CameraRig");
        _camera.ReservedRight = PanelWidth;
        _marks = new EditorMarks { Name = "EditorMarks" };
        AddChild(_marks);
        AddChild(new MeshInstance3D
        {
            Name = "BrushCursor",
            Mesh = _cursorMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
            },
        });
        BuildPanel();
        SelectTool(EditTool.Raise);
        UpdateStatus();
    }

    public override void _Process(double delta)
    {
        if (_lookAt is { } look && _lookFrames++ < 10)
        {
            _camera.CenterOn(new Vector3(look.X, 0f, look.Y));
            _camera.ZoomInFully();
        }

        _pointer = PointerUnder(GetViewport().GetMousePosition());
        if (_painting && Input.IsMouseButtonPressed(MouseButton.Left) && _pointer is { } at)
        {
            _session.Paint(at.X, at.Y, at.Cell);
        }

        _marks.Refresh(_session);
        if (!_painting && _checkedVersion != _session.Version)
        {
            Recheck();
        }

        DrawCursor();
        UpdateStatus();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                if (left.Pressed && PointerUnder(left.Position) is { } at)
                {
                    _session.BeginStroke(_tool, _brush, _options);
                    _session.Paint(at.X, at.Y, at.Cell);
                    _pressedCell = at.Cell;
                    _painting = true;
                }
                else if (!left.Pressed && _painting)
                {
                    if (PointerUnder(left.Position) is { } end)
                    {
                        _session.Paint(end.X, end.Y, end.Cell);
                    }

                    _session.EndStroke();
                    _painting = false;
                    _pressedCell = null;
                    _message = _session.Message is { } refused ? string.Format(Tr("UI_EDITOR_REFUSED"), refused) : _message;
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } right:
                _dragging = right.Pressed;
                if (right.Pressed)
                {
                    _camera.BeginDrag(right.Position);
                }
                else
                {
                    _camera.EndDrag();
                }

                break;
            case InputEventMouseMotion motion when _dragging:
                _camera.DragTo(motion.Position);
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                OnKey(key);
                break;
        }
    }

    private void OnKey(InputEventKey key)
    {
        var command = key.CtrlPressed || key.MetaPressed;
        if (command && key.Keycode == Key.Z)
        {
            _ = key.ShiftPressed ? _session.Redo() : _session.Undo();
        }
        else if (command && key.Keycode == Key.Y)
        {
            _session.Redo();
        }
        else if (key.Keycode == Key.Bracketleft || key.Keycode == Key.Bracketright)
        {
            _sizeSlider.Value = Math.Clamp(_brush.Size + (key.Keycode == Key.Bracketright ? 1 : -1), Brush.MinSize, Brush.MaxSize);
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>The corner nearest the pointer and the cell under it, or null off the map or over the panel.</summary>
    private (int X, int Y, CellPos Cell)? PointerUnder(Vector2 screen)
    {
        if (screen.X > GetViewport().GetVisibleRect().Size.X - PanelWidth || !_camera.TryGroundAt(screen, out var ground))
        {
            return null;
        }

        var map = _session.Map;
        var cell = new CellPos((int)Math.Floor(ground.X), (int)Math.Floor(ground.Z));
        var (x, y) = ((int)Math.Round(ground.X), (int)Math.Round(ground.Z));
        return map.ContainsCorner(x, y) && map.ContainsCell(cell) ? (x, y, cell) : null;
    }

    private void Load(MapEditSession session)
    {
        _session = session;
        Kept = session;
        _viewVersion = -1;
        _lookVersion = -1;
        _checkedVersion = -1;
        _painting = false;
        if (_name is not null)
        {
            _name.Text = session.Map.Name;
            _lighting.Selected = Math.Max(0, MapLighting.Ids.IndexOf(session.Map.Lighting));
            _camera.CenterOn(new Vector3(session.Map.Width / 2f, 0f, session.Map.Height / 2f));
        }
    }

    // ---- Checks and «Play».

    private void Recheck()
    {
        _checkedVersion = _session.Version;
        try
        {
            _problems = MapValidator.Check(_session.Map.ToDefinition(), _session.Types);
        }
        catch (ArgumentException error)
        {
            _problems = [new MapProblem(ProblemLevel.Error, error.Message, null)];
        }

        _problemList.Clear();
        foreach (var problem in _problems)
        {
            var key = problem.Level == ProblemLevel.Error ? "UI_EDITOR_PROBLEM_ERROR" : "UI_EDITOR_PROBLEM_WARNING";
            _problemList.AddItem(string.Format(Tr(key), problem.Message));
        }

        if (_problems.Count == 0)
        {
            _problemList.AddItem(Tr("UI_EDITOR_NO_PROBLEMS"));
        }

        _play.Disabled = MapValidator.HasErrors(_problems);
    }

    private void ShowProblem(long index)
    {
        if (index < _problems.Count && _problems[(int)index].Cell is { } cell)
        {
            _camera.CenterOn(new Vector3(cell.X + 0.5f, 0f, cell.Y + 0.5f));
        }
    }

    /// <summary>Saves the map to a scratch file and opens it in the debug match; leaving the match comes back here.</summary>
    private void Play()
    {
        if (!Write(PlayPath, _session.Map.ToDefinition(), out var error))
        {
            _message = string.Format(Tr("UI_EDITOR_SAVE_FAILED"), error);
            return;
        }

        SceneRouter.Instance?.GoTo(MatchScene, new Dictionary<string, object>
        {
            [SimulationHost.MapArgument] = PlayPath,
            [FromEditorArgument] = true,
        });
    }

    // ---- The cursor: crosses on the corners under a height brush, outlines of the cells under the other brushes, the
    // footprint of a building, the line of a bridge from where the button went down.

    private void DrawCursor()
    {
        var state = (_tool, _brush, _pointer, _pressedCell, _session.Version, _options);
        if (state == _drawnCursor)
        {
            return;
        }

        _drawnCursor = state;
        _cursorMesh.ClearSurfaces();
        if (_pointer is not { } at || WorldCoords.Terrain is not { } terrain)
        {
            return;
        }

        _cursorMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        switch (_tool)
        {
            case EditTool.Raise or EditTool.Lower or EditTool.Flatten or EditTool.Smooth or EditTool.Cliff:
                foreach (var (x, y) in _brush.Corners(_session.Map, at.X, at.Y))
                {
                    var point = Corner(terrain, x, y);
                    Segment(point + new Vector3(-CrossHalf, 0, 0), point + new Vector3(CrossHalf, 0, 0));
                    Segment(point + new Vector3(0, 0, -CrossHalf), point + new Vector3(0, 0, CrossHalf));
                }

                break;
            case EditTool.Building:
                if (_session.Types.TryGetBuilding(_options.BuildingType, out var type))
                {
                    Outline(terrain, at.Cell, type.Width, type.Height);
                }

                break;
            case EditTool.Start:
                Outline(terrain, at.Cell, 1, 1);
                break;
            case EditTool.Bridge:
                var from = _pressedCell ?? at.Cell;
                Outline(terrain, from, 1, 1);
                Outline(terrain, at.Cell, 1, 1);
                Segment(Corner(terrain, from.X, from.Y) + new Vector3(0.5f, 0, 0.5f), Corner(terrain, at.Cell.X, at.Cell.Y) + new Vector3(0.5f, 0, 0.5f));
                break;
            default:
                var brush = _tool == EditTool.Road ? _brush with { Shape = BrushShape.Square } : _brush;
                foreach (var cell in brush.Cells(_session.Map, at.Cell))
                {
                    Outline(terrain, cell, 1, 1);
                }

                break;
        }

        _cursorMesh.SurfaceEnd();
    }

    private static Vector3 Corner(TerrainHeights terrain, int x, int y) => new(x, terrain.VertexHeight(x, y) + CursorLift, y);

    private void Outline(TerrainHeights terrain, CellPos cell, int width, int height)
    {
        var a = Corner(terrain, cell.X, cell.Y);
        var b = Corner(terrain, cell.X + width, cell.Y);
        var c = Corner(terrain, cell.X + width, cell.Y + height);
        var d = Corner(terrain, cell.X, cell.Y + height);
        Segment(a, b);
        Segment(b, c);
        Segment(c, d);
        Segment(d, a);
    }

    private void Segment(Vector3 from, Vector3 to)
    {
        _cursorMesh.SurfaceSetColor(CursorColor);
        _cursorMesh.SurfaceAddVertex(from);
        _cursorMesh.SurfaceSetColor(CursorColor);
        _cursorMesh.SurfaceAddVertex(to);
    }

    // ---- The panel.

    private void BuildPanel()
    {
        var layer = new CanvasLayer { Name = "EditorUi" };
        AddChild(layer);
        var panel = new PanelContainer { Name = "Panel", CustomMinimumSize = new Vector2(PanelWidth, 0) };
        panel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        panel.OffsetLeft = -PanelWidth;
        panel.OffsetRight = 0;
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        layer.AddChild(panel);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        var stack = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(stack);

        stack.AddChild(new Label { Text = Tr("UI_EDITOR_TITLE"), HorizontalAlignment = HorizontalAlignment.Center });

        // New map: size and players.
        stack.AddChild(Caption("UI_EDITOR_NEW_MAP"));
        var newRow = Row(stack);
        _sizeList = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var size in Sizes)
        {
            _sizeList.AddItem(string.Format(Tr("UI_EDITOR_SIZE_ITEM"), size));
        }

        _sizeList.Selected = 1;
        newRow.AddChild(_sizeList);
        newRow.AddChild(new Label { Text = Tr("UI_EDITOR_PLAYERS"), VerticalAlignment = VerticalAlignment.Center });
        _players = new SpinBox { MinValue = 2, MaxValue = PlayerId.MaxPlayers, Value = 2 };
        newRow.AddChild(_players);
        stack.AddChild(ActionButton("UI_EDITOR_NEW", () =>
        {
            // As in RA2 the playable area is a rectangle on the screen, about size² cells (MapArea).
            Load(new MapEditSession(EditableMap.NewWithArea(Sizes[_sizeList.Selected], (int)_players.Value), DataFiles.Current.Rules.MapTypes));
            _message = "";
        }));

        // A random map (M7 step 13): seed and settings, then it can be worked on with the brushes.
        stack.AddChild(Caption("UI_EDITOR_RANDOM_MAP"));
        BuildRandomMap(stack);

        // Open: the maps of the game and the player's own, by where they come from; delete one, after a confirmation.
        stack.AddChild(Caption("UI_EDITOR_OPEN_MAP"));
        _openFilter = Choice([Tr("UI_MAP_ORIGIN_ALL"), .. MapOrigin.Ids.Select(id => Tr(MapCatalog.OriginKey(id)))], 0, _ => FillOpenList());
        stack.AddChild(_openFilter);
        _openList = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight) };
        stack.AddChild(_openList);
        var openRow = Row(stack);
        openRow.AddChild(ActionButton("UI_EDITOR_OPEN", OpenSelected, expand: true));
        openRow.AddChild(ActionButton("UI_EDITOR_DELETE", AskDeleteSelected, expand: true));
        RefreshOpenList();

        // Name and save.
        stack.AddChild(Caption("UI_EDITOR_NAME"));
        _name = new LineEdit { Text = _session.Map.Name, CustomMinimumSize = new Vector2(0, FieldHeight) };
        stack.AddChild(_name);
        stack.AddChild(ActionButton("UI_EDITOR_SAVE", Save));

        // The climate (RA2's theatres): the ground, the trees and the light; the texture follows role by role.
        stack.AddChild(Caption("UI_EDITOR_CLIMATE"));
        var climates = Rts.Data.Maps.ClimateGrounds.Ids;
        stack.AddChild(Choice(
            [.. climates.Select(id => Tr(Rts.Client.Maps.MapGenOptions.ClimateKey(id)))],
            climates.IndexOf(_session.Map.Climate),
            i =>
            {
                _session.SetClimate(climates[i]);
                // The texture brush offers the materials of the new climate.
                SelectTool(_tool);
            }));

        // The time of day: the light of the map's climate (look only).
        stack.AddChild(Caption("UI_EDITOR_LIGHTING"));
        _lighting = Choice(
            [.. MapLighting.Ids.Select(id => Tr(Rts.Client.Climates.SceneLighting.LabelKey(id)))],
            MapLighting.Ids.IndexOf(_session.Map.Lighting),
            i => _session.SetLighting(MapLighting.Ids[i]));
        stack.AddChild(_lighting);

        // Tools and the settings of the chosen one.
        stack.AddChild(Caption("UI_EDITOR_TOOLS"));
        var tools = new GridContainer { Columns = 2 };
        tools.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(tools);
        foreach (var (tool, key) in Tools)
        {
            var button = new Button { Text = Tr(key), ToggleMode = true, CustomMinimumSize = new Vector2(0, FieldHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            button.Pressed += () => SelectTool(tool);
            _toolButtons[tool] = button;
            tools.AddChild(button);
        }

        _toolOptions = new VBoxContainer();
        _toolOptions.AddThemeConstantOverride("separation", 6);
        stack.AddChild(_toolOptions);

        // Brush.
        stack.AddChild(Caption("UI_EDITOR_BRUSH"));
        _sizeSlider = new HSlider { MinValue = Brush.MinSize, MaxValue = Brush.MaxSize, Step = 1, Value = _brush.Size, CustomMinimumSize = new Vector2(0, FieldHeight) };
        _sizeSlider.ValueChanged += value => _brush = _brush with { Size = (int)value };
        stack.AddChild(_sizeSlider);
        stack.AddChild(Choice([Tr("UI_EDITOR_CIRCLE"), Tr("UI_EDITOR_SQUARE")], 0, index => _brush = _brush with { Shape = index == 0 ? BrushShape.Circle : BrushShape.Square }));

        // History.
        var history = Row(stack);
        history.AddChild(ActionButton("UI_EDITOR_UNDO", () => _session.Undo(), expand: true));
        history.AddChild(ActionButton("UI_EDITOR_REDO", () => _session.Redo(), expand: true));

        // Checks and «Play».
        stack.AddChild(Caption("UI_EDITOR_CHECKS"));
        _problemList = new ItemList { CustomMinimumSize = new Vector2(0, 150), AutoHeight = false };
        _problemList.ItemSelected += ShowProblem;
        stack.AddChild(_problemList);
        _play = ActionButton("UI_EDITOR_PLAY", Play);
        stack.AddChild(_play);
        stack.AddChild(ActionButton("UI_BUTTON_BACK", () => SceneRouter.Instance?.Back()));

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_status);
        stack.AddChild(new Label { Text = Tr("UI_EDITOR_HINT"), AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 1, 1, 0.7f) });
    }

    private void BuildRandomMap(VBoxContainer stack)
    {
        var options = new MapGenOptions { Name = "RandomMapOptions", ShowLighting = true };
        stack.AddChild(options);
        stack.AddChild(ActionButton("UI_EDITOR_GENERATE", () =>
        {
            try
            {
                var (map, used) = Rts.MapGen.MapGenerator.Generate(options.Settings with { Types = DataFiles.Current.Rules.MapTypes });
                Load(new MapEditSession(EditableMap.FromDefinition(map), DataFiles.Current.Rules.MapTypes));
                _message = string.Format(Tr("UI_EDITOR_GENERATED"), used);
            }
            catch (Rts.MapGen.MapGenException error)
            {
                _message = string.Format(Tr("UI_EDITOR_GENERATE_FAILED"), error.Message);
            }
        }));
    }

    private void SelectTool(EditTool tool)
    {
        _tool = tool;
        foreach (var (each, button) in _toolButtons)
        {
            button.SetPressedNoSignal(each == tool);
        }

        foreach (var child in _toolOptions.GetChildren())
        {
            child.QueueFree();
        }

        switch (tool)
        {
            case EditTool.Land:
                var lands = Enum.GetValues<LandType>();
                _toolOptions.AddChild(Choice(Array.ConvertAll(lands, l => l.ToString()), Array.IndexOf(lands, _options.Land), i => _options = _options with { Land = lands[i] }));
                break;
            case EditTool.Texture:
                // The materials of the climate, but water: painted on dry land it would look like water one cannot sail.
                var materials = Rts.Client.Climates.ClimateLooks.Get(_session.Map.Climate) is { } climate
                    ? climate.Materials.Where(m => !m.Water).Select(m => m.Id).ToList()
                    : ["snow"];
                _toolOptions.AddChild(Choice([.. materials], Math.Max(0, materials.IndexOf(_options.Material)), i => _options = _options with { Material = materials[i] }));
                break;
            case EditTool.Water:
                _toolOptions.AddChild(Choice(
                    [Tr("UI_EDITOR_SHALLOW"), Tr("UI_EDITOR_DEEP")], _options.Water == LandType.Deep ? 1 : 0,
                    i => _options = _options with { Water = i == 1 ? LandType.Deep : LandType.Shallow }));
                break;
            case EditTool.Ore:
                var gems = _options.Ore > 9;
                _toolOptions.AddChild(Choice([Tr("UI_EDITOR_ORE"), Tr("UI_EDITOR_GEMS")], gems ? 1 : 0, i => _options = _options with { Ore = i == 1 ? 11 : 5 }));
                _toolOptions.AddChild(Slider("UI_EDITOR_DENSITY", 1, 9, gems ? _options.Ore - 9 : _options.Ore, value =>
                    _options = _options with { Ore = _options.Ore > 9 ? Math.Clamp(9 + value, 10, 12) : value }));
                break;
            case EditTool.Objects:
                var objects = new List<string>();
                foreach (var type in DataFiles.Current.Rules.MapTypes.Objects)
                {
                    objects.Add(type.Id);
                }

                _toolOptions.AddChild(Choice([.. objects], Math.Max(0, objects.IndexOf(_options.ObjectType)), i => _options = _options with { ObjectType = objects[i] }));
                _toolOptions.AddChild(Slider("UI_EDITOR_DENSITY", 1, 100, _options.Density, value => _options = _options with { Density = value }));
                break;
            case EditTool.Bridge:
                var bridges = new List<string>();
                foreach (var type in DataFiles.Current.Rules.MapTypes.Bridges)
                {
                    bridges.Add(type.Id);
                }

                _toolOptions.AddChild(Choice([.. bridges], Math.Max(0, bridges.IndexOf(_options.BridgeType)), i => _options = _options with { BridgeType = bridges[i] }));
                break;
            case EditTool.Building:
                var buildings = new List<string>();
                foreach (var type in DataFiles.Current.Rules.MapTypes.Buildings)
                {
                    buildings.Add(type.Id);
                }

                _toolOptions.AddChild(Choice([.. buildings], Math.Max(0, buildings.IndexOf(_options.BuildingType)), i => _options = _options with { BuildingType = buildings[i] }));
                _toolOptions.AddChild(Slider("UI_EDITOR_FACING", 0, 7, _options.Facing, value => _options = _options with { Facing = value }));
                break;
            case EditTool.Eraser:
                var layers = Enum.GetValues<EraseLayer>();
                _toolOptions.AddChild(Choice(Array.ConvertAll(layers, l => l.ToString()), Array.IndexOf(layers, _options.Erase), i => _options = _options with { Erase = layers[i] }));
                break;
        }
    }

    private void RefreshOpenList()
    {
        var (maps, errors) = MapCatalog.Load(MapPreviews.ReadMapFiles(), Rts.Client.App.DataFiles.Current.Rules.MapTypes);
        foreach (var error in errors)
        {
            GD.PushWarning($"Map left out: {error}");
        }

        _maps = maps;
        FillOpenList();
    }

    /// <summary>The maps of the origin the filter shows, each with its origin after the name.</summary>
    private void FillOpenList()
    {
        var origin = _openFilter.Selected > 0 ? MapOrigin.Ids[_openFilter.Selected - 1] : null;
        _openListed = [.. MapCatalog.OfOrigin(_maps, origin)];
        _openList.Clear();
        foreach (var entry in _openListed)
        {
            _openList.AddItem($"{entry.Map.Name} · {Tr(MapCatalog.OriginKey(entry.Map.Origin))}");
        }
    }

    /// <summary>
    /// Asks before deleting the map chosen in the list: its name, its file, and that it cannot be undone. Cancel has the focus, so
    /// Enter does not delete.
    /// </summary>
    private void AskDeleteSelected()
    {
        if (_openList.Selected < 0 || _openList.Selected >= _openListed.Count)
        {
            return;
        }

        var entry = _openListed[_openList.Selected];
        if (!MapCatalog.CanDelete(entry.File, fromProject: !OS.HasFeature("template")))
        {
            _message = string.Format(Tr("UI_EDITOR_DELETE_FAILED"), entry.File, Tr("UI_EDITOR_DELETE_READ_ONLY"));
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Title = Tr("UI_EDITOR_DELETE_TITLE"),
            DialogText = string.Format(Tr("UI_EDITOR_DELETE_CONFIRM"), entry.Map.Name, entry.File),
            OkButtonText = Tr("UI_EDITOR_DELETE"),
            CancelButtonText = Tr("UI_BUTTON_CANCEL"),
            DialogAutowrap = true,
            MinSize = new Vector2I(560, 0),
        };
        dialog.Confirmed += () => Delete(entry);
        dialog.Canceled += dialog.QueueFree;
        dialog.Confirmed += dialog.QueueFree;
        GetNode("EditorUi").AddChild(dialog);
        dialog.PopupCentered();
        dialog.GetCancelButton().GrabFocus();
    }

    /// <summary>Deletes the file of a map and its cached preview; the list is read again.</summary>
    private void Delete(MapEntry entry)
    {
        var error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(entry.File));
        if (error != Error.Ok)
        {
            _message = string.Format(Tr("UI_EDITOR_DELETE_FAILED"), entry.File, error.ToString());
            return;
        }

        var preview = ProjectSettings.GlobalizePath(MapPreview.CachePath(entry.Hash));
        if (Godot.FileAccess.FileExists(preview))
        {
            DirAccess.RemoveAbsolute(preview);
        }

        _message = string.Format(Tr("UI_EDITOR_DELETED"), entry.File);
        RefreshOpenList();
    }

    private static MapEditSession? OpenedFromArguments()
    {
        var args = OS.GetCmdlineUserArgs();
        var index = Array.IndexOf(args, "--open");
        var path = index >= 0 && index + 1 < args.Length ? $"res://data/maps/{args[index + 1]}.rtsmap" : null;
        return path is not null && Godot.FileAccess.FileExists(path)
            ? new MapEditSession(EditableMap.FromDefinition(RtsMapReader.Read(Godot.FileAccess.GetFileAsString(path))), DataFiles.Current.Rules.MapTypes)
            : null;
    }

    private void OpenSelected()
    {
        if (_openList.Selected < 0 || _openList.Selected >= _openListed.Count)
        {
            return;
        }

        var entry = _openListed[_openList.Selected];
        Load(new MapEditSession(EditableMap.FromDefinition(entry.Map), DataFiles.Current.Rules.MapTypes));
        _message = string.Format(Tr("UI_EDITOR_OPENED"), entry.File);
    }

    /// <summary>Saves under the name in the field as user://maps/&lt;id&gt;.rtsmap; a map with errors is saved with a warning.</summary>
    private void Save()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0 || !MapNames.IsText(name))
        {
            _message = string.Format(Tr("UI_EDITOR_SAVE_FAILED"), Tr("UI_EDITOR_BAD_NAME"));
            return;
        }

        var map = _session.Map;
        map.Name = name;
        map.Id = UserMapPaths.IdFromName(name);
        map.Origin = MapOrigin.User; // saved here, it is the player's own map
        MapDefinition definition;
        try
        {
            definition = map.ToDefinition();
        }
        catch (ArgumentException error)
        {
            _message = string.Format(Tr("UI_EDITOR_SAVE_FAILED"), error.Message);
            return;
        }

        var path = UserMapPaths.SavePath(map.Id);
        if (!Write(path, definition, out var failure))
        {
            _message = string.Format(Tr("UI_EDITOR_SAVE_FAILED"), failure);
            return;
        }

        Recheck();
        _message = MapValidator.HasErrors(_problems)
            ? string.Format(Tr("UI_EDITOR_SAVED_NOT_PLAYABLE"), path, _problems.Find(p => p.Level == ProblemLevel.Error)!.Message)
            : string.Format(Tr("UI_EDITOR_SAVED"), path);
        RefreshOpenList();
    }

    private static bool Write(string path, MapDefinition definition, out string error)
    {
        DirAccess.MakeDirRecursiveAbsolute(path[..path.LastIndexOf('/')]);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            error = FileAccess.GetOpenError().ToString();
            return false;
        }

        file.StoreString(RtsMapWriter.Write(definition));
        error = "";
        return true;
    }

    private void UpdateStatus()
    {
        var map = _session.Map;
        var pointer = _pointer is { } at ? string.Format(Tr("UI_EDITOR_CURSOR"), at.X, at.Y, map.CornerLevel(at.X, at.Y)) : "";
        _status.Text = string.Format(Tr("UI_EDITOR_STATUS"), map.Width, map.Height, _session.History.UndoCount, UndoStack.Depth) + $"\n{pointer}\n{_message}";
    }

    private Label Caption(string key) => new() { Text = Tr(key), Modulate = new Color(1, 1, 1, 0.8f) };

    private static HBoxContainer Row(Container parent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        return row;
    }

    private static OptionButton Choice(string[] items, int selected, Action<int> chosen)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight) };
        foreach (var item in items)
        {
            choice.AddItem(item);
        }

        choice.Selected = Math.Max(0, selected);
        choice.ItemSelected += index => chosen((int)index);
        return choice;
    }

    private HBoxContainer Slider(string key, int min, int max, int value, Action<int> changed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var shown = new Label { Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture), CustomMinimumSize = new Vector2(36, 0) };
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 1, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, FieldHeight) };
        slider.ValueChanged += v =>
        {
            shown.Text = ((int)v).ToString(System.Globalization.CultureInfo.InvariantCulture);
            changed((int)v);
        };
        row.AddChild(new Label { Text = Tr(key), VerticalAlignment = VerticalAlignment.Center });
        row.AddChild(slider);
        row.AddChild(shown);
        return row;
    }

    private Button ActionButton(string key, Action pressed, bool expand = false)
    {
        var button = new Button
        {
            Text = Tr(key),
            CustomMinimumSize = new Vector2(0, FieldHeight),
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        };
        button.Pressed += pressed;
        return button;
    }
}
