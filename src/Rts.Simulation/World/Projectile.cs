using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Simulation.World;

/// <summary>
/// A shot in flight (docs/implementation/M4_combat.md, 4.8): a ballistic one on its way to the point it was aimed at, or a homing one
/// after its target. An instant shot never becomes one. It lives until it lands, then goes at the end of the tick. State: hashed.
/// </summary>
public sealed class Projectile : Entity
{
    internal Projectile(EntityId id, PlayerId owner, EntityId shooter, int weapon, EntityId? target, WorldPos origin, WorldPos aimPoint, int launchTick)
        : base(id, owner)
    {
        Shooter = shooter;
        Weapon = weapon;
        Target = target;
        Origin = origin;
        Position = origin;
        AimPoint = aimPoint;
        LastTargetPoint = aimPoint;
        LaunchTick = launchTick;
    }

    /// <summary>Who fired it.</summary>
    public EntityId Shooter { get; }

    /// <summary>The weapon that fired it: its place in <see cref="Rules.RulesDatabase.Weapons"/>.</summary>
    public int Weapon { get; }

    /// <summary>What it was fired at; it still lands when that dies.</summary>
    public EntityId? Target { get; }

    public WorldPos Origin { get; }

    public WorldPos Position { get; internal set; }

    /// <summary>Where a ballistic shot lands: the target as it stood at the shot, scattered.</summary>
    public WorldPos AimPoint { get; }

    /// <summary>Where a homing shot last saw its living target; it flies there once the target is gone.</summary>
    public WorldPos LastTargetPoint { get; internal set; }

    /// <summary>The course of a homing shot.</summary>
    public BAngle Facing { get; internal set; }

    /// <summary>Ticks a ballistic shot flies in all.</summary>
    public int FlightTicks { get; internal set; }

    /// <summary>Ticks left: of the flight of a ballistic shot, of the life of a homing one.</summary>
    public int TicksLeft { get; internal set; }

    /// <summary>The tick it was fired on; it starts to fly on the next one.</summary>
    public int LaunchTick { get; }
}
