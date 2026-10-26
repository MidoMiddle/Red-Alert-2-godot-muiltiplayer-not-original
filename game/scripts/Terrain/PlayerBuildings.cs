using System.Collections.Generic;
using Godot;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// The buildings players place (docs/implementation/M3_base_economy.md, 4.17): one view per building, made when it appears, so the
/// others are not rebuilt. A view is the look of the building (<see cref="BuildingLooks"/>) with the owner's band and the pads of its
/// <see cref="BuildingShape"/>. It
/// rises out of the ground smoothly while the building grows, with dust around it, and sinks back into the ground when the building
/// is sold. A green cross turns over a building under repair. A destroyed building (M4 step 9) leaves a dark slab of rubble on its
/// footprint, with a puff of dust, until another building stands on those cells. A yellow bolt blinks over a defense off for want of
/// power (M4 step 10, <see cref="Defenses.IsOnline"/>). Another player's buildings show as the local player knows them (M4 step 12,
/// <see cref="BuildingGhosts"/>): as they stand while seen, as last seen in the fog, not at all before they are seen; what happens to
/// one in the fog — a sale, a fall — shows only when it is seen again. A damaged building smokes, a critically damaged one burns and
/// smokes (M4 step 13, by <see cref="DamageStates"/>).
/// </summary>
public partial class PlayerBuildings : Node3D
{
    /// <summary>How long a sold building takes to sink into the ground, in seconds.</summary>
    public const float SinkSeconds = 0.75f;

    private const float Sink = 0.3f;

    private static readonly Color Dark = new(0.25f, 0.26f, 0.29f);
    private static readonly Color Pad = new(0.95f, 0.8f, 0.15f);
    private static readonly Color RepairColor = new(0.3f, 0.95f, 0.35f);
    private static readonly Color RubbleColor = new(0.17f, 0.16f, 0.15f);
    private static readonly Color BoltColor = new(1f, 0.85f, 0.1f);

    private readonly Dictionary<EntityId, View> _views = new();
    private readonly List<View> _sinking = [];
    private readonly List<Rubble> _rubble = [];
    private readonly BuildingGhosts _ghosts = new();
    private SimulationHost? _host;
    private double _time;

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../../SimulationHost");
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
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("PlayerBuildings.process");
        if (_host is null || WorldCoords.Terrain is null)
        {
            return;
        }

        _time += delta;
        var world = _host.Simulation.World;
        var seen = new HashSet<EntityId>();
        foreach (var (building, buildUpLeft, ghost) in _ghosts.Update(world, SimulationHost.LocalPlayer, FogOfWar.Current?.Revealed ?? true))
        {
            var def = building.Def!;
            seen.Add(building.Id);
            if (!_views.TryGetValue(building.Id, out var view) || !ReferenceEquals(view.Source, building))
            {
                // A new session numbers its entities again: an id seen before may be another building now.
                view?.Root.QueueFree();
                view = MakeView(building);
                _views[building.Id] = view;
            }

            // A ghost stands as it was last seen, without the marks of what it does now.
            var growth = BuildingShape.Growth(def.BuildUp, buildUpLeft, ghost ? 1f : _host.Alpha);
            view.Body.Scale = new Vector3(1f, view.Rise(growth), 1f);
            view.Dust.Emitting = growth < 1f && !ghost;
            view.Repair.Visible = building.Repairing && !ghost;
            view.Unpowered.Visible = !ghost && !Defenses.IsOnline(world, building) && (int)(_time * 2.5) % 2 == 0;
            var state = DamageStates.Of(building.Hp, building.MaxHp);
            view.Smoke.Emitting = !ghost && state != DamageState.Healthy;
            view.Fire.Emitting = !ghost && state == DamageState.Critical;
            view.Repair.Rotation = new Vector3(0, (float)(_time * 2.5), 0);
        }

        // A view whose building is gone without a sale (a new session, a replay) goes at once.
        var gone = new List<EntityId>();
        foreach (var (id, view) in _views)
        {
            if (!seen.Contains(id))
            {
                gone.Add(id);
                view.Root.QueueFree();
            }
        }

        foreach (var id in gone)
        {
            _views.Remove(id);
        }

        for (var i = _rubble.Count - 1; i >= 0; i--)
        {
            // Rubble of another session goes, and so does rubble that a new building covers.
            var rubble = _rubble[i];
            if (!ReferenceEquals(rubble.World, world) || rubble.Covered(world))
            {
                rubble.Root.QueueFree();
                _rubble.RemoveAt(i);
            }
        }

        for (var i = _sinking.Count - 1; i >= 0; i--)
        {
            var view = _sinking[i];
            view.SinkLeft -= (float)delta;
            view.Body.Scale = new Vector3(1f, view.Rise(Mathf.Max(0f, view.SinkLeft / SinkSeconds)), 1f);
            if (view.SinkLeft <= 0f)
            {
                view.Root.QueueFree();
                _sinking.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// A sold building keeps its view a moment, sinking into the ground; a destroyed one turns into rubble. Only where the local player
    /// sees it: in the fog its ghost stays.
    /// </summary>
    private void OnEvent(SimEvent simEvent)
    {
        if (_host is null || simEvent is not (BuildingSold or BuildingDestroyed))
        {
            return;
        }

        var id = simEvent is BuildingSold { Building: var soldId } ? soldId : ((BuildingDestroyed)simEvent).Building;
        if (!_views.TryGetValue(id, out var shown) || !FogOfWar.Shows(_host.Simulation.World, shown.Source))
        {
            return;
        }

        if (simEvent is BuildingSold sold && _views.Remove(sold.Building, out var view))
        {
            view.SinkLeft = SinkSeconds;
            view.Dust.Emitting = true;
            view.Repair.Visible = false;
            view.Unpowered.Visible = false;
            view.Smoke.Emitting = false;
            view.Fire.Emitting = false;
            _sinking.Add(view);
        }
        else if (simEvent is BuildingDestroyed destroyed && _views.Remove(destroyed.Building, out var fallen))
        {
            fallen.Root.QueueFree();
            _rubble.Add(MakeRubble(fallen.Source, _host.Simulation.World));
        }
    }

    /// <summary>A dark slab over the footprint with a few blocks on it, and one puff of dust.</summary>
    private Rubble MakeRubble(Building building, GameWorld world)
    {
        var centre = new Vector3(building.Cell.X + building.Width / 2f, 0, building.Cell.Y + building.Height / 2f);
        var root = new Node3D { Position = centre + new Vector3(0, WorldCoords.Terrain!.HeightAt(centre.X, centre.Z), 0) };
        AddChild(root);
        Box(root, new Vector3(building.Width - 0.2f, 0.12f, building.Height - 0.2f), new Vector3(0, 0.06f, 0), RubbleColor);
        for (var i = 0; i < building.Width * building.Height; i++)
        {
            // Blocks at fixed spots of the footprint, so the same building always leaves the same heap.
            var x = (i % building.Width) - building.Width / 2f + 0.5f + ((i * 37 % 7) - 3) * 0.06f;
            var z = (i / building.Width) - building.Height / 2f + 0.5f + ((i * 53 % 5) - 2) * 0.08f;
            Box(root, new Vector3(0.35f, 0.18f + (i % 3) * 0.08f, 0.3f), new Vector3(x, 0.2f, z), i % 2 == 0 ? Dark : RubbleColor);
        }

        // Emitting starts once the dust is in the tree: particles need its place in the world.
        var dust = Dust(building.Width, building.Height);
        dust.OneShot = true;
        dust.Position = new Vector3(0, 0.1f, 0);
        root.AddChild(dust);
        dust.Emitting = true;
        return new Rubble(world, building.Cell, building.Width, building.Height, root);
    }

    private View MakeView(Building building)
    {
        var shape = BuildingShape.Of(building.Def!);
        var centre = new Vector3(building.Cell.X + building.Width / 2f, 0, building.Cell.Y + building.Height / 2f);
        var ground = WorldCoords.Terrain!.HeightAt(centre.X, centre.Z);
        var root = new Node3D { Position = centre + new Vector3(0, ground - Sink, 0) };
        // The body grows from below the ground: its scale is the share grown.
        var body = new Node3D();
        root.AddChild(body);
        AddChild(root);

        var full = shape.Height + Sink;
        BuildingLookView.AddTo(body, building.Def!, Buildings.OwnerColor(building.Owner), Sink, new Vector2I(building.Cell.X, building.Cell.Y));

        // The dock and the exit lie on the ground, outside the scaled body: the rules use them while the building grows too.
        var northWest = new Vector3(-building.Width / 2f, Sink + 0.02f, -building.Height / 2f);
        if (shape.Dock is { } dock)
        {
            Box(root, new Vector3(0.8f, 0.04f, 0.8f), northWest + new Vector3(dock.X + 0.5f, 0, dock.Y + 0.5f), Pad);
        }

        if (shape.Exit is { } exit)
        {
            Box(root, new Vector3(0.8f, 0.04f, 0.25f), northWest + new Vector3(exit.X + 0.5f, 0, exit.Y + 0.15f), Pad);
        }

        var repair = new Node3D { Position = new Vector3(0, Sink + shape.Height + 0.8f, 0), Visible = false };
        Box(repair, new Vector3(0.5f, 0.15f, 0.15f), Vector3.Zero, RepairColor);
        Box(repair, new Vector3(0.15f, 0.5f, 0.15f), Vector3.Zero, RepairColor);
        root.AddChild(repair);

        // A zigzag of three bars: the "no power" bolt of RA2, over the building.
        var unpowered = new Node3D { Position = new Vector3(0, Sink + shape.Height + 0.9f, 0), Visible = false };
        Box(unpowered, new Vector3(0.12f, 0.35f, 0.12f), new Vector3(0.08f, 0.22f, 0), BoltColor);
        Box(unpowered, new Vector3(0.3f, 0.1f, 0.12f), Vector3.Zero, BoltColor);
        Box(unpowered, new Vector3(0.12f, 0.35f, 0.12f), new Vector3(-0.08f, -0.22f, 0), BoltColor);
        root.AddChild(unpowered);

        var dust = Dust(building.Width, building.Height);
        dust.Position = new Vector3(0, Sink + 0.1f, 0);
        root.AddChild(dust);

        // Damage (M4 step 13): dark smoke rises off the roof, flames lick under it once it is critical.
        var top = new Vector3(0, Sink + shape.Height + 0.1f, 0);
        var smoke = BuildingBlaze.Plume(building.Width, building.Height, new Color(0.25f, 0.25f, 0.25f), 0.12f, 1.6, 0.6f);
        smoke.Position = top;
        root.AddChild(smoke);
        var fire = BuildingBlaze.Plume(building.Width, building.Height, new Color(1f, 0.45f, 0.1f), 0.09f, 0.6, 0.9f);
        fire.Position = top;
        root.AddChild(fire);
        return new View(building, root, body, dust, repair, unpowered, smoke, fire, Sink / full);
    }

    /// <summary>Grey dust round the footprint while a building rises or sinks: small cubes that fly up and fade.</summary>
    private static CpuParticles3D Dust(int width, int height) =>
        new()
        {
            Emitting = false,
            Amount = 40,
            Lifetime = 0.9,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(width / 2f, 0.05f, height / 2f),
            Direction = Vector3.Up,
            Spread = 60f,
            InitialVelocityMin = 0.6f,
            InitialVelocityMax = 1.4f,
            Gravity = new Vector3(0, -0.6f, 0),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.2f,
            Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.2f, 0.2f), Material = Material(new Color(0.78f, 0.75f, 0.7f)) },
        };

    private static void Box(Node3D parent, Vector3 size, Vector3 position, Color color) =>
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = Material(color) }, Position = position });

    private static StandardMaterial3D Material(Color color) => new() { AlbedoColor = color };

    /// <summary>The rubble of a destroyed building on its footprint, until another building covers a cell of it.</summary>
    private sealed record Rubble(GameWorld World, Rts.Simulation.Geometry.CellPos Cell, int Width, int Height, Node3D Root)
    {
        public bool Covered(GameWorld world)
        {
            for (var y = Cell.Y; y < Cell.Y + Height; y++)
            {
                for (var x = Cell.X; x < Cell.X + Width; x++)
                {
                    if (world.Map.HasBuilding(new Rts.Simulation.Geometry.CellPos(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private sealed class View(
        Building source, Node3D root, Node3D body, CpuParticles3D dust, Node3D repair, Node3D unpowered, CpuParticles3D smoke, CpuParticles3D fire,
        float buried)
    {
        /// <summary>The scale of the body for a share grown: from the ground (the buried foot) up to the full height.</summary>
        public float Rise(float growth) => Mathf.Max(0.01f, buried + (1f - buried) * growth);

        public Building Source { get; } = source;

        public Node3D Root { get; } = root;

        public Node3D Body { get; } = body;

        public CpuParticles3D Dust { get; } = dust;

        public Node3D Repair { get; } = repair;

        public Node3D Unpowered { get; } = unpowered;

        public CpuParticles3D Smoke { get; } = smoke;

        public CpuParticles3D Fire { get; } = fire;

        public float SinkLeft { get; set; }
    }
}
