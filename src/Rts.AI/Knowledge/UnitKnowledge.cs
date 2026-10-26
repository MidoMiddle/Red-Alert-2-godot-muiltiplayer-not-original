using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rts.Simulation.Combat;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Knowledge;

/// <summary>What a bot does with a kind of unit (M11, the bot that knows every weapon).</summary>
public enum UnitUse : byte
{
    /// <summary>It fights in the squads of the army, on the ground or in the air.</summary>
    Army = 1,

    /// <summary>A hero, of whom a player may have only so many (<see cref="UnitDef.BuildLimit"/>): fights in the army, built apart from the roles.</summary>
    Hero = 7,

    /// <summary>
    /// It fires only when ordered (<see cref="UnitDef.Passive"/>: the jets, the artillery, the bombers on foot and on wheels, the ships that
    /// launch): bought by the roles as the army or the fleet is, sent at its targets by the strikes of the bot (<c>StrikeManager</c>).
    /// </summary>
    Striker = 8,

    /// <summary>It takes and mends buildings: the bot builds one when there is a building to take (<c>EngineerManager</c>).</summary>
    Engineer = 9,

    /// <summary>It carries units and fights not by itself: the ferry of the bot over the water (<c>FerryManager</c>).</summary>
    Transport = 10,

    /// <summary>It goes into the enemy's buildings for what they give (<c>SpyManager</c>).</summary>
    Spy = 11,

    /// <summary>A ship that fights: it sails with the fleet.</summary>
    Fleet = 2,

    /// <summary>It gathers ore.</summary>
    Harvester = 3,

    /// <summary>It deploys into a construction yard.</summary>
    Mcv = 4,

    /// <summary>The bot does not use it yet: its use needs a mechanic the bot does not know (<see cref="UnitCard.Reason"/>).</summary>
    Special = 5,

    /// <summary>No factory builds it: another unit launches it, a paradrop brings it.</summary>
    NeverBuilt = 6,
}

/// <summary>
/// What a bot knows of one kind of unit, worked out from the data alone (<see cref="UnitKnowledge"/>): how it is used, and how much of
/// its role it gives for its cost.
/// </summary>
public sealed record UnitCard(int Index, string Id, UnitUse Use, string Reason)
{
    /// <summary>
    /// The worth of the unit in each <see cref="ArmyRole"/> (by its value − 1): the damage a minute it does to that enemy on average, times
    /// its hit points, times the reach of its longest weapon over 5 cells, over its cost to the power 1.5. By Lanchester's square law the
    /// strength of an army bought with the same money goes with the cost squared; but a crowd of cheap soldiers does not all fight at
    /// once, splash and tracks thin it, and the power of 2 made conscripts the best buy against tanks — 1.5 lies between. The hit points
    /// of what flies count more: only what fires at the air hits it.
    /// Zero for a role it cannot fight, and for a unit that does not fight: not of the army, the fleet, a hero or a striker.
    /// </summary>
    public long[] Worth { get; init; } = new long[RoleCount];

    /// <summary>The worth in <paramref name="role"/>.</summary>
    public long In(ArmyRole role) => Worth[(int)role - 1];

    public const int RoleCount = 4;
}

/// <summary>
/// What a bot knows of every unit of the rules (M11): read from the data, never written by hand, so that a new unit or weapon is known
/// to the bots as soon as it is in the data.
/// <list type="bullet">
/// <item><b>The use</b> (<see cref="UnitUse"/>): what launches or brings it is never built; a harvester, an MCV; an engineer; a spy; a
/// transport — with no gun of its own, or one that fires only when ordered; a unit with no weapon waits for a mechanic of its own
/// (<see cref="UnitCard.Reason"/> says which); a unit that fires only when ordered is a striker; an armed ship is of the fleet; a unit a player may have only so many of is a hero; the rest fight in the
/// army.</item>
/// <item><b>The damage</b> a unit does to another or to a building in a minute (<see cref="DamagePerMinute"/>): of its primary and
/// secondary weapons the best that fires at the layer of the target — the air for what flies, under the water for a submarine, the water
/// and the ground for a ship, the ground for the rest — by the warhead against the target's armor, every shot of the volley, once a
/// reload. A warhead that takes the unit (mind control) does its hit points; one that erases (temporal) a tenth of the damage of the beam
/// a tick (<see cref="ErasureSystem.HitPointsTimes"/>); one that plants a bomb the damage of the bomb; a parasite the damage it does
/// inside a vehicle or a ship a second. A jet fires its ammo and flies home to load it again (<see cref="AmmoSystem.RearmTicks"/> a
/// volley, <see cref="FlightTicks"/> the flight); a carrier does what all its jets do; a weapon that is its shooter's end hits once in
/// a minute. The splash, the splinters and the speed are left out.</item>
/// <item><b>The worth</b> in each role (<see cref="UnitCard.Worth"/>), against every kind of enemy of the role in the rules alike:
/// soldiers on foot; vehicles — for the fleet, ships and submarines; what flies; buildings. The reach of the unit counts too: of two
/// alike, the one that shoots from farther is shot at less.</item>
/// </list>
/// One for the rules of a match, shared by its bots: worked out once, as the rules never change.
/// </summary>
public sealed class UnitKnowledge
{
    private static readonly ConditionalWeakTable<GameRules, UnitKnowledge> Known = new();

    private readonly GameRules _rules;
    private readonly UnitCard[] _cards;

    /// <summary>How many times the hit points of a flier count, in 256ths: the armed kinds of units over those that fire at the air, 4 at most.</summary>
    private readonly long _flierCover;

    private UnitKnowledge(GameRules rules)
    {
        _rules = rules;
        var database = rules.Database;
        _cards = new UnitCard[database.Units.Length];
        var mcvs = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var faction in database.Factions)
        {
            mcvs.Add(faction.Mcv);
        }

        var (armed, againstAir) = (0L, 0L);
        foreach (var unit in database.Units)
        {
            var weapons = new[] { database.IndexOfWeapon(unit.Primary), database.IndexOfWeapon(unit.Secondary) };
            armed += weapons[0] >= 0 ? 1 : 0;
            againstAir += (weapons[0] >= 0 && (database.Weapons[weapons[0]].Targets & WeaponTargets.Air) != 0)
                || (weapons[1] >= 0 && (database.Weapons[weapons[1]].Targets & WeaponTargets.Air) != 0) ? 1 : 0;
        }

        _flierCover = againstAir == 0 ? 4 * 256 : IntMath.Clamp(armed * 256 / againstAir, 256, 4 * 256);
        for (var i = 0; i < _cards.Length; i++)
        {
            var (use, reason) = UseOf(database.Units[i], mcvs);
            _cards[i] = new UnitCard(i, database.Units[i].Id, use, reason);
        }

        foreach (var card in _cards)
        {
            if (card.Use is UnitUse.Army or UnitUse.Fleet or UnitUse.Hero or UnitUse.Striker)
            {
                for (var role = 1; role <= UnitCard.RoleCount; role++)
                {
                    card.Worth[role - 1] = WorthIn(card.Index, (ArmyRole)role, database.Units[card.Index].Category == UnitCategory.Naval);
                }
            }
        }
    }

    /// <summary>The knowledge of <paramref name="rules"/>, worked out on the first call.</summary>
    public static UnitKnowledge Of(GameRules rules) => Known.GetValue(rules, static rules => new UnitKnowledge(rules));

    /// <summary>Every unit of the rules, by its index in <see cref="RulesDatabase.Units"/>.</summary>
    public IReadOnlyList<UnitCard> Cards => _cards;

    /// <summary>The card of the unit at <paramref name="index"/> of <see cref="RulesDatabase.Units"/>.</summary>
    public UnitCard this[int index] => _cards[index];

    /// <summary>The card of <paramref name="unit"/>'s kind.</summary>
    public UnitCard Of(Unit unit) => _cards[_rules.Database.IndexOfUnit(unit.Archetype.Id)];

    /// <summary>Whether <paramref name="unit"/> fights with the squads of the army.</summary>
    public bool InArmy(Unit unit) => unit.Harvester is null && Of(unit).Use is UnitUse.Army or UnitUse.Hero;

    /// <summary>Whether <paramref name="unit"/> sails with the fleet: a ship that fights, or one that fires when ordered.</summary>
    public bool InFleet(Unit unit) => Of(unit).Use == UnitUse.Fleet || (Of(unit).Use == UnitUse.Striker && unit.Archetype.Category == UnitCategory.Naval);

    /// <summary>Whether <paramref name="unit"/> fires only when ordered: the strikes of the bot send it.</summary>
    public bool InStrikes(Unit unit) => Of(unit).Use == UnitUse.Striker;

    /// <summary>
    /// The part of the army a kind of unit is bought in by the roles: <see cref="UnitUse.Army"/> for the ground and the air,
    /// <see cref="UnitUse.Fleet"/> for the water — a unit that fires when ordered in the one of its category; null for one not bought so.
    /// </summary>
    public UnitUse? PoolOf(UnitCard card) => card.Use switch
    {
        UnitUse.Army => UnitUse.Army,
        UnitUse.Fleet => UnitUse.Fleet,
        UnitUse.Striker => _rules.Database.Units[card.Index].Category == UnitCategory.Naval ? UnitUse.Fleet : UnitUse.Army,
        _ => null,
    };

    /// <summary>The role an enemy of <paramref name="unit"/>'s kind is fought by: soldiers, vehicles and ships, or what flies.</summary>
    public static ArmyRole RoleAgainst(UnitDef unit) =>
        unit.Category == UnitCategory.Aircraft || unit.Jumpjet ? ArmyRole.AntiAir
        : unit.Category == UnitCategory.Infantry ? ArmyRole.AntiInfantry
        : ArmyRole.AntiArmor;

    /// <summary>
    /// The damage a unit of index <paramref name="shooter"/> does in a minute to one of index <paramref name="target"/> (or, with
    /// <paramref name="building"/>, to the building of that index), by its best weapon at the target's layer; 0 when none fires there.
    /// </summary>
    public long DamagePerMinute(int shooter, int target, bool building = false)
    {
        var database = _rules.Database;
        var unit = database.Units[shooter];
        long best = 0;
        foreach (var id in new[] { unit.Primary, unit.Secondary })
        {
            var index = database.IndexOfWeapon(id);
            if (index < 0)
            {
                continue;
            }

            var weapon = database.Weapons[index];
            if (weapon.LaunchesSpawns && database.IndexOfUnit(unit.Spawns) is >= 0 and var spawn)
            {
                // The carrier: what its jets do, all of them.
                best = IntMath.Max(best, DamagePerMinute(spawn, target, building) * IntMath.Max(1, unit.SpawnCount));
                continue;
            }

            var damage = WeaponDamagePerMinute(weapon, target, building);
            if (unit.Ammo > 0)
            {
                // A jet fires its ammo, flies home and loads a volley every RearmTicks: its volleys over the whole round.
                damage = damage * IntMath.Max(1, weapon.Reload) * unit.Ammo / (unit.Ammo * (IntMath.Max(1, weapon.Reload) + AmmoSystem.RearmTicks) + FlightTicks);
            }

            if (weapon.Suicide)
            {
                // It fires once and is gone: one hit in a minute of its life.
                damage = damage * IntMath.Max(1, weapon.Reload) / 900;
            }

            best = IntMath.Max(best, damage);
        }

        return best;
    }

    /// <summary>
    /// The damage the defense at <paramref name="building"/> of <see cref="RulesDatabase.Buildings"/> does in a minute to a unit of index
    /// <paramref name="target"/>, by its weapon, as a unit's (the defenses of the bot, before the tuning); 0 when it fires not there.
    /// </summary>
    public long DefenseDamagePerMinute(int building, int target)
    {
        var database = _rules.Database;
        return database.IndexOfWeapon(database.Buildings[building].Primary) is >= 0 and var weapon
            ? WeaponDamagePerMinute(database.Weapons[weapon], target, building: false)
            : 0;
    }

    /// <summary>How long a jet flies to its target and back, in ticks: 20 s, a guess.</summary>
    public const int FlightTicks = 300;

    private long WeaponDamagePerMinute(WeaponDef weapon, int target, bool building)
    {
        var database = _rules.Database;
        var (layer, armorId, hp, category) = building
            ? (database.Buildings[target].Water ? WeaponTargets.Ground | WeaponTargets.Naval : WeaponTargets.Ground,
                database.Buildings[target].Armor, database.Buildings[target].Hp, (UnitCategory)0)
            : (LayerOf(database.Units[target]), database.Units[target].Armor, database.Units[target].Hp, database.Units[target].Category);
        if ((weapon.Targets & layer) == 0)
        {
            return 0;
        }

        var warheadIndex = database.IndexOfWarhead(weapon.Warhead);
        if (warheadIndex < 0)
        {
            return 0;
        }

        var warhead = database.Warheads[warheadIndex];
        var armor = IntMath.Max(0, database.IndexOfArmor(armorId));
        var reload = IntMath.Max(1, weapon.Reload);
        long hit;
        if (warhead.MindControl)
        {
            hit = building ? 0 : hp;
        }
        else if (warhead.Temporal)
        {
            // The beam gathers the damage every tick it holds; the target is gone at ten times its hit points.
            return (long)weapon.Damage * 900 / ErasureSystem.HitPointsTimes;
        }
        else if (warhead.Parasite && !building
            && (warhead.ParasiteNaval ? category == UnitCategory.Naval : category == UnitCategory.Vehicle))
        {
            return (long)warhead.ParasiteDamage * 900 / IntMath.Max(1, warhead.ParasiteDelay);
        }
        else if (warhead.Bomb.Length > 0 && database.IndexOfWeapon(warhead.Bomb) is >= 0 and var bombIndex)
        {
            var bomb = database.Weapons[bombIndex];
            var bombWarhead = database.IndexOfWarhead(bomb.Warhead);
            hit = bombWarhead < 0 ? 0 : DamageCalculator.Direct(bomb.Damage, _rules.Verses(bombWarhead, armor));
            reload = IntMath.Max(reload, warhead.BombDelay);
        }
        else
        {
            hit = DamageCalculator.Direct(weapon.Damage, _rules.Verses(warheadIndex, armor));
        }

        // What is past the hit points of the target is wasted: a bite of 1000 kills a soldier of 125 once.
        return IntMath.Min(hit * IntMath.Max(1, weapon.Burst), hp) * 900 / reload;
    }

    /// <summary>
    /// The damage a minute of <paramref name="shooter"/> against unit <paramref name="target"/> with running over counted: a vehicle on the
    /// ground that crushes runs a soldier it can crush over once in <see cref="CrushTicks"/>; a soldier that can be crushed does a quarter of its
    /// damage to a vehicle that crushes, run over before the fight is done.
    /// </summary>
    private long WithCrushing(int shooter, int target, long damage)
    {
        var (unit, other) = (_rules.Database.Units[shooter], _rules.Database.Units[target]);
        return unit.Crusher && unit.Category == UnitCategory.Vehicle && other.Crushable ? damage + (long)other.Hp * 900 / CrushTicks
            : unit.Crushable && other.Crusher ? damage / 4
            : damage;
    }

    /// <summary>How often a vehicle that crushes runs a soldier over, in ticks: 4 s, a guess.</summary>
    public const int CrushTicks = 60;

    /// <summary>The layer a weapon fires at to hit a unit of <paramref name="unit"/>'s kind, as the targeting of the simulation has it.</summary>
    private static WeaponTargets LayerOf(UnitDef unit) =>
        unit.Category == UnitCategory.Aircraft || unit.Jumpjet ? WeaponTargets.Air
        : unit.Submerged ? WeaponTargets.Underwater
        : unit.Category == UnitCategory.Naval ? WeaponTargets.Ground | WeaponTargets.Naval
        : WeaponTargets.Ground;

    /// <summary>The worth of unit <paramref name="index"/> in <paramref name="role"/> — for a ship of the fleet, against ships for vehicles.</summary>
    private long WorthIn(int index, ArmyRole role, bool fleet)
    {
        var database = _rules.Database;
        var unit = database.Units[index];
        if (unit.Cost <= 0)
        {
            return 0;
        }

        long damage = 0;
        var targets = 0;
        if (role == ArmyRole.AntiBuilding)
        {
            for (var b = 0; b < database.Buildings.Length; b++)
            {
                if (database.Buildings[b].Hp > 0 && !database.Buildings[b].Wall)
                {
                    damage += DamagePerMinute(index, b, building: true);
                    targets++;
                }
            }
        }
        else
        {
            for (var t = 0; t < database.Units.Length; t++)
            {
                var target = database.Units[t];
                var of = role == ArmyRole.AntiArmor && fleet ? target.Category == UnitCategory.Naval
                    : role == ArmyRole.AntiArmor ? target.Category == UnitCategory.Vehicle
                    : RoleAgainst(target) == role;
                if (of)
                {
                    damage += WithCrushing(index, t, DamagePerMinute(index, t));
                    targets++;
                }
            }
        }

        if (targets == 0)
        {
            return 0;
        }

        // Kept whole: the damage a minute on average × the hit points × 10⁶ over the cost to the power 1.5, × the reach over 5 cells.
        // What flies is hit only by what fires at the air: its hit points count as many times more as the units that cannot, 4 at most.
        var costPower = (long)unit.Cost * IntMath.Sqrt((long)unit.Cost * 256) / 16;
        var hp = unit.Category == UnitCategory.Aircraft || unit.Jumpjet ? (long)unit.Hp * _flierCover / 256 : unit.Hp;
        return damage * hp * 1_000_000 / targets / costPower * Reach(unit) / (5 * 256);
    }

    /// <summary>
    /// The reach of the longest weapon of <paramref name="unit"/>, in 256ths of a cell: a unit that outranges what it fights shoots while
    /// it is not shot at. A cell at least.
    /// </summary>
    private long Reach(UnitDef unit)
    {
        var database = _rules.Database;
        long reach = 256;
        foreach (var id in new[] { unit.Primary, unit.Secondary })
        {
            if (database.IndexOfWeapon(id) is >= 0 and var index)
            {
                reach = IntMath.Max(reach, database.Weapons[index].Range.Raw >> (Fixed.FractionalBits - 8));
            }
        }

        return reach;
    }

    private static (UnitUse Use, string Reason) UseOf(UnitDef unit, HashSet<string> mcvs) => unit switch
    {
        { Spawned: true } => (UnitUse.NeverBuilt, "launched by another unit or brought by a paradrop"),
        { HarvestCapacity: > 0 } => (UnitUse.Harvester, ""),
        _ when mcvs.Contains(unit.Id) => (UnitUse.Mcv, ""),
        { Engineer: true } => (UnitUse.Engineer, ""),
        { Infiltrator: true } => (UnitUse.Spy, ""),
        { PassengerSlots: > 0, Primary.Length: 0 } or { PassengerSlots: > 0, Passive: true } => (UnitUse.Transport, ""),
        { Passive: true } => (UnitUse.Striker, ""),
        { Primary.Length: 0 } => (UnitUse.Special, "has no weapon"),
        { Category: UnitCategory.Naval } => (UnitUse.Fleet, ""),
        { BuildLimit: > 0 } => (UnitUse.Hero, ""),
        _ => (UnitUse.Army, ""),
    };
}
