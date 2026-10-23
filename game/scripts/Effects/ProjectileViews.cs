using System.Collections.Generic;
using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// The shots in flight (docs/implementation/M4_combat.md, 4.8, step 13), drawn from the <see cref="Projectile"/> entities and moved
/// smoothly between the ticks: a dark ball for a shell along its arc, a grey one for flak, a long body with a trail of smoke for a
/// rocket. A shot over a cell the local player's team does not see is not drawn (the fog, step 12; M6, A6). Symbolic
/// (docs/implementation/conventions.md, section 12).
/// </summary>
public partial class ProjectileViews : Node3D
{
    private const float Lift = 0.45f;

    private static readonly StandardMaterial3D ShellMaterial = new() { AlbedoColor = new Color(0.12f, 0.12f, 0.12f) };
    private static readonly StandardMaterial3D FlakMaterial = new() { AlbedoColor = new Color(0.6f, 0.6f, 0.6f) };
    private static readonly StandardMaterial3D RocketMaterial = new() { AlbedoColor = new Color(0.85f, 0.85f, 0.8f) };

    private readonly Dictionary<EntityId, View> _views = new();
    private readonly HashSet<EntityId> _alive = new();
    private readonly List<EntityId> _gone = new();
    private SimulationHost? _host;
    private int _tick = -1;

    private sealed class View(Node3D root, CpuParticles3D? trail)
    {
        public Node3D Root { get; } = root;

        public CpuParticles3D? Trail { get; } = trail;

        public Vector3 Previous { get; set; }

        public Vector3 Current { get; set; }
    }

    public override void _Ready() => _host = GetNodeOrNull<SimulationHost>("../SimulationHost");

    public override void _Process(double delta)
    {
        if (_host is null || WorldCoords.Terrain is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var newTick = world.CurrentTick != _tick;
        _tick = world.CurrentTick;
        _alive.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is not Projectile { IsAlive: true } projectile)
            {
                continue;
            }

            var rules = world.Rules.Database;
            rules.TryGetProjectile(rules.Weapons[projectile.Weapon].Projectile, out var def);
            var where = PointOf(projectile, def);
            if (!_views.TryGetValue(projectile.Id, out var view))
            {
                view = MakeView(def);
                view.Previous = view.Current = where;
                _views.Add(projectile.Id, view);
            }
            else if (newTick)
            {
                view.Previous = view.Current;
                view.Current = where;
            }

            var at = view.Previous.Lerp(view.Current, _host.Alpha);
            view.Root.Position = at;
            if ((view.Current - view.Previous).LengthSquared() > 1e-6f)
            {
                view.Root.LookAt(at + (view.Current - view.Previous), Vector3.Up);
            }

            var seen = FogOfWar.Current is not { Revealed: false }
                || Vision.IsVisible(world, SimulationHost.LocalPlayer, projectile.Position.ToCell());
            view.Root.Visible = seen;
            if (view.Trail is not null)
            {
                view.Trail.Emitting = seen;
            }

            _alive.Add(projectile.Id);
        }

        _gone.Clear();
        foreach (var id in _views.Keys)
        {
            if (!_alive.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _views[id].Root.QueueFree();
            _views.Remove(id);
        }
    }

    /// <summary>Where the shot is at its tick: on the ground track, raised along its arc for a ballistic one.</summary>
    private static Vector3 PointOf(Projectile projectile, ProjectileDef def)
    {
        var lift = Lift;
        if (def.Motion == ProjectileMotion.Ballistic && projectile.FlightTicks > 0)
        {
            var t = 1f - (float)projectile.TicksLeft / projectile.FlightTicks;
            var arc = (float)def.Arc.Raw / Fixed.OneRaw * 2f;
            lift += 4f * arc * t * (1f - t);
        }

        return WorldCoords.ToGodotOnGround(projectile.Position, lift);
    }

    private View MakeView(ProjectileDef def)
    {
        var root = new Node3D();
        AddChild(root);
        CpuParticles3D? trail = null;
        if (def.Motion == ProjectileMotion.Homing)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.05f, Height = 0.35f },
                MaterialOverride = RocketMaterial,
                RotationDegrees = new Vector3(90, 0, 0),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            trail = new CpuParticles3D
            {
                Amount = 24,
                Lifetime = 0.6,
                LocalCoords = false,
                Direction = Vector3.Up,
                Spread = 30f,
                InitialVelocityMin = 0.1f,
                InitialVelocityMax = 0.3f,
                Gravity = Vector3.Zero,
                Mesh = new SphereMesh { Radius = 0.05f, Height = 0.1f, Material = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.8f, 0.8f) } },
            };
            root.AddChild(trail);
            trail.Emitting = true;
        }
        else
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = def.Inaccuracy > Fixed.Zero ? 0.07f : 0.06f, Height = 0.13f },
                MaterialOverride = def.Inaccuracy > Fixed.Zero ? FlakMaterial : ShellMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        return new View(root, trail);
    }
}
