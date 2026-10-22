using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>
/// When a building works (docs/implementation/M4_combat.md, 4.17): a <c>powered</c> building that drains power itself is off while its
/// owner is low on power, as RA2 <c>Powered</c> — the tesla coil and the flak cannon go silent, the sentry bunker of power 0 fires on.
/// The simulation and the client ask the same rule.
/// </summary>
public static class Defenses
{
    public static bool IsOnline(GameWorld world, Building building) =>
        building.Def is not { Powered: true, Power: < 0 } || building.Owner.IsNeutral || !world.Player(building.Owner).LowPower;
}
