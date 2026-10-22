using System.Collections.Generic;
using Godot;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// What a harvester carries (docs/implementation/M3_base_economy.md, 4.17): a yellow strip on its roof, as long as its load is a
/// share of what it holds, and while it unloads at the dock, yellow cubes rising from it into the refinery. Follows the harvesters
/// between ticks the way <see cref="UnitRenderer"/> does.
/// </summary>
public partial class HarvesterMarks : Node3D
{
    private const float Roof = 0.46f;
    private const float StripLength = 0.8f;

    private static readonly Color LoadColor = new(0.95f, 0.8f, 0.15f);

    private readonly Dictionary<EntityId, Mark> _marks = new();
    private readonly HashSet<EntityId> _alive = new();
    private SimulationHost _host = null!;

    public override void _Ready() => _host = GetNode<SimulationHost>("../SimulationHost");

    public override void _Process(double delta)
    {
        _alive.Clear();
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is not Unit { IsAlive: true, Harvester: { } harvester } unit || !unit.Archetype.Harvests)
            {
                continue;
            }

            if (!_marks.TryGetValue(unit.Id, out var mark))
            {
                mark = MakeMark();
                _marks.Add(unit.Id, mark);
            }

            var current = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
            var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before : current;
            var alpha = _host.Alpha;
            mark.Root.Position = WorldCoords.ToGodotOnGround(previous.Position).Lerp(WorldCoords.ToGodotOnGround(current.Position), alpha);
            mark.Root.Rotation = new Vector3(0, UnitFacing.InterpolatedYaw(previous.Facing, current.Facing, alpha), 0);
            var share = Mathf.Clamp(harvester.Load / (float)unit.Archetype.HarvestCapacity, 0f, 1f);
            mark.Strip.Visible = share > 0f;
            mark.Strip.Scale = new Vector3(1f, 1f, Mathf.Max(0.01f, share));
            mark.Strip.Position = new Vector3(0, Roof, StripLength * (1f - share) / 2f);
            mark.Unloading.Emitting = harvester.Mode == HarvesterMode.Unloading;
            _alive.Add(unit.Id);
        }

        var gone = new List<EntityId>();
        foreach (var id in _marks.Keys)
        {
            if (!_alive.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (var id in gone)
        {
            _marks[id].Root.QueueFree();
            _marks.Remove(id);
        }
    }

    private Mark MakeMark()
    {
        var material = new StandardMaterial3D { AlbedoColor = LoadColor, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var root = new Node3D();
        var strip = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.16f, 0.04f, StripLength), Material = material } };
        root.AddChild(strip);
        var unloading = new CpuParticles3D
        {
            Emitting = false,
            Amount = 16,
            Lifetime = 0.7,
            Position = new Vector3(0, Roof + 0.05f, 0),
            Direction = Vector3.Up,
            Spread = 20f,
            InitialVelocityMin = 0.8f,
            InitialVelocityMax = 1.3f,
            Gravity = Vector3.Zero,
            Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.1f), Material = material },
        };
        root.AddChild(unloading);
        AddChild(root);
        return new Mark(root, strip, unloading);
    }

    private sealed record Mark(Node3D Root, MeshInstance3D Strip, CpuParticles3D Unloading);
}
