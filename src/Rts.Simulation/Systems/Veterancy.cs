using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>The rank of a unit (loose ends, stage 3).</summary>
public enum Rank : byte
{
    Rookie = 0,
    Veteran = 1,
    Elite = 2,
}

/// <summary>
/// Veterancy (loose ends, stage 3; numbers of RA2 rules.ini). A unit that kills an enemy gains its cost as experience — its own units
/// and allies' give none, nor does one that is not <see cref="Rules.UnitDef.Trainable"/> (dogs, engineers, spies, robots, the drone,
/// the Hornet, the paradrop plane). It is a veteran at <see cref="VeteranRatio"/> times its own cost, elite at twice that. A veteran
/// takes two thirds of the damage (<c>VeteranArmor=1.5</c>), deals a tenth more (<c>VeteranCombat=1.1</c>) and moves a fifth faster
/// (<c>VeteranSpeed=1.2</c>); a veteran soldier reloads in <see cref="ReloadPercent"/>% of the time (<c>VeteranROF</c>, infantry only).
/// An elite one has the same and heals <see cref="EliteHeal"/> hit point a second (RA2 elite <c>SELF_HEAL</c>). A spy in the enemy's
/// barracks or war factory makes the spy's player's infantry or vehicles come out veteran. Elite weapons are not done.
/// </summary>
public static class Veterancy
{
    /// <summary>RA2 <c>VeteranRatio</c>.</summary>
    public const int VeteranRatio = 3;

    /// <summary>RA2 <c>VeteranROF</c>, in percent.</summary>
    public const int ReloadPercent = 60;

    /// <summary>The hit points an elite unit heals a second (RA2 self-healing: 1 every <c>RepairRate</c>).</summary>
    public const int EliteHeal = 1;

    /// <summary>The rank of the unit by its experience.</summary>
    public static Rank RankOf(Unit unit)
    {
        var cost = unit.Archetype.Cost;
        return cost <= 0 || unit.Experience < cost * VeteranRatio ? Rank.Rookie
            : unit.Experience < cost * VeteranRatio * 2 ? Rank.Veteran
            : Rank.Elite;
    }

    /// <summary>Whether the unit is a veteran or elite.</summary>
    public static bool IsVeteran(Unit unit) => unit.Experience > 0 && RankOf(unit) != Rank.Rookie;

    /// <summary>The unit of <paramref name="killer"/> gains the worth of <paramref name="victim"/>, an enemy, killed.</summary>
    internal static void Credit(GameWorld world, EntityId killer, Entity victim, List<SimEvent>? events)
    {
        if (!world.TryGet(killer, out var entity) || entity is not Unit { IsAlive: true } unit || unit.Archetype.Has(UnitTalents.Untrainable)
            || unit.Archetype.Cost <= 0 || unit.Owner == victim.Owner || victim.Owner.IsNeutral || world.AreAllies(unit.Owner, victim.Owner))
        {
            return;
        }

        var worth = victim switch
        {
            Unit dead => dead.Archetype.Cost,
            Building { Def: { } def } => def.Cost,
            _ => 0,
        };
        Gain(world, unit, worth, events);
    }

    /// <summary>The unit gains <paramref name="experience"/>, up to elite; a new rank is told.</summary>
    internal static void Gain(GameWorld world, Unit unit, int experience, List<SimEvent>? events)
    {
        if (experience <= 0 || unit.Archetype.Has(UnitTalents.Untrainable))
        {
            return;
        }

        var before = RankOf(unit);
        unit.Experience = IntMath.Min(unit.Experience + experience, unit.Archetype.Cost * VeteranRatio * 2);
        if (RankOf(unit) != before)
        {
            events?.Add(new UnitPromoted(world.CurrentTick, unit.Id, unit.Owner, RankOf(unit)));
        }
    }

    /// <summary>A unit just built: a veteran when a spy went into the enemy's barracks or war factory for its player.</summary>
    internal static void Produced(GameWorld world, Unit unit, List<SimEvent>? events)
    {
        var player = world.Player(unit.Owner);
        var veteran = unit.Archetype.Category == UnitCategory.Infantry ? player.VeteranInfantry
            : unit.Archetype.Category == UnitCategory.Vehicle && player.VeteranVehicles;
        if (veteran && unit.Experience < unit.Archetype.Cost * VeteranRatio)
        {
            Gain(world, unit, (unit.Archetype.Cost * VeteranRatio) - unit.Experience, events);
        }
    }

    /// <summary>The damage of a hit of <paramref name="shooter"/> on <paramref name="victim"/>: a tenth more from a veteran, two thirds on one.</summary>
    internal static int Damage(GameWorld world, Entity victim, EntityId shooter, int amount)
    {
        if (world.TryGet(shooter, out var entity) && entity is Unit attacker && IsVeteran(attacker))
        {
            amount = (int)((long)amount * 11 / 10);
        }

        if (victim is Unit unit && IsVeteran(unit))
        {
            amount = IntMath.Max(1, (int)((long)amount * 2 / 3));
        }

        // The Firepower and the Armor of the crates (M11 a6).
        return CrateSystem.Damage(world, victim, shooter, amount);
    }

    /// <summary>The speed of the unit: a fifth more for a veteran.</summary>
    public static int Speed(Unit unit, int speed) => IsVeteran(unit) ? speed * 6 / 5 : speed;

    /// <summary>The speed of the unit in the match: a veteran's, and the Speed of a crate on top (M11 a6).</summary>
    internal static int Speed(GameWorld world, Unit unit, int speed) => CrateSystem.Speed(world, unit, Speed(unit, speed));

    /// <summary>The reload of a weapon of <paramref name="shooter"/>: shorter for a veteran soldier.</summary>
    internal static int Reload(GameWorld world, EntityId shooter, int reload) =>
        world.TryGet(shooter, out var entity) && entity is Unit { Archetype.Category: UnitCategory.Infantry } soldier && IsVeteran(soldier)
            ? IntMath.Max(1, reload * ReloadPercent / 100)
            : reload;
}
