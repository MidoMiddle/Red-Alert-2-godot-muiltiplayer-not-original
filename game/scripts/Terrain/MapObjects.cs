using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic obstacles on the ground (docs/implementation/conventions.md, section 12): a dark green cone on a trunk for
/// every tree, a grey block for every rock cell, so impassable cells look like walls. One MultiMesh per shape.
/// </summary>
public static class MapObjects
{
    private const float RockHeight = 0.5f;
    private const float TrunkHeight = 0.25f;
    private const float CrownHeight = 0.8f;

    public static void Build(Node3D parent, MapGrid map, TerrainHeights heights)
    {
        var trees = new List<Vector3>();
        var rocks = new List<Vector3>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var ground = new Vector3(x + 0.5f, heights.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);
                if (map.IsBlocked(cell))
                {
                    trees.Add(ground);
                }
                else if (map.LandAt(cell) == LandType.Rock)
                {
                    rocks.Add(ground);
                }
            }
        }

        AddInstances(parent, "Rocks", new BoxMesh { Size = new Vector3(0.98f, RockHeight, 0.98f) }, new Color(0.45f, 0.45f, 0.48f), rocks, RockHeight / 2);
        AddInstances(parent, "TreeTrunks", new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = TrunkHeight }, new Color(0.35f, 0.24f, 0.15f), trees, TrunkHeight / 2);
        AddInstances(parent, "TreeCrowns", new CylinderMesh { TopRadius = 0f, BottomRadius = 0.38f, Height = CrownHeight }, new Color(0.12f, 0.32f, 0.16f), trees, TrunkHeight + CrownHeight / 2);
    }

    private static void AddInstances(Node3D parent, string name, PrimitiveMesh mesh, Color color, List<Vector3> positions, float lift)
    {
        mesh.Material = new StandardMaterial3D { AlbedoColor = color };
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = positions.Count,
        };
        for (var i = 0; i < positions.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, positions[i] + new Vector3(0, lift, 0)));
        }

        parent.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }
}
