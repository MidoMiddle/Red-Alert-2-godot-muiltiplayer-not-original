using Godot;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The minimap of the sidebar (docs/implementation/M6_menus_skirmish.md, 4.7): the whole map, every living unit as a dot in its
/// owner's colour, and the frame of the field the camera shows. A click or a drag moves the camera. Since M2 step 14 the ground
/// shows the land of every cell and the trees (<see cref="MinimapImage"/>), and selected units have a white ring. Since M3 step 12
/// it is offline without a radar or power when the radar rule is on (<see cref="MinimapRule"/>): dark, and clicks do not move the
/// camera. Fog comes later (M4).
/// </summary>
public partial class Minimap : Control
{
    private static readonly Color GroundColor = new("#15171B");
    private static readonly Color OfflineColor = new("#7D838C");

    private SimulationHost? _host;
    private RtsCamera? _camera;
    private Color _frameColor = new("#B3121C");
    private bool _dragging;
    private PlayerInput? _input;
    private ImageTexture? _ground;
    private MapGrid? _groundOf;

    /// <summary>Connects the minimap to the simulation and to the camera it moves.</summary>
    public void Bind(SimulationHost host, RtsCamera camera)
    {
        _host = host;
        _camera = camera;
        _input = host.GetNodeOrNull<PlayerInput>("../PlayerInput");
        // One texel per cell, with sharp cell edges.
        TextureFilter = TextureFilterEnum.Nearest;
    }

    /// <summary>Whether the minimap shows the map; the sidebar sets it every frame from <see cref="MinimapRule"/>.</summary>
    public bool Online { get; set; } = true;

    /// <summary>The colour of the frame around the field: the faction accent.</summary>
    public void SetFrameColor(Color color) => _frameColor = color;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var side = SidebarLayout.MinimapSize;
        DrawRect(new Rect2(Vector2.Zero, new Vector2(side, side)), GroundColor);
        if (_host is null || _camera is null)
        {
            return;
        }

        if (!Online)
        {
            // The shutters of RA2: closed until a radar works again.
            var font = GetThemeFont("font", "Label");
            DrawString(font, new Vector2(0f, side / 2f), Tr("UI_MINIMAP_OFFLINE"), HorizontalAlignment.Center, side, 16, OfflineColor);
            DrawRect(new Rect2(Vector2.Zero, new Vector2(side, side)), _frameColor, false, 1.5f);
            return;
        }

        var world = _host.Simulation.World;
        DrawTextureRect(GroundOf(world.Map), new Rect2(Vector2.Zero, new Vector2(side, side)), false);
        if (Terrain.FogOfWar.Current?.MinimapMask is { } fog)
        {
            // The fog of war (M4 step 12): black on the shroud, dim on the fog.
            DrawTextureRect(fog, new Rect2(Vector2.Zero, new Vector2(side, side)), false);
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
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Online)
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

        var world = _host.Simulation.World;
        var (x, y) = MinimapMath.FromMinimap(local.X, local.Y, world.WidthCells, world.HeightCells, SidebarLayout.MinimapSize);
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

    /// <summary>A map point in cells, as a point on the minimap.</summary>
    private Vector2 ToMinimap(float cellX, float cellY)
    {
        var world = _host!.Simulation.World;
        var (x, y) = MinimapMath.ToMinimap(cellX, cellY, world.WidthCells, world.HeightCells, SidebarLayout.MinimapSize);
        return new Vector2(x, y);
    }

    /// <summary>The colours of the units by owner, the same as in <c>UnitRenderer</c>.</summary>
    private static Color OwnerColor(int owner) => Matches.PlayerColors.Of(new PlayerId(owner));
}
