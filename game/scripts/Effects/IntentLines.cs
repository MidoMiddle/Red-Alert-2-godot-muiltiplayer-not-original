using Godot;
using Rts.Simulation.Combat;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// The lines of the mind (RA2), read from the world every frame:
/// <list type="bullet">
/// <item>what enemies aim at, shown by a psychic sensor of the player's team (<c>PsychicDetectionRadius</c>): a red line from every
/// enemy unit in its reach that the player sees to the target it has chosen, while the sensor stands with power;</item>
/// <item>Yuri's hold: a purple line from a Yuri to the unit his mind control holds, where the player sees either.</item>
/// </list>
/// </summary>
public partial class IntentLines : Node3D
{
    private const float Lift = 0.25f;
    private static readonly Color Line = new(1f, 0.2f, 0.25f);
    private static readonly Color Hold = new(0.75f, 0.35f, 1f);

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
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("IntentLines.process");
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
            if (entity is Unit { IsAlive: true, ControlledBy: { } holderId } held && world.TryGet(holderId, out var holder) && holder.IsAlive
                && (Vision.Sees(world, viewer, held) || Vision.Sees(world, viewer, holder)))
            {
                if (!begun)
                {
                    _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
                    begun = true;
                }

                Segment(((Unit)holder).Position, held.Position, Hold);
            }

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
            Segment(unit.Position, to, Line);
        }

        if (begun)
        {
            _mesh.SurfaceEnd();
        }
    }

    private void Segment(Rts.Simulation.Geometry.WorldPos from, Rts.Simulation.Geometry.WorldPos to, Color color)
    {
        _mesh.SurfaceSetColor(color);
        _mesh.SurfaceAddVertex(WorldCoords.ToGodotOnGround(from, Lift));
        _mesh.SurfaceSetColor(color);
        _mesh.SurfaceAddVertex(WorldCoords.ToGodotOnGround(to, Lift));
    }
}
