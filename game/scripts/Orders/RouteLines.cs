using System;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Orders;

/// <summary>
/// Route lines of the selected units (docs/implementation/M2_unit_control.md, 4.14): from each unit through the legs of its
/// route that are left, green for a move and yellow for an attack-move; a patrol closes the line back to its first point.
/// Waypoints laid out with Z and not sent yet are a dashed line. The lines follow the hills, cut into short pieces.
/// </summary>
public partial class RouteLines : Node3D
{
    private const float Lift = 0.08f;

    /// <summary>Length of a piece of line, in leptons: half a cell keeps the line on the hills.</summary>
    private const int PieceLeptons = WorldPos.LeptonsPerCell / 2;

    private static readonly Color MoveColor = new(0.35f, 1f, 0.35f, 0.8f);
    private static readonly Color AttackColor = new(1f, 0.85f, 0.1f, 0.8f);
    private static readonly Color DraftColor = new(1f, 1f, 1f, 0.9f);

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private bool _drawn;

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
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            },
        });
    }

    public override void _Process(double delta)
    {
        if (_drawn)
        {
            _mesh.ClearSurfaces();
            _drawn = false;
        }

        var world = _host.Simulation.World;
        var started = false;
        WorldPos? firstUnit = null;
        foreach (var id in _input.Selection.Units)
        {
            if (!world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            firstUnit ??= unit.Position;
            var orders = unit.Orders;
            if (unit.OrderIndex >= orders.Length)
            {
                continue;
            }

            Begin(ref started);
            var from = unit.Position;
            for (var i = unit.OrderIndex; i < orders.Length; i++)
            {
                Line(from, orders[i].Point, orders[i].AttackMove ? AttackColor : MoveColor, dashed: false);
                from = orders[i].Point;
            }

            if (unit.LoopOrders)
            {
                // The patrol goes on from the last point through the first ones back to where the unit is heading now.
                for (var i = 0; i <= unit.OrderIndex; i++)
                {
                    Line(from, orders[i].Point, orders[i].AttackMove ? AttackColor : MoveColor, dashed: false);
                    from = orders[i].Point;
                }
            }
        }

        var draft = _input.Waypoints.Points;
        if (draft.Count > 0 && firstUnit is { } start)
        {
            Begin(ref started);
            var from = start;
            foreach (var point in draft)
            {
                Line(from, point, DraftColor, dashed: true);
                from = point;
            }
        }

        if (started)
        {
            _mesh.SurfaceEnd();
            _drawn = true;
        }
    }

    private void Begin(ref bool started)
    {
        if (!started)
        {
            _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            started = true;
        }
    }

    /// <summary>A line on the ground from <paramref name="from"/> to <paramref name="to"/>; a dashed one skips every other piece.</summary>
    private void Line(WorldPos from, WorldPos to, Color color, bool dashed)
    {
        var length = MathF.Sqrt((to - from).LengthSquared);
        var pieces = Mathf.Max(1, (int)(length / PieceLeptons));
        var previous = WorldCoords.ToGodotOnGround(from, Lift);
        for (var i = 1; i <= pieces; i++)
        {
            var point = Lerp(from, to, (float)i / pieces);
            var next = WorldCoords.ToGodotOnGround(point, Lift);
            if (!dashed || i % 2 == 1)
            {
                _mesh.SurfaceSetColor(color);
                _mesh.SurfaceAddVertex(previous);
                _mesh.SurfaceSetColor(color);
                _mesh.SurfaceAddVertex(next);
            }

            previous = next;
        }
    }

    private static WorldPos Lerp(WorldPos from, WorldPos to, float t) =>
        new(Mathf.RoundToInt(from.X + (to.X - from.X) * t), Mathf.RoundToInt(from.Y + (to.Y - from.Y) * t));
}
