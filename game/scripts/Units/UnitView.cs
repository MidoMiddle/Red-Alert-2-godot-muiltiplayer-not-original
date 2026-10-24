using System.Collections.Generic;
using Godot;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// Symbolic look of one unit (docs/implementation/conventions.md, section 12): primitives in the team colour, a dark nose that shows
/// where the hull points, and a turret on top for types that have one. Until the models come, every type has a shape of its own that
/// tells its role at a glance (the owner's wish, 2026-10-24):
/// <list type="bullet">
/// <item>infantry is a thin column with what it carries — a rifle, a launcher, a hard hat, coils, a tank on the back, a jet pack; dogs
/// are low and long;</item>
/// <item>vehicles are boxes sized by role on tracks or wheels, with their gun: a long barrel, two barrels, a coil, a crystal, a rocket
/// rail, barrels to the sky for anti-air; a hover disk for the robot, a spider for the drone, a truck for the demolition truck;</item>
/// <item>ships are long hulls with their deck — a gun, missile cells, a flight deck; the submarine is a low cigar, the dolphin and the
/// squid are round;</item>
/// <item>aircraft have wings, the helicopter a rotor, the airship is a big balloon.</item>
/// </list>
/// The model looks along −Z; the parent sets the yaw.
/// </summary>
public partial class UnitView : Node3D
{
    private static readonly StandardMaterial3D DarkMaterial = new() { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) };
    private static readonly StandardMaterial3D EngineerMaterial = new() { AlbedoColor = new Color(0.95f, 0.8f, 0.15f) };
    private static readonly StandardMaterial3D HopperMaterial = new() { AlbedoColor = new Color(0.55f, 0.42f, 0.22f) };
    private static readonly StandardMaterial3D TrackMaterial = new() { AlbedoColor = new Color(0.36f, 0.36f, 0.38f) };
    private static readonly StandardMaterial3D RedMaterial = new() { AlbedoColor = new Color(0.8f, 0.12f, 0.1f) };
    private static readonly StandardMaterial3D ToxinMaterial = new() { AlbedoColor = new Color(0.45f, 0.85f, 0.15f) };
    private static readonly StandardMaterial3D GlowMaterial = new()
    {
        AlbedoColor = new Color(0.45f, 0.85f, 1f),
        EmissionEnabled = true,
        Emission = new Color(0.3f, 0.7f, 1f),
    };

    private static readonly StandardMaterial3D SandbagMaterial = new() { AlbedoColor = new Color(0.66f, 0.58f, 0.4f) };

    private MeshInstance3D _body = null!;
    private Node3D? _turret;
    private List<MeshInstance3D>? _teamMeshes;
    private MeshInstance3D? _turretBody;
    private Node3D? _sandbags;

    public UnitType Type { get; private set; }

    /// <summary>Builds the shapes for <paramref name="archetype"/>. Called once; pooled views keep their type.</summary>
    public void Build(UnitArchetype archetype)
    {
        Type = archetype.Type;
        if (Models.ModelLibrary.Instantiate("unit/" + archetype.Id) is { } model)
        {
            // The model of the table (docs/art/model_standards.md): its turret node turns, its team_mask takes the player's colour.
            AddChild(model);
            _turret = model.FindChild("turret", true, false) as Node3D;
            _teamMeshes = [];
            CollectTeam(model, false, _teamMeshes);
            return;
        }

        if (archetype.Category == UnitCategory.Infantry)
        {
            BuildInfantry(archetype.Type);
        }
        else if (archetype.Category == UnitCategory.Aircraft)
        {
            BuildAircraft(archetype.Type);
        }
        else if (IsShip(archetype.Type))
        {
            BuildShip(archetype.Type);
        }
        else
        {
            BuildVehicle(archetype);
        }
    }

    /// <summary>Shows or hides the ring of sandbags of a soldier dug in; made the first time it is shown.</summary>
    public void SetSandbags(bool shown)
    {
        if (_sandbags is null)
        {
            if (!shown)
            {
                return;
            }

            _sandbags = new Node3D { Name = "Sandbags" };
            AddChild(_sandbags);
            const int Bags = 8;
            for (var i = 0; i < Bags; i++)
            {
                var angle = i * Mathf.Tau / Bags;
                _sandbags.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(0.14f, 0.09f, 0.07f) },
                    Position = new Vector3(Mathf.Cos(angle) * 0.24f, 0.045f, Mathf.Sin(angle) * 0.24f),
                    Rotation = new Vector3(0, -angle + (Mathf.Pi / 2), 0),
                    MaterialOverride = SandbagMaterial,
                });
            }
        }

        _sandbags.Visible = shown;
    }

    public void SetMaterial(Material teamMaterial)
    {
        if (_teamMeshes is not null)
        {
            foreach (var mesh in _teamMeshes)
            {
                mesh.MaterialOverride = teamMaterial;
            }

            return;
        }

        _body.MaterialOverride = teamMaterial;
        if (_turretBody is not null)
        {
            _turretBody.MaterialOverride = teamMaterial;
        }
    }

    /// <summary>Places the unit: <paramref name="hullYaw"/> and <paramref name="turretYaw"/> are Godot yaw angles in radians.</summary>
    public void SetPose(Vector3 groundPoint, float hullYaw, float turretYaw)
    {
        Position = groundPoint;
        Rotation = new Vector3(0, hullYaw, 0);
        if (_turret is not null)
        {
            _turret.Rotation = new Vector3(0, turretYaw - hullYaw, 0);
        }
    }

    // The meshes under a node named team_mask.
    private static void CollectTeam(Node node, bool inMask, List<MeshInstance3D> meshes)
    {
        inMask |= node.Name.ToString().StartsWith("team_mask", System.StringComparison.Ordinal);
        if (inMask && node is MeshInstance3D mesh)
        {
            meshes.Add(mesh);
        }

        foreach (var child in node.GetChildren())
        {
            CollectTeam(child, inMask, meshes);
        }
    }

    /// <summary>The types drawn as boats: the ships and the transports over the water.</summary>
    private static bool IsShip(UnitType type) => type is UnitType.TyphoonSub or UnitType.SeaScorpion or UnitType.GiantSquid
        or UnitType.Dreadnought or UnitType.HoverTransport or UnitType.AllianceAmphibiousTransport or UnitType.AllianceDolphin
        or UnitType.AllianceDestroyer or UnitType.AllianceAegisCruiser or UnitType.AllianceAircraftCarrier;

    private void BuildInfantry(UnitType type)
    {
        const float Height = 0.45f;
        const float Radius = 0.1f;
        if (type is UnitType.AttackDog or UnitType.AllianceAttackDog)
        {
            // A dog: low and long, a head in front, four legs.
            _body = AddPart(new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.34f) }, new Vector3(0, 0.16f, 0));
            AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.12f) }, new Vector3(0, 0.24f, -0.2f), DarkMaterial);
            foreach (var (x, z) in new[] { (-0.04f, -0.12f), (0.04f, -0.12f), (-0.04f, 0.12f), (0.04f, 0.12f) })
            {
                AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.1f, 0.03f) }, new Vector3(x, 0.05f, z), DarkMaterial);
            }

            return;
        }

        _body = AddPart(new CylinderMesh { TopRadius = Radius, BottomRadius = Radius, Height = Height }, new Vector3(0, Height / 2, 0));
        AddPart(new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.1f) }, new Vector3(0, Height * 0.75f, -Radius), DarkMaterial);

        switch (type)
        {
            case UnitType.Conscript or UnitType.AllianceRifleman:
                // A rifle at the hip.
                AddPart(new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.24f) }, new Vector3(Radius, Height * 0.55f, -0.08f), DarkMaterial);
                break;
            case UnitType.Rocketeer:
                // Launcher tube over the shoulder.
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.32f) }, new Vector3(Radius, Height * 0.9f, 0), DarkMaterial);
                break;
            case UnitType.Engineer or UnitType.AllianceEngineer:
                // Hard hat.
                AddPart(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.05f }, new Vector3(0, Height + 0.025f, 0), EngineerMaterial);
                break;
            case UnitType.TeslaTrooper:
                // Two coils on the shoulders.
                foreach (var side in new[] { -1f, 1f })
                {
                    AddPart(new SphereMesh { Radius = 0.06f, Height = 0.12f }, new Vector3(side * Radius, Height + 0.04f, 0.02f), GlowMaterial);
                }

                break;
            case UnitType.CrazyIvan:
                // A bomb on the back.
                AddPart(new SphereMesh { Radius = 0.08f, Height = 0.16f }, new Vector3(0, Height * 0.7f, Radius + 0.05f), RedMaterial);
                break;
            case UnitType.Terrorist:
                // A vest of explosives.
                AddPart(new CylinderMesh { TopRadius = Radius + 0.02f, BottomRadius = Radius + 0.02f, Height = 0.14f }, new Vector3(0, Height * 0.55f, 0), RedMaterial);
                break;
            case UnitType.Desolator:
                // A toxin tank on the back and a nozzle in front.
                AddPart(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.28f }, new Vector3(0, Height * 0.6f, Radius + 0.05f), ToxinMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.22f) }, new Vector3(Radius, Height * 0.55f, -0.1f), DarkMaterial);
                break;
            case UnitType.AllianceRocketeer:
                // A jet pack: a box and two nozzles.
                AddPart(new BoxMesh { Size = new Vector3(0.16f, 0.18f, 0.08f) }, new Vector3(0, Height * 0.65f, Radius + 0.04f), DarkMaterial);
                foreach (var side in new[] { -1f, 1f })
                {
                    AddPart(new CylinderMesh { TopRadius = 0.03f, BottomRadius = 0.04f, Height = 0.08f }, new Vector3(side * 0.05f, Height * 0.42f, Radius + 0.05f), GlowMaterial);
                }

                break;
            case UnitType.AllianceSpy:
                // A hat with a wide brim.
                AddPart(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 0.02f }, new Vector3(0, Height + 0.01f, 0), DarkMaterial);
                AddPart(new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.08f }, new Vector3(0, Height + 0.05f, 0), DarkMaterial);
                break;
            case UnitType.AllianceChronoLegionnaire:
                // A time pack that glows.
                AddPart(new BoxMesh { Size = new Vector3(0.18f, 0.22f, 0.1f) }, new Vector3(0, Height * 0.6f, Radius + 0.05f), DarkMaterial);
                AddPart(new SphereMesh { Radius = 0.06f, Height = 0.12f }, new Vector3(0, Height * 0.95f, Radius + 0.05f), GlowMaterial);
                break;
            case UnitType.AllianceTanya:
                // A red band and two pistols.
                AddPart(new CylinderMesh { TopRadius = Radius + 0.01f, BottomRadius = Radius + 0.01f, Height = 0.04f }, new Vector3(0, Height * 0.88f, 0), RedMaterial);
                foreach (var side in new[] { -1f, 1f })
                {
                    AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.12f) }, new Vector3(side * Radius, Height * 0.55f, -0.06f), DarkMaterial);
                }

                break;
            case UnitType.AllianceSniper:
                // A long rifle.
                AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.42f) }, new Vector3(Radius, Height * 0.6f, -0.14f), DarkMaterial);
                break;
        }
    }

    private void BuildVehicle(UnitArchetype archetype)
    {
        var type = archetype.Type;
        var size = VehicleSize(type);
        switch (type)
        {
            case UnitType.TerrorDrone:
                // A spider: a small body on four legs.
                _body = AddPart(new SphereMesh { Radius = 0.14f, Height = 0.18f }, new Vector3(0, 0.18f, 0));
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.06f) }, new Vector3(0, 0.2f, -0.14f), GlowMaterial);
                foreach (var (x, z) in new[] { (-0.18f, -0.14f), (0.18f, -0.14f), (-0.18f, 0.14f), (0.18f, 0.14f) })
                {
                    // Splayed out from the body, along the diagonal.
                    var leg = AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.24f) }, new Vector3(x / 2, 0.1f, z / 2), DarkMaterial);
                    leg.Rotation = new Vector3(0, Mathf.Atan2(x, z), 0);
                }

                return;
            case UnitType.AllianceRobotTank:
                // A hover disk with a small gun turret.
                _body = AddPart(new CylinderMesh { TopRadius = 0.32f, BottomRadius = 0.36f, Height = 0.14f }, new Vector3(0, 0.16f, 0));
                AddPart(new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.3f, Height = 0.02f }, new Vector3(0, 0.07f, 0), GlowMaterial);
                AddTurret(new Vector3(0.36f, 0.12f, 0.36f), 0.23f, barrel: 0.3f);
                return;
        }

        _body = AddPart(new BoxMesh { Size = size }, new Vector3(0, size.Y / 2, 0));
        AddPart(new BoxMesh { Size = new Vector3(size.X * 0.6f, 0.04f, 0.12f) }, new Vector3(0, size.Y + 0.02f, -size.Z / 2 + 0.08f), DarkMaterial);
        AddLocomotorMarks(archetype.Locomotor, size);
        AddRoleMarks(type, size);

        if (!archetype.HasTurret)
        {
            return;
        }

        var turretSize = type == UnitType.ApocalypseTank ? new Vector3(size.X * 0.62f, 0.18f, size.X * 0.62f) : new Vector3(size.X * 0.5f, 0.14f, size.X * 0.55f);
        AddTurret(turretSize, size.Y, barrel: 0f);
        var barrelLength = size.Z * 0.45f;
        switch (type)
        {
            case UnitType.TeslaTank:
                // Tesla coil instead of a gun barrel.
                AddPart(new SphereMesh { Radius = 0.08f, Height = 0.16f }, new Vector3(0, turretSize.Y + 0.08f, -turretSize.Z / 4), GlowMaterial, _turret);
                break;
            case UnitType.AlliancePrismTank:
                // A crystal on the turret.
                AddPart(new CylinderMesh { TopRadius = 0f, BottomRadius = 0.11f, Height = 0.24f, RadialSegments = 3 }, new Vector3(0, turretSize.Y + 0.12f, 0), GlowMaterial, _turret);
                break;
            case UnitType.SupportVehicle:
                // Anti-air: two barrels to the sky.
                foreach (var side in new[] { -1f, 1f })
                {
                    var barrel = AddPart(new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.3f) }, new Vector3(side * 0.06f, turretSize.Y + 0.1f, -0.04f), DarkMaterial, _turret);
                    barrel.Rotation = new Vector3(Mathf.DegToRad(55f), 0, 0);
                }

                break;
            case UnitType.ApocalypseTank:
                // Two long barrels and a missile pod on each side.
                foreach (var side in new[] { -1f, 1f })
                {
                    AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, barrelLength * 1.1f) }, new Vector3(side * 0.06f, turretSize.Y / 2, -turretSize.Z / 2 - barrelLength * 0.55f), DarkMaterial, _turret);
                    AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.22f) }, new Vector3(side * (turretSize.X / 2 + 0.06f), turretSize.Y * 0.7f, 0), RedMaterial, _turret);
                }

                break;
            case UnitType.AllianceIfv:
                // Missile pods instead of a gun.
                foreach (var side in new[] { -1f, 1f })
                {
                    AddPart(new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.2f) }, new Vector3(side * 0.08f, turretSize.Y + 0.04f, 0), DarkMaterial, _turret);
                }

                break;
            case UnitType.AllianceMirageTank:
                // A flat projector panel.
                AddPart(new BoxMesh { Size = new Vector3(turretSize.X * 0.9f, 0.14f, 0.03f) }, new Vector3(0, turretSize.Y + 0.07f, 0), GlowMaterial, _turret);
                break;
            default:
                // A gun barrel, longer on the heavy tank.
                var length = type == UnitType.HeavyTank ? barrelLength * 1.2f : barrelLength;
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, length) }, new Vector3(0, turretSize.Y / 2, -turretSize.Z / 2 - length / 2), DarkMaterial, _turret);
                break;
        }
    }

    private void AddTurret(Vector3 turretSize, float height, float barrel)
    {
        _turret = new Node3D { Position = new Vector3(0, height, 0) };
        AddChild(_turret);
        _turretBody = AddPart(new BoxMesh { Size = turretSize }, new Vector3(0, turretSize.Y / 2, 0), parent: _turret);
        if (barrel > 0f)
        {
            AddPart(new BoxMesh { Size = new Vector3(0.05f, 0.05f, barrel) }, new Vector3(0, turretSize.Y / 2, -turretSize.Z / 2 - barrel / 2), DarkMaterial, _turret);
        }
    }

    private void BuildShip(UnitType type)
    {
        switch (type)
        {
            case UnitType.TyphoonSub:
                // A low cigar and a small tower.
                _body = AddPart(new CapsuleMesh { Radius = 0.16f, Height = 1.1f }, new Vector3(0, 0.08f, 0));
                _body.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
                AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.16f, 0.22f) }, new Vector3(0, 0.26f, -0.05f), DarkMaterial);
                return;
            case UnitType.AllianceDolphin:
                // A round body and a fin.
                _body = AddPart(new SphereMesh { Radius = 0.18f, Height = 0.3f }, new Vector3(0, 0.12f, 0));
                _body.Scale = new Vector3(0.8f, 0.8f, 1.8f);
                AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.16f, 0.12f) }, new Vector3(0, 0.3f, 0.05f), DarkMaterial);
                return;
            case UnitType.GiantSquid:
                // A round body and tentacles.
                _body = AddPart(new SphereMesh { Radius = 0.22f, Height = 0.36f }, new Vector3(0, 0.16f, 0.06f));
                for (var i = 0; i < 4; i++)
                {
                    AddPart(new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.04f, Height = 0.36f }, new Vector3(-0.12f + (i * 0.08f), 0.08f, -0.26f), DarkMaterial)
                        .Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
                }

                return;
        }

        var size = type switch
        {
            UnitType.AllianceAircraftCarrier => new Vector3(0.7f, 0.22f, 1.9f),
            UnitType.Dreadnought => new Vector3(0.7f, 0.28f, 1.7f),
            UnitType.AllianceAegisCruiser => new Vector3(0.6f, 0.24f, 1.5f),
            UnitType.AllianceDestroyer => new Vector3(0.55f, 0.22f, 1.4f),
            UnitType.SeaScorpion => new Vector3(0.45f, 0.18f, 0.9f),
            _ => new Vector3(0.8f, 0.2f, 1.1f),
        };
        _body = AddPart(new BoxMesh { Size = size }, new Vector3(0, size.Y / 2, 0));
        // The bow: a dark wedge in front.
        var bow = AddPart(new BoxMesh { Size = new Vector3(size.X * 0.7f, size.Y * 0.9f, size.X * 0.7f) }, new Vector3(0, size.Y * 0.45f, -size.Z / 2), DarkMaterial);
        bow.Rotation = new Vector3(0, Mathf.Pi / 4, 0);

        switch (type)
        {
            case UnitType.AllianceDestroyer:
                AddPart(new BoxMesh { Size = new Vector3(0.24f, 0.2f, 0.3f) }, new Vector3(0, size.Y + 0.1f, 0.2f), DarkMaterial);
                AddTurret(new Vector3(0.2f, 0.1f, 0.2f), size.Y, barrel: 0.3f);
                _turret!.Position = new Vector3(0, size.Y, -0.35f);
                break;
            case UnitType.AllianceAegisCruiser:
                // A tall bridge with radar panels and missile cells.
                AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.32f, 0.36f) }, new Vector3(0, size.Y + 0.16f, 0.1f), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.34f, 0.12f, 0.02f) }, new Vector3(0, size.Y + 0.24f, -0.09f), GlowMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.06f, 0.3f) }, new Vector3(0, size.Y + 0.03f, -0.45f), RedMaterial);
                break;
            case UnitType.AllianceAircraftCarrier:
                // A flat flight deck and an island on the side.
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.7f, 0.04f, size.Z * 0.9f) }, new Vector3(-size.X * 0.1f, size.Y + 0.02f, 0), TrackMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.14f, 0.3f, 0.4f) }, new Vector3(size.X / 2, size.Y + 0.15f, 0.2f), DarkMaterial);
                break;
            case UnitType.Dreadnought:
                // A bridge and two missile tubes standing up.
                AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.26f, 0.34f) }, new Vector3(0, size.Y + 0.13f, 0.35f), DarkMaterial);
                foreach (var z in new[] { -0.4f, -0.1f })
                {
                    AddPart(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 0.3f }, new Vector3(0, size.Y + 0.15f, z), RedMaterial);
                }

                break;
            case UnitType.SeaScorpion:
                // Anti-air: two barrels to the sky.
                foreach (var side in new[] { -1f, 1f })
                {
                    var barrel = AddPart(new BoxMesh { Size = new Vector3(0.04f, 0.04f, 0.26f) }, new Vector3(side * 0.06f, size.Y + 0.1f, 0f), DarkMaterial);
                    barrel.Rotation = new Vector3(Mathf.DegToRad(55f), 0, 0);
                }

                break;
            case UnitType.HoverTransport or UnitType.AllianceAmphibiousTransport:
                // A wide skirt and a ramp at the front.
                AddPart(new BoxMesh { Size = new Vector3(size.X + 0.1f, 0.06f, size.Z + 0.06f) }, new Vector3(0, 0.03f, 0), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.6f, 0.04f, 0.14f) }, new Vector3(0, size.Y + 0.02f, -size.Z / 2 + 0.1f), HopperMaterial);
                break;
        }
    }

    private void BuildAircraft(UnitType type)
    {
        switch (type)
        {
            case UnitType.KirovAirship:
                // A big balloon and a gondola under it.
                _body = AddPart(new SphereMesh { Radius = 0.45f, Height = 0.8f }, Vector3.Zero);
                _body.Scale = new Vector3(1f, 1f, 2.2f);
                AddPart(new BoxMesh { Size = new Vector3(0.24f, 0.16f, 0.5f) }, new Vector3(0, -0.45f, 0), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.6f, 0.04f, 0.2f) }, new Vector3(0, 0, 0.9f), RedMaterial);
                return;
            case UnitType.AllianceNighthawk:
                // A fuselage, a tail boom and a rotor.
                _body = AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.26f, 0.6f) }, Vector3.Zero);
                AddPart(new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.5f) }, new Vector3(0, 0.04f, 0.5f), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.9f, 0.02f, 0.06f) }, new Vector3(0, 0.2f, 0), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.02f, 0.9f) }, new Vector3(0, 0.2f, 0), DarkMaterial);
                return;
        }

        var (length, span, wing) = type switch
        {
            UnitType.ParadropPlane => (1.3f, 1.8f, 0.3f),
            UnitType.AllianceBlackEagle => (0.8f, 0.9f, 0.4f),
            UnitType.AllianceHarrier => (0.7f, 0.75f, 0.3f),
            UnitType.AllianceOsprey => (0.55f, 0.8f, 0.14f),
            _ => (0.45f, 0.5f, 0.2f),
        };
        _body = AddPart(new BoxMesh { Size = new Vector3(0.16f, 0.14f, length) }, Vector3.Zero);
        AddPart(new BoxMesh { Size = new Vector3(0.12f, 0.1f, 0.14f) }, new Vector3(0, 0.03f, -length / 2), DarkMaterial);
        AddPart(new BoxMesh { Size = new Vector3(span, 0.03f, wing) }, new Vector3(0, 0, 0.02f),
            type == UnitType.AllianceBlackEagle ? DarkMaterial : TrackMaterial);
        if (type is UnitType.AllianceHarrier or UnitType.AllianceBlackEagle or UnitType.AllianceHornet)
        {
            // A jet: a tail fin.
            AddPart(new BoxMesh { Size = new Vector3(0.03f, 0.18f, 0.16f) }, new Vector3(0, 0.1f, length / 2 - 0.08f), DarkMaterial);
        }

        if (type == UnitType.AllianceOsprey)
        {
            // Rotors at the wing tips.
            foreach (var side in new[] { -1f, 1f })
            {
                AddPart(new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.02f }, new Vector3(side * span / 2, 0.08f, 0), DarkMaterial);
            }
        }
    }

    /// <summary>Track belts for tracked vehicles, four wheels for wheeled ones: the shape shows how the unit moves.</summary>
    private void AddLocomotorMarks(Locomotor locomotor, Vector3 size)
    {
        const float TrackWidth = 0.1f;
        const float WheelRadius = 0.11f;
        if (locomotor == Locomotor.Track)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                AddPart(new BoxMesh { Size = new Vector3(TrackWidth, size.Y * 0.9f, size.Z * 0.96f) },
                    new Vector3(side * (size.X / 2 + TrackWidth / 2), size.Y * 0.45f, 0), TrackMaterial);
            }
        }
        else if (locomotor == Locomotor.Wheel)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                foreach (var z in new[] { -size.Z * 0.32f, size.Z * 0.32f })
                {
                    // The cylinder stands up by default; turning it 90° on Z lays the axle across the hull.
                    var wheel = AddPart(new CylinderMesh { TopRadius = WheelRadius, BottomRadius = WheelRadius, Height = 0.08f },
                        new Vector3(side * (size.X / 2 + 0.02f), WheelRadius, z), DarkMaterial);
                    wheel.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                }
            }
        }
    }

    /// <summary>
    /// The extra shape of the roles that have no turret to tell them: an ore hopper for the harvester, a crane for the MCV, a rocket on
    /// its rail for the rocket artillery, a long fixed gun for the tank hunter, a hazard load for the demolition truck.
    /// </summary>
    private void AddRoleMarks(UnitType type, Vector3 size)
    {
        switch (type)
        {
            case UnitType.Harvester or UnitType.AllianceHarvester:
                // Hopper on the rear part of the roof, behind the turret.
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.8f, 0.12f, size.Z * 0.3f) },
                    new Vector3(0, size.Y + 0.06f, size.Z * 0.36f), HopperMaterial);
                break;
            case UnitType.Mcv or UnitType.AllianceMcv:
                // Mast and jib of the construction crane, front right.
                AddPart(new BoxMesh { Size = new Vector3(0.05f, 0.3f, 0.05f) },
                    new Vector3(size.X * 0.3f, size.Y + 0.15f, -size.Z * 0.3f), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.04f, 0.04f, size.Z * 0.5f) },
                    new Vector3(size.X * 0.3f, size.Y + 0.3f, -size.Z * 0.05f), DarkMaterial);
                break;
            case UnitType.RocketArtillery:
                // A long rocket on a raised rail.
                var rail = AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.06f, size.Z * 0.9f) }, new Vector3(0, size.Y + 0.12f, 0.05f), DarkMaterial);
                rail.Rotation = new Vector3(Mathf.DegToRad(-18f), 0, 0);
                var rocket = AddPart(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = size.Z * 0.8f }, new Vector3(0, size.Y + 0.2f, 0.02f), RedMaterial);
                rocket.Rotation = new Vector3(Mathf.DegToRad(72f), 0, 0);
                break;
            case UnitType.AllianceTankDestroyer:
                // A long gun fixed in the hull, a low casemate.
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.6f, 0.12f, size.Z * 0.5f) }, new Vector3(0, size.Y + 0.06f, 0.05f), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, size.Z * 0.7f) }, new Vector3(0, size.Y + 0.06f, -size.Z * 0.55f), DarkMaterial);
                break;
            case UnitType.DemolitionTruck:
                // A cab in front and a striped load of explosives.
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.9f, 0.16f, size.Z * 0.3f) }, new Vector3(0, size.Y + 0.08f, -size.Z * 0.3f), DarkMaterial);
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.8f, 0.2f, size.Z * 0.5f) }, new Vector3(0, size.Y + 0.1f, size.Z * 0.2f), EngineerMaterial);
                AddPart(new BoxMesh { Size = new Vector3(size.X * 0.82f, 0.06f, size.Z * 0.52f) }, new Vector3(0, size.Y + 0.1f, size.Z * 0.2f), RedMaterial);
                break;
        }
    }

    /// <summary>Width, height and length in cells: the MCV and harvester are big, tanks by their weight, the light ones small.</summary>
    private static Vector3 VehicleSize(UnitType type) => type switch
    {
        UnitType.Mcv or UnitType.AllianceMcv => new Vector3(0.8f, 0.5f, 1.1f),
        UnitType.Harvester or UnitType.AllianceHarvester => new Vector3(0.75f, 0.42f, 1f),
        UnitType.ApocalypseTank => new Vector3(0.82f, 0.34f, 1.1f),
        UnitType.HeavyTank => new Vector3(0.66f, 0.3f, 0.9f),
        UnitType.TeslaTank => new Vector3(0.6f, 0.3f, 0.85f),
        UnitType.AllianceLightTank => new Vector3(0.58f, 0.27f, 0.8f),
        UnitType.AlliancePrismTank => new Vector3(0.6f, 0.28f, 0.85f),
        UnitType.AllianceMirageTank => new Vector3(0.6f, 0.22f, 0.85f),
        UnitType.AllianceTankDestroyer => new Vector3(0.64f, 0.2f, 0.9f),
        UnitType.AllianceIfv => new Vector3(0.56f, 0.3f, 0.82f),
        UnitType.RocketArtillery => new Vector3(0.56f, 0.26f, 0.95f),
        UnitType.DemolitionTruck => new Vector3(0.5f, 0.2f, 0.85f),
        UnitType.SupportVehicle => new Vector3(0.5f, 0.28f, 0.75f),
        _ => new Vector3(0.6f, 0.3f, 0.8f),
    };

    private MeshInstance3D AddPart(Mesh mesh, Vector3 position, Material? material = null, Node3D? parent = null)
    {
        var part = new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = material };
        (parent ?? this).AddChild(part);
        return part;
    }
}
