using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Spatial;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>
/// Debug cell overlay (docs/implementation/M2_unit_control.md, 4.14): F7 goes through passability for the locomotor of the
/// first selected unit (tracks without a selection), the reservations (vehicle cells blue, soldiers yellow with a cross on
/// each taken spot), the buckets of the spatial index (warmer with more units, a line every 4 cells) and the cells the
/// selected units see, height bonus included (M7 step 8, <see cref="SightRules"/>), then off. The
/// colours go into the ground shader, one texel per cell, so they lie on the hills. Read-only: the match never sees it.
/// </summary>
public partial class CellOverlay : Node3D
{
    private const float CrossHalf = 0.07f;
    private const float Lift = 0.05f;
    private static readonly Color CrossColor = new(0.15f, 0.12f, 0f);

    private readonly ImmediateMesh _crosses = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private ShaderMaterial? _ground;
    private ImageTexture? _texture;
    private byte[] _pixels = System.Array.Empty<byte>();
    private (CellOverlayMode Mode, int Tick, Locomotor Locomotor, MapGrid? Map) _drawn;

    public CellOverlayMode Mode { get; private set; }

    /// <summary>The line for the debug panel.</summary>
    public string Description => CellOverlayModes.Describe(Mode, ShownLocomotor().ToString().ToLowerInvariant());

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _input = GetNode<PlayerInput>("../PlayerInput");
        AddChild(new MeshInstance3D
        {
            Mesh = _crosses,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
            },
        });
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("debug_cells", false, true))
        {
            Mode = CellOverlayModes.Next(Mode);
        }
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("CellOverlay.process");
        _ground ??= GetNodeOrNull<TerrainBuilder>("../Terrain")?.GroundMaterial;
        if (_ground is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var state = (Mode, Mode == CellOverlayMode.Passability ? 0 : _host.Simulation.CurrentTick, ShownLocomotor(), world.Map);
        if (state == _drawn)
        {
            return;
        }

        _drawn = state;
        _crosses.ClearSurfaces();
        _ground.SetShaderParameter("overlay_enabled", Mode != CellOverlayMode.Off);
        _ground.SetShaderParameter("bucket_lines", Mode == CellOverlayMode.Buckets);
        if (Mode == CellOverlayMode.Off)
        {
            return;
        }

        Fill(world, state.Item3);
        var image = Image.CreateFromData(world.Map.Width, world.Map.Height, false, Image.Format.Rgba8, _pixels);
        if (_texture is null || _texture.GetWidth() != image.GetWidth() || _texture.GetHeight() != image.GetHeight())
        {
            _texture = ImageTexture.CreateFromImage(image);
            _ground.SetShaderParameter("overlay_map", _texture);
        }
        else
        {
            _texture.Update(image);
        }

        if (Mode == CellOverlayMode.Reservations)
        {
            DrawSpots(world);
        }
    }

    private void Fill(GameWorld world, Locomotor locomotor)
    {
        var map = world.Map;
        if (_pixels.Length != map.Width * map.Height * 4)
        {
            _pixels = new byte[map.Width * map.Height * 4];
        }

        var seen = Mode == CellOverlayMode.Sight ? SeenCells(world) : null;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var color = Mode switch
                {
                    CellOverlayMode.Passability => OverlayColors.Passability(map.SpeedPercent(cell, locomotor)),
                    CellOverlayMode.Reservations => OverlayColors.Occupancy(
                        world.Reservations.VehicleAt(cell) != EntityId.None, world.Reservations.InfantryCount(cell)),
                    CellOverlayMode.Sight => seen![y * map.Width + x] ? OverlayColors.Seen : OverlayColor.None,
                    _ => OverlayColors.Heat(world.Spatial.CountIn(x / SpatialIndex.BucketCells, y / SpatialIndex.BucketCells)),
                };
                var index = (y * map.Width + x) * 4;
                _pixels[index] = color.R;
                _pixels[index + 1] = color.G;
                _pixels[index + 2] = color.B;
                _pixels[index + 3] = color.A;
            }
        }
    }

    /// <summary>The cells within the sight radius of any selected living unit, in index order.</summary>
    private bool[] SeenCells(GameWorld world)
    {
        var map = world.Map;
        var seen = new bool[map.Width * map.Height];
        foreach (var id in _input.Selection.Units)
        {
            if (!world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            var at = unit.Position.ToCell();
            var radius = SightRules.SightOf(unit, map);
            for (var y = System.Math.Max(0, at.Y - radius); y <= System.Math.Min(map.Height - 1, at.Y + radius); y++)
            {
                for (var x = System.Math.Max(0, at.X - radius); x <= System.Math.Min(map.Width - 1, at.X + radius); x++)
                {
                    seen[y * map.Width + x] |= SightRules.InSight(x - at.X, y - at.Y, radius);
                }
            }
        }

        return seen;
    }

    /// <summary>A small cross on every taken soldier spot.</summary>
    private void DrawSpots(GameWorld world)
    {
        var started = false;
        for (var y = 0; y < world.Map.Height; y++)
        {
            for (var x = 0; x < world.Map.Width; x++)
            {
                var cell = new CellPos(x, y);
                if (world.Reservations.InfantryCount(cell) == 0)
                {
                    continue;
                }

                for (var spot = 0; spot < Reservations.SubcellsPerCell; spot++)
                {
                    if (world.Reservations.InfantryAt(cell, spot) == EntityId.None)
                    {
                        continue;
                    }

                    if (!started)
                    {
                        _crosses.SurfaceBegin(Mesh.PrimitiveType.Lines);
                        started = true;
                    }

                    var at = WorldCoords.ToGodotOnGround(Reservations.SubcellPoint(cell, spot), Lift);
                    Segment(at + new Vector3(-CrossHalf, 0, -CrossHalf), at + new Vector3(CrossHalf, 0, CrossHalf));
                    Segment(at + new Vector3(-CrossHalf, 0, CrossHalf), at + new Vector3(CrossHalf, 0, -CrossHalf));
                }
            }
        }

        if (started)
        {
            _crosses.SurfaceEnd();
        }
    }

    private void Segment(Vector3 from, Vector3 to)
    {
        _crosses.SurfaceSetColor(CrossColor);
        _crosses.SurfaceAddVertex(from);
        _crosses.SurfaceSetColor(CrossColor);
        _crosses.SurfaceAddVertex(to);
    }

    /// <summary>The locomotor of the first selected living unit; tracks without one.</summary>
    private Locomotor ShownLocomotor()
    {
        var world = _host.Simulation.World;
        foreach (var id in _input.Selection.Units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit)
            {
                return unit.Archetype.Locomotor;
            }
        }

        return Locomotor.Track;
    }
}
