using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;

namespace Rts.Client.Orders;

/// <summary>What an order marker shows.</summary>
public enum MarkerKind
{
    Move,
    AttackMove,
    Forbidden,

    /// <summary>On the target of an attack or a forced attack (M4 step 7): two red rings.</summary>
    Attack,
}

/// <summary>
/// The marker an order leaves on the ground (docs/implementation/M2_unit_control.md, 4.14): a ring that shrinks and fades
/// in <see cref="Lifetime"/> seconds, green for a move or a waypoint, yellow and red for an attack-move, and a red cross
/// where nobody of the selection can go. Symbolic (docs/implementation/conventions.md, section 12).
/// </summary>
public partial class OrderMarkers : Node3D
{
    private const float Lifetime = 0.6f;
    private const float Radius = 0.45f;
    private const float Lift = 0.06f;
    private const int Segments = 24;

    private static readonly Color MoveColor = new(0.35f, 1f, 0.35f);
    private static readonly Color AttackOuter = new(1f, 0.25f, 0.15f);
    private static readonly Color AttackInner = new(1f, 0.85f, 0.1f);
    private static readonly Color ForbiddenColor = new(1f, 0.2f, 0.15f);

    private readonly ImmediateMesh _mesh = new();
    private readonly List<(Vector3 At, MarkerKind Kind, float Age)> _markers = new();

    public void Add(WorldPos point, MarkerKind kind) => _markers.Add((WorldCoords.ToGodotOnGround(point, Lift), kind, 0f));

    public override void _Ready()
    {
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
        if (_markers.Count == 0)
        {
            return;
        }

        for (var i = _markers.Count - 1; i >= 0; i--)
        {
            var marker = _markers[i];
            marker.Age += (float)delta;
            if (marker.Age >= Lifetime)
            {
                _markers.RemoveAt(i);
            }
            else
            {
                _markers[i] = marker;
            }
        }

        _mesh.ClearSurfaces();
        if (_markers.Count == 0)
        {
            return;
        }

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var (at, kind, age) in _markers)
        {
            var t = age / Lifetime;
            var fade = 1f - t;
            var radius = Radius * (1f - 0.5f * t);
            switch (kind)
            {
                case MarkerKind.Move:
                    Ring(at, radius, WithAlpha(MoveColor, fade));
                    break;
                case MarkerKind.AttackMove:
                    Ring(at, radius, WithAlpha(AttackOuter, fade));
                    Ring(at, radius * 0.6f, WithAlpha(AttackInner, fade));
                    break;
                case MarkerKind.Attack:
                    Ring(at, radius, WithAlpha(AttackInner, fade));
                    Ring(at, radius * 0.6f, WithAlpha(AttackInner, fade));
                    break;
                default:
                    Cross(at, Radius * 0.6f, WithAlpha(ForbiddenColor, fade));
                    break;
            }
        }

        _mesh.SurfaceEnd();
    }

    private void Ring(Vector3 at, float radius, Color color)
    {
        for (var i = 0; i < Segments; i++)
        {
            Segment(at + OnCircle(i, radius), at + OnCircle(i + 1, radius), color);
        }
    }

    private void Cross(Vector3 at, float half, Color color)
    {
        Segment(at + new Vector3(-half, 0, -half), at + new Vector3(half, 0, half), color);
        Segment(at + new Vector3(-half, 0, half), at + new Vector3(half, 0, -half), color);
    }

    private void Segment(Vector3 from, Vector3 to, Color color)
    {
        _mesh.SurfaceSetColor(color);
        _mesh.SurfaceAddVertex(from);
        _mesh.SurfaceSetColor(color);
        _mesh.SurfaceAddVertex(to);
    }

    private static Vector3 OnCircle(int index, float radius)
    {
        var angle = index * Mathf.Tau / Segments;
        return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
    }

    private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
