using System.Collections.Generic;
using Godot;
using Rts.Simulation.Rules;

namespace Rts.Client.Terrain;

/// <summary>
/// The nodes of a building's look: its model from the table (<see cref="Models.ModelLibrary"/>, key <c>building/&lt;id&gt;</c>) with
/// its <c>team_mask</c> in the owner's colour, or else its placeholder (<see cref="BuildingLooks"/>): the parts as meshes in the colours
/// of its faction and the owner's band round its base. Shared by the buildings on the map and the gallery
/// (<c>-- --ui-units --buildings</c>).
/// </summary>
public static class BuildingLookView
{
    /// <summary>
    /// Adds the parts of <paramref name="def"/> to <paramref name="body"/>, whose origin lies <paramref name="sink"/> cells under the
    /// ground at the footprint's centre: what stands on the ground reaches down to it, so the building can rise out of the ground.
    /// Of several models the one of <paramref name="cell"/>, its north-west cell.
    /// </summary>
    public static void AddTo(Node3D body, BuildingDef def, Color owner, float sink, Vector2I? cell = null)
    {
        if (Models.ModelLibrary.Instantiate("building/" + def.Id, owner, cell: cell) is { } model)
        {
            // Its pivot is the footprint's centre on the ground.
            model.Position += new Vector3(0f, sink, 0f);
            body.AddChild(model);
            return;
        }

        AddParts(body, BuildingLooks.Of(def), def.Faction == "union" ? LookFaction.Soviet : LookFaction.Allied, owner, sink, !def.Wall);
    }

    /// <summary>
    /// Adds <paramref name="parts"/> in the colours of <paramref name="faction"/> to <paramref name="body"/>, as <see cref="AddTo"/>;
    /// the owner's band round the base when <paramref name="band"/> and an owner is given (a building of the map nobody owns has none).
    /// </summary>
    public static void AddParts(Node3D body, IReadOnlyList<LookPart> parts, LookFaction faction, Color? owner, float sink, bool band)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var bottom = part.Y - part.SizeY / 2f;
            if (part.Tilt == 0f && part.Shape is LookShape.Box or LookShape.Cylinder && bottom <= 0.001f)
            {
                // From the ground down to the body's origin.
                part = part with { SizeY = part.SizeY + sink, Y = part.Y - sink / 2f };
            }

            body.AddChild(Mesh(part, part.Tint == LookTint.Owner && owner is { } own ? own : ColorOf(part.Tint, faction), sink));
            if (i == 0 && band && owner is { } colour)
            {
                body.AddChild(Mesh(Band(parts[0]), colour, sink));
            }
        }
    }

    /// <summary>The colour of a tint for a faction.</summary>
    public static Color ColorOf(LookTint tint, LookFaction faction)
    {
        var (r, g, b) = BuildingLooks.Palette(tint, faction);
        return new Color(r, g, b);
    }

    // The owner's band: a thin strip round the top of the base.
    private static LookPart Band(LookPart basePart)
    {
        var top = basePart.Y + basePart.SizeY / 2f;
        return basePart with
        {
            SizeX = basePart.SizeX + 0.03f,
            SizeY = 0.08f,
            SizeZ = basePart.SizeZ + 0.03f,
            Y = top - 0.07f,
        };
    }

    internal static MeshInstance3D Mesh(LookPart part, Color color, float sink)
    {
        var (mesh, place) = MeshOf(part, color);
        return new MeshInstance3D { Mesh = mesh, Transform = new Transform3D(Basis.Identity, new Vector3(0, sink, 0)) * place };
    }

    /// <summary>The mesh of a part in its colour, and its place about the look's origin: for a MultiMesh of many (the props).</summary>
    internal static (PrimitiveMesh Mesh, Transform3D Place) MeshOf(LookPart part, Color color)
    {
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.8f };
        if (part.Tint == LookTint.Glow)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 0.6f;
        }

        PrimitiveMesh mesh = part.Shape switch
        {
            LookShape.Cylinder => new CylinderMesh { BottomRadius = part.SizeX / 2f, TopRadius = part.SizeZ / 2f, Height = part.SizeY },
            // A four-sided cone, turned so its sides face the axes: across its base is its width.
            LookShape.Pyramid => new CylinderMesh
            {
                BottomRadius = part.SizeX / 2f * Mathf.Sqrt2, TopRadius = 0f, Height = part.SizeY, RadialSegments = 4, Rings = 1,
            },
            // A whole ball whose lower half sinks into what it stands on.
            LookShape.Dome => new SphereMesh { Radius = part.SizeX / 2f, Height = part.SizeX },
            LookShape.Sphere => new SphereMesh { Radius = part.SizeX / 2f, Height = part.SizeX },
            LookShape.Ring => new TorusMesh { InnerRadius = part.SizeX / 2f - part.SizeY, OuterRadius = part.SizeX / 2f },
            _ => new BoxMesh { Size = new Vector3(part.SizeX, part.SizeY, part.SizeZ) },
        };
        mesh.Material = material;
        var yaw = part.Yaw + (part.Shape == LookShape.Pyramid ? 45f : 0f);
        // Godot's order: the tilt about x first, then the yaw.
        var basis = Basis.FromEuler(new Vector3(Mathf.DegToRad(part.Tilt), Mathf.DegToRad(yaw), 0f));
        return (mesh, new Transform3D(basis, new Vector3(part.X, part.Y, part.Z)));
    }
}
