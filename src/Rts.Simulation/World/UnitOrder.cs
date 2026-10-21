using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>One leg of a unit's route: where to go, and whether to attack on the way (the mark only, until M4).</summary>
public readonly record struct UnitOrder(WorldPos Point, bool AttackMove);
