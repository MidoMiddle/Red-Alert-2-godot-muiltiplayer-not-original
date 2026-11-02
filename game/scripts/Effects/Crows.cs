using System;
using System.Collections.Generic;
using Godot;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// The crows of the towns and the villages (docs/map_elements/13_civilians/appearance.md: "small and quick, they scatter at danger"; the
/// owner, 2026-11-02: the look only). A few flocks peck on the open ground of a map of the town, the snow or the farms; a shot landing
/// within <see cref="ShotCells"/> cells or anyone coming within <see cref="NearCells"/> sends a flock up — it flies off, circles and
/// lands again a few cells away. The simulation never hears of them; the fog covers them as it covers the ground.
/// </summary>
public partial class Crows : Node3D
{
    // Few of them (the owner, 2026-11-02: there were far too many): a flock of two or three to every 6400 cells, two on a map of
    // 136 by 136.
    private const int CellsPerFlock = 6400;
    private const int ShotCells = 4;
    private const float NearCells = 1.3f;
    private const float FlightSeconds = 7f;
    private const float FlightHeight = 1.8f;
    private const int LandingCells = 6;

    private static readonly string[] Climates = ["urban", "winter", "temperate"];
    private static readonly StandardMaterial3D Feathers = new() { AlbedoColor = new Color(0.07f, 0.07f, 0.09f), Roughness = 0.7f };
    private static readonly StandardMaterial3D Beak = new() { AlbedoColor = new Color(0.2f, 0.18f, 0.16f) };

    private sealed class Crow(Node3D body, Node3D leftWing, Node3D rightWing, Vector3 offset, float phase)
    {
        public Node3D Body { get; } = body;
        public Node3D LeftWing { get; } = leftWing;
        public Node3D RightWing { get; } = rightWing;
        public Vector3 Offset { get; set; } = offset;
        public float Phase { get; } = phase;
    }

    private sealed class Flock(Node3D root, List<Crow> crows, Vector3 at)
    {
        public Node3D Root { get; } = root;
        public List<Crow> Crows { get; } = crows;
        public Vector3 At { get; set; } = at;
        public Vector3 From { get; set; } = at;
        public Vector3 To { get; set; } = at;
        public float Flying { get; set; } = -1f;
    }

    private readonly List<Flock> _flocks = new();
    private readonly Random _random = new(7);
    private SimulationHost? _host;
    private float _look;

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

    public override void _Process(double delta)
    {
        if (_host is null || WorldCoords.Terrain is null)
        {
            return;
        }

        if (_flocks.Count == 0 && !_placed)
        {
            Place(_host.Simulation.World);
        }

        var dt = (float)delta;
        var time = (float)Time.GetTicksMsec() / 1000f;
        _look -= dt;
        var look = _look <= 0f;
        if (look)
        {
            _look = 0.25f;
        }

        foreach (var flock in _flocks)
        {
            if (flock.Flying >= 0f)
            {
                Fly(flock, dt, time);
                continue;
            }

            if (look && Someone(_host.Simulation.World, flock.At))
            {
                TakeOff(flock);
                continue;
            }

            // On the ground: they bob as they peck, each in its own time.
            foreach (var crow in flock.Crows)
            {
                var peck = Mathf.Max(0f, Mathf.Sin((time * 3f) + crow.Phase));
                crow.Body.Position = crow.Offset;
                crow.Body.Rotation = new Vector3(peck * 0.5f, crow.Body.Rotation.Y, 0f);
            }
        }
    }

    private bool _placed;

    private void OnEvent(SimEvent simEvent)
    {
        if (simEvent is not ProjectileImpact impact || WorldCoords.Terrain is null)
        {
            return;
        }

        var point = WorldCoords.ToGodotOnGround(impact.Point);
        foreach (var flock in _flocks)
        {
            if (flock.Flying < 0f && new Vector2(flock.At.X - point.X, flock.At.Z - point.Z).Length() <= ShotCells)
            {
                TakeOff(flock);
            }
        }
    }

    // The flocks on the open ground of a map of their climates, a flock to every CellsPerFlock cells, on cells picked by the random
    // of the view (the look only).
    private void Place(GameWorld world)
    {
        _placed = true;
        var climate = GetNodeOrNull<Terrain.TerrainBuilder>("../Terrain")?.Climate?.Id;
        if (climate is null || Array.IndexOf(Climates, climate) < 0)
        {
            return;
        }

        var map = world.Map;
        var count = System.Math.Max(1, (map.Width * map.Height) / CellsPerFlock);
        for (var tries = 0; _flocks.Count < count && tries < count * 40; tries++)
        {
            var cell = new CellPos(_random.Next(map.Width), _random.Next(map.Height));
            if (Open(world, cell))
            {
                _flocks.Add(Make(WorldCoords.ToGodotOnGround(cell.Center)));
            }
        }

        GD.Print(_flocks.Count > 0
            ? $"[crows] {_flocks.Count} flocks, the first at {(int)_flocks[0].At.X},{(int)_flocks[0].At.Z}"
            : "[crows] no open ground for a flock");
    }

    private static bool Open(GameWorld world, CellPos cell) =>
        world.Map.Contains(cell) && world.Map.LandAt(cell) == LandType.Clear && world.Map.CanEnter(cell, Locomotor.Foot)
        && world.Map.OreAt(cell).IsEmpty;

    private Flock Make(Vector3 at)
    {
        var root = new Node3D { Position = at };
        AddChild(root);
        var crows = new List<Crow>();
        var size = 2 + _random.Next(2);
        for (var i = 0; i < size; i++)
        {
            // Twice a crow's size, so it reads from the camera of the game.
            var body = new Node3D { Rotation = new Vector3(0f, (float)_random.NextDouble() * Mathf.Tau, 0f), Scale = Vector3.One * 2f };
            body.AddChild(Part(new BoxMesh { Size = new Vector3(0.045f, 0.04f, 0.09f) }, Feathers, new Vector3(0f, 0.035f, 0f)));
            body.AddChild(Part(new SphereMesh { Radius = 0.022f, Height = 0.044f }, Feathers, new Vector3(0f, 0.06f, -0.05f)));
            body.AddChild(Part(new BoxMesh { Size = new Vector3(0.01f, 0.01f, 0.025f) }, Beak, new Vector3(0f, 0.058f, -0.077f)));
            var left = Wing(-1);
            var right = Wing(1);
            body.AddChild(left);
            body.AddChild(right);
            var offset = new Vector3(((float)_random.NextDouble() - 0.5f) * 1.3f, 0f, ((float)_random.NextDouble() - 0.5f) * 1.3f);
            root.AddChild(body);
            crows.Add(new Crow(body, left, right, offset, (float)_random.NextDouble() * Mathf.Tau));
        }

        return new Flock(root, crows, at);
    }

    // A wing folded along the body; spread and beating in flight.
    private static Node3D Wing(int side)
    {
        var wing = new Node3D { Position = new Vector3(side * 0.02f, 0.05f, 0f), Visible = false };
        wing.AddChild(Part(new BoxMesh { Size = new Vector3(0.09f, 0.006f, 0.05f) }, Feathers, new Vector3(side * 0.045f, 0f, 0f)));
        return wing;
    }

    private static MeshInstance3D Part(Mesh mesh, Material material, Vector3 at) =>
        new() { Mesh = mesh, MaterialOverride = material, Position = at, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

    // Anyone — a soldier, a vehicle, a civilian — on the ground near the flock.
    private static bool Someone(GameWorld world, Vector3 at)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && unit.Archetype.Category != UnitCategory.Aircraft)
            {
                var position = WorldCoords.ToGodotOnGround(unit.Position);
                if (new Vector2(position.X - at.X, position.Z - at.Z).Length() <= NearCells)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Up, off a few cells to open ground; on the old spot again when there is none.
    private void TakeOff(Flock flock)
    {
        var world = _host!.Simulation.World;
        flock.From = flock.At;
        flock.To = flock.At;
        for (var tries = 0; tries < 12; tries++)
        {
            var angle = (float)_random.NextDouble() * Mathf.Tau;
            var reach = 3f + ((float)_random.NextDouble() * (LandingCells - 3));
            var spot = flock.At + new Vector3(Mathf.Cos(angle) * reach, 0f, Mathf.Sin(angle) * reach);
            var cell = new CellPos((int)Mathf.Floor(spot.X), (int)Mathf.Floor(spot.Z));
            if (Open(world, cell))
            {
                flock.To = WorldCoords.ToGodotOnGround(cell.Center);
                break;
            }
        }

        flock.Flying = 0f;
        foreach (var crow in flock.Crows)
        {
            crow.LeftWing.Visible = crow.RightWing.Visible = true;
        }
    }

    // In the air: up and over in an arc, circling as they go, the wings beating; down on the new spot at the end.
    private static void Fly(Flock flock, float dt, float time)
    {
        flock.Flying += dt;
        var t = Mathf.Clamp(flock.Flying / FlightSeconds, 0f, 1f);
        var ground = flock.From.Lerp(flock.To, t);
        var height = Mathf.Sin(t * Mathf.Pi) * FlightHeight;
        flock.Root.Position = ground + new Vector3(0f, height, 0f);
        var heading = flock.To - flock.From;
        var yaw = heading.LengthSquared() > 0.01f ? Mathf.Atan2(-heading.X, -heading.Z) : 0f;
        for (var i = 0; i < flock.Crows.Count; i++)
        {
            var crow = flock.Crows[i];
            var circle = (time * 1.6f) + crow.Phase;
            crow.Body.Position = crow.Offset + new Vector3(Mathf.Cos(circle) * 0.35f * height, Mathf.Sin(circle * 1.3f) * 0.1f * height, Mathf.Sin(circle) * 0.35f * height);
            crow.Body.Rotation = new Vector3(0f, yaw, 0f);
            var beat = Mathf.Sin((time * 18f) + crow.Phase) * 0.7f;
            crow.LeftWing.Rotation = new Vector3(0f, 0f, beat);
            crow.RightWing.Rotation = new Vector3(0f, 0f, -beat);
        }

        if (t >= 1f)
        {
            flock.Flying = -1f;
            flock.At = flock.To;
            flock.Root.Position = flock.At;
            foreach (var crow in flock.Crows)
            {
                crow.LeftWing.Visible = crow.RightWing.Visible = false;
            }
        }
    }
}
