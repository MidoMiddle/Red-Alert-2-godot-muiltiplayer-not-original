using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic map objects on the ground (docs/implementation/conventions.md, section 12; M7_maps.md, 4.7): a dark green cone on
/// a trunk for a tree and a short trunk for its stump, a grey lump for a boulder, a thin brown rail for a fence, a low steel
/// block for an anti-tank hedgehog, a thin dark pipe for a pipeline (ore and gem sources: <see cref="OreSources"/>), a tall
/// stone block for a wall, a dark pole with a yellow lamp for a lamp post, a grey block for every rock cell, and for a low
/// bridge a plank deck just above the water with a rail on each side, or charred stubs where a segment fell. One MultiMesh
/// per shape (and per direction for bridges). Rebuilt whenever the map changes (a tree felled, a bridge blown up), which is rare.
/// </summary>
public partial class MapObjects : Node3D
{
    private const float RockHeight = 0.5f;
    private const float TrunkHeight = 0.25f;
    private const float CrownHeight = 0.8f;

    private MapGrid _map = null!;
    private int _builtVersion = -1;
    private MapGrid? _builtMap;

    public override void _Process(double delta)
    {
        // A new match or a new map in the editor (another map object), or a change of this one.
        var map = GetParent<TerrainBuilder>().Map;
        if (!ReferenceEquals(map, _builtMap) || map.Version != _builtVersion)
        {
            _map = map;
            Rebuild();
        }
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _builtMap = _map;
        _builtVersion = _map.Version;
        var trees = new List<Vector3>();
        var stumps = new List<Vector3>();
        var boulders = new List<Vector3>();
        var fences = new List<Vector3>();
        var walls = new List<Vector3>();
        var posts = new List<Vector3>();
        var rocks = new List<Vector3>();
        var hedgehogs = new List<Vector3>();
        var pipes = new List<Vector3>();
        var drifts = new List<Vector3>();
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                if (_map.LandAt(new CellPos(x, y)) == LandType.Rock)
                {
                    rocks.Add(Ground(x, y));
                }
            }
        }

        foreach (var mapObject in _map.Objects)
        {
            var at = Ground(mapObject.Cell.X, mapObject.Cell.Y);
            var list = (mapObject.Kind, mapObject.Destroyed) switch
            {
                (MapObjectKind.PineTree, false) => trees,
                (MapObjectKind.PineTree, true) => stumps,
                (MapObjectKind.Boulder, _) => boulders,
                (MapObjectKind.FenceWood, false) => fences,
                (MapObjectKind.FenceWood, true) => null,
                (MapObjectKind.WallStone, false) => walls,
                (MapObjectKind.WallStone, true) => null,
                (MapObjectKind.LampPost, false) => posts,
                (MapObjectKind.LampPost, true) => null,
                (MapObjectKind.AntiTankHedgehog, false) => hedgehogs,
                (MapObjectKind.AntiTankHedgehog, true) => null,
                (MapObjectKind.Pipeline, false) => pipes,
                (MapObjectKind.Pipeline, true) => null,
                (MapObjectKind.Snowdrift, false) => drifts,
                (MapObjectKind.Snowdrift, true) => null,
                // Drawn and animated by OreSources.
                (MapObjectKind.OreSource or MapObjectKind.GemSource, _) => null,
                _ => throw new System.ArgumentOutOfRangeException(nameof(mapObject), mapObject.Kind, "No symbolic look for this kind."),
            };
            list?.Add(at);
        }

        foreach (var bridge in _map.Bridges)
        {
            AddBridge(bridge);
        }

        AddInstances("Rocks", new BoxMesh { Size = new Vector3(0.98f, RockHeight, 0.98f) }, new Color(0.45f, 0.45f, 0.48f), rocks, RockHeight / 2);
        AddInstances("TreeTrunks", new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.07f, Height = TrunkHeight }, new Color(0.35f, 0.24f, 0.15f), trees, TrunkHeight / 2);
        AddInstances("TreeCrowns", new CylinderMesh { TopRadius = 0f, BottomRadius = 0.38f, Height = CrownHeight }, new Color(0.12f, 0.32f, 0.16f), trees, TrunkHeight + CrownHeight / 2);
        AddInstances("Stumps", new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.09f, Height = 0.08f }, new Color(0.42f, 0.3f, 0.18f), stumps, 0.04f);
        AddInstances("Boulders", new SphereMesh { Radius = 0.36f, Height = 0.5f }, new Color(0.5f, 0.5f, 0.52f), boulders, 0.2f);
        AddInstances("Fences", new BoxMesh { Size = new Vector3(0.96f, 0.25f, 0.06f) }, new Color(0.5f, 0.35f, 0.2f), fences, 0.125f);
        AddInstances("Walls", new BoxMesh { Size = new Vector3(0.98f, 0.6f, 0.98f) }, new Color(0.68f, 0.65f, 0.58f), walls, 0.3f);
        AddInstances("LampPosts", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.7f }, new Color(0.15f, 0.15f, 0.17f), posts, 0.35f);
        AddInstances("Lamps", new SphereMesh { Radius = 0.06f, Height = 0.12f }, new Color(1f, 0.85f, 0.35f), posts, 0.72f);
        AddInstances("Hedgehogs", new BoxMesh { Size = new Vector3(0.9f, 0.3f, 0.9f) }, new Color(0.3f, 0.31f, 0.34f), hedgehogs, 0.15f);
        AddInstances("Pipes", new BoxMesh { Size = new Vector3(0.98f, 0.12f, 0.12f) }, new Color(0.22f, 0.25f, 0.28f), pipes, 0.06f);
        AddInstances("Snowdrifts", new SphereMesh { Radius = 0.45f, Height = 0.22f }, new Color(0.9f, 0.93f, 0.97f), drifts, 0.11f);
    }

    /// <summary>The deck of a standing segment lies flat just above the water, so units on it seem to drive on the planks.</summary>
    private void AddBridge(Bridge bridge)
    {
        var alongX = bridge.From.Y == bridge.To.Y;
        var decks = new List<Vector3>();
        var rails = new List<Vector3>();
        var stubs = new List<Vector3>();
        var side = alongX ? new Vector3(0, 0, 0.44f) : new Vector3(0.44f, 0, 0);
        foreach (var segment in bridge.Segments)
        {
            var at = Ground(segment.Cell.X, segment.Cell.Y);
            if (segment.IsIntact)
            {
                decks.Add(at);
                rails.Add(at + side);
                rails.Add(at - side);
            }
            else
            {
                stubs.Add(at + side);
                stubs.Add(at - side);
            }
        }

        var name = $"Bridge{bridge.Id}";
        var deck = alongX ? new Vector3(1f, 0.05f, 0.9f) : new Vector3(0.9f, 0.05f, 1f);
        var rail = alongX ? new Vector3(1f, 0.1f, 0.04f) : new Vector3(0.04f, 0.1f, 1f);
        AddInstances(name + "Deck", new BoxMesh { Size = deck }, new Color(0.55f, 0.4f, 0.24f), decks, 0.03f);
        AddInstances(name + "Rails", new BoxMesh { Size = rail }, new Color(0.38f, 0.26f, 0.15f), rails, 0.1f);
        AddInstances(name + "Stubs", new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) }, new Color(0.2f, 0.16f, 0.13f), stubs, 0.02f);
    }

    private static Vector3 Ground(int x, int y) => new(x + 0.5f, WorldCoords.Terrain!.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);

    private void AddInstances(string name, PrimitiveMesh mesh, Color color, List<Vector3> positions, float lift)
    {
        if (positions.Count == 0)
        {
            return;
        }

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

        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }
}
