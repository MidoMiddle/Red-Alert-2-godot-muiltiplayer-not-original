using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>
/// The load of a paradrop plane (<see cref="Systems.ParadropSystem"/>): <paramref name="Count"/> soldiers of <paramref name="Type"/> to
/// drop over <paramref name="Point"/>, none once dropped; then it flies off the map at <paramref name="Exit"/>.
/// </summary>
public sealed record Paradrop(UnitType Type, int Count, WorldPos Point, WorldPos Exit);
