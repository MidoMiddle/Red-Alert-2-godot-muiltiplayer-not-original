using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic map objects on the ground (docs/implementation/conventions.md, section 12; M7_maps.md, 4.7): a tree in the shape of
/// the climate's trees (<see cref="TreeShapes"/>: a pine, a broadleaf tree, a palm or a cactus) and a short trunk for its stump, a grey lump for a boulder, a thin brown rail for a fence, a low steel
/// block for an anti-tank hedgehog, a thin dark pipe for a pipeline (ore and gem sources: <see cref="OreSources"/>), a tall
/// stone block for a wall, a dark pole with a yellow lamp for a lamp post, a grey block for every rock cell, and for a low
/// bridge a plank deck just above the water with a rail on each side, or charred stubs where a segment fell. One MultiMesh
/// per shape (and per direction for bridges). A snowdrift is a bush in summer and a dune in the desert. Rebuilt whenever the map changes
/// (a tree felled, a bridge blown up) or its climate does (the editor), which is rare.
/// </summary>
public partial class MapObjects : Node3D
{
    private const float RockHeight = 0.5f;
    private const float TrunkHeight = 0.25f;
    private const float CrownHeight = 0.8f;

    private MapGrid _map = null!;
    private int _builtVersion = -1;
    private MapGrid? _builtMap;
    private Climates.ClimateLook? _builtClimate;

    public override void _Process(double delta)
    {
        // A new match or a new map in the editor (another map object), or a change of this one.
        var map = GetParent<TerrainBuilder>().Map;
        var climate = GetParent<TerrainBuilder>().Climate;
        if (!ReferenceEquals(map, _builtMap) || map.Version != _builtVersion || !ReferenceEquals(climate, _builtClimate))
        {
            _map = map;
            _builtClimate = climate;
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
        var kind = _builtClimate?.Trees ?? Climates.TreeKind.Pine;
        var trees = new List<(Vector3 At, int Variant)>();
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
                (MapObjectKind.PineTree, false) => null,
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
            if (mapObject is { Kind: MapObjectKind.PineTree, Destroyed: false })
            {
                trees.Add((at, mapObject.Variant));
            }
        }

        // The pines beyond the edge (MapSkirt): only a picture, the rules never see them.
        foreach (var (x, z) in GetParent<TerrainBuilder>().Skirt.Trees)
        {
            trees.Add((new Vector3(x, WorldCoords.Terrain!.HeightAt(x, z), z), (int)(System.MathF.Abs(x * 7f + z * 13f) % 4)));
        }

        AddTrees(kind, trees);

        foreach (var bridge in _map.Bridges)
        {
            AddBridge(bridge);
        }

        AddInstances("Rocks", new BoxMesh { Size = new Vector3(0.98f, RockHeight, 0.98f) }, new Color(0.45f, 0.45f, 0.48f), rocks, RockHeight / 2);
        AddInstances("Stumps", new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.09f, Height = 0.08f }, new Color(0.42f, 0.3f, 0.18f), stumps, 0.04f);
        // The models of the table by the object's id (docs/art/model_standards.md), the primitives without one.
        AddInstances("Boulders", new SphereMesh { Radius = 0.36f, Height = 0.5f }, new Color(0.5f, 0.5f, 0.52f), boulders, 0.2f, "object/boulder");
        AddInstances("Fences", new BoxMesh { Size = new Vector3(0.96f, 0.25f, 0.06f) }, new Color(0.5f, 0.35f, 0.2f), fences, 0.125f, "object/fence_wood");
        AddInstances("Walls", new BoxMesh { Size = new Vector3(0.98f, 0.6f, 0.98f) }, new Color(0.68f, 0.65f, 0.58f), walls, 0.3f, "object/wall_stone");
        if (!AddInstances("LampPosts", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.7f }, new Color(0.15f, 0.15f, 0.17f), posts, 0.35f, "object/lamp_post"))
        {
            AddInstances("Lamps", new SphereMesh { Radius = 0.06f, Height = 0.12f }, new Color(1f, 0.85f, 0.35f), posts, 0.72f);
        }

        AddInstances("Hedgehogs", new BoxMesh { Size = new Vector3(0.9f, 0.3f, 0.9f) }, new Color(0.3f, 0.31f, 0.34f), hedgehogs, 0.15f, "object/anti_tank_hedgehog");
        AddInstances("Pipes", new BoxMesh { Size = new Vector3(0.98f, 0.12f, 0.12f) }, new Color(0.22f, 0.25f, 0.28f), pipes, 0.06f, "object/pipeline");
        // A snowdrift of the map is a bush in summer and in town, a dune in the desert.
        var drift = kind switch
        {
            Climates.TreeKind.Broadleaf => new Color(0.24f, 0.4f, 0.2f),
            Climates.TreeKind.Desert => new Color(0.85f, 0.74f, 0.52f),
            _ => new Color(0.9f, 0.93f, 0.97f),
        };
        AddInstances("Snowdrifts", new SphereMesh { Radius = 0.45f, Height = 0.22f }, drift, drifts, 0.11f, "object/snowdrift");
    }

    /// <summary>
    /// The deck of a standing segment lies flat just above the water across all its cells (three tanks side by side), so units on it
    /// seem to drive on the planks; the rails run along its outer edges.
    /// </summary>
    private void AddBridge(Bridge bridge)
    {
        // A bridge of one cell runs across the cells beside it.
        var alongX = bridge.From != bridge.To ? bridge.From.Y == bridge.To.Y : !System.Linq.Enumerable.Any(bridge.Segments[0].Cells, cell => cell.X != bridge.From.X);
        var decks = new List<Vector3>();
        var rails = new List<Vector3>();
        var stubs = new List<Vector3>();
        var side = alongX ? new Vector3(0, 0, 0.44f) : new Vector3(0.44f, 0, 0);
        foreach (var segment in bridge.Segments)
        {
            // The outer cells of the span across, for the rails.
            var (first, last) = (segment.Cell, segment.Cell);
            foreach (var cell in segment.Cells)
            {
                decks.Add(Ground(cell.X, cell.Y));
                (first, last) = alongX
                    ? (cell.Y < first.Y ? cell : first, cell.Y > last.Y ? cell : last)
                    : (cell.X < first.X ? cell : first, cell.X > last.X ? cell : last);
            }

            if (!segment.IsIntact)
            {
                decks.RemoveRange(decks.Count - segment.Cells.Length, segment.Cells.Length);
                stubs.Add(Ground(first.X, first.Y) - side);
                stubs.Add(Ground(last.X, last.Y) + side);
                continue;
            }

            rails.Add(Ground(first.X, first.Y) - side);
            rails.Add(Ground(last.X, last.Y) + side);
        }

        var name = $"Bridge{bridge.Id}";
        var deck = alongX ? new Vector3(1f, 0.05f, 0.9f) : new Vector3(0.9f, 0.05f, 1f);
        var rail = alongX ? new Vector3(1f, 0.1f, 0.04f) : new Vector3(0.04f, 0.1f, 1f);
        AddInstances(name + "Deck", new BoxMesh { Size = deck }, new Color(0.55f, 0.4f, 0.24f), decks, 0.03f);
        AddInstances(name + "Rails", new BoxMesh { Size = rail }, new Color(0.38f, 0.26f, 0.15f), rails, 0.1f);
        AddInstances(name + "Stubs", new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) }, new Color(0.2f, 0.16f, 0.13f), stubs, 0.02f);
    }

    private static Vector3 Ground(int x, int y) => new(x + 0.5f, WorldCoords.Terrain!.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);

    /// <summary>
    /// The trees of the map and the skirt: the models of a shape from the table (<c>tree/&lt;shape&gt;</c>, <see cref="TreeShapes.NameOf"/>),
    /// each tree the kind, size and turn of its cell, one MultiMesh for every mesh of every kind; or else every part of the shape of
    /// <see cref="TreeShapes"/> one MultiMesh.
    /// </summary>
    private void AddTrees(Climates.TreeKind kind, List<(Vector3 At, int Variant)> trees)
    {
        var parts = new Dictionary<string, (TreePart Part, List<Transform3D> Places)>();
        var modelled = new Dictionary<string, List<Vector3>>();
        foreach (var (at, variant) in trees)
        {
            var shape = TreeShapes.NameOf(kind, variant);
            if (Models.ModelLibrary.Find("tree/" + shape) is not null)
            {
                if (!modelled.TryGetValue(shape, out var points))
                {
                    points = [];
                    modelled[shape] = points;
                }

                points.Add(at);
                continue;
            }

            foreach (var part in TreeShapes.Of(kind, variant))
            {
                var key = part.Key + "/" + part.Color.ToHtml();
                if (!parts.TryGetValue(key, out var entry))
                {
                    entry = (part, []);
                    parts[key] = entry;
                }

                entry.Places.Add(new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.DegToRad(part.YawDegrees), 0)), at + part.Offset));
            }
        }

        foreach (var (key, (part, places)) in parts)
        {
            var mesh = part.Mesh();
            mesh.Material = new StandardMaterial3D { AlbedoColor = part.Color };
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = places.Count };
            for (var i = 0; i < places.Count; i++)
            {
                multiMesh.SetInstanceTransform(i, places[i]);
            }

            AddChild(new MultiMeshInstance3D { Name = "Trees" + part.Key + parts.Keys.ToList().IndexOf(key), Multimesh = multiMesh });
        }

        foreach (var (shape, points) in modelled)
        {
            AddModelInstances("TreeModel" + shape, Models.ModelLibrary.Instances("tree/" + shape, null, points)!);
        }
    }

    /// <summary>A MultiMesh for every mesh of the copies of a model (<see cref="Models.ModelLibrary.Instances"/>).</summary>
    private void AddModelInstances(string name, List<(Mesh Mesh, List<Transform3D> Places)> groups)
    {
        for (var g = 0; g < groups.Count; g++)
        {
            var (mesh, places) = groups[g];
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = places.Count };
            for (var i = 0; i < places.Count; i++)
            {
                multiMesh.SetInstanceTransform(i, places[i]);
            }

            AddChild(new MultiMeshInstance3D { Name = name + g, Multimesh = multiMesh });
        }
    }

    /// <summary>
    /// One MultiMesh of a kind of map object: its model of the table (<paramref name="key"/>, pivot on the ground) when there is one
    /// for the climate — a MultiMesh for every mesh of every kind of it, each copy the kind, size and turn of its cell — or else the
    /// primitive raised by <paramref name="lift"/>. True for a model.
    /// </summary>
    private bool AddInstances(string name, PrimitiveMesh mesh, Color color, List<Vector3> positions, float lift, string? key = null)
    {
        if (positions.Count == 0)
        {
            return false;
        }

        if (key is not null && Models.ModelLibrary.Instances(key, _builtClimate?.Id, positions) is { } groups)
        {
            AddModelInstances(name + "Model", groups);
            return true;
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
        return false;
    }
}
