using System.Collections.Generic;
using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Audio;

/// <summary>
/// The sound of the fight (docs/implementation/M6_menus_skirmish.md, 4.8): a shot at the gun of the shooter (<c>WeaponFired</c>, the
/// sound of its weapon in <c>bindings.yaml</c>), an impact where a shot lands (<c>ProjectileImpact</c>: a blast when the warhead has a
/// spread, a thud when not), the death of a unit (<c>EntityDestroyed</c>), the fall of a building by its material
/// (<c>BuildingDestroyed</c>), the fall of a bridge (<c>BridgeSegmentDestroyed</c>: a span, or the whole bridge when more spans fell
/// in the tick) and the support beam of a lens tower (<c>TowerSupported</c>). Nothing plays where the local player does not see, the
/// same rule as the effects (<c>CombatEffects</c>): in the fog it is silent. Placed in the main scene, next to the host (like
/// <c>CombatEffects</c>).
/// </summary>
public partial class CombatSounds : Node
{
    private const string ShotSound = "snd_shot";
    private const string ImpactSound = "snd_impact";
    private const string ExplosionSound = "snd_explosion";
    private const string DeathSound = "snd_unit_death";
    private const string SupportSound = "snd_prism_support";
    private const string BindingsPath = "res://data/audio/bindings.yaml";

    /// <summary>Height above the ground of a sound: the middle of a unit or a blast.</summary>
    private const float SoundHeight = 0.3f;

    private SimulationHost? _host;
    private SoundBindings _bindings = SoundBindings.Empty;

    /// <summary>The cells of each bridge that fell in a tick, played together on the next frame: every event of a tick comes first.</summary>
    private readonly Dictionary<(int Tick, int Bridge), List<CellPos>> _bridgeFalls = new();

    public override void _Ready()
    {
        var errors = new List<string>();
        _bindings = SoundBindings.Parse(Godot.FileAccess.GetFileAsString(BindingsPath), BindingsPath, errors);
        foreach (var error in errors)
        {
            GD.PushError($"[audio] {error}");
        }

        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
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

    private void OnEvent(SimEvent simEvent)
    {
        if (_host is null || AudioManager.Instance is null || WorldCoords.Terrain is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var rules = world.Rules.Database;
        switch (simEvent)
        {
            case WeaponFired fired when PositionOf(world, fired.Shooter) is { } gun && Seen(world, gun):
                Play(fired.Weapon >= 0 ? _bindings.ShotOf(rules.Weapons[fired.Weapon].Id) : ShotSound, gun);
                break;

            case ProjectileImpact impact when Seen(world, impact.Point):
                var warhead = rules.Warheads[rules.IndexOfWarhead(rules.Weapons[impact.Weapon].Warhead)];
                Play(warhead.Spread > Fixed.Zero ? ExplosionSound : ImpactSound, impact.Point);
                break;

            // A unit only: a building has the sound of its fall.
            case EntityDestroyed destroyed when _host.TryGetPreviousPose(destroyed.Entity, out var pose) && Seen(world, pose.Position):
                Play(DeathSound, pose.Position);
                break;

            case BuildingDestroyed fallen when Seen(world, fallen.Centre) && _bindings.FallOf(fallen.Type) is { Length: > 0 } fall:
                Play(fall, fallen.Centre);
                break;

            case BridgeSegmentDestroyed segment:
                if (!_bridgeFalls.TryGetValue((segment.Tick, segment.BridgeId), out var cells))
                {
                    _bridgeFalls[(segment.Tick, segment.BridgeId)] = cells = [];
                }

                cells.Add(segment.Cell);
                break;

            case TowerSupported supported when PositionOf(world, supported.Supporter) is { } supporter && Seen(world, supporter):
                Play(SupportSound, supporter);
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (_bridgeFalls.Count == 0)
        {
            return;
        }

        if (_host is not null && AudioManager.Instance is not null && WorldCoords.Terrain is not null)
        {
            var world = _host.Simulation.World;
            foreach (var ((_, id), cells) in _bridgeFalls)
            {
                PlayBridgeFall(world, id, cells);
            }
        }

        _bridgeFalls.Clear();
    }

    /// <summary>The fall of the spans <paramref name="cells"/> of a bridge, from the middle of them, when the local player sees one.</summary>
    private void PlayBridgeFall(GameWorld world, int id, List<CellPos> cells)
    {
        Bridge? bridge = null;
        foreach (var candidate in world.Map.Bridges)
        {
            if (candidate.Id == id)
            {
                bridge = candidate;
            }
        }

        if (bridge is null || !_bindings.Bridges.TryGetValue(bridge.Type.Id, out var sounds) || !cells.Exists(cell => Seen(world, cell.Center)))
        {
            return;
        }

        long x = 0, y = 0;
        foreach (var cell in cells)
        {
            x += cell.Center.X;
            y += cell.Center.Y;
        }

        Play(cells.Count > 1 ? sounds.Whole : sounds.Span, new WorldPos((int)(x / cells.Count), (int)(y / cells.Count)));
    }

    private void Play(string sound, WorldPos point) =>
        AudioManager.Instance?.PlayAt(sound, WorldCoords.ToGodotOnGround(point, SoundHeight));

    /// <summary>
    /// Where an entity was when the tick began, for a unit; for a building, its centre. A unit that died in the tick is gone from the
    /// world, but its position before the tick is kept by the host.
    /// </summary>
    private WorldPos? PositionOf(GameWorld world, EntityId id)
    {
        if (_host!.TryGetPreviousPose(id, out var pose))
        {
            return pose.Position;
        }

        return world.TryGet(id, out var entity) && entity is Building building ? Aim.CentreOf(building) : null;
    }

    /// <summary>Whether the local player's team sees the point now (M6, A6); with no fog, or the fog revealed, everything is seen.</summary>
    private static bool Seen(GameWorld world, WorldPos point) =>
        FogOfWar.Current is not { Revealed: false } || Vision.IsVisible(world, SimulationHost.LocalPlayer, point.ToCell());
}
