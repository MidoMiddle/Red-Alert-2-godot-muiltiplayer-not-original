using System.Collections.Generic;
using Godot;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic buildings (docs/implementation/conventions.md, section 12; M7_maps.md, 4.8): a block over the footprint, beige for
/// civilian houses and grey-teal for tech buildings, with a mark of their own — a tower on the oil derrick, a white cross on
/// the hospital, a pale runway on the airfield, an orange roof on the bridge repair hut — and a band in the owner's colour on a building of a player. The buildings
/// players place are drawn by <see cref="PlayerBuildings"/> (M3 step 13), one view each. The footprint sinks a little into the
/// ground so a block on uneven ground leaves no gap. Rebuilt whenever the map changes (a building is placed or falls), a building
/// changes hands (a tech one taken by loose ends, stage 1; a civilian one with soldiers) or its state (hurt, or with soldiers inside),
/// which is rare.
/// </summary>
public partial class Buildings : Node3D
{
    private const float Margin = 0.08f;
    private const float Sink = 0.3f;
    // A working pumpjack nods its beam: how fast, and how far (radians either way).
    private const float NodSpeed = 1.6f;
    private const float NodAngle = 0.25f;

    private static readonly Color Civilian = new(0.78f, 0.7f, 0.55f);
    private static readonly Color Tech = new(0.45f, 0.58f, 0.6f);

    private object? _builtMap;
    private int _builtVersion = -1;
    private long _builtOwners = -1;
    private SimulationHost? _host;
    private static readonly Color RubbleColor = new(0.2f, 0.19f, 0.18f);
    private readonly List<Ruin> _ruins = [];
    // The beams of the working pumpjacks, and the clock they nod by.
    private readonly List<Node3D> _beams = [];
    private double _clock;

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
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("Buildings.process");
        _clock += delta;
        foreach (var beam in _beams)
        {
            // Turned about the node's origin, which is the pivot of the beam.
            beam.Rotation = new Vector3(0f, 0f, Mathf.Sin((float)_clock * NodSpeed) * NodAngle);
        }

        // The map of the terrain: a match's, or the editor's (M7 step 12), which has no world and shows its start buildings.
        var map = GetParent<TerrainBuilder>().Map;
        var world = GetNodeOrNull<SimulationHost>("../../SimulationHost")?.Simulation.World;
        var owners = Owners(world);
        if (!ReferenceEquals(map, _builtMap) || map.LayoutVersion != _builtVersion || owners != _builtOwners)
        {
            _builtOwners = owners;
            Rebuild(map, world);
        }
    }

    // A digest of the buildings of the map — who owns them, how hurt they are, who is inside — to see one change.
    private static long Owners(GameWorld? world)
    {
        // The editor has no world. (An empty EntityList made by [] holds no list: enumerating it threw every frame.)
        long digest = 0;
        if (world is null)
        {
            return digest;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def: null } building)
            {
                // Who owns it, whether it is being repaired (its green cross), how hurt it is and who is inside (its look).
                digest = (digest * 31) + (building.Id.Value * 17) + building.Owner.Value + (building.Repairing ? 7 : 0)
                    + ((int)DamageStates.Of(building.Hp, building.MaxHp) * 101) + (building.Occupants.Count * 1009);
            }
        }

        return digest;
    }

    private void Rebuild(Rts.Simulation.Map.MapGrid map, GameWorld? world)
    {
        _beams.Clear();
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _builtMap = map;
        _builtVersion = map.LayoutVersion;
        if (world is null)
        {
            foreach (var start in map.StartBuildings)
            {
                Add(start.Kind, start.Cell, start.Owner);
            }

            return;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def: null } building)
            {
                Add(building.Kind, building.Cell, building.Owner, building.Repairing,
                    DamageStates.Of(building.Hp, building.MaxHp), building.Occupants.Count > 0);
            }
        }

        // The ruins of the fallen stay on their footprints; a building put on them, or a new map, takes them away.
        for (var i = _ruins.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_ruins[i].World, world) || Covered(world, _ruins[i]))
            {
                _ruins.RemoveAt(i);
            }
        }

        foreach (var ruin in _ruins)
        {
            AddRuin(ruin);
        }
    }

    private void Add(BuildingKind kind, Rts.Simulation.Geometry.CellPos cell, PlayerId owner, bool repairing = false,
        DamageState state = DamageState.Healthy, bool occupied = false)
    {
        var type = BuildingTypes.Get(kind);
        var tech = type.Capturable;
        var height = kind switch
        {
            BuildingKind.NeutralShed => 0.35f,
            BuildingKind.NeutralHouseA => 0.6f,
            BuildingKind.NeutralApartment => 1.4f,
            BuildingKind.TechOilDerrick => 0.5f,
            BuildingKind.TechHospital => 0.5f,
            BuildingKind.TechAirfield => 0.2f,
            BuildingKind.NeutralFuelTank => 0.6f,
            BuildingKind.NeutralSmokestack => 1.8f,
            BuildingKind.NeutralCoolingTower => 1.2f,
            BuildingKind.NeutralFactoryHall => 0.9f,
            BuildingKind.NeutralBoilerHouse => 0.6f,
            BuildingKind.TechBridgeRepairHut => 0.45f,
            // A building of the element documents has its look (BuildingLooks); the height is for the others only.
            _ => 0.6f,
        };
        var centre = new Vector3(cell.X + type.Width / 2f, 0, cell.Y + type.Height / 2f);
        if (repairing)
        {
            // Under repair (the repair mode of the sidebar): a green cross over it, as over the player's own buildings.
            var over = centre + new Vector3(0, WorldCoords.Terrain!.HeightAt(centre.X, centre.Z) + 1.6f, 0);
            Box(new Vector3(0.5f, 0.15f, 0.15f), over, new Color(0.3f, 0.95f, 0.35f));
            Box(new Vector3(0.15f, 0.5f, 0.15f), over, new Color(0.3f, 0.95f, 0.35f));
        }

        var ground = WorldCoords.Terrain!.HeightAt(centre.X, centre.Z);
        Color? colour = owner.IsNeutral ? null : OwnerColor(owner);
        var at = new Vector2I(cell.X, cell.Y);
        // The file of the state it is in, by its damage and its soldiers (BuildingStates); a state the table gives no file for is drawn
        // as the whole one, as before.
        var variant = Models.BuildingStates.Of(state, occupied);
        var model = Models.ModelLibrary.Instantiate("building/" + type.Id, colour, cell: at, variant: variant)
            ?? (variant == Models.ModelVariant.Intact ? null : Models.ModelLibrary.Instantiate("building/" + type.Id, colour, cell: at));
        if (model is not null)
        {
            // The model of the table (docs/art/model_standards.md); a taken tech building shows its owner in its team_mask.
            model.Position += centre + new Vector3(0, ground, 0);
            AddChild(model);
            // A working pumpjack: its beam is the node named beam, and it nods.
            if (variant == Models.ModelVariant.Intact && FindNode(model, "beam") is { } beam)
            {
                _beams.Add(beam);
            }

            // The smoke and the fire stand over the height the bars of the building stand over: the table's, else the placeholder's.
            var modelHeight = Models.ModelLibrary.Find("building/" + type.Id)?.Height ?? 0f;
            BuildingBlaze.Add(this, model, new Vector3(centre.X, ground + (modelHeight > 0f ? modelHeight : height) + 0.1f, centre.Z),
                type.Width, type.Height, state);
            return;
        }

        if (BuildingLooks.Known(type.Id))
        {
            // A building of the element documents: its look in the civilian colours, the owner's band when someone holds it.
            var body = new Node3D { Position = centre + new Vector3(0, ground - Sink, 0) };
            AddChild(body);
            BuildingLookView.AddParts(body, BuildingLooks.Of(type.Id, type.Width, type.Height, false), LookFaction.Civilian,
                owner.IsNeutral ? null : OwnerColor(owner), Sink, band: true);
            return;
        }

        var size = new Vector3(type.Width - 2 * Margin, height + Sink, type.Height - 2 * Margin);
        Box(size, centre + new Vector3(0, ground + (height - Sink) / 2, 0), tech ? Tech : Civilian);
        var top = ground + height;

        switch (kind)
        {
            case BuildingKind.TechOilDerrick:
                Box(new Vector3(0.18f, 1.1f, 0.18f), centre + new Vector3(0.4f, top + 0.55f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
                break;
            case BuildingKind.TechHospital:
                Box(new Vector3(1.4f, 0.04f, 0.4f), centre + new Vector3(0, top + 0.02f, 0), Colors.White);
                Box(new Vector3(0.4f, 0.04f, 1.4f), centre + new Vector3(0, top + 0.02f, 0), Colors.White);
                break;
            case BuildingKind.TechBridgeRepairHut:
                // The bridge repair hut (loose ends, stage 2): an orange roof.
                Box(new Vector3(0.9f, 0.08f, 0.9f), centre + new Vector3(0, top + 0.04f, 0), new Color(0.9f, 0.5f, 0.15f));
                break;
            case BuildingKind.TechAirfield:
                Box(new Vector3(type.Width - 0.6f, 0.03f, 0.5f), centre + new Vector3(0, top + 0.015f, 0), new Color(0.85f, 0.85f, 0.8f));
                break;
        }

        if (!owner.IsNeutral)
        {
            Box(new Vector3(size.X + 0.02f, 0.08f, size.Z + 0.02f), centre + new Vector3(0, top - 0.1f, 0), OwnerColor(owner));
        }
    }

    /// <summary>The colour of a player's band.</summary>
    internal static Color OwnerColor(PlayerId owner) => Matches.PlayerColors.Of(owner);

    // A building of the map that falls leaves its ruins on the footprint (RA2): the model of them where the table names one, else a slab
    // of rubble.
    private void OnEvent(SimEvent simEvent)
    {
        if (simEvent is BuildingDestroyed destroyed && _host is not null && BuildingTypes.IsKnown(destroyed.Kind)
            && destroyed.Kind != BuildingKind.TechBridgeRepairHut)
        {
            _ruins.Add(new Ruin(_host.Simulation.World, destroyed.Kind, destroyed.Cell));
            _builtOwners = -1;
        }
    }

    private void AddRuin(Ruin ruin)
    {
        var type = BuildingTypes.Get(ruin.Kind);
        var centre = new Vector3(ruin.Cell.X + type.Width / 2f, 0, ruin.Cell.Y + type.Height / 2f);
        var ground = WorldCoords.Terrain!.HeightAt(centre.X, centre.Z);
        if (Models.ModelLibrary.Find("building/" + type.Id)?.Ruins is not null
            && Models.ModelLibrary.Instantiate("building/" + type.Id, variant: Models.ModelVariant.Ruins) is { } model)
        {
            model.Position += centre + new Vector3(0, ground, 0);
            AddChild(model);
            return;
        }

        // A low slab of rubble with a few broken blocks on it.
        var rubble = new StandardMaterial3D { AlbedoColor = RubbleColor, Roughness = 1f };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(type.Width - 0.1f, 0.18f, type.Height - 0.1f) },
            MaterialOverride = rubble,
            Position = centre + new Vector3(0, ground + 0.09f, 0),
        });
        for (var i = 0; i < type.Width * type.Height / 2 + 1; i++)
        {
            var x = ((i * 37 % 10) / 10f - 0.45f) * (type.Width - 0.4f);
            var z = ((i * 53 % 10) / 10f - 0.45f) * (type.Height - 0.4f);
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.22f + (i % 3) * 0.08f, 0.3f) },
                MaterialOverride = rubble,
                Position = centre + new Vector3(x, ground + 0.25f, z),
                RotationDegrees = new Vector3(0, i * 29 % 90, (i % 2) * 12f),
            });
        }
    }

    // Whether a building stands on any cell of the footprint of the ruins.
    private static bool Covered(GameWorld world, Ruin ruin)
    {
        var type = BuildingTypes.Get(ruin.Kind);
        for (var y = ruin.Cell.Y; y < ruin.Cell.Y + type.Height; y++)
        {
            for (var x = ruin.Cell.X; x < ruin.Cell.X + type.Width; x++)
            {
                if (world.Map.HasBuilding(new Rts.Simulation.Geometry.CellPos(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record Ruin(GameWorld World, BuildingKind Kind, Rts.Simulation.Geometry.CellPos Cell);

    // The node of that name below a node, the first one found.
    private static Node3D? FindNode(Node node, string name)
    {
        if (node is Node3D spatial && node.Name.ToString() == name)
        {
            return spatial;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindNode(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void Box(Vector3 size, Vector3 position, Color color) =>
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = position,
        });
}
