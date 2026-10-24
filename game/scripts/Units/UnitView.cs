using Godot;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// Symbolic look of one unit (docs/implementation/conventions.md, section 12): a primitive per type in the team colour,
/// a dark nose that shows where the hull points, and a turret on top for types that have one.
/// Infantry is a thin column, vehicles are boxes sized by role. Track belts or wheels show how a vehicle moves, and a
/// hopper (harvester) or a crane (MCV) shows its role. The model looks along −Z; the parent sets the yaw.
/// </summary>
public partial class UnitView : Node3D
{
    private static readonly StandardMaterial3D DarkMaterial = new() { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) };
    private static readonly StandardMaterial3D EngineerMaterial = new() { AlbedoColor = new Color(0.95f, 0.8f, 0.15f) };
    private static readonly StandardMaterial3D HopperMaterial = new() { AlbedoColor = new Color(0.55f, 0.42f, 0.22f) };
    private static readonly StandardMaterial3D TrackMaterial = new() { AlbedoColor = new Color(0.36f, 0.36f, 0.38f) };

    private MeshInstance3D _body = null!;
    private Node3D? _turret;
    private MeshInstance3D? _turretBody;

    public UnitType Type { get; private set; }

    /// <summary>Builds the shapes for <paramref name="archetype"/>. Called once; pooled views keep their type.</summary>
    public void Build(UnitArchetype archetype)
    {
        Type = archetype.Type;
        if (archetype.Category == UnitCategory.Infantry)
        {
            BuildInfantry(archetype.Type);
        }
        else
        {
            BuildVehicle(archetype);
        }
    }

    private static readonly StandardMaterial3D SandbagMaterial = new() { AlbedoColor = new Color(0.66f, 0.58f, 0.4f) };

    private Node3D? _sandbags;

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

    private void BuildInfantry(UnitType type)
    {
        const float Height = 0.45f;
        const float Radius = 0.1f;
        _body = AddPart(new CylinderMesh { TopRadius = Radius, BottomRadius = Radius, Height = Height }, new Vector3(0, Height / 2, 0));
        AddPart(new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.1f) }, new Vector3(0, Height * 0.75f, -Radius), DarkMaterial);

        switch (type)
        {
            case UnitType.Rocketeer:
                // Launcher tube over the shoulder.
                AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, 0.32f) }, new Vector3(Radius, Height * 0.9f, 0), DarkMaterial);
                break;
            case UnitType.Engineer:
                // Hard hat.
                AddPart(new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.05f }, new Vector3(0, Height + 0.025f, 0), EngineerMaterial);
                break;
        }
    }

    private void BuildVehicle(UnitArchetype archetype)
    {
        var size = VehicleSize(archetype.Type);
        _body = AddPart(new BoxMesh { Size = size }, new Vector3(0, size.Y / 2, 0));
        AddPart(new BoxMesh { Size = new Vector3(size.X * 0.6f, 0.04f, 0.12f) }, new Vector3(0, size.Y + 0.02f, -size.Z / 2 + 0.08f), DarkMaterial);
        AddLocomotorMarks(archetype.Locomotor, size);
        AddRoleMarks(archetype.Type, size);

        if (!archetype.HasTurret)
        {
            return;
        }

        _turret = new Node3D { Position = new Vector3(0, size.Y, 0) };
        AddChild(_turret);
        var turretSize = new Vector3(size.X * 0.5f, 0.14f, size.X * 0.55f);
        _turretBody = AddPart(new BoxMesh { Size = turretSize }, new Vector3(0, turretSize.Y / 2, 0), parent: _turret);
        if (archetype.Type == UnitType.TeslaTank)
        {
            // Tesla coil instead of a gun barrel.
            AddPart(new SphereMesh { Radius = 0.08f, Height = 0.16f }, new Vector3(0, turretSize.Y + 0.08f, -turretSize.Z / 4), DarkMaterial, _turret);
        }
        else
        {
            var barrel = size.Z * 0.45f;
            AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.06f, barrel) }, new Vector3(0, turretSize.Y / 2, -turretSize.Z / 2 - barrel / 2), DarkMaterial, _turret);
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

    /// <summary>The extra shape of the two roles that look like tanks: an ore hopper for the harvester, a crane for the MCV.</summary>
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
        }
    }

    /// <summary>Width, height and length in cells: the MCV and harvester are big, tanks medium, the support vehicle small.</summary>
    private static Vector3 VehicleSize(UnitType type) => type switch
    {
        UnitType.Mcv or UnitType.AllianceMcv => new Vector3(0.8f, 0.5f, 1.1f),
        UnitType.Harvester or UnitType.AllianceHarvester => new Vector3(0.75f, 0.42f, 1f),
        UnitType.AllianceLightTank => new Vector3(0.58f, 0.27f, 0.8f),
        UnitType.SupportVehicle => new Vector3(0.5f, 0.28f, 0.75f),
        UnitType.HeavyTank => new Vector3(0.66f, 0.3f, 0.9f),
        UnitType.TeslaTank => new Vector3(0.6f, 0.3f, 0.85f),
        _ => new Vector3(0.6f, 0.3f, 0.8f),
    };

    private MeshInstance3D AddPart(Mesh mesh, Vector3 position, Material? material = null, Node3D? parent = null)
    {
        var part = new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = material };
        (parent ?? this).AddChild(part);
        return part;
    }
}
