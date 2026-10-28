using System.Collections.Generic;
using Godot;
using Rts.Client.Climates;

namespace Rts.Client.Terrain;

/// <summary>One shape of a tree: a mesh of its own (by <see cref="Key"/>, shared by every tree that has it), its colour and its place.</summary>
public sealed record TreePart(string Key, System.Func<PrimitiveMesh> Mesh, Color Color, Vector3 Offset, float YawDegrees = 0f);

/// <summary>
/// The trees by the climate of the map (<see cref="ClimateLook.Trees"/>; the rules have one kind of tree): a pine is a dark cone on a
/// trunk; a broadleaf tree a round crown, small or big by its variant; in the desert a palm with four fronds (variants 0, 1) or a cactus
/// with two arms (2, 3); by the sea a palm (0…2) or a small broadleaf tree (3). The map's trees, the skirt's and the holo tank's disguise use
/// the same shapes. A forest is tens of thousands of copies (the owner, 2026-10-28: docs/art/model_standards.md, «the forest of a map»):
/// a trunk has 6 sides and no caps — its foot is in the ground, its top in the crown — a cone 8 sides and no bottom, a round crown 10 × 5
/// segments: a pine is 28 triangles instead of 132, a broadleaf tree 132 instead of 240.
/// </summary>
public static class TreeShapes
{
    private static readonly Color Trunk = new(0.35f, 0.24f, 0.15f);
    private static readonly Color PineGreen = new(0.12f, 0.32f, 0.16f);
    private static readonly Color LeafGreen = new(0.2f, 0.42f, 0.16f);
    private static readonly Color DarkLeaf = new(0.15f, 0.34f, 0.13f);
    private static readonly Color PalmTrunk = new(0.55f, 0.43f, 0.29f);
    private static readonly Color Frond = new(0.24f, 0.5f, 0.2f);
    private static readonly Color Cactus = new(0.3f, 0.5f, 0.26f);

    private static readonly TreePart[] Pine =
    [
        new("PineTrunk", () => Trunk6(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = 0.25f }), Trunk, new Vector3(0, 0.125f, 0)),
        new("PineCrown", () => new CylinderMesh { TopRadius = 0f, BottomRadius = 0.38f, Height = 0.8f, RadialSegments = 8, Rings = 0, CapBottom = false }, PineGreen, new Vector3(0, 0.65f, 0)),
    ];

    private static readonly TreePart[] SmallBroadleaf =
    [
        new("LeafTrunk", () => Trunk6(new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = 0.36f }), Trunk, new Vector3(0, 0.18f, 0)),
        new("LeafCrown", () => Crown(new SphereMesh { Radius = 0.32f, Height = 0.56f }), LeafGreen, new Vector3(0, 0.58f, 0)),
    ];

    private static readonly TreePart[] BigBroadleaf =
    [
        new("OakTrunk", () => Trunk6(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 0.42f }), Trunk, new Vector3(0, 0.21f, 0)),
        new("OakCrown", () => Crown(new SphereMesh { Radius = 0.42f, Height = 0.72f }), DarkLeaf, new Vector3(0, 0.72f, 0)),
    ];

    private static readonly TreePart[] Palm =
    [
        new("PalmTrunk", () => Trunk6(new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.06f, Height = 1f }), PalmTrunk, new Vector3(0, 0.5f, 0)),
        new("PalmFrond", () => new BoxMesh { Size = new Vector3(0.75f, 0.03f, 0.12f) }, Frond, new Vector3(0, 1f, 0)),
        new("PalmFrond", () => new BoxMesh { Size = new Vector3(0.75f, 0.03f, 0.12f) }, Frond, new Vector3(0, 1f, 0), 45f),
        new("PalmFrond", () => new BoxMesh { Size = new Vector3(0.75f, 0.03f, 0.12f) }, Frond, new Vector3(0, 1f, 0), 90f),
        new("PalmFrond", () => new BoxMesh { Size = new Vector3(0.75f, 0.03f, 0.12f) }, Frond, new Vector3(0, 1f, 0), 135f),
    ];

    private static readonly TreePart[] CactusParts =
    [
        // Its top is seen from above: 8 sides and its cap.
        new("CactusBody", () => new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 0.62f, RadialSegments = 8, Rings = 0, CapBottom = false }, Cactus, new Vector3(0, 0.31f, 0)),
        new("CactusArm", () => new BoxMesh { Size = new Vector3(0.05f, 0.24f, 0.05f) }, Cactus, new Vector3(0.14f, 0.42f, 0)),
        new("CactusArm", () => new BoxMesh { Size = new Vector3(0.05f, 0.24f, 0.05f) }, Cactus, new Vector3(-0.13f, 0.34f, 0)),
        new("CactusJoint", () => new BoxMesh { Size = new Vector3(0.1f, 0.05f, 0.05f) }, Cactus, new Vector3(0.09f, 0.31f, 0)),
        new("CactusJoint", () => new BoxMesh { Size = new Vector3(0.1f, 0.05f, 0.05f) }, Cactus, new Vector3(-0.08f, 0.24f, 0)),
    ];

    // A trunk: 6 sides, its ends in the ground and in the crown (a palm's under its fronds).
    private static CylinderMesh Trunk6(CylinderMesh trunk)
    {
        (trunk.RadialSegments, trunk.Rings, trunk.CapTop, trunk.CapBottom) = (6, 0, false, false);
        return trunk;
    }

    // A round crown: 10 segments round, 5 down.
    private static SphereMesh Crown(SphereMesh crown)
    {
        (crown.RadialSegments, crown.Rings) = (10, 5);
        return crown;
    }

    /// <summary>The shapes of a tree of <paramref name="variant"/> (0…3) in a climate whose trees are <paramref name="kind"/>.</summary>
    public static IReadOnlyList<TreePart> Of(TreeKind kind, int variant) => kind switch
    {
        TreeKind.Broadleaf => (variant & 3) >= 2 ? BigBroadleaf : SmallBroadleaf,
        TreeKind.Desert => (variant & 3) >= 2 ? CactusParts : Palm,
        TreeKind.Coast => (variant & 3) == 3 ? SmallBroadleaf : Palm,
        _ => Pine,
    };

    /// <summary>The name of the shape of a tree, its key in the table of models: <c>tree/palm</c> and the like.</summary>
    public static string NameOf(TreeKind kind, int variant) => kind switch
    {
        TreeKind.Broadleaf => (variant & 3) >= 2 ? "broadleaf_big" : "broadleaf_small",
        TreeKind.Desert => (variant & 3) >= 2 ? "cactus" : "palm",
        TreeKind.Coast => (variant & 3) == 3 ? "broadleaf_small" : "palm",
        _ => "pine",
    };

    /// <summary>A tree as nodes, for one that moves: the holo tank's disguise. Its model from the table when there is one.</summary>
    public static Node3D Build(TreeKind kind, int variant)
    {
        // Of several kinds of the shape, the one of the variant.
        if (Models.ModelLibrary.Instantiate("tree/" + NameOf(kind, variant), cell: new Vector2I(variant, 0)) is { } model)
        {
            return model;
        }

        var tree = new Node3D();
        foreach (var part in Of(kind, variant))
        {
            tree.AddChild(new MeshInstance3D
            {
                Mesh = LowPoly.Of(part.Mesh()),
                Position = part.Offset,
                RotationDegrees = new Vector3(0, part.YawDegrees, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = part.Color },
            });
        }

        return tree;
    }
}
