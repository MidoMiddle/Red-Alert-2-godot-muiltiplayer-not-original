using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;
using Rts.Client.Terrain;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Editor;

/// <summary>
/// The map editor, its base (docs/implementation/M7_maps.md, 4.13, step 11): the RA2 camera over the terrain of the map being
/// edited, a brush cursor, and a panel on the right — new map, open, name and save; the tools raise, lower, flatten, smooth and
/// land; brush size and shape; undo and redo. The left button paints a stroke while held, the right one drags the camera.
/// The editing itself is <see cref="MapEditSession"/> (Rts.Data, tested without Godot); this scene only turns the pointer into
/// dabs and shows the result. The terrain and the camera read the map through <see cref="IMapSource"/>. Maps are saved in
/// <see cref="UserMapPaths.UserDirectory"/>; the maps of the game are only read, so a changed one is saved as a copy there.
/// </summary>
public partial class MapEditorScene : Node3D, IMapSource
{
    private const float PanelWidth = 380f;
    private const float FieldHeight = 40f;
    private const float CursorLift = 0.04f;
    private const float CrossHalf = 0.12f;
    private static readonly int[] Sizes = [32, 64, 96, 128];
    private static readonly Color CursorColor = new(1f, 0.85f, 0.2f);

    private readonly ImmediateMesh _cursorMesh = new();
    private MapEditSession _session = null!;
    private MapGrid _view = null!;
    private int _viewVersion = -1;
    private RtsCamera _camera = null!;
    private EditTool _tool = EditTool.Raise;
    private Brush _brush = new(BrushShape.Circle, 3);
    private LandType _land = LandType.Road;
    private bool _painting;
    private bool _dragging;
    private (int X, int Y, CellPos Cell)? _pointer;
    private string _message = "";
    private (EditTool, Brush, (int, int, CellPos)?, int) _drawnCursor;

    private Label _status = null!;
    private LineEdit _name = null!;
    private OptionButton _openList = null!;
    private OptionButton _sizeList = null!;
    private SpinBox _players = null!;
    private OptionButton _landList = null!;
    private HSlider _sizeSlider = null!;
    private readonly Dictionary<EditTool, Button> _toolButtons = new();
    private List<MapEntry> _maps = [];

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

    public override void _EnterTree()
    {
        // Before the terrain and the camera are ready: they ask for the map at once.
        Load(new MapEditSession(EditableMap.New(64, 64, 2)));
    }

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);
        _camera = GetNode<RtsCamera>("CameraRig");
        GetNode<DirectionalLight3D>("Sun").Basis = Basis.LookingAt(Main.SunDirection, Vector3.Up);
        _camera.ReservedRight = PanelWidth;
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
        _pointer = PointerUnder(GetViewport().GetMousePosition());
        if (_painting && Input.IsMouseButtonPressed(MouseButton.Left) && _pointer is { } at)
        {
            _session.Paint(at.X, at.Y, at.Cell);
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
                    _session.BeginStroke(_tool, _brush, _land);
                    _session.Paint(at.X, at.Y, at.Cell);
                    _painting = true;
                }
                else if (!left.Pressed && _painting)
                {
                    _session.EndStroke();
                    _painting = false;
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
        return map.ContainsCorner(x, y) ? (x, y, cell) : null;
    }

    private void Load(MapEditSession session)
    {
        _session = session;
        _viewVersion = -1;
        _painting = false;
        if (_name is not null)
        {
            _name.Text = session.Map.Name;
            _camera.CenterOn(new Vector3(session.Map.Width / 2f, 0f, session.Map.Height / 2f));
        }
    }

    // ---- The brush cursor: a cross on every corner under a height brush, the outline of every cell under the land brush.

    private void DrawCursor()
    {
        var state = (_tool, _brush, _pointer, _session.Version);
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
        if (_tool == EditTool.Land)
        {
            foreach (var cell in _brush.Cells(_session.Map, at.Cell))
            {
                Corner(terrain, cell.X, cell.Y, out var a);
                Corner(terrain, cell.X + 1, cell.Y, out var b);
                Corner(terrain, cell.X + 1, cell.Y + 1, out var c);
                Corner(terrain, cell.X, cell.Y + 1, out var d);
                Segment(a, b);
                Segment(b, c);
                Segment(c, d);
                Segment(d, a);
            }
        }
        else
        {
            foreach (var (x, y) in _brush.Corners(_session.Map, at.X, at.Y))
            {
                Corner(terrain, x, y, out var point);
                Segment(point + new Vector3(-CrossHalf, 0, 0), point + new Vector3(CrossHalf, 0, 0));
                Segment(point + new Vector3(0, 0, -CrossHalf), point + new Vector3(0, 0, CrossHalf));
            }
        }

        _cursorMesh.SurfaceEnd();
    }

    private static void Corner(TerrainHeights terrain, int x, int y, out Vector3 point) =>
        point = new Vector3(x, terrain.VertexHeight(x, y) + CursorLift, y);

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
        _players = new SpinBox { MinValue = 2, MaxValue = Rts.Simulation.World.PlayerId.MaxPlayers, Value = 2 };
        newRow.AddChild(_players);
        stack.AddChild(ActionButton("UI_EDITOR_NEW", () =>
        {
            Load(new MapEditSession(EditableMap.New(Sizes[_sizeList.Selected], Sizes[_sizeList.Selected], (int)_players.Value)));
            _message = "";
        }));

        // Open: the maps of the game and the player's own.
        stack.AddChild(Caption("UI_EDITOR_OPEN_MAP"));
        _openList = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight) };
        stack.AddChild(_openList);
        stack.AddChild(ActionButton("UI_EDITOR_OPEN", OpenSelected));
        RefreshOpenList();

        // Name and save.
        stack.AddChild(Caption("UI_EDITOR_NAME"));
        _name = new LineEdit { Text = _session.Map.Name, CustomMinimumSize = new Vector2(0, FieldHeight) };
        stack.AddChild(_name);
        stack.AddChild(ActionButton("UI_EDITOR_SAVE", Save));

        // Tools.
        stack.AddChild(Caption("UI_EDITOR_TOOLS"));
        var tools = new GridContainer { Columns = 2 };
        tools.AddThemeConstantOverride("h_separation", 8);
        stack.AddChild(tools);
        foreach (var (tool, key) in new[]
        {
            (EditTool.Raise, "UI_EDITOR_RAISE"), (EditTool.Lower, "UI_EDITOR_LOWER"), (EditTool.Flatten, "UI_EDITOR_FLATTEN"),
            (EditTool.Smooth, "UI_EDITOR_SMOOTH"), (EditTool.Land, "UI_EDITOR_LAND"),
        })
        {
            var button = new Button { Text = Tr(key), ToggleMode = true, CustomMinimumSize = new Vector2(0, FieldHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            button.Pressed += () => SelectTool(tool);
            _toolButtons[tool] = button;
            tools.AddChild(button);
        }

        _landList = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var land in Enum.GetValues<LandType>())
        {
            _landList.AddItem(land.ToString(), (int)land);
        }

        _landList.Selected = _landList.GetItemIndex((int)_land);
        _landList.ItemSelected += index =>
        {
            _land = (LandType)_landList.GetItemId((int)index);
            SelectTool(EditTool.Land);
        };
        tools.AddChild(_landList);

        // Brush.
        stack.AddChild(Caption("UI_EDITOR_BRUSH"));
        _sizeSlider = new HSlider { MinValue = Brush.MinSize, MaxValue = Brush.MaxSize, Step = 1, Value = _brush.Size, CustomMinimumSize = new Vector2(0, FieldHeight) };
        _sizeSlider.ValueChanged += value => _brush = _brush with { Size = (int)value };
        stack.AddChild(_sizeSlider);
        var shapes = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight) };
        shapes.AddItem(Tr("UI_EDITOR_CIRCLE"));
        shapes.AddItem(Tr("UI_EDITOR_SQUARE"));
        shapes.ItemSelected += index => _brush = _brush with { Shape = index == 0 ? BrushShape.Circle : BrushShape.Square };
        stack.AddChild(shapes);

        // History and back.
        var history = Row(stack);
        history.AddChild(ActionButton("UI_EDITOR_UNDO", () => _session.Undo(), expand: true));
        history.AddChild(ActionButton("UI_EDITOR_REDO", () => _session.Redo(), expand: true));
        stack.AddChild(ActionButton("UI_BUTTON_BACK", () => SceneRouter.Instance?.Back()));

        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_status);
        stack.AddChild(new Label
        {
            Text = Tr("UI_EDITOR_HINT"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.7f),
        });
    }

    private void SelectTool(EditTool tool)
    {
        _tool = tool;
        foreach (var (each, button) in _toolButtons)
        {
            button.SetPressedNoSignal(each == tool);
        }
    }

    private void RefreshOpenList()
    {
        var (maps, errors) = MapCatalog.Load(MapPreviews.ReadMapFiles());
        foreach (var error in errors)
        {
            GD.PushWarning($"Map left out: {error}");
        }

        _maps = maps;
        _openList.Clear();
        foreach (var entry in _maps)
        {
            _openList.AddItem(entry.Map.Name);
        }
    }

    private void OpenSelected()
    {
        if (_openList.Selected < 0 || _openList.Selected >= _maps.Count)
        {
            return;
        }

        var entry = _maps[_openList.Selected];
        Load(new MapEditSession(EditableMap.FromDefinition(entry.Map)));
        _message = string.Format(Tr("UI_EDITOR_OPENED"), entry.File);
    }

    /// <summary>Saves under the name in the field as user://maps/&lt;id&gt;.rtsmap; a map that does not play is saved with a warning.</summary>
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
        DirAccess.MakeDirRecursiveAbsolute(UserMapPaths.UserDirectory);
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                _message = string.Format(Tr("UI_EDITOR_SAVE_FAILED"), FileAccess.GetOpenError().ToString());
                return;
            }

            file.StoreString(RtsMapWriter.Write(definition));
        }

        try
        {
            definition.ToMapGrid();
            _message = string.Format(Tr("UI_EDITOR_SAVED"), path);
        }
        catch (ArgumentException error)
        {
            _message = string.Format(Tr("UI_EDITOR_SAVED_NOT_PLAYABLE"), path, error.Message);
        }

        RefreshOpenList();
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
