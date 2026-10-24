using System.Collections.Generic;
using Godot;

namespace Rts.Client.Models;

/// <summary>
/// The models of the game by the table <c>res://data/visuals/models.yaml</c> (<see cref="ModelTable"/>, docs/art/model_standards.md),
/// read once. Every renderer asks here first and draws its placeholder when the answer is null: the object is not in the table, none
/// of its files is there (a warning for each missing one, once), or the climate is not one of its own. Of several files the cell
/// picks one, and the size and the turn (<see cref="ModelEntry.Pick"/>). A model's <c>team_mask</c> node takes the owner's colour.
/// </summary>
public static class ModelLibrary
{
    public const string TablePath = "res://data/visuals/models.yaml";
    public const string Root = "res://assets/models/";

    private static IReadOnlyDictionary<string, ModelEntry>? _table;
    private static readonly Dictionary<string, PackedScene?> Scenes = new();
    private static readonly Dictionary<string, List<PackedScene>> Available = new();
    private static readonly Dictionary<string, List<List<(Mesh Mesh, Transform3D Place)>>> MeshSets = new();

    /// <summary>The entries of the table; empty when it is missing or cannot be read (an error, once).</summary>
    public static IReadOnlyDictionary<string, ModelEntry> Table
    {
        get
        {
            if (_table is null)
            {
                _table = new Dictionary<string, ModelEntry>();
                if (FileAccess.FileExists(TablePath))
                {
                    try
                    {
                        _table = ModelTable.Read(FileAccess.GetFileAsString(TablePath));
                    }
                    catch (ModelTableException e)
                    {
                        GD.PushError($"[models] {TablePath}:{e.Message}: every object is drawn with its placeholder");
                    }
                }
            }

            return _table;
        }
    }

    /// <summary>The entry of <paramref name="key"/> with a file there, for a map of <paramref name="climate"/> (any when null).</summary>
    public static ModelEntry? Find(string key, string? climate = null) =>
        Table.TryGetValue(key, out var entry) && (climate is null || entry.DrawnIn(climate)) && ScenesOf(entry).Count > 0 ? entry : null;

    /// <summary>The path of the model's first file there, for the renderers that load meshes themselves; null without one.</summary>
    public static string? PathOf(string key) => Find(key) is { } entry ? ScenesOf(entry)[0].ResourcePath : null;

    /// <summary>
    /// The model of <paramref name="key"/> placed as the table says, its pivot at the origin of the node it is added to; of several
    /// files the one of <paramref name="cell"/> (the first without one); its <c>team_mask</c> in <paramref name="team"/> when given.
    /// Null when there is none — the caller draws its placeholder.
    /// </summary>
    public static Node3D? Instantiate(string key, Color? team = null, string? climate = null, Vector2I? cell = null)
    {
        if (Find(key, climate) is not { } entry)
        {
            return null;
        }

        var scenes = ScenesOf(entry);
        var pick = cell is { } at ? entry.Pick(at.X, at.Y, scenes.Count) : new ModelPick(0, entry.Scale, entry.Yaw);
        var holder = new Node3D { Name = "Model", Transform = Placement(entry, pick) };
        var model = scenes[pick.Index].Instantiate<Node3D>();
        holder.AddChild(model);
        if (team is { } colour)
        {
            Tint(model, colour);
        }

        return holder;
    }

    /// <summary>
    /// Many of one model at once (MultiMesh: trees, snowdrifts, fences): at every point, the file, the size and the turn of its cell.
    /// For every mesh of every file used, the places of its copies. Null when there is no model — the caller draws its placeholder.
    /// </summary>
    public static List<(Mesh Mesh, List<Transform3D> Places)>? Instances(string key, string? climate, IReadOnlyList<Vector3> points)
    {
        if (Find(key, climate) is not { } entry)
        {
            return null;
        }

        var sets = MeshSetsOf(entry);
        var groups = new Dictionary<(int File, int Mesh), (Mesh Mesh, List<Transform3D> Places)>();
        foreach (var point in points)
        {
            var pick = entry.Pick(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Z), sets.Count);
            var placed = new Transform3D(Basis.Identity, point) * Placement(entry, pick);
            var set = sets[pick.Index];
            for (var m = 0; m < set.Count; m++)
            {
                if (!groups.TryGetValue((pick.Index, m), out var group))
                {
                    group = (set[m].Mesh, []);
                    groups[(pick.Index, m)] = group;
                }

                group.Places.Add(placed * set[m].Place);
            }
        }

        return [.. groups.Values];
    }

    /// <summary>How a model of the entry sits in its holder: moved, turned and scaled as the pick says.</summary>
    private static Transform3D Placement(ModelEntry entry, ModelPick pick) =>
        new(new Basis(Vector3.Up, Mathf.DegToRad(pick.Yaw)).Scaled(Vector3.One * pick.Scale), new Vector3(entry.Offset.X, entry.Offset.Y, entry.Offset.Z));

    // The scenes of the files of the entry that are there, in the order of the table.
    private static List<PackedScene> ScenesOf(ModelEntry entry)
    {
        if (!Available.TryGetValue(entry.Key, out var scenes))
        {
            scenes = [];
            foreach (var file in entry.Files)
            {
                if (Scene(entry.Key, Root + file) is { } scene)
                {
                    scenes.Add(scene);
                }
            }

            Available[entry.Key] = scenes;
        }

        return scenes;
    }

    // The meshes of every file of the entry with their places inside the model.
    private static List<List<(Mesh Mesh, Transform3D Place)>> MeshSetsOf(ModelEntry entry)
    {
        if (!MeshSets.TryGetValue(entry.Key, out var sets))
        {
            sets = [];
            foreach (var scene in ScenesOf(entry))
            {
                var set = new List<(Mesh, Transform3D)>();
                var model = scene.Instantiate<Node3D>();
                Collect(model, Transform3D.Identity, set);
                model.Free();
                sets.Add(set);
            }

            MeshSets[entry.Key] = sets;
        }

        return sets;
    }

    private static PackedScene? Scene(string key, string path)
    {
        if (!Scenes.TryGetValue(path, out var scene))
        {
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            if (scene is null)
            {
                GD.PushWarning($"[models] {key}: no file {path}; it is left out, and with no file left the placeholder is drawn");
            }

            Scenes[path] = scene;
        }

        return scene;
    }

    // Every mesh below the node, with the transform from the model's root down to it.
    private static void Collect(Node node, Transform3D parent, List<(Mesh, Transform3D)> set)
    {
        var place = node is Node3D spatial ? parent * spatial.Transform : parent;
        if (node is MeshInstance3D { Mesh: { } mesh })
        {
            set.Add((mesh, place));
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, place, set);
        }
    }

    // The meshes under a node named team_mask take the owner's colour over their own material.
    private static void Tint(Node node, Color colour, bool inMask = false)
    {
        inMask |= node.Name.ToString().StartsWith("team_mask", System.StringComparison.Ordinal);
        if (inMask && node is MeshInstance3D mesh)
        {
            mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.7f };
        }

        foreach (var child in node.GetChildren())
        {
            Tint(child, colour, inMask);
        }
    }
}
