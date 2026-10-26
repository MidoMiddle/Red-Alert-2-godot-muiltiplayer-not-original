using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rts.AI.Knowledge;
using Rts.AI.Modules;
using Rts.Data.Tests;
using Rts.Simulation.Rules;
using Xunit;

namespace Rts.AI.Tests;

/// <summary>
/// What the bots know of the units of the game (M11, the bot that knows every weapon): worked out from the data, never written by hand.
/// The tests that pin what the bots do not use yet fail when a unit or a field of the data comes in that nobody has decided on.
/// </summary>
public sealed class UnitKnowledgeTests
{
    private static readonly UnitKnowledge Knowledge = UnitKnowledge.Of(GameDataFiles.Rules);
    private static RulesDatabase Data => GameDataFiles.Rules.Database;

    /// <summary>
    /// The units of the game the bots do not use yet and why — the work of the next steps (docs/implementation/M11_content.md). A new unit
    /// of the data that fits none of the uses the bots know lands here and fails the test: decide what the bots do with it.
    /// </summary>
    private static readonly SortedDictionary<string, string> NotUsedYet = new(System.StringComparer.Ordinal)
    {
        ["alliance_amphibious_transport"] = "carries units",
        ["alliance_nighthawk"] = "carries units",
        ["alliance_spy"] = "goes into the enemy's buildings",
        ["hover_transport"] = "carries units",
    };

    [Fact]
    public void Every_unit_of_the_game_has_a_use_and_those_not_used_yet_are_known()
    {
        var special = Knowledge.Cards.Where(card => card.Use == UnitUse.Special).ToDictionary(card => card.Id, card => card.Reason);

        Assert.Equal(NotUsedYet, new SortedDictionary<string, string>(special, System.StringComparer.Ordinal));
        Assert.All(Knowledge.Cards, card => Assert.True(System.Enum.IsDefined(card.Use), card.Id));
    }

    [Fact]
    public void The_ships_sail_with_the_fleet_and_the_rest_fight_in_the_army()
    {
        Assert.Equal(UnitUse.Fleet, Card("alliance_destroyer").Use);
        Assert.Equal(UnitUse.Fleet, Card("typhoon_sub").Use);
        Assert.Equal(UnitUse.Army, Card("kirov_airship").Use);
        Assert.Equal(UnitUse.Army, Card("alliance_rocketeer").Use);
        Assert.Equal(UnitUse.Harvester, Card("harvester").Use);
        Assert.Equal(UnitUse.Mcv, Card("alliance_mcv").Use);
        Assert.Equal(UnitUse.NeverBuilt, Card("alliance_hornet").Use);
        Assert.Equal(UnitUse.Engineer, Card("engineer").Use);
        Assert.Equal(UnitUse.Striker, Card("rocket_artillery").Use);
        Assert.Equal(UnitUse.Striker, Card("alliance_harrier").Use);
        Assert.Equal(UnitUse.Fleet, Knowledge.PoolOf(Card("dreadnought")));
        Assert.Equal(UnitUse.Army, Knowledge.PoolOf(Card("alliance_harrier")));
    }

    [Fact]
    public void The_damage_counts_the_armor_the_volley_and_no_more_than_the_hit_points()
    {
        var dog = Data.IndexOfUnit("attack_dog");
        var conscript = Data.IndexOfUnit("conscript");

        // The bite of 1000 kills a conscript of 125 once in 2 s: 125 × 30 a minute.
        Assert.Equal(125L * 30, Knowledge.DamagePerMinute(dog, conscript));

        // A torpedo hits nothing on land, flak nothing under the water.
        Assert.Equal(0L, Knowledge.DamagePerMinute(Data.IndexOfUnit("typhoon_sub"), Data.IndexOfUnit("heavy_tank")));
        Assert.Equal(0L, Knowledge.DamagePerMinute(Data.IndexOfUnit("sea_scorpion"), Data.IndexOfUnit("alliance_dolphin")));
        Assert.True(Knowledge.DamagePerMinute(Data.IndexOfUnit("sea_scorpion"), Data.IndexOfUnit("alliance_harrier")) > 0);
    }

    [Fact]
    public void The_worth_of_a_unit_follows_what_its_weapon_is_for()
    {
        // The tank destroyer is the Alliance's best against armour, the flak track the Union's best on the ground against the air.
        Assert.Equal("alliance_tank_destroyer", Best("alliance", UnitUse.Army, ArmyRole.AntiArmor));
        Assert.Equal("support_vehicle", Best("union", UnitUse.Army, ArmyRole.AntiAir));

        // A dog fights no tank; a sniper fights soldiers far better than tanks.
        Assert.Equal(0L, Card("attack_dog").In(ArmyRole.AntiArmor));
        Assert.True(Card("alliance_sniper").In(ArmyRole.AntiInfantry) > 100 * Card("alliance_sniper").In(ArmyRole.AntiArmor));

        // Of the ships: the dolphin against ships and submarines, the cruiser against the air.
        Assert.Equal("alliance_dolphin", Best("alliance", UnitUse.Fleet, ArmyRole.AntiArmor));
        Assert.Equal("alliance_aegis_cruiser", Best("alliance", UnitUse.Fleet, ArmyRole.AntiAir));
    }

    /// <summary>
    /// The fighters no army picks with every unit of their faction open (stolen technology aside): never among the best three of a role for
    /// their cost. Each is worth most by a mechanic the worth does not count yet — the next steps of the bot (docs/implementation/M11_content.md);
    /// before the technology that outclasses them is there, an army picks some of them.
    /// </summary>
    private static readonly SortedSet<string> Outclassed = new(System.StringComparer.Ordinal)
    {
        "alliance_chrono_legionnaire", // erases slowly by our numbers; its jumps are not counted
        "alliance_harrier", // the Black Eagle does it better for the money
        "alliance_mirage_tank", // its disguise is not counted
        "alliance_prism_tank", // its splinters are not counted
        "alliance_rifleman", // the soldier of the start: outclassed once the battle lab stands; digging in is not counted
        "alliance_sniper", // its reach from a garrison is not counted
        "attack_dog", // the Union's: the flak track, the airship and the conscript do better against soldiers
        "crazy_ivan", // its bomb goes off late: the worth counts one blast in the delay, not bombs on many targets
        "demolition_truck", // the splash of its blast is not counted
        "desolator", // its poison when deployed is not counted
        "giant_squid", // its hold of a ship is not counted
        "heavy_tank", // the tank of the start: the apocalypse, the airship and the rockets do better with every unit open
        "rocketeer", // the rocket trooper: the apocalypse, the airship and the rockets do it better for the money
        "terror_drone", // the damage inside a vehicle is slow, the vehicle lost to it is not counted
        "terrorist", // the splash of its blast is not counted
        "tesla_tank", // the apocalypse does it better for the money
        "tesla_trooper", // charging the tesla coils is not counted
        "yuri", // mind control takes one unit at a time; the psychic wave is not counted
    };

    [Fact]
    public void Every_fighter_is_picked_by_some_role_with_every_unit_open()
    {
        var picked = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var faction in Data.Factions)
        {
            foreach (var use in new[] { UnitUse.Army, UnitUse.Fleet })
            {
                for (var role = 1; role <= UnitCard.RoleCount; role++)
                {
                    var r = role - 1;
                    picked.UnionWith(Knowledge.Cards
                        .Where(card => Knowledge.PoolOf(card) == use && Data.Units[card.Index].Faction == faction.Id && card.Worth[r] > 0
                            && Data.Units[card.Index].RequiresStolen.Length == 0)
                        .OrderByDescending(card => card.Worth[r]).ThenBy(card => card.Index)
                        .Take(ProductionManager.RoleParts.Length).Select(card => card.Id));
                }
            }
        }

        var never = Knowledge.Cards
            .Where(card => Knowledge.PoolOf(card) is not null && Data.Units[card.Index].RequiresStolen.Length == 0 && !picked.Contains(card.Id))
            .Select(card => card.Id);

        Assert.Equal(Outclassed, new SortedSet<string>(never, System.StringComparer.Ordinal));
    }

    /// <summary>
    /// Every field of a unit of the data, and what the bots do with it: <c>used</c> — they read it; <c>later</c> — a mechanic of its own
    /// the bots learn in the next steps; <c>no need</c> — it changes nothing a bot decides. A new field lands in none and fails the test.
    /// </summary>
    private static readonly SortedDictionary<string, string> UnitFields = new(System.StringComparer.Ordinal)
    {
        ["Id"] = "used", ["Name"] = "no need: shown to people", ["Faction"] = "used", ["Category"] = "used", ["Queue"] = "used",
        ["ProducedIn"] = "used", ["Cost"] = "used", ["BuildTime"] = "no need: the queue's", ["Hp"] = "used", ["Armor"] = "used",
        ["Speed"] = "later: the worth leaves the speed out", ["TurnRate"] = "no need: movement", ["TurretTurnRate"] = "no need: movement",
        ["Turret"] = "no need: movement", ["Sight"] = "no need: vision", ["Locomotor"] = "used: the map reading goes by tank and by ship",
        ["Placement"] = "no need: movement", ["Prerequisites"] = "used", ["Countries"] = "used", ["ForbiddenCountries"] = "used",
        ["HarvestCapacity"] = "used", ["HarvestTime"] = "no need: the economy counts harvesters", ["UnloadRate"] = "no need: the economy counts harvesters",
        ["Threat"] = "no need: targeting", ["Stance"] = "no need: targeting", ["Crusher"] = "used", ["Crushable"] = "used",
        ["Jumpjet"] = "used", ["Engineer"] = "used", ["Infiltrator"] = "later: the spy",
        ["Disguised"] = "later: the spy", ["DetectsDisguise"] = "later: the spy", ["Passive"] = "used",
        ["DeathWeapon"] = "later: units that blow up", ["Teleports"] = "later: chrono jumps", ["TeleportsHome"] = "no need: the miner jumps by itself",
        ["DeployWeapon"] = "later: deploying", ["DeployedWeapon"] = "later: deploying", ["DeployOnce"] = "later: deploying",
        ["UndeployDelay"] = "later: deploying", ["RadiationImmune"] = "later: radiation", ["BuildLimit"] = "used: the tech tree stops it",
        ["RequiresStolen"] = "used: the tech-up leaves it out", ["Occupier"] = "used", ["OccupyWeapon"] = "no need: the garrison fires it",
        ["ChargesCoils"] = "later: charging tesla coils", ["PsionicImmune"] = "later: mind control", ["DisguiseWhenStill"] = "later: the mirage",
        ["PassengerSlots"] = "later: transports", ["PassengerSizeLimit"] = "later: transports", ["Size"] = "later: transports",
        ["PassengerWeapon"] = "later: transports", ["IfvWeapon"] = "later: transports", ["Submerged"] = "used", ["Sensors"] = "later: submarines",
        ["Ammo"] = "used", ["SelfHeal"] = "no need: it heals by itself", ["Trainable"] = "no need: veterancy",
        ["Spawns"] = "used", ["SpawnCount"] = "used", ["SpawnRegen"] = "no need: the carrier replaces them", ["Spawned"] = "used",
        ["Primary"] = "used", ["Secondary"] = "used", ["Model"] = "no need: the client", ["Icon"] = "no need: the client",
        ["AvailableLater"] = "used", ["AbilityLater"] = "no need: no unit has it",
    };

    /// <summary>Every field of a warhead and what the bots do with it, as for <see cref="UnitFields"/>.</summary>
    private static readonly SortedDictionary<string, string> WarheadFields = new(System.StringComparer.Ordinal)
    {
        ["Id"] = "used", ["Name"] = "no need: shown to people", ["Verses"] = "used", ["Spread"] = "later: the worth leaves the splash out",
        ["PercentAtMax"] = "later: the worth leaves the splash out", ["WallDamage"] = "no need: walls", ["AffectsAir"] = "later: the splash",
        ["Temporal"] = "used", ["Bomb"] = "used", ["BombDelay"] = "used", ["Bridges"] = "later: bridges", ["Radiation"] = "later: radiation",
        ["Parasite"] = "used", ["ParasiteDamage"] = "used", ["ParasiteDelay"] = "used", ["ParasiteNaval"] = "used",
        ["Paralyzes"] = "later: the squid", ["Repairs"] = "later: repair", ["FreesParasite"] = "later: parasites", ["MindControl"] = "used",
        ["BurnsOre"] = "no need: ore", ["SparesOwnKind"] = "no need: targeting", ["InfantryDeath"] = "no need: the client",
    };

    /// <summary>Every field of a weapon and what the bots do with it, as for <see cref="UnitFields"/>.</summary>
    private static readonly SortedDictionary<string, string> WeaponFields = new(System.StringComparer.Ordinal)
    {
        ["Id"] = "used", ["Name"] = "no need: shown to people", ["Damage"] = "used", ["Reload"] = "used", ["Range"] = "used: by the squads",
        ["MinRange"] = "later: artillery", ["Projectile"] = "no need: flight", ["Warhead"] = "used", ["Burst"] = "used",
        ["BurstDelay"] = "no need: inside the reload", ["Targets"] = "used", ["Report"] = "no need: the client",
        ["Suicide"] = "used", ["Fragments"] = "later: the worth leaves the splinters out",
        ["FragmentWeapon"] = "later: the worth leaves the splinters out", ["FragmentRange"] = "later: the worth leaves the splinters out",
        ["LaunchesSpawns"] = "used",
    };

    public static TheoryData<string> Kinds => new() { nameof(UnitDef), nameof(WarheadDef), nameof(WeaponDef) };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_field_of_the_data_is_decided_on_for_the_bots(string kind)
    {
        var (type, known) = kind switch
        {
            nameof(UnitDef) => (typeof(UnitDef), UnitFields),
            nameof(WarheadDef) => (typeof(WarheadDef), WarheadFields),
            _ => (typeof(WeaponDef), WeaponFields),
        };
        var fields = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(System.StringComparer.Ordinal);

        Assert.Equal(known.Keys, fields);
    }

    private static UnitCard Card(string id) => Knowledge[Data.IndexOfUnit(id)];

    private static string Best(string faction, UnitUse use, ArmyRole role, string[]? except = null) => Knowledge.Cards
        .Where(card => card.Use == use && Data.Units[card.Index].Faction == faction && !(except ?? []).Contains(card.Id))
        .OrderByDescending(card => card.In(role)).ThenBy(card => card.Index).First().Id;
}
