using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>
/// A shot of this tick (M4, 4.7): fired in the walk over the entities and launched after it, as the list of entities must not change
/// while it is walked. Gone by the end of <see cref="Systems.WeaponSystem"/>.
/// </summary>
internal readonly record struct Shot(PlayerId Owner, EntityId Shooter, int Weapon, EntityId? Target, WorldPos Origin, WorldPos AimAt);
