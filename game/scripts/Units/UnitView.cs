using Godot;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// Symbolic look of one unit (docs/implementation/conventions.md, section 12): a primitive per type in the team colour,
/// a dark nose that shows where the hull points, and a turret on top for types that have one.
/// Infantry is a thin column, vehicles are boxes sized by role. The model looks along −Z; the parent sets the yaw.
/// </summary>
public partial class UnitView : Node3D
{
    private static readonly StandardMaterial3D DarkMaterial = new() { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) };
    private static readonly StandardMaterial3D EngineerMaterial = new() { AlbedoColor = new Color(0.95f, 0.8f, 0.15f) };

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

    /// <summary>Width, height and length in cells: the MCV and harvester are big, tanks medium, the support vehicle small.</summary>
    private static Vector3 VehicleSize(UnitType type) => type switch
    {
        UnitType.Mcv => new Vector3(0.8f, 0.5f, 1.1f),
        UnitType.Harvester => new Vector3(0.75f, 0.42f, 1f),
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
