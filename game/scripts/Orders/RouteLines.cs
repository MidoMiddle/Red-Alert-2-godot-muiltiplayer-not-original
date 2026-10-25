using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Orders;

/// <summary>
/// Route lines of the selected units (docs/implementation/M2_unit_control.md, 4.14): from each unit through the legs of its
/// route that are left, green for a move and yellow for an attack-move; a patrol closes the line back to its first point. Dashed,
/// and only for <see cref="ShowSeconds"/> after an order changes the route of a selected unit, the last part fading (the owner,
/// 2026-10-25: «the line must be dashed, not stay visible all the time, gone after a second»). Waypoints laid out with Z and not
/// sent yet are a white dashed line while they are laid out. The lines follow the hills, cut into short pieces.
/// </summary>
public partial class RouteLines : Node3D
{
    private const float Lift = 0.08f;

    /// <summary>How long the route shows after an order, in seconds; it fades over the last <see cref="FadeSeconds"/>.</summary>
    public const float ShowSeconds = 1f;

    private const float FadeSeconds = 0.3f;

    /// <summary>Length of a piece of line, in leptons: half a cell keeps the line on the hills.</summary>
    private const int PieceLeptons = WorldPos.LeptonsPerCell / 2;

    private static readonly Color MoveColor = new(0.35f, 1f, 0.35f, 0.8f);
    private static readonly Color AttackColor = new(1f, 0.85f, 0.1f, 0.8f);
    private static readonly Color DraftColor = new(1f, 1f, 1f, 0.9f);

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private bool _drawn;

    // The route of every selected unit as last seen: a new one (a new order) shows the lines again.
    private readonly Dictionary<EntityId, ImmutableArray<UnitOrder>> _seen = [];
    private readonly HashSet<EntityId> _selected = [];
    private float _left;

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
        _left = Mathf.Max(0f, _left - (float)delta);
        Watch(world);
        var alpha = Mathf.Min(1f, _left / FadeSeconds);
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
            if (unit.OrderIndex >= orders.Length || _left <= 0f)
            {
                continue;
            }

            Begin(ref started);
            var from = unit.Position;
            for (var i = unit.OrderIndex; i < orders.Length; i++)
            {
                Line(from, orders[i].Point, Faded(orders[i].AttackMove ? AttackColor : MoveColor, alpha), dashed: true);
                from = orders[i].Point;
            }

            if (unit.LoopOrders)
            {
                // The patrol goes on from the last point through the first ones back to where the unit is heading now.
                for (var i = 0; i <= unit.OrderIndex; i++)
                {
                    Line(from, orders[i].Point, Faded(orders[i].AttackMove ? AttackColor : MoveColor, alpha), dashed: true);
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

    // A selected unit whose route is not the one seen last got a new order: the lines show again. A unit just selected is only noted.
    private void Watch(GameWorld world)
    {
        _selected.Clear();
        foreach (var id in _input.Selection.Units)
        {
            if (!world.TryGet(id, out var entity) || entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            _selected.Add(id);
            if (_seen.TryGetValue(id, out var before) && before != unit.Orders && unit.OrderIndex < unit.Orders.Length)
            {
                _left = ShowSeconds;
            }

            _seen[id] = unit.Orders;
        }

        foreach (var id in new List<EntityId>(_seen.Keys))
        {
            if (!_selected.Contains(id))
            {
                _seen.Remove(id);
            }
        }
    }

    private static Color Faded(Color color, float alpha) => new(color.R, color.G, color.B, color.A * alpha);

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
