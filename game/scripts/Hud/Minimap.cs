using Godot;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The minimap of the sidebar (docs/implementation/M6_menus_skirmish.md, 4.7): the whole map, every living unit as a dot in its
/// owner's colour, and the frame of the field the camera shows. A click or a drag moves the camera. Since M2 step 14 the ground
/// shows the land of every cell and the trees (<see cref="MinimapImage"/>), and selected units have a white ring. Since M3 step 12
/// it is offline without a radar or power when the radar rule is on (<see cref="MinimapRule"/>): dark, and clicks do not move the
/// camera. Fog comes later (M4). A map with a playable area shows that rectangle, as the screen does (<see cref="MinimapMath.For"/>):
/// the textures of the cells are drawn turned and clipped to it. Since M6 step 11 the shutters of RA2 slide shut and open
/// (<see cref="ShutterSeconds"/>) and say so (<see cref="OnlineChanged"/>), the ore and the gems lie on the ground, and every building
/// seen stands as its footprint in its owner's colour, the neutral ones grey.
/// </summary>
public partial class Minimap : Control
{
    /// <summary>How long the shutters take to close or open.</summary>
    public const double ShutterSeconds = 0.6;

    private static readonly Color GroundColor = new("#15171B");
    private static readonly Color OfflineColor = new("#7D838C");
    private static readonly Color ShutterColor = new("#2A2D33");
    private static readonly Color ShutterEdge = new("#4A4F57");
    private static readonly Color NeutralBuilding = new("#9A9A9A");

    private SimulationHost? _host;
    private RtsCamera? _camera;
    private Color _frameColor = new("#B3121C");
    private bool _dragging;
    private PlayerInput? _input;
    private ImageTexture? _ground;
    private MapGrid? _groundOf;
    private ImageTexture? _ore;
    private MapGrid? _oreOf;
    private int _oreVersion = -1;
    private double _oreAge;
    private bool _online = true;

    // 0 shut, 1 open; null until the first frame, which takes the state as it is without sliding.
    private double? _shutter;

    /// <summary>Connects the minimap to the simulation and to the camera it moves.</summary>
    public void Bind(SimulationHost host, RtsCamera camera)
    {
        _host = host;
        _camera = camera;
        _input = host.GetNodeOrNull<PlayerInput>("../PlayerInput");
        // One texel per cell, with sharp cell edges; the turned map of a playable area is cut at the minimap's edge.
        TextureFilter = TextureFilterEnum.Nearest;
        ClipContents = true;
    }

    /// <summary>Whether the minimap shows the map; the sidebar sets it every frame from <see cref="MinimapRule"/>.</summary>
    public bool Online
    {
        get => _online;
        set
        {
            if (value != _online && _shutter is not null)
            {
                OnlineChanged?.Invoke(value);
            }

            _online = value;
        }
    }

    /// <summary>The radar came on or went off during the match: the shutters start to move (not at the start of the match).</summary>
    public event System.Action<bool>? OnlineChanged;

    /// <summary>The shutters are fully open: the map shows and clicks move the camera.</summary>
    private bool Open => _shutter >= 1;

    /// <summary>The colour of the frame around the field: the faction accent.</summary>
    public void SetFrameColor(Color color) => _frameColor = color;

    public override void _Process(double delta)
    {
        var target = Online ? 1.0 : 0.0;
        _shutter = _shutter is { } shutter ? Mathf.MoveToward(shutter, target, delta / ShutterSeconds) : target;
        _oreAge += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var side = SidebarLayout.MinimapSize;
        DrawRect(new Rect2(Vector2.Zero, new Vector2(side, side)), GroundColor);
        if (_host is null || _camera is null)
        {
            return;
        }

        var shutter = (float)(_shutter ?? (Online ? 1 : 0));
        if (shutter <= 0f)
        {
            // The shutters of RA2: closed until a radar works again.
            DrawShutters(side, 0f);
            var font = GetThemeFont("font", "Label");
            DrawString(font, new Vector2(0f, side / 2f), Tr("UI_MINIMAP_OFFLINE"), HorizontalAlignment.Center, side, 16, OfflineColor);
            DrawRect(new Rect2(Vector2.Zero, new Vector2(side, side)), _frameColor, false, 1.5f);
            return;
        }

        var world = _host.Simulation.World;
        // The textures are one texel a cell: drawn over the map's own rectangle through the transform of the minimap.
        var view = Transform();
        DrawSetTransformMatrix(new Transform2D(new Vector2(view.Xx, view.Xy), new Vector2(view.Zx, view.Zy), new Vector2(view.Ox, view.Oy)));
        var cells = new Rect2(Vector2.Zero, new Vector2(world.WidthCells, world.HeightCells));
        DrawTextureRect(GroundOf(world.Map), cells, false);
        DrawTextureRect(OreOf(world.Map), cells, false);
        if (Terrain.FogOfWar.Current?.MinimapMask is { } fog)
        {
            // The fog of war (M4 step 12): black on the shroud, dim on the fog.
            DrawTextureRect(fog, cells, false);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
        // Round the rectangle of a playable area the scenery is not shown.
        var frame = view.Frame;
        DrawRect(new Rect2(0f, 0f, side, frame.Y), GroundColor);
        DrawRect(new Rect2(0f, frame.Y + frame.Height, side, side - frame.Y - frame.Height), GroundColor);
        DrawRect(new Rect2(0f, 0f, frame.X, side), GroundColor);
        DrawRect(new Rect2(frame.X + frame.Width, 0f, side - frame.X - frame.Width, side), GroundColor);

        // The buildings seen: their footprints in their owners' colours, under the dots of the units.
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building && Terrain.FogOfWar.Shows(world, building))
            {
                var color = building.Owner.IsNeutral ? NeutralBuilding : OwnerColor(building.Owner.Value);
                DrawColoredPolygon(
                [
                    ToMinimap(building.Cell.X, building.Cell.Y),
                    ToMinimap(building.Cell.X + building.Width, building.Cell.Y),
                    ToMinimap(building.Cell.X + building.Width, building.Cell.Y + building.Height),
                    ToMinimap(building.Cell.X, building.Cell.Y + building.Height),
                ], color);
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && Terrain.FogOfWar.Shows(world, unit))
            {
                var cell = WorldCoords.ToGodot(unit.Position);
                var at = ToMinimap(cell.X, cell.Z);
                if (_input?.Selection.Contains(unit.Id) == true)
                {
                    DrawCircle(at, 3.5f, Colors.White);
                }

                DrawCircle(at, 2.5f, OwnerColor(unit.Owner.Value));
            }
        }

        DrawFieldFrame();
        if (shutter < 1f)
        {
            DrawShutters(side, shutter);
        }
    }

    // Two plates from the top and the bottom that meet in the middle when shut; open is 1.
    private void DrawShutters(float side, float open)
    {
        var half = side / 2f * (1f - open);
        if (half <= 0f)
        {
            return;
        }

        DrawRect(new Rect2(0f, 0f, side, half), ShutterColor);
        DrawRect(new Rect2(0f, side - half, side, half), ShutterColor);
        DrawLine(new Vector2(0f, half), new Vector2(side, half), ShutterEdge, 2f);
        DrawLine(new Vector2(0f, side - half), new Vector2(side, side - half), ShutterEdge, 2f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Open)
        {
            _dragging = false;
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            _dragging = button.Pressed;
            if (button.Pressed)
            {
                MoveCameraTo(button.Position);
            }

            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            MoveCameraTo(motion.Position);
            AcceptEvent();
        }
    }

    /// <summary>Draws the ground outline of the field: the four corners of the view, as far as they hit the ground.</summary>
    private void DrawFieldFrame()
    {
        var field = _camera!.FieldRect();
        var corners = new[]
        {
            field.Position,
            new Vector2(field.End.X, field.Position.Y),
            field.End,
            new Vector2(field.Position.X, field.End.Y),
        };

        var outline = new Vector2[corners.Length + 1];
        for (var i = 0; i < corners.Length; i++)
        {
            if (!_camera.TryGroundAt(corners[i], out var ground))
            {
                return;
            }

            outline[i] = ToMinimap(ground.X, ground.Z);
        }

        outline[^1] = outline[0];
        DrawPolyline(outline, _frameColor, 1.5f);
    }

    private void MoveCameraTo(Vector2 local)
    {
        if (_host is null || _camera is null)
        {
            return;
        }

        var (x, y) = Transform().FromMinimap(local.X, local.Y);
        _camera.CenterOn(new Vector3(x, 0f, y));
    }

    /// <summary>The ground texture of the map, made once per map: a new match or a replay brings a new map object.</summary>
    private ImageTexture GroundOf(MapGrid map)
    {
        if (_ground is null || !ReferenceEquals(_groundOf, map))
        {
            var image = Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgb8, MinimapImage.Pixels(map));
            _ground = ImageTexture.CreateFromImage(image);
            _groundOf = map;
        }

        return _ground;
    }

    /// <summary>The ore of the map, made again once a second at most while it changes (M6 step 11).</summary>
    private ImageTexture OreOf(MapGrid map)
    {
        if (_ore is null || !ReferenceEquals(_oreOf, map) || (map.OreVersion != _oreVersion && _oreAge >= 1))
        {
            var image = Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, MinimapImage.OrePixels(map));
            _ore = ImageTexture.CreateFromImage(image);
            _oreOf = map;
            _oreVersion = map.OreVersion;
            _oreAge = 0;
        }

        return _ore;
    }

    /// <summary>A map point in cells, as a point on the minimap.</summary>
    private Vector2 ToMinimap(float cellX, float cellY)
    {
        var (x, y) = Transform().ToMinimap(cellX, cellY);
        return new Vector2(x, y);
    }

    /// <summary>The minimap of the map being played: its square, or the rectangle of its playable area.</summary>
    private MinimapTransform Transform()
    {
        var world = _host!.Simulation.World;
        var area = _host.Look?.Playable is { } a ? (a.Left, a.Top, a.Right, a.Bottom) : ((int, int, int, int)?)null;
        return MinimapMath.For(world.WidthCells, world.HeightCells, area, SidebarLayout.MinimapSize);
    }

    /// <summary>The colours of the units by owner, the same as in <c>UnitRenderer</c>.</summary>
    private static Color OwnerColor(int owner) => Matches.PlayerColors.Of(new PlayerId(owner));
}
