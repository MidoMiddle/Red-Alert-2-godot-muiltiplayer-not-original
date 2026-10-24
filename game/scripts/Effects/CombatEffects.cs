using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Effects;

/// <summary>
/// The stand-in effects of the fight (docs/implementation/M4_combat.md, 4.19, step 13), symbolic (docs/implementation/conventions.md,
/// section 12): a flash at the gun of every shot; a yellow tracer for an instant shot, a blue zigzag for the tesla bolt; a ball of fire
/// where a shot lands, as wide as the area of its warhead, a puff of dust for a bullet; at a death, a blast by the warhead's
/// <c>infantry_death</c> — explode orange, electro blue, normal grey — larger for a vehicle and a building, a dark stain for the
/// crushed. Nothing shows where the local player does not see (the fog, step 12). The dead are gone from the world when their event
/// comes, so where and what they were is kept from the last frame.
/// </summary>
public partial class CombatEffects : Node3D
{
    private const float FlashLife = 0.08f;
    private const float TracerLife = 0.1f;
    private const float BoltLife = 0.2f;
    private const float BlastLife = 0.4f;
    private const float StainLife = 2f;

    private static readonly Color FlashColor = new(1f, 0.95f, 0.6f);
    private static readonly Color TracerColor = new(1f, 0.9f, 0.3f);
    private static readonly Color BoltColor = new(0.55f, 0.8f, 1f);

    // The beams of stage 3a: the Toxin Trooper's green, the Time Trooper's pale blue.
    private static readonly Color ToxinColor = new(0.45f, 1f, 0.3f);
    private static readonly Color ChronoColor = new(0.7f, 0.9f, 1f);

    // The beam tank's beam and its splinters (stage 3c): near white.
    private static readonly Color PrismColor = new(0.9f, 0.95f, 1f);

    // The dolphin's wave (stage 3e): sea blue.
    private static readonly Color SonicColor = new(0.3f, 0.7f, 1f);
    private static readonly Color FireColor = new(1f, 0.5f, 0.1f);
    private static readonly Color ElectroColor = new(0.45f, 0.75f, 1f);
    private static readonly Color DustColor = new(0.7f, 0.66f, 0.58f);
    private static readonly Color StainColor = new(0.1f, 0.08f, 0.06f);

    private readonly ImmediateMesh _lines = new();
    private readonly List<Line> _lineEffects = [];
    private readonly List<Ball> _balls = [];
    private readonly Dictionary<EntityId, Known> _known = new();
    private readonly Random _jitter = new(7);
    private SimulationHost? _host;

    /// <summary>What an entity was in the last frame: where it stood and what it was.</summary>
    private readonly record struct Known(WorldPos Centre, Vector3 Gun, bool Infantry, bool Building, float Size);

    private sealed record Line(Vector3[] Points, Color Color, float Life)
    {
        public float Age { get; set; }
    }

    private sealed record Ball(MeshInstance3D Mesh, StandardMaterial3D Material, float From, float To, float Life, bool Flat)
    {
        public float Age { get; set; }
    }

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        if (_host is not null)
        {
            _host.EventRaised += OnEvent;
        }

        AddChild(new MeshInstance3D
        {
            Mesh = _lines,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        });
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
        if (_host is null)
        {
            return;
        }

        Remember(_host.Simulation.World);
        var step = (float)delta;
        for (var i = _balls.Count - 1; i >= 0; i--)
        {
            var ball = _balls[i];
            ball.Age += step;
            var t = Math.Min(1f, ball.Age / ball.Life);
            var size = Mathf.Lerp(ball.From, ball.To, t);
            ball.Mesh.Scale = ball.Flat ? new Vector3(size, 1f, size) : new Vector3(size, size, size);
            var color = ball.Material.AlbedoColor;
            ball.Material.AlbedoColor = new Color(color.R, color.G, color.B, 1f - t);
            if (t >= 1f)
            {
                ball.Mesh.QueueFree();
                _balls.RemoveAt(i);
            }
        }

        _lines.ClearSurfaces();
        if (_lineEffects.Count == 0)
        {
            return;
        }

        _lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (var i = _lineEffects.Count - 1; i >= 0; i--)
        {
            var line = _lineEffects[i];
            line.Age += step;
            var fade = 1f - Math.Min(1f, line.Age / line.Life);
            for (var p = 0; p + 1 < line.Points.Length; p++)
            {
                _lines.SurfaceSetColor(new Color(line.Color, fade));
                _lines.SurfaceAddVertex(line.Points[p]);
                _lines.SurfaceSetColor(new Color(line.Color, fade));
                _lines.SurfaceAddVertex(line.Points[p + 1]);
            }

            if (line.Age >= line.Life)
            {
                _lineEffects.RemoveAt(i);
            }
        }

        _lines.SurfaceEnd();
    }

    /// <summary>Where every unit and building stands now, for the events of the next tick, when the dead are already gone.</summary>
    private void Remember(GameWorld world)
    {
        _known.Clear();
        foreach (var entity in world.Entities)
        {
            switch (entity)
            {
                case Unit { IsAlive: true } unit:
                    var infantry = unit.Archetype.Category == UnitCategory.Infantry;
                    _known[unit.Id] = new Known(unit.Position, WorldCoords.ToGodotOnGround(unit.Position, infantry ? 0.45f : 0.6f), infantry, false, infantry ? 0.4f : 0.9f);
                    break;
                case Building { IsAlive: true } building:
                    var centre = Aim.CentreOf(building);
                    _known[building.Id] = new Known(centre, WorldCoords.ToGodotOnGround(centre, 0.9f), false, true, Math.Max(building.Width, building.Height) * 0.7f);
                    break;
            }
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (_host is null || WorldCoords.Terrain is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var rules = world.Rules.Database;
        switch (simEvent)
        {
            case WeaponFired fired when Know(fired.Shooter) is { } shooter && Seen(world, shooter.Centre):
                AddBall(shooter.Gun, 0.15f, 0.3f, FlashLife, FlashColor);
                break;

            case UnitTeleported jump when Seen(world, jump.To) || Seen(world, jump.From):
                // A time trooper's jump (stage 3b): a pale blue flash where it left and where it came.
                AddBall(WorldCoords.ToGodotOnGround(jump.From, 0.5f), 0.3f, 0.9f, BlastLife, ChronoColor);
                AddBall(WorldCoords.ToGodotOnGround(jump.To, 0.5f), 0.9f, 0.3f, BlastLife, ChronoColor);
                break;

            case TowerSupported support when Know(support.Supporter) is { } supporter && Know(support.Tower) is { } tower && Seen(world, tower.Centre):
                // A lens tower sends its charge to the one that fires (stage 2e).
                _lineEffects.Add(new Line([supporter.Gun, tower.Gun], BoltColor, BoltLife));
                break;

            case ProjectileImpact impact when Seen(world, impact.Point):
                var weapon = rules.Weapons[impact.Weapon];
                rules.TryGetProjectile(weapon.Projectile, out var projectile);
                var warhead = rules.Warheads[rules.IndexOfWarhead(weapon.Warhead)];
                var point = WorldCoords.ToGodotOnGround(impact.Point, 0.3f);
                // A dog's bite (stage 3a) leaves no trace in the air.
                if (impact.Projectile is null && projectile.Visual != "none" && Know(impact.Shooter) is { } from)
                {
                    _lineEffects.Add(projectile.Visual switch
                    {
                        "electric_bolt" => new Line(Zigzag(from.Gun, point), BoltColor, BoltLife),
                        "toxin_beam" => new Line([from.Gun, point], ToxinColor, BoltLife),
                        "chrono_beam" => new Line([from.Gun, point], ChronoColor, BoltLife),
                        "prism_ray" => new Line([from.Gun, point], PrismColor, BoltLife),
                        "sonic" => new Line([from.Gun, point], SonicColor, BoltLife),
                        _ => new Line([from.Gun, point], TracerColor, TracerLife),
                    });
                }

                var spread = (float)warhead.Spread.Raw / Fixed.OneRaw;
                if (spread > 0f)
                {
                    AddBall(point, 0.2f, Math.Max(0.4f, spread * 1.6f), BlastLife, FireColor);
                }
                else
                {
                    AddBall(point, 0.08f, 0.25f, BlastLife * 0.6f, projectile.Visual == "electric_bolt" ? ElectroColor : DustColor);
                }

                break;

            case EntityDestroyed destroyed when Know(destroyed.Entity) is { Building: false } dead && Seen(world, dead.Centre):
                Death(rules, destroyed, dead);
                break;

            // Every fallen building, to a shot or to the debug blow: a large blast before its rubble (step 9).
            case BuildingDestroyed fallen when Know(fallen.Building) is { } building && Seen(world, building.Centre):
                AddBall(WorldCoords.ToGodotOnGround(building.Centre, 0.6f), building.Size * 0.4f, building.Size * 1.4f, BlastLife * 1.6f, FireColor);
                break;
        }
    }

    private void Death(RulesDatabase rules, EntityDestroyed destroyed, Known dead)
    {
        var ground = WorldCoords.ToGodotOnGround(dead.Centre, 0.05f);
        if (destroyed.Cause == DeathCause.Crushed)
        {
            AddBall(ground, 0.5f, 0.6f, StainLife, StainColor, flat: true);
            return;
        }

        var death = destroyed.Weapon >= 0 ? rules.Warheads[rules.IndexOfWarhead(rules.Weapons[destroyed.Weapon].Warhead)].InfantryDeath : "";
        var color = !dead.Infantry ? FireColor : death switch
        {
            "explode" => FireColor,
            "electro" => ElectroColor,
            _ => DustColor,
        };
        var size = dead.Infantry ? 0.6f : dead.Size * 1.4f;
        AddBall(WorldCoords.ToGodotOnGround(dead.Centre, dead.Building ? 0.6f : 0.4f), size * 0.3f, size, BlastLife * (dead.Infantry ? 1f : 1.6f), color);
    }

    private Known? Know(EntityId id) => _known.TryGetValue(id, out var known) ? known : null;

    /// <summary>Whether the local player's team sees the point now (M6, A6); with no fog, or the fog revealed, everything is seen.</summary>
    private static bool Seen(GameWorld world, WorldPos point) =>
        FogOfWar.Current is not { Revealed: false } || Vision.IsVisible(world, SimulationHost.LocalPlayer, point.ToCell());

    private void AddBall(Vector3 at, float from, float to, float life, Color color, bool flat = false)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
        };
        var mesh = new MeshInstance3D
        {
            Mesh = flat ? new CylinderMesh { TopRadius = 1f, BottomRadius = 1f, Height = 0.02f } : new SphereMesh { Radius = 1f, Height = 2f },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = at,
            Scale = new Vector3(from, flat ? 1f : from, from),
        };
        AddChild(mesh);
        _balls.Add(new Ball(mesh, material, from, to, life, flat));
    }

    /// <summary>A lightning from <paramref name="from"/> to <paramref name="to"/>: eight pieces, the middle ones pushed aside at random.</summary>
    private Vector3[] Zigzag(Vector3 from, Vector3 to)
    {
        const int Pieces = 8;
        var points = new Vector3[Pieces + 1];
        for (var i = 0; i <= Pieces; i++)
        {
            var t = (float)i / Pieces;
            var shift = i is 0 or Pieces ? Vector3.Zero
                : new Vector3((float)_jitter.NextDouble() - 0.5f, (float)_jitter.NextDouble() - 0.5f, (float)_jitter.NextDouble() - 0.5f) * 0.35f;
            points[i] = from.Lerp(to, t) + shift;
        }

        return points;
    }
}
