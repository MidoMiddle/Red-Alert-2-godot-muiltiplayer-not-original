using System.Collections.Generic;
using Godot;
using Rts.Simulation.Events;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The ore and gem sources of the map (docs/implementation/M3_base_economy.md, 4.12, step 16): the drill tower of
/// <see cref="ModelPath"/> — its still <c>tower</c> and its turning <c>drill</c>, the screw and the head, tinted in the colour of what it
/// throws: gold for ore, blue for gems. Without the model file, a tower of boxes on a dark pad with a head of that colour. The drill turns
/// slowly all the time; on <see cref="OreSourceErupted"/> it spins fast for <see cref="SpinSeconds"/> and a burst of crystals flies in an
/// arc from the head to the cell that got the stage. In the editor there is no simulation, so the drills only turn. One view per source,
/// made again only when the sources of the map change.
/// </summary>
public partial class OreSources : Node3D
{
    /// <summary>How long a head spins fast after an eruption, in seconds.</summary>
    public const float SpinSeconds = 1.5f;

    private const float IdleSpin = 1.2f;
    private const float EruptionSpin = 14f;
    private const float HeadHeight = 1.05f;

    /// <summary>The drill tower (docs/assets_registry.md): 0.9 of a cell wide, the head 3 cells up, as modelled.</summary>
    public const string ModelPath = "res://assets/models/map/ore_source/ore_source.glb";

    // Where the crystals leave the model: by the head, under the top of the screw.
    private const float ModelHeadHeight = 2.7f;

    // How much of the colour of the resource the drill of the model takes over its own texture.
    private const float ModelTint = 0.65f;
    private const float Gravity = 5f;

    private static readonly Color PadColor = new(0.22f, 0.23f, 0.25f);
    private static readonly Color TowerColor = new(0.5f, 0.52f, 0.56f);

    private readonly Dictionary<int, View> _views = new();
    private readonly Dictionary<MapObjectKind, Material> _tints = new();
    private PackedScene? _model;
    private bool _modelLooked;
    private string _builtKey = "";
    private int _placedVersion = -1;
    private SimulationHost? _host;

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../../SimulationHost");
        if (_host is not null)
        {
            _host.EventRaised += OnEvent;
        }
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
        }
    }

    public override void _Process(double delta)
    {
        var map = GetParent<TerrainBuilder>().Map;
        var key = KeyOf(map);
        if (key != _builtKey)
        {
            Rebuild(map, key);
        }
        else if (map.Version != _placedVersion)
        {
            // The ground under a source rose or fell in the editor.
            Place(map);
        }

        foreach (var view in _views.Values)
        {
            view.SpinLeft = Mathf.Max(0f, view.SpinLeft - (float)delta);
            var speed = IdleSpin + (EruptionSpin - IdleSpin) * (view.SpinLeft / SpinSeconds);
            view.Head.RotateY(speed * (float)delta);
        }
    }

    /// <summary>Which sources stand where: a new match, or a source placed or erased in the editor, gives another key.</summary>
    private static string KeyOf(MapGrid map)
    {
        var key = new System.Text.StringBuilder();
        key.Append(map.Width).Append('x').Append(map.Height);
        foreach (var source in map.Sources)
        {
            key.Append(';').Append(source.Id).Append(',').Append((int)source.Kind).Append(',').Append(source.Cell.X).Append(',').Append(source.Cell.Y);
        }

        return key.ToString();
    }

    private void Rebuild(MapGrid map, string key)
    {
        foreach (var view in _views.Values)
        {
            view.Root.QueueFree();
        }

        _views.Clear();
        if (WorldCoords.Terrain is null)
        {
            return;
        }

        _builtKey = key;
        foreach (var source in map.Sources)
        {
            var color = source.Kind == MapObjectKind.GemSource ? OreField.GemColor : OreField.OreColor;
            var root = new Node3D { Name = $"Source{source.Id}" };
            if (Model() is { } model && ModelView(root, model, source.Kind, color) is { } view)
            {
                AddChild(root);
                _views[source.Id] = view;
                continue;
            }

            Box(root, new Vector3(0.95f, 0.12f, 0.95f), new Vector3(0, 0.06f, 0), PadColor);
            foreach (var (x, z) in new[] { (-0.28f, -0.28f), (0.28f, -0.28f), (-0.28f, 0.28f), (0.28f, 0.28f) })
            {
                Box(root, new Vector3(0.07f, 0.9f, 0.07f), new Vector3(x, 0.55f, z), TowerColor);
            }

            Box(root, new Vector3(0.66f, 0.06f, 0.66f), new Vector3(0, 0.95f, 0), TowerColor);

            // The head: a hub with two blades and the bit under it, all turning together.
            var head = new Node3D { Position = new Vector3(0, HeadHeight, 0) };
            head.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.16f, Height = 0.2f, Material = Material(color) } });
            Box(head, new Vector3(0.7f, 0.06f, 0.12f), new Vector3(0, 0.05f, 0), color);
            head.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0f, Height = 0.75f, Material = Material(color.Darkened(0.3f)) },
                Position = new Vector3(0, -0.5f, 0),
            });
            root.AddChild(head);
            AddChild(root);
            _views[source.Id] = new View(root, head, color, HeadHeight);
        }

        Place(map);
    }

    private void Place(MapGrid map)
    {
        _placedVersion = map.Version;
        if (WorldCoords.Terrain is not { } terrain)
        {
            return;
        }

        foreach (var source in map.Sources)
        {
            if (_views.TryGetValue(source.Id, out var view))
            {
                view.Root.Position = new Vector3(source.Cell.X + 0.5f, terrain.HeightAt(source.Cell.X + 0.5f, source.Cell.Y + 0.5f), source.Cell.Y + 0.5f);
            }
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (simEvent is not OreSourceErupted erupted || !_views.TryGetValue(erupted.Source, out var view) || WorldCoords.Terrain is not { } terrain)
        {
            return;
        }

        view.SpinLeft = SpinSeconds;
        var from = view.Root.Position + new Vector3(0, view.EmitHeight + 0.1f, 0);
        var to = new Vector3(erupted.Cell.X + 0.5f, terrain.HeightAt(erupted.Cell.X + 0.5f, erupted.Cell.Y + 0.5f), erupted.Cell.Y + 0.5f);
        var flat = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        var distance = Mathf.Max(flat.Length(), 0.5f);

        // Thrown at 45°: speed √(g·d) lands about d away after √(2d/g) seconds (a little short, as the cell lies below the head); the
        // crystals live as long as the flight, so none falls on through the ground.
        var speed = Mathf.Sqrt(Gravity * distance);
        var flight = Mathf.Sqrt(2f * distance / Gravity);
        var burst = new CpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.85f,
            Amount = 18,
            Lifetime = flight,
            LocalCoords = false,
            Position = from,
            Direction = (flat / distance + Vector3.Up).Normalized(),
            Spread = 14f,
            InitialVelocityMin = speed * 0.9f,
            InitialVelocityMax = speed * 1.1f,
            Gravity = new Vector3(0, -Gravity, 0),
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.2f,
            Mesh = new BoxMesh { Size = new Vector3(0.11f, 0.11f, 0.11f), Material = Material(view.Color) },
        };
        AddChild(burst);
        burst.Emitting = true;
        GetTree().CreateTimer(burst.Lifetime + 0.5).Timeout += burst.QueueFree;
    }

    /// <summary>The model, loaded once; null when the file is not there — the boxes stand in.</summary>
    private PackedScene? Model()
    {
        if (!_modelLooked)
        {
            _modelLooked = true;
            _model = ResourceLoader.Exists(ModelPath) ? GD.Load<PackedScene>(ModelPath) : null;
            if (_model is null)
            {
                GD.PushWarning($"No drill model at {ModelPath}: the drills are drawn with boxes.");
            }
        }

        return _model;
    }

    /// <summary>The model under <paramref name="root"/>, its drill tinted for <paramref name="kind"/>; null when it has no <c>drill</c> node.</summary>
    private View? ModelView(Node3D root, PackedScene model, MapObjectKind kind, Color color)
    {
        var instance = model.Instantiate<Node3D>();
        if (instance.FindChild("drill", true, false) is not Node3D drill)
        {
            instance.QueueFree();
            return null;
        }

        root.AddChild(instance);
        if (drill is MeshInstance3D mesh && Tint(mesh, kind, color) is { } tint)
        {
            mesh.MaterialOverride = tint;
        }

        return new View(root, drill, color, ModelHeadHeight);
    }

    // The drill's own material with the colour of the resource over its texture, one per kind for every drill of it.
    private Material? Tint(MeshInstance3D mesh, MapObjectKind kind, Color color)
    {
        if (_tints.TryGetValue(kind, out var tint))
        {
            return tint;
        }

        if (mesh.Mesh?.SurfaceGetMaterial(0) is not BaseMaterial3D own)
        {
            return null;
        }

        var tinted = (BaseMaterial3D)own.Duplicate();
        tinted.AlbedoColor = Colors.White.Lerp(color, ModelTint);
        _tints[kind] = tinted;
        return tinted;
    }

    private static void Box(Node3D parent, Vector3 size, Vector3 position, Color color) =>
        parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = Material(color) }, Position = position });

    private static StandardMaterial3D Material(Color color) => new() { AlbedoColor = color };

    private sealed class View(Node3D root, Node3D head, Color color, float emitHeight)
    {
        /// <summary>The height the crystals leave from.</summary>
        public float EmitHeight { get; } = emitHeight;

        public Node3D Root { get; } = root;

        public Node3D Head { get; } = head;

        public Color Color { get; } = color;

        public float SpinLeft { get; set; }
    }
}
