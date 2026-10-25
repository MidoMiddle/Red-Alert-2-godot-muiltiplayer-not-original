using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>
/// When a building works (docs/implementation/M4_combat.md, 4.17): a <c>powered</c> building that drains power itself is off while its
/// owner is low on power, as RA2 <c>Powered</c> — the tesla coil and the flak cannon go silent, the sentry bunker of power 0 fires on.
/// The simulation and the client ask the same rule.
/// </summary>
public static class Defenses
{
    /// <summary>
    /// Whether the building works: always, unless it is powered and its owner is low on power — then a tesla coil works while two
    /// tesla troopers charge it (RA2).
    /// </summary>
    public static bool IsOnline(GameWorld world, Building building) =>
        building.Def is not { Powered: true, Power: < 0 } || building.Owner.IsNeutral || !world.Player(building.Owner).LowPower
        || building.Chargers >= 2;

    /// <summary>
    /// Whether a tesla coil fires its charged shot (RA2): with power, one tesla trooper charging it is enough; low on power, three.
    /// </summary>
    public static bool Overcharged(GameWorld world, Building building) =>
        building.Def is { OverchargeWeapon.Length: > 0 } && building.Chargers > 0 && !building.Owner.IsNeutral
        && building.Chargers >= (world.Player(building.Owner).LowPower && building.Def.Powered ? 3 : 1);

    /// <summary>The weapon a building fires now: its charged one while charged, else its own.</summary>
    public static System.Collections.Immutable.ImmutableArray<int> WeaponsOf(GameWorld world, Building building) =>
        Overcharged(world, building) ? world.Rules.OverchargeOf(building) : world.Rules.WeaponsOf(building);
}
