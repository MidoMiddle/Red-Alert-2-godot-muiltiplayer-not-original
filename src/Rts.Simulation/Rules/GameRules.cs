using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Rules;

/// <summary>
/// A data mistake that only the engine can see: a definition it needs is missing, or a value it cannot use
/// (docs/implementation/M3_base_economy.md, 4.5). <see cref="Id"/> and <see cref="Key"/> name where it is written; an empty
/// <see cref="Id"/> means the whole data, an empty <see cref="Key"/> the whole definition.
/// </summary>
public readonly record struct RulesProblem(string Id, string Key, string Reason);

/// <summary>
/// The rules of a match as the simulation reads them every tick (docs/implementation/M3_base_economy.md, 4.5): made once from a
/// <see cref="RulesDatabase"/>, with the unit types of M2 (<see cref="UnitArchetype"/> by <see cref="UnitType"/>) and the land speeds
/// (<see cref="LandSpeeds"/>). The data must define every unit type, locomotor and land type the engine knows, by their ids;
/// <see cref="Problems"/> says what is missing. Other definitions may be there too: the buildings are used from M3 step 6 on.
/// </summary>
public sealed class GameRules
{
    private readonly UnitArchetype[] _units;

    // The archetypes of the known types, without the numbers that were taken out.
    private readonly UnitArchetype[] _known;
    private readonly bool[] _buildable;
    private readonly Fixed[] _verses;
    private readonly int[] _buildingArmor;
    private readonly int[] _buildingWeapon;
    private readonly ImmutableArray<int>[] _unitWeapons;

    // What each type fires from inside a civilian building: its weapon of the garrison (RA2), else its own.
    private readonly ImmutableArray<int>[] _garrisonWeapons;

    // Every weapon alone in the first place, the second empty: what a passenger gives an IFV (stage 3d).
    private readonly ImmutableArray<int>[] _single;
    private readonly ImmutableArray<int>[] _buildingWeapons;

    // The weapon of each building of the data while charged (the tesla coil), -1 without one.
    private readonly ImmutableArray<int>[] _overcharge;

    // The weapon and armor of the buildings of the map by kind (the outpost): -1 and 0 for those without, and for data that lacks them
    // (the small datasets of the tests); the game data has both (GameDataTests).
    private readonly ImmutableArray<int>[] _kindWeapons;
    private readonly int[] _kindArmor;

    private GameRules(
        RulesDatabase database, UnitArchetype[] units, LandSpeeds land, bool[] buildable, Fixed[] verses, int[] buildingArmor, int[] buildingWeapon)
    {
        _buildingWeapon = buildingWeapon;
        Database = database;
        _units = units;
        _known = System.Array.FindAll(units, unit => unit is not null);
        Land = land;
        _buildable = buildable;
        _verses = verses;
        _buildingArmor = buildingArmor;

        // The weapons of every unit type and building, laid out once (M4 step 15), so a tick builds no arrays for them.
        _unitWeapons = System.Array.ConvertAll(units, unit => unit is null ? ImmutableArray<int>.Empty : ImmutableArray.Create(unit.Primary, unit.Secondary));
        _garrisonWeapons = System.Array.ConvertAll(units, unit => unit is null ? ImmutableArray<int>.Empty
            : unit.OccupyWeapon >= 0 ? ImmutableArray.Create(unit.OccupyWeapon) : ImmutableArray.Create(unit.Primary, unit.Secondary));
        _single = new ImmutableArray<int>[database.Weapons.Length];
        for (var weapon = 0; weapon < _single.Length; weapon++)
        {
            _single[weapon] = ImmutableArray.Create(weapon, -1);
        }
        _buildingWeapons = System.Array.ConvertAll(buildingWeapon, weapon => ImmutableArray.Create(weapon));
        _overcharge = new ImmutableArray<int>[database.Buildings.Length];
        for (var i = 0; i < _overcharge.Length; i++)
        {
            _overcharge[i] = ImmutableArray.Create(database.IndexOfWeapon(database.Buildings[i].OverchargeWeapon));
        }
        var kinds = 0;
        foreach (var type in BuildingTypes.All)
        {
            kinds = IntMath.Max(kinds, (int)type.Kind + 1);
        }

        _kindWeapons = new ImmutableArray<int>[kinds];
        _kindArmor = new int[kinds];
        System.Array.Fill(_kindWeapons, ImmutableArray.Create(-1));
        foreach (var type in BuildingTypes.All)
        {
            _kindWeapons[(int)type.Kind] = ImmutableArray.Create(database.IndexOfWeapon(type.Weapon));
            _kindArmor[(int)type.Kind] = IntMath.Max(0, database.IndexOfArmor(type.Armor));
        }
    }

    /// <summary>The definitions the rules are made from.</summary>
    public RulesDatabase Database { get; }

    /// <summary>The speeds of every locomotor on every land type: the map of a match goes by them.</summary>
    public LandSpeeds Land { get; }

    /// <summary>The unit types, in <see cref="UnitType"/> order.</summary>
    public IReadOnlyList<UnitArchetype> Units => _known;

    /// <summary>
    /// The faction a player plays unless the skirmish setup chooses one: the one the data marks <c>default</c>, else the first in id
    /// order.
    /// </summary>
    public FactionDef DefaultFaction
    {
        get
        {
            foreach (var faction in Database.Factions)
            {
                if (faction.IsDefault)
                {
                    return faction;
                }
            }

            return Database.Factions[0];
        }
    }

    /// <summary>Whether the building <paramref name="id"/> is the construction yard of a faction of the data: it comes from an MCV.</summary>
    public bool IsConstructionYard(string id)
    {
        foreach (var faction in Database.Factions)
        {
            if (faction.ConstructionYard == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="type"/> is the MCV of a faction of the data: the core of a base that is still on its wheels.</summary>
    public bool IsMcv(UnitType type)
    {
        foreach (var faction in Database.Factions)
        {
            if (UnitTypes.TryGetById(faction.Mcv, out var mcv) && mcv == type)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The crates of a match (M11 a6): the <c>crates</c> record of the data, or the defaults without one.</summary>
    public CratesDef Crates => Database.Crates.Length > 0 ? Database.Crates[0] : CratesDef.Default;

    /// <summary>The general rules of the economy: the one <c>economy</c> record of the data.</summary>
    public EconomyDef Economy => Database.Economies[0];

    /// <summary>Credits a cell of ore holds: <c>(stage + 1) × ore_value</c>, or <c>gem_value</c> for gems; 0 without ore.</summary>
    public int CreditsOf(OreCell ore) => ore.Kind switch
    {
        OreKind.Ore => (ore.Stage + 1) * Economy.OreValue,
        OreKind.Gems => (ore.Stage + 1) * Economy.GemValue,
        _ => 0,
    };

    /// <summary>Whether a building may stand on <paramref name="land"/>: <c>buildable</c> of the land type in the data.</summary>
    public bool IsBuildable(LandType land) => LandSpeeds.IsKnown(land) && _buildable[(int)land - 1];

    /// <summary>
    /// The share of damage the warhead at <paramref name="warhead"/> in <see cref="RulesDatabase.Warheads"/> does against the armor numbered
    /// <paramref name="armor"/> (M4, 4.5): laid out by number when the rules are made, so a hit looks nothing up by name.
    /// </summary>
    public Fixed Verses(int warhead, int armor) => _verses[(warhead * Database.Armors.Length) + armor];

    /// <summary>The armor number of the building at <paramref name="defIndex"/> in <see cref="RulesDatabase.Buildings"/>.</summary>
    public int ArmorOfBuilding(int defIndex) => _buildingArmor[defIndex];

    /// <summary>The weapon of the building at <paramref name="defIndex"/>, its place in <see cref="RulesDatabase.Weapons"/>; -1 without one.</summary>
    public int WeaponOfBuilding(int defIndex) => _buildingWeapon[defIndex];

    /// <summary>The primary and the secondary weapon of a unit type, their places in <see cref="RulesDatabase.Weapons"/>; -1 for none.</summary>
    public ImmutableArray<int> WeaponsOf(UnitType type) => _unitWeapons[(int)type - 1];

    /// <summary>What a soldier of the type fires from inside a building: its weapon of the garrison (RA2 <c>OccupyW</c>), else its own.</summary>
    public ImmutableArray<int> GarrisonWeaponsOf(UnitType type) => _garrisonWeapons[(int)type - 1];

    /// <summary>The weapon of the building at <paramref name="defIndex"/> as a list of one, -1 without one.</summary>
    public ImmutableArray<int> WeaponsOfBuilding(int defIndex) => _buildingWeapons[defIndex];

    /// <summary>The weapon of a building of the data or of the map (the outpost) as a list of one, -1 without one.</summary>
    public ImmutableArray<int> WeaponsOf(Building building) =>
        building.DefIndex >= 0 ? _buildingWeapons[building.DefIndex] : _kindWeapons[(int)building.Kind];

    /// <summary>The weapon of a building of the data while charged (the tesla coil) as a list of one, -1 without one.</summary>
    public ImmutableArray<int> OverchargeOf(Building building) =>
        building.DefIndex >= 0 ? _overcharge[building.DefIndex] : ImmutableArray.Create(-1);

    /// <summary>The armor number of a building of the data or of the map; a building of the map without armor counts as the first type.</summary>
    public int ArmorOf(Building building) => building.DefIndex >= 0 ? _buildingArmor[building.DefIndex] : _kindArmor[(int)building.Kind];

    public UnitArchetype Unit(UnitType type) => UnitTypes.IsKnown(type)
        ? _units[(int)type - 1]
        : throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown unit type.");

    /// <summary>The rules made from <paramref name="database"/>.</summary>
    /// <exception cref="ArgumentException">The data has <see cref="Problems"/>; the message lists them.</exception>
    public static GameRules From(RulesDatabase database)
    {
        var problems = Problems(database);
        if (problems.Count > 0)
        {
            var reasons = new List<string>(problems.Count);
            foreach (var problem in problems)
            {
                reasons.Add(problem.Reason);
            }

            throw new ArgumentException($"The data cannot be played: {string.Join("; ", reasons)}.", nameof(database));
        }

        var units = new UnitArchetype[UnitTypes.Highest];
        foreach (var type in UnitTypes.All)
        {
            database.TryGetUnit(UnitTypes.IdOf(type), out var unit);
            units[(int)type - 1] = new UnitArchetype(
                type,
                unit.Id,
                unit.Category,
                LocomotorOf(unit.Locomotor)!.Value,
                unit.Placement,
                unit.Speed,
                unit.TurnRate,
                unit.Turret,
                unit.Sight,
                unit.HarvestCapacity,
                unit.HarvestTime,
                unit.UnloadRate,
                unit.Hp,
                IntMath.Max(0, database.IndexOfArmor(unit.Armor)),
                unit.TurretTurnRate ?? unit.TurnRate,
                database.IndexOfWeapon(unit.Primary),
                database.IndexOfWeapon(unit.Secondary),
                unit.Threat,
                unit.Stance,
                unit.Crusher,
                unit.Crushable,
                unit.Jumpjet,
                TalentsOf(unit),
                database.IndexOfWeapon(unit.DeathWeapon),
                database.IndexOfWeapon(unit.DeployWeapon),
                unit.Size > 0 ? unit.Size : unit.Category == UnitCategory.Infantry ? 1 : 3,
                unit.PassengerSlots,
                unit.PassengerSizeLimit,
                database.IndexOfWeapon(unit.IfvWeapon),
                unit.Sensors,
                unit.Ammo,
                unit.SelfHeal,
                UnitTypes.TryGetById(unit.Spawns, out var spawn) ? spawn : 0,
                unit.SpawnCount,
                unit.SpawnRegen,
                unit.Cost,
                database.IndexOfWeapon(unit.DeployedWeapon),
                database.IndexOfWeapon(unit.OccupyWeapon));
        }

        var percent = new int[LandSpeeds.LandCount, LandSpeeds.LocomotorCount];
        var slope = new int[LandSpeeds.LocomotorCount];
        var buildable = new bool[LandSpeeds.LandCount];
        for (var land = LandType.Clear; land <= LandType.Path; land++)
        {
            database.TryGetTerrain(LandSpeeds.IdOf(land), out var terrain);
            buildable[(int)land - 1] = terrain.Buildable;
        }

        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            database.TryGetLocomotor(LandSpeeds.IdOf(locomotor), out var definition);
            slope[(int)locomotor - 1] = PercentOf(definition.Slope);
            for (var land = LandType.Clear; land <= LandType.Path; land++)
            {
                database.TryGetTerrain(LandSpeeds.IdOf(land), out var terrain);
                percent[(int)land - 1, (int)locomotor - 1] = PercentOf(SpeedOn(terrain, LandSpeeds.IdOf(locomotor)));
            }
        }

        // M4 step 2: the verses of every warhead by armor number, and the armor number of every building; armor the data does not
        // define (the checks of the data refuse it) counts as the first.
        var armors = database.Armors.Length;
        var verses = new Fixed[database.Warheads.Length * armors];
        for (var warhead = 0; warhead < database.Warheads.Length; warhead++)
        {
            foreach (var verse in database.Warheads[warhead].Verses)
            {
                var armor = database.IndexOfArmor(verse.Armor);
                if (armor >= 0)
                {
                    verses[(warhead * armors) + armor] = verse.Percent;
                }
            }
        }

        var buildingArmor = new int[database.Buildings.Length];
        var buildingWeapon = new int[database.Buildings.Length];
        for (var i = 0; i < buildingArmor.Length; i++)
        {
            buildingArmor[i] = IntMath.Max(0, database.IndexOfArmor(database.Buildings[i].Armor));
            buildingWeapon[i] = database.IndexOfWeapon(database.Buildings[i].Primary);
        }

        return new GameRules(database, units, new LandSpeeds(percent, slope), buildable, verses, buildingArmor, buildingWeapon);
    }

    /// <summary>
    /// What keeps the engine from playing <paramref name="database"/>: an engine locomotor, land type or unit type without a definition,
    /// a land code other than the one of the map format, a locomotor the engine does not know, a speed over 255 percent, no faction.
    /// </summary>
    public static List<RulesProblem> Problems(RulesDatabase database)
    {
        var problems = new List<RulesProblem>();
        if (database.Factions.Length == 0)
        {
            problems.Add(new RulesProblem("", "", "the engine needs a faction for the players, and none is defined"));
        }

        foreach (var faction in database.Factions)
        {
            if (database.TryGetUnit(faction.Harvester, out var harvester) && harvester.HarvestCapacity <= 0)
            {
                problems.Add(new RulesProblem(harvester.Id, "harvest_capacity", $"'{harvester.Id}' is the harvester of '{faction.Id}', so it needs a harvest_capacity"));
            }

            if (database.TryGetBuilding(faction.Refinery, out var refinery) && refinery.DockCell is null)
            {
                problems.Add(new RulesProblem(refinery.Id, "", $"'{refinery.Id}' is the refinery of '{faction.Id}', so it needs a dock_cell"));
            }
        }

        if (database.Economies.Length != 1)
        {
            problems.Add(new RulesProblem(
                database.Economies.Length > 1 ? database.Economies[1].Id : "",
                "",
                $"the engine needs exactly one economy record, and there are {database.Economies.Length}"));
        }

        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            if (!database.TryGetLocomotor(LandSpeeds.IdOf(locomotor), out _))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs locomotor '{LandSpeeds.IdOf(locomotor)}', which is not defined"));
            }
        }

        foreach (var locomotor in database.Locomotors)
        {
            if (LocomotorOf(locomotor.Id) is null)
            {
                problems.Add(new RulesProblem(locomotor.Id, "", $"the engine has no locomotor '{locomotor.Id}'; it knows {KnownLocomotors()}"));
            }
        }

        for (var land = LandType.Clear; land <= LandType.Path; land++)
        {
            var id = LandSpeeds.IdOf(land);
            if (!database.TryGetTerrain(id, out var terrain))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs land type '{id}', which is not defined"));
                continue;
            }

            var code = TestMapParser.CodeOf(land);
            if (terrain.Code != code)
            {
                problems.Add(new RulesProblem(id, "code", $"'{id}' is written '{code}' in map files, so its code must be \"{code}\""));
            }

            foreach (var speed in terrain.Speed)
            {
                if (PercentOf(speed.Multiplier) > 255)
                {
                    problems.Add(new RulesProblem(id, "speed", $"the speed of '{speed.Locomotor}' on '{id}' must be 255% at most"));
                }
            }
        }

        foreach (var type in UnitTypes.All)
        {
            var id = UnitTypes.IdOf(type);
            if (!database.TryGetUnit(id, out var unit))
            {
                problems.Add(new RulesProblem("", "", $"the engine needs unit '{id}', which is not defined"));
            }
            else if (LocomotorOf(unit.Locomotor) is null)
            {
                problems.Add(new RulesProblem(id, "locomotor", $"the engine has no locomotor '{unit.Locomotor}'; it knows {KnownLocomotors()}"));
            }
        }

        return problems;
    }

    /// <summary>
    /// The weapons <paramref name="unit"/> fires now: its type's, or — an IFV (stage 3d) — the weapon its first passenger gives it, in the
    /// first place.
    /// </summary>
    public ImmutableArray<int> WeaponsOf(Unit unit)
    {
        // Dug in (the Rifleman behind sandbags), it fires its deployed weapon only.
        if (unit.Deployed && unit.Archetype.DeployedWeapon >= 0)
        {
            return _single[unit.Archetype.DeployedWeapon];
        }

        if (unit.Archetype.Has(UnitTalents.PassengerWeapon) && unit.Passengers.Count > 0 && Unit(unit.Passengers[0].Type).IfvWeapon is >= 0 and var given)
        {
            return _single[given];
        }

        return WeaponsOf(unit.Type);
    }

    /// <summary>The armor of a shot of <paramref name="weapon"/> that can be brought down (stage 3c); the first armor without one.</summary>
    public int ArmorOfShot(int weapon) =>
        Database.TryGetProjectile(Database.Weapons[weapon].Projectile, out var shot) ? IntMath.Max(0, Database.IndexOfArmor(shot.Armor)) : 0;

    private static UnitTalents TalentsOf(UnitDef unit) =>
        (unit.Engineer ? UnitTalents.Engineer : 0) | (unit.Infiltrator ? UnitTalents.Infiltrator : 0)
        | (unit.Disguised ? UnitTalents.Disguised : 0) | (unit.DetectsDisguise ? UnitTalents.DetectsDisguise : 0)
        | (unit.Passive ? UnitTalents.Passive : 0) | (unit.Teleports ? UnitTalents.Teleports : 0) | (unit.TeleportsHome ? UnitTalents.TeleportsHome : 0)
        | (unit.RadiationImmune ? UnitTalents.RadiationImmune : 0) | (unit.DisguiseWhenStill ? UnitTalents.StillDisguise : 0)
        | (unit.PassengerWeapon ? UnitTalents.PassengerWeapon : 0)
        | (unit.Submerged ? UnitTalents.Submerged : 0) | (unit.Spawned ? UnitTalents.Spawned : 0)
        | (unit.Trainable ? 0 : UnitTalents.Untrainable)
        | (unit.PsionicImmune ? UnitTalents.PsionicImmune : 0) | (unit.DeployOnce ? UnitTalents.DeployOnce : 0)
        | (unit.ChargesCoils ? UnitTalents.ChargesCoils : 0) | (unit.Occupier ? UnitTalents.Occupier : 0);

    private static Locomotor? LocomotorOf(string id)
    {
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            if (string.Equals(LandSpeeds.IdOf(locomotor), id, StringComparison.Ordinal))
            {
                return locomotor;
            }
        }

        return null;
    }

    private static string KnownLocomotors()
    {
        var ids = new List<string>(LandSpeeds.LocomotorCount);
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            ids.Add(LandSpeeds.IdOf(locomotor));
        }

        return string.Join(", ", ids);
    }

    /// <summary>The multiplier of a locomotor on a land type; zero when the land type does not list it.</summary>
    private static Fixed SpeedOn(TerrainDef terrain, string locomotor)
    {
        foreach (var speed in terrain.Speed)
        {
            if (speed.Locomotor == locomotor)
            {
                return speed.Multiplier;
            }
        }

        return Fixed.Zero;
    }

    /// <summary>A fraction as whole percent, rounded to the nearest: 0.9 (written <c>90%</c>) is 90.</summary>
    private static int PercentOf(Fixed fraction) =>
        (int)IntMath.Clamp(IntMath.DivRound(fraction.Raw * 100, Fixed.OneRaw), int.MinValue, int.MaxValue);
}
