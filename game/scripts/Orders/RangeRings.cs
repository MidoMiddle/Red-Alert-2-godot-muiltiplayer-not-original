using System;
using Godot;
using Rts.Client.Controls;
using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Orders;

/// <summary>
/// The range of a defense on the ground (RA2; the owner, 2026-10-26: «when I click a Prism Tower or a Pillbox I should see its range»):
/// a ring at the longest range of the selected defense, a dashed one at its least range when it has one (the Grand Cannon), and the
/// same rings around a defense being placed. The rings follow the hills; drawn again only when what they show changes.
/// </summary>
public partial class RangeRings : Node3D
{
    private const float Lift = 0.1f;

    /// <summary>Half the width of a ring, in leptons.</summary>
    private const int HalfWidth = 20;

    /// <summary>Length of a piece of a ring, in leptons: half a cell keeps it on the hills.</summary>
    private const int PieceLeptons = WorldPos.LeptonsPerCell / 2;

    private static readonly Color RangeColor = new(1f, 0.15f, 0.1f, 0.95f);
    private static readonly Color NearColor = new(1f, 0.8f, 0.05f, 0.95f);
    private static readonly Color PlacingColor = new(0.95f, 0.95f, 0.95f, 0.65f);

    private readonly ImmediateMesh _mesh = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private object? _drawn;

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
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        });
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("RangeRings.process");
        var (centre, reach, placing) = Shown();
        var state = (centre, reach, placing);
        if (Equals(state, _drawn))
        {
            return;
        }

        _drawn = state;
        _mesh.ClearSurfaces();
        if (reach is not { } shown || WorldCoords.Terrain is null)
        {
            return;
        }

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Ring(centre, shown.Max, placing ? PlacingColor : RangeColor, dashed: false);
        if (shown.Min > 0)
        {
            Ring(centre, shown.Min, placing ? PlacingColor : NearColor, dashed: true);
        }

        _mesh.SurfaceEnd();
    }

    /// <summary>What to show: the defense being placed, else the selected one.</summary>
    private (WorldPos Centre, WeaponReach.Reach? Reach, bool Placing) Shown()
    {
        var world = _host.Simulation.World;
        if (_input.BuildArmed && _host.BuildIndex >= 0 && _input.PointerGround is { } pointer
            && WeaponReach.OfDefinition(world.Rules, _host.BuildIndex) is { } placed)
        {
            var footprint = _host.Data.Database.Buildings[_host.BuildIndex].Footprint;
            var corner = PlacementPreview.CornerOf(footprint, pointer);
            return (WeaponReach.Centre(corner, footprint.Width, footprint.Height), placed, true);
        }

        if (_input.SelectedDefense is { } id && world.TryGet(id, out var entity) && entity is Building building
            && WeaponReach.Of(world, building) is { } reach)
        {
            return (Aim.CentreOf(building), reach, false);
        }

        return (default, null, false);
    }

    /// <summary>A band on the ground at <paramref name="radius"/> leptons around <paramref name="centre"/>; a dashed one skips every other piece.</summary>
    private void Ring(WorldPos centre, int radius, Color color, bool dashed)
    {
        var pieces = Mathf.Max(24, (int)(Mathf.Tau * radius / PieceLeptons));
        if (dashed && pieces % 2 == 1)
        {
            pieces++;
        }

        for (var i = 0; i < pieces; i++)
        {
            if (dashed && i % 2 == 1)
            {
                continue;
            }

            var from = Mathf.Tau * i / pieces;
            var to = Mathf.Tau * (i + 1) / pieces;
            var innerFrom = Point(centre, radius - HalfWidth, from);
            var outerFrom = Point(centre, radius + HalfWidth, from);
            var innerTo = Point(centre, radius - HalfWidth, to);
            var outerTo = Point(centre, radius + HalfWidth, to);
            foreach (var point in new[] { innerFrom, outerFrom, outerTo, innerFrom, outerTo, innerTo })
            {
                _mesh.SurfaceSetColor(color);
                _mesh.SurfaceAddVertex(point);
            }
        }
    }

    private static Vector3 Point(WorldPos centre, int radius, float angle) =>
        WorldCoords.ToGodotOnGround(
            new WorldPos(centre.X + Mathf.RoundToInt(MathF.Cos(angle) * radius), centre.Y + Mathf.RoundToInt(MathF.Sin(angle) * radius)), Lift);
}
