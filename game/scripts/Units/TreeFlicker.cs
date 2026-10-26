using Rts.Simulation;

namespace Rts.Client.Units;

/// <summary>
/// The holo tank's own side sees its disguise now and then (RA2): standing still, it shows the tree for <see cref="ShownTicks"/> of
/// every <see cref="CycleTicks"/>, about one second in four, each tank in its own phase. The look only; the enemy sees the tree all
/// the time it stands (<see cref="Disguises.LooksLikeTree"/>).
/// </summary>
public static class TreeFlicker
{
    public const int CycleTicks = 4 * SimulationSettings.DefaultTicksPerSecond;
    public const int ShownTicks = SimulationSettings.DefaultTicksPerSecond;

    /// <summary>Whether the tank of <paramref name="unitId"/> shows its tree to its side on <paramref name="tick"/>.</summary>
    public static bool Shown(int tick, int unitId) => (tick + (unitId * 17)) % CycleTicks < ShownTicks;
}
