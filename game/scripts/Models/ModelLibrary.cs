using System.Collections.Generic;
using Godot;

namespace Rts.Client.Models;

/// <summary>
/// The models of the game by the table <c>res://data/visuals/models.yaml</c> (<see cref="ModelTable"/>, docs/art/model_standards.md),
/// read once. Every renderer asks here first and draws its placeholder when the answer is null: the object is not in the table, its
/// file is not there (a warning, once), or the climate is not one of its own. A model's <c>team_mask</c> node takes the owner's colour.
/// </summary>
public static class ModelLibrary
{
    public const string TablePath = "res://data/visuals/models.yaml";
    public const string Root = "res://assets/models/";

    private static IReadOnlyDictionary<string, ModelEntry>? _table;
    private static readonly Dictionary<string, PackedScene?> Scenes = new();
    private static readonly Dictionary<string, List<(Mesh Mesh, Transform3D Place)>?> MeshSets = new();

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

    /// <summary>The entry of <paramref name="key"/> whose file is there, for a map of <paramref name="climate"/> (any when null).</summary>
    public static ModelEntry? Find(string key, string? climate = null) =>
        Table.TryGetValue(key, out var entry) && (climate is null || entry.DrawnIn(climate)) && Scene(entry) is not null ? entry : null;

    /// <summary>The path of the model's file in the project, for the renderers that load meshes themselves; null without one.</summary>
    public static string? PathOf(string key) => Find(key) is { } entry ? Root + entry.File : null;

    /// <summary>
    /// The model of <paramref name="key"/> placed as the table says, its pivot at the origin of the node it is added to; its
    /// <c>team_mask</c> in <paramref name="team"/> when given. Null when there is none — the caller draws its placeholder.
    /// </summary>
    public static Node3D? Instantiate(string key, Color? team = null, string? climate = null)
    {
        if (Find(key, climate) is not { } entry || Scene(entry) is not { } scene)
        {
            return null;
        }

        var holder = new Node3D
        {
            Name = "Model",
            Position = new Vector3(entry.Offset.X, entry.Offset.Y, entry.Offset.Z),
            RotationDegrees = new Vector3(0f, entry.Yaw, 0f),
            Scale = Vector3.One * entry.Scale,
        };
        var model = scene.Instantiate<Node3D>();
        holder.AddChild(model);
        if (team is { } colour)
        {
            Tint(model, colour);
        }

        return holder;
    }

    /// <summary>
    /// The meshes of the model with their places in the holder's frame, the table's placement in them: for the renderers that draw
    /// many of one object at once (MultiMesh: trees, snowdrifts). Null when there is none.
    /// </summary>
    public static IReadOnlyList<(Mesh Mesh, Transform3D Place)>? Meshes(string key, string? climate = null)
    {
        if (Find(key, climate) is not { } entry)
        {
            return null;
        }

        if (!MeshSets.TryGetValue(key, out var set))
        {
            set = [];
            var model = Scene(entry)!.Instantiate<Node3D>();
            var placement = new Transform3D(
                new Basis(Vector3.Up, Mathf.DegToRad(entry.Yaw)).Scaled(Vector3.One * entry.Scale),
                new Vector3(entry.Offset.X, entry.Offset.Y, entry.Offset.Z));
            Collect(model, placement, set);
            model.Free();
            MeshSets[key] = set = set.Count > 0 ? set : null;
        }

        return set;
    }

    private static PackedScene? Scene(ModelEntry entry)
    {
        var path = Root + entry.File;
        if (!Scenes.TryGetValue(path, out var scene))
        {
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            if (scene is null)
            {
                GD.PushWarning($"[models] {entry.Key}: no file {path}; its placeholder is drawn");
            }

            Scenes[path] = scene;
        }

        return scene;
    }

    // Every mesh below the node, with the transform from the holder down to it.
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
