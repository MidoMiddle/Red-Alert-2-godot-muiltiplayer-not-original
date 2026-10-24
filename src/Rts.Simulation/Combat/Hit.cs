using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>
/// A shot that landed this tick, waiting in <see cref="GameWorld.Hits"/> for <see cref="Systems.DamageSystem"/> (docs/implementation/
/// M4_combat.md, 4.9). <paramref name="Source"/> is the projectile, or the shooter for an instant shot: with the target and the point
/// it orders the hits, whatever system found them first. <paramref name="BonusPercent"/> adds to the damage of the weapon (stage 2e).
/// </summary>
public readonly record struct Hit(EntityId Source, EntityId Shooter, PlayerId Owner, int Weapon, WorldPos Point, EntityId? Target, int BonusPercent = 0);
