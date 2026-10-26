using Godot;
using Rts.Client.App;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;

namespace Rts.Client.Effects;

/// <summary>
/// The toxin on the ground (stage 3b of filling the game): a green film over every cell with some, the stronger the level the less
/// see-through, redrawn a few times a second. What the fog hides is not shown.
/// </summary>
public partial class RadiationView : Node3D
{
    private const float Lift = 0.06f;
    private const double Interval = 0.25;

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost? _host;
    private double _wait;

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        AddChild(new MeshInstance3D
        {
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("RadiationView.process");
        if (_host is null || (_wait -= delta) > 0)
        {
            return;
        }

        _wait = Interval;
        var world = _host.Simulation.World;
        _mesh.ClearSurfaces();
        var any = false;
        for (var y = 0; y < world.HeightCells; y++)
        {
            for (var x = 0; x < world.WidthCells; x++)
            {
                var cell = new CellPos(x, y);
                var level = world.RadiationAt(cell);
                if (level <= 0 || !Rts.Simulation.Visibility.Vision.IsExplored(world, SimulationHost.LocalPlayer, cell))
                {
                    continue;
                }

                if (!any)
                {
                    _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
                    any = true;
                }

                var colour = new Color(0.4f, 1f, 0.2f, 0.15f + 0.4f * level / RadiationSystem.LevelMax);
                var corners = new[]
                {
                    WorldCoords.ToGodotOnGround(cell.Origin, Lift),
                    WorldCoords.ToGodotOnGround(cell.Origin + new WorldPos(WorldPos.LeptonsPerCell, 0), Lift),
                    WorldCoords.ToGodotOnGround(cell.Origin + new WorldPos(WorldPos.LeptonsPerCell, WorldPos.LeptonsPerCell), Lift),
                    WorldCoords.ToGodotOnGround(cell.Origin + new WorldPos(0, WorldPos.LeptonsPerCell), Lift),
                };
                foreach (var corner in new[] { corners[0], corners[1], corners[2], corners[0], corners[2], corners[3] })
                {
                    _mesh.SurfaceSetColor(colour);
                    _mesh.SurfaceAddVertex(corner);
                }
            }
        }

        if (any)
        {
            _mesh.SurfaceEnd();
        }
    }
}
