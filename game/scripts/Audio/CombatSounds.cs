using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Audio;

/// <summary>
/// The sound of the fight (docs/implementation/M6_menus_skirmish.md, 4.8): a shot at the gun of the shooter (<c>WeaponFired</c>), an
/// impact where a shot lands (<c>ProjectileImpact</c>: a blast when the warhead has a spread, a thud when not), and the death of a
/// unit (<c>EntityDestroyed</c>). Nothing plays where the local player does not see, the same rule as the effects
/// (<c>CombatEffects</c>): in the fog it is silent. Placed in the main scene, next to the host (like <c>CombatEffects</c>).
/// </summary>
public partial class CombatSounds : Node
{
    private const string ShotSound = "snd_shot";
    private const string ImpactSound = "snd_impact";
    private const string ExplosionSound = "snd_explosion";
    private const string DeathSound = "snd_unit_death";

    /// <summary>Height above the ground of a sound: the middle of a unit or a blast.</summary>
    private const float SoundHeight = 0.3f;

    private SimulationHost? _host;

    public override void _Ready()
    {
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
                Play(ShotSound, gun);
                break;

            case ProjectileImpact impact when Seen(world, impact.Point):
                var warhead = rules.Warheads[rules.IndexOfWarhead(rules.Weapons[impact.Weapon].Warhead)];
                Play(warhead.Spread > Fixed.Zero ? ExplosionSound : ImpactSound, impact.Point);
                break;

            case EntityDestroyed destroyed when PositionOf(world, destroyed.Entity) is { } dead && Seen(world, dead):
                Play(DeathSound, dead);
                break;
        }
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
