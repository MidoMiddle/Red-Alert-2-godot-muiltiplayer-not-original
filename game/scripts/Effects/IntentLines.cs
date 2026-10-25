using Godot;
using Rts.Simulation.Combat;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// What enemies aim at, shown by a psychic sensor of the player's team (RA2 <c>PsychicDetectionRadius</c>): a red line from every enemy
/// unit in its reach that the player sees to the target it has chosen, while the sensor stands with power. Read from the world every
/// frame.
/// </summary>
public partial class IntentLines : Node3D
{
    private const float Lift = 0.25f;
    private static readonly Color Line = new(1f, 0.2f, 0.25f);

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost? _host;

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
                NoDepthTest = true,
            },
        });
    }

    public override void _Process(double delta)
    {
        _mesh.ClearSurfaces();
        if (_host is null || SimulationHost.LocalPlayer.IsNeutral)
        {
            return;
        }

        var world = _host.Simulation.World;
        var viewer = SimulationHost.LocalPlayer;
        var begun = false;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, Target: { } id } unit || world.AreAllies(viewer, unit.Owner) || unit.Owner.IsNeutral
                || !Vision.IntentShown(world, viewer, unit) || !Vision.Sees(world, viewer, unit)
                || !world.TryGet(id, out var target) || !target.IsAlive)
            {
                continue;
            }

            if (!begun)
            {
                _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
                begun = true;
            }

            var to = target switch
            {
                Building building => Aim.CentreOf(building),
                Unit aimed => aimed.Position,
                Projectile shot => shot.Position,
                _ => unit.Position,
            };
            _mesh.SurfaceSetColor(Line);
            _mesh.SurfaceAddVertex(WorldCoords.ToGodotOnGround(unit.Position, Lift));
            _mesh.SurfaceSetColor(Line);
            _mesh.SurfaceAddVertex(WorldCoords.ToGodotOnGround(to, Lift));
        }

        if (begun)
        {
            _mesh.SurfaceEnd();
        }
    }
}
