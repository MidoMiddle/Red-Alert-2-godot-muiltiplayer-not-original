using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>
/// A storm of the Storm Generator (stage 2d) over <paramref name="Centre"/>: its lightning, the weapon of <paramref name="Source"/>, strikes
/// from <paramref name="Start"/> to before <paramref name="End"/>, as <paramref name="Owner"/>'s.
/// </summary>
public sealed record Storm(PlayerId Owner, EntityId Source, int Weapon, CellPos Centre, int Start, int End);
