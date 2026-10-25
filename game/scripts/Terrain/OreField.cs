using System.Collections.Generic;
using Godot;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The ore of a match (docs/implementation/M3_base_economy.md, 4.12, step 9): on every cell of ore a cluster of golden stones, on every
/// cell of gems one of blue gems (the models of the table, <c>ore/ore</c> and <c>ore/gems</c>: their files only), larger the higher the stage and turned by the cell
/// (<see cref="OreCrystals.ClusterScale"/>). Without the model files, yellow and blue diamonds as before, one to four a cell
/// (<see cref="OreCrystals.Of"/>, step 13). Rebuilt when the ore changes (<see cref="MapGrid.OreVersion"/>): it grows every 30 seconds,
/// the drills throw it and the harvesters take it — at most every <see cref="RebuildPause"/> seconds on the same map, since every
/// harvester takes a bite of a cell every few ticks and the whole field is drawn anew each time.
/// </summary>
public partial class OreField : Node3D
{
    public static readonly Color OreColor = new(0.95f, 0.8f, 0.15f);
    public static readonly Color GemColor = new(0.35f, 0.8f, 1f);

    /// <summary>The cluster of ore stones (docs/assets_registry.md): about 0.7 of a cell wide at full size.</summary>
    public const string OreModel = "ore/ore";

    /// <summary>The cluster of gems, the same size.</summary>
    public const string GemModel = "ore/gems";

    private static readonly Dictionary<string, Mesh?> Meshes = new();

    /// <summary>The shortest time between two redraws of the ore of one map: a bite taken shows a moment later, no one sees it.</summary>
    private const double RebuildPause = 0.25;

    private object? _builtMap;
    private int _builtVersion = -1;
    private double _sinceBuilt;

    public override void _Process(double delta)
    {
        var map = GetParent<TerrainBuilder>().Map;
        _sinceBuilt += delta;
        // The editor draws its own ore (EditorMarks), from the map it edits.
        if (GetNodeOrNull<SimulationHost>("../../SimulationHost") is null || WorldCoords.Terrain is not { } terrain
            || (ReferenceEquals(map, _builtMap) && (map.OreVersion == _builtVersion || _sinceBuilt < RebuildPause)))
        {
            return;
        }

        _sinceBuilt = 0;
        _builtMap = map;
        _builtVersion = map.OreVersion;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var oreMesh = MeshOf(OreModel);
        var gemMesh = MeshOf(GemModel);
        if (oreMesh is not null && gemMesh is not null)
        {
            AddClusters(map, terrain, oreMesh, gemMesh);
            return;
        }

        var ore = new List<Transform3D>();
        var gems = new List<Transform3D>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = map.OreAt(new Rts.Simulation.Geometry.CellPos(x, y));
                foreach (var crystal in OreCrystals.Of(x, y, cell))
                {
                    (cell.Kind == OreKind.Gems ? gems : ore).Add(Diamond(terrain, x + crystal.X - 0.5f, y + crystal.Z - 0.5f, crystal.Size));
                }
            }
        }

        AddDiamonds(this, "Ore", ore, OreColor);
        AddDiamonds(this, "Gems", gems, GemColor);
    }

    /// <summary>One cluster of the model a cell, on the ground in its middle, sized by the stage and turned by the cell.</summary>
    private void AddClusters(MapGrid map, TerrainHeights terrain, Mesh oreMesh, Mesh gemMesh)
    {
        var ore = new List<Transform3D>();
        var gems = new List<Transform3D>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = map.OreAt(new Rts.Simulation.Geometry.CellPos(x, y));
                if (cell.IsEmpty)
                {
                    continue;
                }

                var scale = OreCrystals.ClusterScale(cell);
                var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);
                var basis = new Basis(Vector3.Up, OreCrystals.ClusterTurn(x, y)).Scaled(new Vector3(scale, scale, scale));
                (cell.Kind == OreKind.Gems ? gems : ore).Add(new Transform3D(basis, at));
            }
        }

        AddInstances(this, "Ore", ore, oreMesh);
        AddInstances(this, "Gems", gems, gemMesh);
    }

    /// <summary>The mesh of the <c>body</c> of a model, loaded once; null without the file — the diamonds stand in.</summary>
    internal static Mesh? MeshOf(string key)
    {
        if (!Meshes.TryGetValue(key, out var mesh))
        {
            mesh = null;
            if (Models.ModelLibrary.PathOf(key) is { } path && GD.Load<PackedScene>(path) is { } scene)
            {
                var root = scene.Instantiate<Node3D>();
                mesh = (root.FindChild("body", true, false) as MeshInstance3D)?.Mesh;
                root.QueueFree();
            }

            if (mesh is null)
            {
                GD.PushWarning($"No ore model for {key}: the ore is drawn with diamonds.");
            }

            Meshes[key] = mesh;
        }

        return mesh;
    }

    internal static void AddInstances(Node3D parent, string name, List<Transform3D> transforms, Mesh mesh)
    {
        if (transforms.Count == 0)
        {
            return;
        }

        var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (var i = 0; i < transforms.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, transforms[i]);
        }

        parent.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }

    /// <summary>A diamond of <paramref name="size"/> standing on the middle of the cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static Transform3D Diamond(TerrainHeights terrain, float x, float y, float size)
    {
        var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f) + size, y + 0.5f);
        return new Transform3D(new Basis(Vector3.Forward, Mathf.Pi / 4f).Scaled(new Vector3(size, size, size)), at);
    }

    /// <summary>Adds the diamonds under <paramref name="parent"/> as one multi-mesh.</summary>
    public static void AddDiamonds(Node3D parent, string name, List<Transform3D> transforms, Color color)
    {
        if (transforms.Count == 0)
        {
            return;
        }

        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = color } },
            InstanceCount = transforms.Count,
        };
        for (var i = 0; i < transforms.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, transforms[i]);
        }

        parent.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }
}
