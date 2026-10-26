using System.Collections.Immutable;
using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Client.Orders;

/// <summary>
/// The rings of a defense's range (RA2: a selected defense, or one being placed, shows how far it fires): the longest range of its
/// weapons, and the least one when every weapon has one (the Grand Cannon), in leptons from the centre of its footprint — the numbers
/// <see cref="TargetingSystem.CanFireAt"/> and <c>WeaponSystem</c> use. The soldiers in a garrison fire from its wall. Pure, tested in
/// Rts.Simulation.Tests.
/// </summary>
public static class WeaponReach
{
    /// <summary>How far the weapons reach from the centre: <paramref name="Max"/>, and no nearer than <paramref name="Min"/> (0: no least range).</summary>
    public readonly record struct Reach(int Max, int Min);

    /// <summary>The reach of <paramref name="weapons"/> fired from <paramref name="wall"/> leptons off the centre; null when none fires.</summary>
    public static Reach? Of(GameRules rules, ImmutableArray<int> weapons, int wall = 0)
    {
        var max = -1;
        int? min = null;
        foreach (var index in weapons)
        {
            if (index < 0)
            {
                continue;
            }

            var weapon = rules.Database.Weapons[index];
            max = IntMath.Max(max, Aim.Leptons(weapon.Range));
            // A weapon without a least range covers the near ground: then there is no least range to show.
            min = min is { } least ? IntMath.Min(least, Aim.Leptons(weapon.MinRange)) : Aim.Leptons(weapon.MinRange);
        }

        return max < 0 ? null : new Reach(max + wall, min is > 0 ? min.Value + wall : 0);
    }

    /// <summary>The reach of a ready building of a player that fires — a defense with the weapon it fires now, or a garrison — or null.</summary>
    public static Reach? Of(GameWorld world, Building building)
    {
        if (!building.IsAlive || !building.IsReady)
        {
            return null;
        }

        if (building.Weapons.Length > 0)
        {
            return Of(world.Rules, Defenses.WeaponsOf(world, building));
        }

        return GarrisonSystem.IsArmed(building) ? Of(world.Rules, GarrisonSystem.WeaponsOf(world.Rules, building), GarrisonSystem.Wall(building)) : null;
    }

    /// <summary>The reach of the building of the data at <paramref name="defIndex"/> (its placement), or null when it has no weapon.</summary>
    public static Reach? OfDefinition(GameRules rules, int defIndex) => Of(rules, rules.WeaponsOfBuilding(defIndex));

    /// <summary>The centre of a footprint of <paramref name="width"/> × <paramref name="height"/> cells at <paramref name="corner"/>, as <see cref="Aim.CentreOf"/>.</summary>
    public static WorldPos Centre(CellPos corner, int width, int height) =>
        new((corner.X * WorldPos.LeptonsPerCell) + (width * WorldPos.LeptonsPerCell / 2), (corner.Y * WorldPos.LeptonsPerCell) + (height * WorldPos.LeptonsPerCell / 2));
}
