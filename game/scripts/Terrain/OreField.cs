using System.Collections.Generic;
using Godot;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The ore of a match (docs/implementation/M3_base_economy.md, 4.12, step 9): on every cell of ore a cluster of golden stones, on every
/// cell of gems one of blue gems (the models of the table, <c>ore/ore</c> and <c>ore/gems</c>: their files only), larger the higher the stage and turned by the cell
/// (<see cref="OreCrystals.ClusterScale"/>). Without the model files, yellow and blue diamonds as before, one to four a cell
/// (<see cref="OreCrystals.Of"/>, step 13). Redrawn when the ore changes (<see cref="MapGrid.OreVersion"/>): it grows every 30 seconds,
/// the drills throw it and the harvesters take it — at most every <see cref="RebuildPause"/> seconds on the same map, and only the pieces
/// of the map whose cells changed (<see cref="Redraw"/>).
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

    // The ore of every cell as last drawn (OreCell.Packed) and the nodes of every piece of the map that holds some: when the ore changes,
    // only the pieces whose cells changed are drawn anew. The whole field anew four times a second, as a big map with many drills and
    // harvesters had it, was a third of all the garbage of the game (the performance log, 2026-10-28).
    private byte[] _drawn = [];
    private readonly Dictionary<(int X, int Z), List<Node>> _pieces = new();
    private readonly HashSet<(int X, int Z)> _dirty = new();
    private readonly List<Transform3D> _ore = new();
    private readonly List<Transform3D> _gems = new();
    private Mesh? _oreMesh;
    private Mesh? _gemMesh;
    private bool _clusters;

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("OreField.process");
        var map = GetParent<TerrainBuilder>().Map;
        _sinceBuilt += delta;
        // The editor draws its own ore (EditorMarks), from the map it edits.
        if (GetNodeOrNull<SimulationHost>("../../SimulationHost") is null || WorldCoords.Terrain is not { } terrain
            || (ReferenceEquals(map, _builtMap) && (map.OreVersion == _builtVersion || _sinceBuilt < RebuildPause)))
        {
            return;
        }

        _sinceBuilt = 0;
        if (!ReferenceEquals(map, _builtMap) || _drawn.Length != map.Width * map.Height)
        {
            // Another map: all of it anew.
            foreach (var child in GetChildren())
            {
                child.QueueFree();
            }

            _pieces.Clear();
            _drawn = new byte[map.Width * map.Height];
            LoadMeshes();
        }

        _builtMap = map;
        _builtVersion = map.OreVersion;
        _dirty.Clear();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var packed = map.OreAt(new Rts.Simulation.Geometry.CellPos(x, y)).Packed;
                if (packed != _drawn[y * map.Width + x])
                {
                    _drawn[y * map.Width + x] = packed;
                    _dirty.Add((x / ChunkedInstances.ChunkCells, y / ChunkedInstances.ChunkCells));
                }
            }
        }

        foreach (var piece in _dirty)
        {
            Redraw(map, terrain, piece);
        }
    }

    /// <summary>The meshes of the ore and the gems: the clusters of the models, or the diamonds without their files.</summary>
    private void LoadMeshes()
    {
        (_oreMesh, _gemMesh) = (MeshOf(OreModel), MeshOf(GemModel));
        _clusters = _oreMesh is not null && _gemMesh is not null;
        if (!_clusters)
        {
            _oreMesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = OreColor } };
            _gemMesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = GemColor } };
        }
    }

    /// <summary>
    /// The ore of one piece of the map anew (<see cref="ChunkedInstances.ChunkCells"/> cells a side, as <see cref="ChunkedInstances"/>
    /// cuts it): one cluster of the model a cell, on the ground in its middle, sized by the stage and turned by the cell — or the
    /// diamonds, one to four a cell.
    /// </summary>
    private void Redraw(MapGrid map, TerrainHeights terrain, (int X, int Z) piece)
    {
        if (_pieces.Remove(piece, out var old))
        {
            foreach (var node in old)
            {
                node.QueueFree();
            }
        }

        _ore.Clear();
        _gems.Clear();
        var size = ChunkedInstances.ChunkCells;
        for (var y = piece.Z * size; y < System.Math.Min(map.Height, (piece.Z + 1) * size); y++)
        {
            for (var x = piece.X * size; x < System.Math.Min(map.Width, (piece.X + 1) * size); x++)
            {
                var cell = OreCell.FromPacked(_drawn[y * map.Width + x]);
                if (cell.IsEmpty)
                {
                    continue;
                }

                var list = cell.Kind == OreKind.Gems ? _gems : _ore;
                if (_clusters)
                {
                    var scale = OreCrystals.ClusterScale(cell);
                    var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);
                    list.Add(new Transform3D(new Basis(Vector3.Up, OreCrystals.ClusterTurn(x, y)).Scaled(new Vector3(scale, scale, scale)), at));
                    continue;
                }

                foreach (var crystal in OreCrystals.Of(x, y, cell))
                {
                    list.Add(Diamond(terrain, x + crystal.X - 0.5f, y + crystal.Z - 0.5f, crystal.Size));
                }
            }
        }

        var made = new List<Node>(2);
        Add(made, "Ore", piece, _ore, _oreMesh!);
        Add(made, "Gems", piece, _gems, _gemMesh!);
        if (made.Count > 0)
        {
            _pieces[piece] = made;
        }
    }

    private void Add(List<Node> made, string name, (int X, int Z) piece, List<Transform3D> places, Mesh mesh)
    {
        if (places.Count == 0)
        {
            return;
        }

        var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = places.Count };
        for (var i = 0; i < places.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, places[i]);
        }

        var node = new MultiMeshInstance3D { Name = $"{name}_{piece.X}_{piece.Z}", Multimesh = multiMesh };
        AddChild(node);
        made.Add(node);
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
        // In pieces of the map: those off the screen are not drawn.
        ChunkedInstances.Add(parent, name, mesh, transforms);
    }

    /// <summary>A diamond of <paramref name="size"/> standing on the middle of the cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static Transform3D Diamond(TerrainHeights terrain, float x, float y, float size)
    {
        var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f) + size, y + 0.5f);
        return new Transform3D(new Basis(Vector3.Forward, Mathf.Pi / 4f).Scaled(new Vector3(size, size, size)), at);
    }

    /// <summary>Adds the diamonds under <paramref name="parent"/>, in pieces of the map (<see cref="ChunkedInstances"/>).</summary>
    public static void AddDiamonds(Node3D parent, string name, List<Transform3D> transforms, Color color)
    {
        ChunkedInstances.Add(parent, name, new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = color } }, transforms);
    }
}
