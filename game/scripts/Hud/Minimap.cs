using Godot;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The minimap of the sidebar (docs/implementation/M6_menus_skirmish.md, 4.7): the whole map, every living unit as a dot in its
/// owner's colour, and the frame of the field the camera shows. A click or a drag moves the camera. Since M2 step 14 the ground
/// shows the land of every cell and the trees (<see cref="MinimapImage"/>), and selected units have a white ring. Fog and radar
/// come later (M3, M4).
/// </summary>
public partial class Minimap : Control
{
    private static readonly Color GroundColor = new("#15171B");

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

        DrawTextureRect(GroundOf(_host.Simulation.World.Map), new Rect2(Vector2.Zero, new Vector2(side, side)), false);
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is Unit { IsAlive: true } unit)
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
    private static Color OwnerColor(int owner) => owner switch
    {
        0 => new Color(0.6f, 0.6f, 0.6f),
        1 => new Color(0.2f, 0.45f, 0.95f),
        2 => new Color(0.9f, 0.25f, 0.2f),
        _ => Color.FromHsv(owner * 0.13f % 1f, 0.7f, 0.9f),
    };
}
