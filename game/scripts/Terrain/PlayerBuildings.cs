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
/// smokes (M4 step 13, by <see cref="DamageStates"/>). A factory whose model has a <c>gate</c> node lets it down when a unit comes out
/// (<see cref="UnitProduced"/>), holds it open a moment and lifts it again; the <c>spin_*</c> nodes of a model (a radar array) turn
/// while the owner has power (docs/art/model_standards.md, section 4).
/// </summary>
public partial class PlayerBuildings : Node3D
{
    /// <summary>How long a sold building takes to sink into the ground, in seconds.</summary>
    public const float SinkSeconds = 0.75f;

    private const float Sink = 0.3f;

    // A gate: how long it takes to open or shut, how long it stays open after a unit comes out (seconds), how far it opens (radians).
    private const float GateSeconds = 0.6f;
    private const float GateHoldSeconds = 2.5f;
    private const float GateAngle = 1.48f;

    // How fast a spin_* node turns, radians a second.
    private const float SpinSpeed = 1.2f;

    // How long the arm of a service depot takes to start working or to settle back, in seconds.
    internal const float ArmSettleSeconds = 0.6f;

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
        HashSet<(PlayerId, int, int)>? walls = null;
        foreach (var (building, buildUpLeft, ghost) in _ghosts.Update(world, SimulationHost.LocalPlayer, FogOfWar.Current?.Revealed ?? true))
        {
            var def = building.Def!;
            seen.Add(building.Id);
            // A new session numbers its entities again: an id seen before may be another building now. A building taken by an engineer
            // takes the colour of its new owner; a ghost keeps the colour it was last seen in.
            if (!_views.TryGetValue(building.Id, out var view) || !ReferenceEquals(view.Source, building) || (!ghost && view.Owner != building.Owner))
            {
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
            if (view.Gate is { } gate)
            {
                // The gate opens towards its +X turning about its Z, from its hinge (the node's origin); a ghost's stays shut.
                view.GateHold = Mathf.Max(0f, view.GateHold - (float)delta);
                var target = view.GateHold > 0f && !ghost ? 1f : 0f;
                view.GateOpen = Mathf.MoveToward(view.GateOpen, target, (float)delta / GateSeconds);
                gate.Rotation = new Vector3(0f, 0f, -Mathf.SmoothStep(0f, 1f, view.GateOpen) * GateAngle);
            }

            // A radar turns while the owner has power (RA2: it goes dark when the power is low); a ghost's stands still.
            if (view.Spinners.Count > 0 && !ghost && growth >= 1f && (building.Owner.IsNeutral || !world.Player(building.Owner).LowPower))
            {
                foreach (var spinner in view.Spinners)
                {
                    spinner.RotateObjectLocal(Vector3.Up, (float)delta * SpinSpeed);
                }
            }

            if (view.Machine is { } machine)
            {
                // The arm or the crane of a service depot or a shipyard works while it repairs (RepairSystem.IsRepairing) or builds
                // a ship, and settles back after.
                var working = !ghost && growth >= 1f
                    && (Rts.Simulation.Systems.RepairSystem.IsRepairing(world, building) || Builds(world, building));
                machine.Work = Mathf.MoveToward(machine.Work, working ? 1f : 0f, (float)delta / ArmSettleSeconds);
                machine.Clock += delta * machine.Work;
                machine.Pose(machine.Work, (float)machine.Clock);
            }

            if (view.Yard is { } yard)
            {
                // A construction yard runs its cycle while its owner builds; a cycle begun runs to its end.
                yard.Step((float)delta, !ghost && growth >= 1f && Builds(world, building));
            }

            if (view.Joins is { } joins)
            {
                // A wall shows its half-walls towards the neighbouring walls of its owner, so a line of segments is one wall.
                walls ??= WallCells(world);
                var (x, y) = (building.Cell.X, building.Cell.Y);
                joins.Show(walls.Contains((building.Owner, x, y - 1)), walls.Contains((building.Owner, x + 1, y)),
                    walls.Contains((building.Owner, x, y + 1)), walls.Contains((building.Owner, x - 1, y)));
            }

            if (view.Turret is { } turret)
            {
                // A defense's turret turns to its target and spins its gatlings while it has one; a ghost's stands still.
                turret.Step((float)delta, ghost || growth < 1f ? null : TargetPoint(world, building));
            }

            if (view.Glow is { } glow)
            {
                // A glow sphere (the iron curtain's) heats up red while the superweapon is ready.
                glow.Step((float)delta, !ghost && growth >= 1f
                    && building is { Def.Superweapon: not Rts.Simulation.Rules.SuperweaponKind.None, ChargeLeft: 0 });
            }

            if (view.Chrono is { } chrono)
            {
                // A chronosphere stands open, its drum turning, while its charge is full.
                chrono.Step((float)delta, !ghost && growth >= 1f
                    && building is { Def.Superweapon: not Rts.Simulation.Rules.SuperweaponKind.None, ChargeLeft: 0 });
            }

            if (view.Silo is { } silo)
            {
                // A missile silo stands open, its rocket up, while the missile is ready (its charge is full).
                silo.Step((float)delta, !ghost && growth >= 1f
                    && building is { Def.Superweapon: not Rts.Simulation.Rules.SuperweaponKind.None, ChargeLeft: 0 });
            }
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
    /// A sold building keeps its view a moment, sinking into the ground; a destroyed one turns into rubble; a factory with a gate opens
    /// it for the unit that comes out. Only where the local player sees it: in the fog its ghost stays.
    /// </summary>
    private void OnEvent(SimEvent simEvent)
    {
        if (_host is not null && simEvent is UnitProduced produced && _views.TryGetValue(produced.Factory, out var factory)
            && factory.Gate is not null && FogOfWar.Shows(_host.Simulation.World, factory.Source))
        {
            factory.GateHold = GateHoldSeconds;
            return;
        }

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
        var spinners = new List<Node3D>();
        CollectSpinners(body, spinners);
        return new View(building, root, body, dust, repair, unpowered, smoke, fire, Sink / full)
        {
            Gate = Buildings.FindNode(body, "gate"),
            Spinners = spinners,
            Machine = Buildings.FindNode(body, "arm") is { } arm && Buildings.FindNode(arm, "arm_upper") is { } upper
                && Buildings.FindNode(upper, "arm_fore") is { } fore ? new Arm(arm, upper, fore)
                : Buildings.FindNode(body, "crane") is { } crane && Buildings.FindNode(crane, "cable") is MeshInstance3D cable
                && Buildings.FindNode(crane, "hook") is { } hook ? new Crane(crane, cable, hook)
                : null,
            Yard = Yard.Find(body),
            Silo = Silo.Find(body),
            Joins = Joins.Find(body),
            Turret = Buildings.FindNode(body, "turret") is { } turretNode ? new Turret(turretNode) : null,
            Glow = Buildings.FindNode(body, "glow_sphere") is MeshInstance3D sphere ? new Glow(sphere) : null,
            Chrono = Buildings.FindNode(body, "dome") is { } dome && Buildings.FindNode(body, "drum") is { } drum ? new Chrono(dome, drum) : null,
        };
    }

    // The cells of the standing walls, with their owners.
    private static HashSet<(PlayerId, int, int)> WallCells(GameWorld world)
    {
        var cells = new HashSet<(PlayerId, int, int)>();
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def.Wall: true } wall)
            {
                cells.Add((wall.Owner, wall.Cell.X, wall.Cell.Y));
            }
        }

        return cells;
    }

    // Where a defense's target is, in Godot's space; null when it has none.
    internal static Vector3? TargetPoint(GameWorld world, Building building)
    {
        if (building.Target is not { } id || !world.TryGet(id, out var target))
        {
            return null;
        }

        var point = target switch
        {
            Unit unit => unit.Position,
            Building other => Rts.Simulation.Combat.Aim.CentreOf(other),
            _ => (Rts.Simulation.Geometry.WorldPos?)null,
        };
        return point is { } at ? WorldCoords.ToGodot(at) : null;
    }

    // Whether a factory works now: the first item of one of the queues it builds for (a construction yard's buildings and
    // defenses, a shipyard's ships) goes on, not paused, not waiting for credits, not ready.
    private static bool Builds(GameWorld world, Building factory)
    {
        if (factory.Owner.IsNeutral || factory.Def is not { } def)
        {
            return false;
        }

        var player = world.Player(factory.Owner);
        foreach (var category in def.Produces)
        {
            if (player.Queue(category) is [{ Ready: false, Paused: false, WaitingForFunds: false }, ..])
            {
                return true;
            }
        }

        return false;
    }

    // The nodes below a node whose names begin with spin_.
    private static void CollectSpinners(Node node, List<Node3D> into)
    {
        if (node is Node3D spatial && node.Name.ToString().StartsWith("spin_", System.StringComparison.Ordinal))
        {
            into.Add(spatial);
        }

        foreach (var child in node.GetChildren())
        {
            CollectSpinners(child, into);
        }
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

        /// <summary>The owner whose colour the view was made in.</summary>
        public PlayerId Owner { get; } = source.Owner;

        public Node3D Root { get; } = root;

        public Node3D Body { get; } = body;

        public CpuParticles3D Dust { get; } = dust;

        public Node3D Repair { get; } = repair;

        public Node3D Unpowered { get; } = unpowered;

        public CpuParticles3D Smoke { get; } = smoke;

        public CpuParticles3D Fire { get; } = fire;

        public float SinkLeft { get; set; }

        /// <summary>The gate of the model, or null: a factory's, let down while a unit comes out.</summary>
        public Node3D? Gate { get; init; }

        /// <summary>How far the gate is open, 0 to 1.</summary>
        public float GateOpen { get; set; }

        /// <summary>How long the gate stays open yet, in seconds.</summary>
        public float GateHold { get; set; }

        /// <summary>The spin_* nodes of the model: they turn about their Y while the owner has power.</summary>
        public IReadOnlyList<Node3D> Spinners { get; init; } = [];

        /// <summary>The arm or the crane of the model (a service depot's), or null.</summary>
        public Machine? Machine { get; init; }

        /// <summary>The working parts of a construction yard, or null.</summary>
        public Yard? Yard { get; init; }

        /// <summary>The shroud and the rocket of a missile silo, or null.</summary>
        public Silo? Silo { get; init; }

        /// <summary>The dome and the drum of a chronosphere, or null.</summary>
        public Chrono? Chrono { get; init; }

        /// <summary>The half-walls of a wall segment, or null.</summary>
        public Joins? Joins { get; init; }

        /// <summary>The turret of a defense, or null.</summary>
        public Turret? Turret { get; init; }

        /// <summary>The sphere of an iron curtain, or null.</summary>
        public Glow? Glow { get; init; }
    }

    /// <summary>
    /// The cycle of a construction yard (docs/art/model_standards.md, section 4): the roller gate rolls up, the carriage drives out
    /// to the pad, the claw goes down and lifts the crate, the carriage drives back in, the hatch opens, the claw lowers the crate
    /// into the pit, the hatch and the gate shut. The marks of the model (empties) say where the carriage stops and how far the claw
    /// goes.
    /// </summary>
    private sealed class Yard
    {
        private const float Cycle = 8f;

        private readonly Node3D _gate;
        private readonly Node3D _carriage;
        private readonly Node3D _crate;
        private readonly Node3D _cable;
        private readonly Node3D _hook;
        private readonly Node3D _hatchA;
        private readonly Node3D _hatchB;
        private readonly Vector3 _carriageRest;
        private readonly Vector3 _carriageOut;
        private readonly Vector3 _crateOnCarriage;
        private readonly Vector3 _hookRest;
        private readonly float _grabY;
        private readonly float _dropY;
        private readonly float _cableLength;
        private float _time;

        private Yard(Node3D gate, Node3D carriage, Node3D crate, Node3D cable, Node3D hook, Node3D hatchA, Node3D hatchB, Vector3 carriageOut,
            float grabY, float dropY)
        {
            (_gate, _carriage, _crate, _cable, _hook, _hatchA, _hatchB) = (gate, carriage, crate, cable, hook, hatchA, hatchB);
            _carriageRest = carriage.Position;
            _carriageOut = carriageOut;
            _crateOnCarriage = crate.Position - carriage.Position;
            _hookRest = hook.Position;
            (_grabY, _dropY) = (grabY, dropY);
            _cableLength = Mathf.Max(0.01f, cable.Position.Y - hook.Position.Y - HookHeight);
        }

        // From the claw's grab point up to the top of its block, where the cable ends (the model's).
        private const float HookHeight = 0.22f;

        public static Yard? Find(Node3D body) =>
            Buildings.FindNode(body, "roller_gate") is { } gate && Buildings.FindNode(body, "carriage") is { } carriage
            && Buildings.FindNode(body, "crate") is { } crate && Buildings.FindNode(body, "hoist_cable") is { } cable
            && Buildings.FindNode(body, "hoist_hook") is { } hook && Buildings.FindNode(body, "hatch_a") is { } hatchA
            && Buildings.FindNode(body, "hatch_b") is { } hatchB && Buildings.FindNode(body, "carriage_out") is { } carriageOut
            && Buildings.FindNode(body, "hoist_grab") is { } grab && Buildings.FindNode(body, "hoist_drop") is { } drop
                ? new Yard(gate, carriage, crate, cable, hook, hatchA, hatchB, carriageOut.Position, grab.Position.Y, drop.Position.Y)
                : null;

        public void Step(float delta, bool building)
        {
            if (_time > 0f || building)
            {
                _time += delta;
                if (_time >= Cycle)
                {
                    _time = building ? _time - Cycle : 0f;
                }
            }

            Pose(_time);
        }

        // The share of the way from a to b at time t, eased: 0 before a, 1 after b.
        private static float Span(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp((t - a) / (b - a), 0f, 1f));

        private void Pose(float t)
        {
            // The gate: up over 0–0.8 s, down over 6.4–7.2 s. Its slats roll up to its top, its origin.
            var gate = Span(t, 0f, 0.8f) - Span(t, 6.4f, 7.2f);
            _gate.Scale = new Vector3(1f, 1f - 0.92f * gate, 1f);

            // The carriage: out over 0.8–2.0 s, back in over 3.6–4.6 s.
            var carriage = Span(t, 0.8f, 2.0f) - Span(t, 3.6f, 4.6f);
            _carriage.Position = _carriageRest.Lerp(_carriageOut, carriage);

            // The claw: down to the crate over 2.0–2.8 s, up with it over 2.8–3.6 s; down into the pit over 5.2–6.4 s, up empty after.
            var hookY = _hookRest.Y;
            hookY = Mathf.Lerp(hookY, _grabY, Span(t, 2.0f, 2.8f) - Span(t, 2.8f, 3.6f));
            hookY = Mathf.Lerp(hookY, _dropY, Span(t, 5.2f, 6.4f) - Span(t, 6.4f, 7.2f));
            _hook.Position = new Vector3(_hookRest.X, hookY, _hookRest.Z);
            _cable.Scale = new Vector3(1f, (_cable.Position.Y - hookY - HookHeight) / _cableLength, 1f);

            // The crate: on the carriage until the claw has it (2.8 s), on the claw until it is in the pit (6.4 s), then gone until
            // the next cycle puts it on the carriage again, inside the hangar.
            _crate.Visible = t < 6.4f;
            _crate.Position = t is >= 2.8f and < 6.4f ? _hook.Position : _carriage.Position + _crateOnCarriage;

            // The hatch: open over 4.6–5.2 s, shut over 6.6–7.2 s; its leaves drop into the pit.
            var hatch = Span(t, 4.6f, 5.2f) - Span(t, 6.6f, 7.2f);
            _hatchA.Rotation = new Vector3(-1.48f * hatch, 0f, 0f);
            _hatchB.Rotation = new Vector3(1.48f * hatch, 0f, 0f);
        }
    }

    /// <summary>
    /// A missile silo (docs/art/model_standards.md, section 4): when its missile is ready, the shroud turns an eighth round, its
    /// halves slide apart (shroud_a to the mark shroud_open, shroud_b as far the other way) and the rocket rises to the mark
    /// rocket_up; when the missile is fired the rocket is gone at once and the silo shuts, and a rocket waits inside, down, once it
    /// is shut.
    /// </summary>
    private sealed class Silo
    {
        private const float OpenSeconds = 4f;
        private const float Turn = Mathf.Pi / 4f;

        private readonly Node3D _shroud;
        private readonly Node3D _halfA;
        private readonly Node3D _halfB;
        private readonly Node3D _rocket;
        private readonly Vector3 _openA;
        private readonly Vector3 _rocketDown;
        private readonly Vector3 _rocketUp;
        private float _open;
        private bool _wasReady;

        private Silo(Node3D shroud, Node3D halfA, Node3D halfB, Node3D rocket, Vector3 openA, Vector3 rocketUp)
        {
            (_shroud, _halfA, _halfB, _rocket, _openA, _rocketUp) = (shroud, halfA, halfB, rocket, openA, rocketUp);
            _rocketDown = rocket.Position;
        }

        public static Silo? Find(Node3D body) =>
            Buildings.FindNode(body, "shroud") is { } shroud && Buildings.FindNode(shroud, "shroud_a") is { } halfA
            && Buildings.FindNode(shroud, "shroud_b") is { } halfB && Buildings.FindNode(shroud, "shroud_open") is { } open
            && Buildings.FindNode(body, "rocket") is { } rocket && Buildings.FindNode(body, "rocket_up") is { } up
                ? new Silo(shroud, halfA, halfB, rocket, open.Position, up.Position)
                : null;

        public void Step(float delta, bool ready)
        {
            if (_wasReady && !ready)
            {
                _rocket.Visible = false;  // fired: the rocket has flown
            }

            _wasReady = ready;
            _open = Mathf.MoveToward(_open, ready ? 1f : 0f, delta / OpenSeconds);
            if (_open <= 0f)
            {
                _rocket.Visible = true;  // shut: the next rocket waits inside
            }

            // The shroud turns over the first third, the halves slide apart over the second, the rocket rises over the last.
            _shroud.Rotation = new Vector3(0f, Turn * Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_open * 3f, 0f, 1f)), 0f);
            var slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_open * 3f - 1f, 0f, 1f));
            _halfA.Position = _openA * slide;
            _halfB.Position = -_openA * slide;
            _rocket.Position = _rocketDown.Lerp(_rocketUp, Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_open * 3f - 2f, 0f, 1f)));
        }
    }

    /// <summary>
    /// A chronosphere (docs/art/model_standards.md, section 4): when its charge is full, the dome's moving half turns 90° about its X
    /// (its long axis, the hinge at its foot) onto the fixed half, and the drum inside turns about its X; when it has been used the
    /// drum slows to a stop and the dome shuts.
    /// </summary>
    private sealed class Chrono(Node3D dome, Node3D drum)
    {
        private const float OpenSeconds = 2f;
        private const float DrumSpeed = 2.5f;

        private float _open;
        private float _speed;

        public void Step(float delta, bool ready)
        {
            _open = Mathf.MoveToward(_open, ready ? 1f : 0f, delta / OpenSeconds);
            dome.Rotation = new Vector3(-Mathf.Pi / 2f * Mathf.SmoothStep(0f, 1f, _open), 0f, 0f);
            _speed = Mathf.MoveToward(_speed, ready ? DrumSpeed : 0f, delta * DrumSpeed / OpenSeconds);
            drum.RotateObjectLocal(Vector3.Right, delta * _speed);
        }
    }

    /// <summary>
    /// A sphere that heats up (docs/art/model_standards.md, section 4): while the superweapon is ready it glows red, its glow rising
    /// over a few seconds and then throbbing; when it has been used it cools down. Its own copy of the model's material carries the
    /// glow, so other buildings of the same model are not lit.
    /// </summary>
    private sealed class Glow
    {
        private const float HeatSeconds = 3f;
        private static readonly Color Red = new(1f, 0.12f, 0.06f);

        private readonly BaseMaterial3D? _material;
        private float _heat;
        private float _clock;

        public Glow(MeshInstance3D sphere)
        {
            if (sphere.Mesh?.SurfaceGetMaterial(0) is BaseMaterial3D source)
            {
                _material = (BaseMaterial3D)source.Duplicate();
                _material.EmissionEnabled = true;
                _material.Emission = Red;
                _material.EmissionEnergyMultiplier = 0f;
                sphere.SetSurfaceOverrideMaterial(0, _material);
            }
        }

        public void Step(float delta, bool ready)
        {
            if (_material is null)
            {
                return;
            }

            _heat = Mathf.MoveToward(_heat, ready ? 1f : 0f, delta / HeatSeconds);
            _clock += delta;
            var throb = 0.85f + 0.15f * Mathf.Sin(_clock * 3f);
            _material.EmissionEnergyMultiplier = 4f * _heat * _heat * throb;
            _material.AlbedoColor = Colors.White.Lerp(Red, _heat);
        }
    }

    /// <summary>
    /// The turret of a defense (docs/art/model_standards.md, section 4): it turns about its Y towards its target, its front along
    /// its -Z; its gatling_* nodes spin about their Z while it has a target, and run down after.
    /// </summary>
    internal sealed class Turret
    {
        private const float TurnSpeed = 4f;
        private const float SpinSpeed = 25f;

        private readonly Node3D _turret;
        private readonly List<Node3D> _gatlings = [];
        private float _spin;

        public Turret(Node3D turret)
        {
            _turret = turret;
            foreach (var child in turret.GetChildren())
            {
                if (child is Node3D node && node.Name.ToString().StartsWith("gatling_", System.StringComparison.Ordinal))
                {
                    _gatlings.Add(node);
                }
            }
        }

        public void Step(float delta, Vector3? target)
        {
            if (target is { } at)
            {
                var from = _turret.GlobalPosition;
                var parentYaw = _turret.GetParentNode3D()?.GlobalRotation.Y ?? 0f;
                var yaw = Mathf.Atan2(-(at.X - from.X), -(at.Z - from.Z)) - parentYaw;
                var now = _turret.Rotation.Y;
                var step = Mathf.Wrap(yaw - now, -Mathf.Pi, Mathf.Pi);
                _turret.Rotation = new Vector3(0f, now + Mathf.Clamp(step, -TurnSpeed * delta, TurnSpeed * delta), 0f);
            }

            _spin = Mathf.MoveToward(_spin, target is null ? 0f : SpinSpeed, delta * SpinSpeed);
            foreach (var gatling in _gatlings)
            {
                gatling.RotateObjectLocal(Vector3.Back, delta * _spin);
            }
        }
    }

    /// <summary>The half-walls of a wall segment (docs/art/model_standards.md, section 4): join_n, join_e, join_s and join_w.</summary>
    private sealed class Joins(Node3D north, Node3D east, Node3D south, Node3D west)
    {
        public static Joins? Find(Node3D body) =>
            Buildings.FindNode(body, "join_n") is { } n && Buildings.FindNode(body, "join_e") is { } e
            && Buildings.FindNode(body, "join_s") is { } s && Buildings.FindNode(body, "join_w") is { } w
                ? new Joins(n, e, s, w)
                : null;

        public void Show(bool toNorth, bool toEast, bool toSouth, bool toWest)
        {
            north.Visible = toNorth;
            east.Visible = toEast;
            south.Visible = toSouth;
            west.Visible = toWest;
        }
    }

    /// <summary>The moving parts of a service depot: at rest in the pose of the model, busy while the depot repairs.</summary>
    internal abstract class Machine
    {
        /// <summary>How much it works, 0 (at rest in the pose of the model) to 1.</summary>
        public float Work { get; set; }

        /// <summary>Its own clock, running while it works.</summary>
        public double Clock { get; set; }

        public abstract void Pose(float work, float clock);

        protected static Transform3D Turned(Transform3D rest, Vector3 axis, float angle) =>
            new(rest.Basis * new Basis(axis, angle), rest.Origin);
    }

    /// <summary>
    /// A jointed arm (the Alliance's depot): its base turns about its Y, the upper arm and the forearm about their Z, each from the
    /// pose of the model; at work they swing to and fro out of step, so it looks busy over the vehicle on the pad.
    /// </summary>
    internal sealed class Arm(Node3D bottom, Node3D upper, Node3D fore) : Machine
    {
        private readonly Transform3D _bottom = bottom.Transform;
        private readonly Transform3D _upper = upper.Transform;
        private readonly Transform3D _fore = fore.Transform;

        public override void Pose(float work, float clock)
        {
            bottom.Transform = Turned(_bottom, Vector3.Up, work * 0.45f * Mathf.Sin(clock * 0.9f));
            upper.Transform = Turned(_upper, Vector3.Back, work * 0.2f * Mathf.Sin(clock * 1.4f));
            fore.Transform = Turned(_fore, Vector3.Back, work * 0.3f * Mathf.Sin(clock * 2.1f + 1f));
        }
    }

    /// <summary>
    /// A crane (the Union's depot): it swings left and right about its Y, and lets its hook down and draws it up again; the cable,
    /// hanging from the jib's end along its -Y, stretches down to the hook.
    /// </summary>
    private sealed class Crane(Node3D crane, MeshInstance3D cable, Node3D hook) : Machine
    {
        private readonly Transform3D _crane = crane.Transform;
        private readonly Vector3 _hook = hook.Position;
        private readonly float _cable = Mathf.Max(0.01f, cable.GetAabb().Size.Y);

        public override void Pose(float work, float clock)
        {
            crane.Transform = Turned(_crane, Vector3.Up, work * 0.35f * Mathf.Sin(clock * 0.6f));
            var drop = work * (0.22f + 0.18f * Mathf.Sin(clock * 1.2f));
            hook.Position = _hook - new Vector3(0f, drop, 0f);
            cable.Scale = new Vector3(1f, (_cable + drop) / _cable, 1f);
        }
    }
}
