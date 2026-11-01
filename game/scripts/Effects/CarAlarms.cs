using System.Collections.Generic;
using Godot;
using Rts.Client.Audio;
using Rts.Simulation.Events;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// The alarms of the parked cars (docs/map_elements/13_civilians, 4.6): a shot that lands within <see cref="Reach"/> cells of a car of
/// the map sets it off — its headlights blink and it hoots for <see cref="Seconds"/> s. The look only: the simulation never hears of
/// it. A car that goes on burning as a wreck, or is gone, stops at once.
/// </summary>
public partial class CarAlarms : Node3D
{
    private const int Reach = 2;
    private const float Seconds = 3f;
    private const float BlinksPerSecond = 3f;
    private const string Sound = "snd_car_alarm";

    private static readonly StandardMaterial3D LightMaterial = new()
    {
        AlbedoColor = new Color(1f, 0.85f, 0.45f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.8f, 0.4f),
        EmissionEnergyMultiplier = 3f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private readonly Dictionary<EntityId, (Node3D Lights, float Left)> _alarms = new();
    private readonly List<EntityId> _done = new();
    private SimulationHost? _host;

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        if (_host is not null)
        {
            _host.EventRaised += OnEvent;
        }
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (simEvent is not ProjectileImpact impact || _host is null || WorldCoords.Terrain is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var reach = (long)Reach * Rts.Simulation.Geometry.WorldPos.LeptonsPerCell;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } car || !Parked(car) || _alarms.ContainsKey(car.Id)
                || (car.Position - impact.Point).LengthSquared > reach * reach)
            {
                continue;
            }

            var lights = Lights(car);
            AddChild(lights);
            _alarms.Add(car.Id, (lights, Seconds));
            if (Terrain.FogOfWar.Current is not { Revealed: false } || Vision.IsVisible(world, SimulationHost.LocalPlayer, car.Position.ToCell()))
            {
                AudioManager.Instance?.PlayAt(Sound, WorldCoords.ToGodotOnGround(car.Position, 0.2f));
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_host is null || _alarms.Count == 0)
        {
            return;
        }

        var world = _host.Simulation.World;
        _done.Clear();
        foreach (var (id, (lights, left)) in _alarms)
        {
            var now = left - (float)delta;
            if (now <= 0f || !world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } car || !Parked(car))
            {
                _done.Add(id);
                continue;
            }

            _alarms[id] = (lights, now);
            lights.Visible = Mathf.PosMod((Seconds - now) * BlinksPerSecond, 1f) < 0.5f;
        }

        foreach (var id in _done)
        {
            _alarms[id].Lights.QueueFree();
            _alarms.Remove(id);
        }
    }

    // A car of the map standing whole: no player's, no wreck.
    private static bool Parked(Unit unit) =>
        unit.Owner.IsNeutral && unit.Archetype.Category == UnitCategory.Vehicle && unit.Archetype.Has(UnitTalents.Civilian)
        && !CivilianSystem.IsWreck(unit);

    // Two headlights at the front of the car, turned as it stands.
    private static Node3D Lights(Unit car)
    {
        var root = new Node3D
        {
            Position = WorldCoords.ToGodotOnGround(car.Position),
            Rotation = new Vector3(0f, Units.UnitFacing.InterpolatedYaw(car.Facing, car.Facing, 0f), 0f),
        };
        foreach (var side in (float[])[-0.1f, 0.1f])
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f },
                MaterialOverride = LightMaterial,
                Position = new Vector3(side, 0.14f, -0.26f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        return root;
    }
}
