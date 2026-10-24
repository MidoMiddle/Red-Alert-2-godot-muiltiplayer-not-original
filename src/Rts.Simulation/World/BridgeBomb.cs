namespace Rts.Simulation.World;

/// <summary>
/// The C4 of <paramref name="Shooter"/> (<paramref name="Owner"/>'s) on segment <paramref name="Segment"/> of bridge
/// <paramref name="Bridge"/>: the segment falls on tick <paramref name="GoesOff"/> (<see cref="Systems.BridgeDemolition"/>).
/// </summary>
public sealed record BridgeBomb(PlayerId Owner, EntityId Shooter, int Bridge, int Segment, int GoesOff);
