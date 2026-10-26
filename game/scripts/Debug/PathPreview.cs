using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>
/// Debug view of A* (docs/implementation/M2_unit_control.md, steps 5–6 and 14): F6 toggles a line for the selected unit, yellow when
/// the goal is reached, orange when it is not, through the waypoints of the smoothed path; a thin grey line shows the cells A*
/// found before smoothing. While the unit drives, the lines are its own path from the simulation; otherwise they are a preview
/// to the cell under the pointer, computed on the client outside the simulation, so it never changes the match. With several
/// units selected, every one that drives shows its own path (up to <see cref="MaxPaths"/>).
/// </summary>
public partial class PathPreview : Node3D
{
    private const float Lift = 0.15f;

    /// <summary>The most paths drawn for a selected group; the rest are left out.</summary>
    public const int MaxPaths = 64;
    private static readonly Color FoundColor = new(1f, 0.9f, 0.2f);
    private static readonly Color PartialColor = new(1f, 0.5f, 0.1f);
    private static readonly Color CellsColor = new(0.55f, 0.55f, 0.55f);

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private Pathfinder? _pathfinder;
    private (EntityId Unit, CellPos From, CellPos To)? _lastQuery;
    private bool _following;

    public bool Enabled { get; private set; }

    /// <summary>The path on screen, or null when there is none.</summary>
    public CellPath? Path { get; private set; }

    /// <summary>The line for the debug panel.</summary>
    public string Description => PathText.Describe(Enabled, _input.Selection.Units.Count, Path, _following, _groupPaths.Count);

    private readonly System.Collections.Generic.List<CellPath> _groupPaths = new();
    private bool _groupDrawn;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _input = GetNode<PlayerInput>("../PlayerInput");
        AddChild(new MeshInstance3D
        {
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
            },
        });
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("debug_path"))
        {
            Enabled = !Enabled;
        }
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("PathPreview.process");
        var world = _host.Simulation.World;
        if (Enabled && _input.Selection.Units.Count > 1)
        {
            DrawGroup(world);
            return;
        }

        _groupPaths.Clear();
        if (!Enabled
            || _input.Selection.Units.Count != 1
            || !world.TryGet(_input.Selection.Units[0], out var entity)
            || entity is not Unit { IsAlive: true } unit)
        {
            Clear();
            return;
        }

        if (unit.Path is { } ownPath)
        {
            if (!_following || !ReferenceEquals(Path, ownPath))
            {
                _following = true;
                _lastQuery = null;
                Path = ownPath;
                Draw(ownPath);
            }

            return;
        }

        _following = false;
        if (_input.CellUnderPointer is not { } target)
        {
            Clear();
            return;
        }

        if (_pathfinder is null || _pathfinder.Map != world.Map)
        {
            _pathfinder = new Pathfinder(world.Map);
            _lastQuery = null;
        }

        var query = (unit.Id, unit.Position.ToCell(), target);
        if (_lastQuery == query)
        {
            return;
        }

        _lastQuery = query;
        var locomotor = unit.Archetype.Locomotor;
        Path = PathSmoother.Smooth(world.Map, _pathfinder.FindPath(query.Item2, target, locomotor), locomotor);
        Draw(Path);
    }

    /// <summary>The own paths of the selected units that drive, redrawn every frame (paths change as units replan).</summary>
    private void DrawGroup(Rts.Simulation.World.GameWorld world)
    {
        Path = null;
        _lastQuery = null;
        _following = false;
        _groupPaths.Clear();
        foreach (var id in _input.Selection.Units)
        {
            if (_groupPaths.Count == MaxPaths)
            {
                break;
            }

            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true, Path: { } path })
            {
                _groupPaths.Add(path);
            }
        }

        Draw(_groupPaths);
        _groupDrawn = true;
    }

    private void Draw(CellPath path) => Draw(new[] { path });

    /// <summary>For each path: thin grey through the cells, bright through the waypoints. Two surfaces of line pairs.</summary>
    private void Draw(System.Collections.Generic.IReadOnlyList<CellPath> paths)
    {
        _mesh.ClearSurfaces();
        if (HasSegment(paths, path => path.Cells))
        {
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            foreach (var path in paths)
            {
                AddLine(path.Cells, CellsColor, Lift * 0.5f);
            }

            _mesh.SurfaceEnd();
        }

        if (HasSegment(paths, path => path.Waypoints))
        {
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            foreach (var path in paths)
            {
                AddLine(path.Waypoints, path.Status == PathStatus.Found ? FoundColor : PartialColor, Lift);
            }

            _mesh.SurfaceEnd();
        }
    }

    /// <summary>A surface needs at least one segment, so a line of two points.</summary>
    private static bool HasSegment(
        System.Collections.Generic.IReadOnlyList<CellPath> paths,
        System.Func<CellPath, System.Collections.Immutable.ImmutableArray<CellPos>> points)
    {
        foreach (var path in paths)
        {
            if (points(path).Length >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private void AddLine(System.Collections.Immutable.ImmutableArray<CellPos> cells, Color color, float lift)
    {
        for (var i = 1; i < cells.Length; i++)
        {
            AddVertex(cells[i - 1], color, lift);
            AddVertex(cells[i], color, lift);
        }
    }

    private void AddVertex(CellPos cell, Color color, float lift)
    {
        var x = cell.X + 0.5f;
        var z = cell.Y + 0.5f;
        _mesh.SurfaceSetColor(color);
        _mesh.SurfaceAddVertex(new Vector3(x, (WorldCoords.Terrain?.HeightAt(x, z) ?? 0f) + lift, z));
    }

    private void Clear()
    {
        if (Path is null && _lastQuery is null && !_following && !_groupDrawn)
        {
            return;
        }

        _groupDrawn = false;

        Path = null;
        _lastQuery = null;
        _following = false;
        _mesh.ClearSurfaces();
    }
}
