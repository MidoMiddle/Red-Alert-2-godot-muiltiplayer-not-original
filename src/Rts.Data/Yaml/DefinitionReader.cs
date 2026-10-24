using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// Reads a resolved entry as a definition of its kind (docs/implementation/M3_base_economy.md, 4.1, 4.2). The <c>kind</c> names the schema:
/// the keys it allows and the fields it requires. For a template (<c>complete</c> false) the result is a placeholder that the loader does
/// not keep, but every field that is written is still checked.
/// </summary>
internal static class DefinitionReader
{
    private static readonly string[] Kinds = ["unit", "building", "weapon", "locomotor", "terrain", "faction", "economy", "armor", "warhead", "projectile", "build_order", "army", "difficulty", "personality"];

    private static readonly string[] UnitKeys =
    [
        "name", "faction", "category", "cost", "build_time", "hp", "armor", "speed", "turn_rate", "sight", "locomotor",
        "placement", "turret", "turret_turn_rate", "threat", "stance", "crusher", "crushable", "prerequisites", "primary", "secondary", "model", "icon", "harvest_capacity", "harvest_time", "unload_rate", "available_later", "ability_later", "jumpjet",
        "engineer", "infiltrator", "disguised", "detects_disguise", "passive", "death_weapon", "teleports", "deploy_weapon", "radiation_immune", "disguise_when_still",
        "passengers", "passenger_size", "size", "passenger_weapon", "ifv_weapon", "robotic", "submerged", "sensors",
    ];

    private static readonly string[] BuildingKeys =
    [
        "name", "faction", "category", "cost", "build_time", "hp", "footprint", "power", "build_up", "exit_cell", "adjacent", "extends_base", "produces", "dock_cell", "radar", "armor", "primary", "threat", "wall", "powered", "sight",
        "prerequisites", "model", "icon", "ability_later", "water", "repairs_vehicles", "vehicle_discount", "income_bonus", "death_weapon",
        "reveals_map", "veil_radius", "garrison", "chain_bonus", "chain_max", "capturable", "spy_effect", "controls_robots",
    ];

    private static readonly string[] WeaponKeys =
    [
        "name", "damage", "reload", "range", "min_range", "projectile", "warhead", "burst", "burst_delay", "targets", "report", "suicide", "fragments", "fragment_weapon", "fragment_range",
    ];

    private static readonly string[] ArmorKeys = ["name"];

    private static readonly string[] WarheadKeys = ["name", "verses", "spread", "percent_at_max", "wall_damage", "affects_air", "temporal", "bomb", "bomb_delay", "radiation", "parasite", "parasite_damage", "parasite_delay", "parasite_naval", "paralyzes", "infantry_death"];

    private static readonly string[] ProjectileKeys = ["name", "motion", "speed", "arc", "inaccuracy", "turn_rate", "lifetime", "visual", "hp", "armor"];

    private static readonly string[] LocomotorKeys = ["name", "slope"];

    private static readonly string[] TerrainKeys = ["name", "code", "buildable", "speed"];

    private static readonly string[] FactionKeys = ["name", "mcv", "construction_yard", "refinery", "harvester", "starting_infantry", "default"];

    private static readonly string[] BuildOrderKeys = ["faction", "steps"];

    private static readonly string[] ArmyKeys = ["faction", "shares", "against_infantry", "against_vehicles"];

    private static readonly string[] PersonalityKeys =
    [
        "name", "weights", "armies", "build_orders", "first_attack", "wave_interval", "retreat_threshold", "target_priority",
    ];

    private static readonly string[] WeightKeys = ["economy", "defense", "army", "tech", "harassment"];

    private static readonly string[] QualityKeys = ["simple", "standard", "optimized"];

    private static readonly string[] EconomyKeys =
    [
        "name", "low_power_min_rate", "multiple_factory_bonus", "max_factory_rate", "ore_value", "gem_value", "ore_growth_interval",
        "ore_growth_chance", "ore_spread_stage", "ore_spread_chance", "source_interval", "source_radius", "sell_refund", "repair_cost",
        "repair_rate",
    ];

    /// <summary>The kind of an entry, one of the known kinds.</summary>
    public static string KindOf(RawDefinition definition)
    {
        var field = definition.Fields["kind"];
        return Scalars.Choice(field.FileName, field.Value, Kinds);
    }

    /// <summary>Whether the entry is a template: <c>abstract: true</c>. A template creates no definition.</summary>
    public static bool IsTemplate(RawDefinition definition) =>
        definition.Fields.TryGetValue("abstract", out var field) && Scalars.Flag(field.FileName, field.Value);

    public static UnitDef Unit(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, UnitKeys);
        return new UnitDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Faction = fields.Required("faction", Scalars.Id, ""),
            Category = fields.Required("category", UnitCategoryOf, UnitCategory.Infantry),
            Cost = fields.Required("cost", Scalars.Int, 0),
            BuildTime = fields.Required("build_time", Scalars.Ticks, 0),
            Hp = fields.Required("hp", Scalars.Int, 0),
            Armor = fields.Optional("armor", Scalars.Id, ""),
            Speed = fields.Required("speed", Scalars.Int, 0),
            TurnRate = fields.Required("turn_rate", Scalars.Int, 0),
            TurretTurnRate = fields.Maybe("turret_turn_rate", Scalars.Int),
            Sight = fields.Required("sight", Scalars.Int, 0),
            Locomotor = fields.Required("locomotor", Scalars.Id, ""),
            Placement = fields.Required("placement", PlacementOf, UnitPlacement.Cell),
            Turret = fields.Optional("turret", Scalars.Flag, false),
            Prerequisites = fields.Optional("prerequisites", Scalars.IdList, ImmutableArray<string>.Empty),
            Primary = fields.Optional("primary", Scalars.Id, ""),
            Secondary = fields.Optional("secondary", Scalars.Id, ""),
            Threat = fields.Optional("threat", Scalars.Int, 0),
            Stance = fields.Optional("stance", StanceOf, Stance.Aggressive),
            Crusher = fields.Optional("crusher", Scalars.Flag, false),
            Crushable = fields.Optional("crushable", Scalars.Flag, false),
            Jumpjet = fields.Optional("jumpjet", Scalars.Flag, false),
            Engineer = fields.Optional("engineer", Scalars.Flag, false),
            Infiltrator = fields.Optional("infiltrator", Scalars.Flag, false),
            Disguised = fields.Optional("disguised", Scalars.Flag, false),
            DetectsDisguise = fields.Optional("detects_disguise", Scalars.Flag, false),
            Passive = fields.Optional("passive", Scalars.Flag, false),
            DeathWeapon = fields.Optional("death_weapon", Scalars.Id, ""),
            Teleports = fields.Optional("teleports", Scalars.Flag, false),
            DeployWeapon = fields.Optional("deploy_weapon", Scalars.Id, ""),
            RadiationImmune = fields.Optional("radiation_immune", Scalars.Flag, false),
            DisguiseWhenStill = fields.Optional("disguise_when_still", Scalars.Flag, false),
            PassengerSlots = fields.Optional("passengers", Scalars.Int, 0),
            PassengerSizeLimit = fields.Optional("passenger_size", Scalars.Int, 0),
            Size = fields.Optional("size", Scalars.Int, 0),
            PassengerWeapon = fields.Optional("passenger_weapon", Scalars.Flag, false),
            IfvWeapon = fields.Optional("ifv_weapon", Scalars.Id, ""),
            Robotic = fields.Optional("robotic", Scalars.Flag, false),
            Submerged = fields.Optional("submerged", Scalars.Flag, false),
            Sensors = fields.Optional("sensors", Scalars.Int, 0),
            Model = fields.Optional("model", Scalars.Path, ""),
            Icon = fields.Optional("icon", Scalars.Path, ""),
            HarvestCapacity = fields.Optional("harvest_capacity", Scalars.Int, 0),
            HarvestTime = fields.Optional("harvest_time", Scalars.Ticks, 0),
            UnloadRate = fields.Optional("unload_rate", Scalars.Int, 0),
            AvailableLater = fields.Optional("available_later", Scalars.Flag, false),
            AbilityLater = fields.Optional("ability_later", Scalars.Flag, false),
        };
    }

    public static BuildingDef Building(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, BuildingKeys);
        return new BuildingDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Faction = fields.Required("faction", Scalars.Id, ""),
            Category = fields.Required("category", BuildingCategoryOf, ProductionCategory.Building),
            Cost = fields.Required("cost", Scalars.Int, 0),
            BuildTime = fields.Required("build_time", Scalars.Ticks, 0),
            Hp = fields.Required("hp", Scalars.Int, 0),
            Footprint = fields.Required("footprint", Scalars.Size, new CellSize(1, 1)),
            Power = fields.Optional("power", Scalars.Int, 0),
            BuildUp = fields.Optional("build_up", Scalars.Ticks, 0),
            ExitCell = fields.Maybe("exit_cell", Scalars.Cell),
            DockCell = fields.Maybe("dock_cell", Scalars.Cell),
            Adjacent = fields.Optional("adjacent", Scalars.Int, BuildingDef.DefaultAdjacent),
            ExtendsBase = fields.Optional("extends_base", Scalars.Flag, true),
            Wall = fields.Optional("wall", Scalars.Flag, false),
            Powered = fields.Optional("powered", Scalars.Flag, false),
            Sight = fields.Optional("sight", Scalars.Int, 0),
            Radar = fields.Optional("radar", Scalars.Flag, false),
            Armor = fields.Optional("armor", Scalars.Id, ""),
            Primary = fields.Optional("primary", Scalars.Id, ""),
            Threat = fields.Optional("threat", Scalars.Int, 0),
            Produces = fields.Optional("produces", CategoriesOf, ImmutableArray<ProductionCategory>.Empty),
            Prerequisites = fields.Optional("prerequisites", Scalars.IdList, ImmutableArray<string>.Empty),
            Model = fields.Optional("model", Scalars.Path, ""),
            Icon = fields.Optional("icon", Scalars.Path, ""),
            AbilityLater = fields.Optional("ability_later", Scalars.Flag, false),
            Water = fields.Optional("water", Scalars.Flag, false),
            RepairsVehicles = fields.Optional("repairs_vehicles", Scalars.Flag, false),
            VehicleDiscountPercent = WholePercent(fields.Optional("vehicle_discount", Scalars.Percent, Fixed.Zero)),
            IncomeBonusPercent = WholePercent(fields.Optional("income_bonus", Scalars.Percent, Fixed.Zero)),
            DeathWeapon = fields.Optional("death_weapon", Scalars.Id, ""),
            RevealsMap = fields.Optional("reveals_map", Scalars.Flag, false),
            VeilRadius = fields.Optional("veil_radius", Scalars.Int, 0),
            GarrisonCapacity = fields.Optional("garrison", Scalars.Int, 0),
            ChainBonusPercent = WholePercent(fields.Optional("chain_bonus", Scalars.Percent, Fixed.Zero)),
            ChainMax = fields.Optional("chain_max", Scalars.Int, 0),
            Capturable = fields.Optional("capturable", Scalars.Flag, true),
            SpyEffect = fields.Optional("spy_effect", SpyEffectOf, SpyEffect.None),
            ControlsRobots = fields.Optional("controls_robots", Scalars.Flag, false),
        };
    }

    public static WeaponDef Weapon(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, WeaponKeys);
        return new WeaponDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Damage = fields.Required("damage", Scalars.Int, 0),
            Reload = fields.Required("reload", Scalars.Ticks, 0),
            Range = fields.Required("range", Scalars.FixedNumber, Fixed.Zero),
            MinRange = fields.Optional("min_range", Scalars.FixedNumber, Fixed.Zero),
            Projectile = fields.Required("projectile", Scalars.Id, ""),
            Warhead = fields.Required("warhead", Scalars.Id, ""),
            Burst = fields.Optional("burst", Scalars.Int, 1),
            BurstDelay = fields.Optional("burst_delay", Scalars.Ticks, 0),
            Targets = fields.Optional("targets", TargetsOf, WeaponTargets.Ground),
            Report = fields.Optional("report", Scalars.Id, ""),
            Suicide = fields.Optional("suicide", Scalars.Flag, false),
            Fragments = fields.Optional("fragments", Scalars.Int, 0),
            FragmentWeapon = fields.Optional("fragment_weapon", Scalars.Id, ""),
            FragmentRange = fields.Optional("fragment_range", Scalars.FixedNumber, Fixed.Zero),
        };
    }

    public static ArmorDef Armor(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, ArmorKeys);
        return new ArmorDef { Id = definition.Id, Name = fields.Required("name", Scalars.Text, "") };
    }

    public static WarheadDef Warhead(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, WarheadKeys);
        return new WarheadDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Verses = fields.Required("verses", Scalars.ArmorPercents, ImmutableArray<ArmorPercent>.Empty),
            Spread = fields.Optional("spread", Scalars.FixedNumber, Fixed.Zero),
            PercentAtMax = fields.Optional("percent_at_max", Scalars.Percent, Fixed.One),
            WallDamage = fields.Optional("wall_damage", Scalars.Flag, false),
            AffectsAir = fields.Optional("affects_air", Scalars.Flag, false),
            Temporal = fields.Optional("temporal", Scalars.Flag, false),
            Bomb = fields.Optional("bomb", Scalars.Id, ""),
            BombDelay = fields.Optional("bomb_delay", Scalars.Ticks, 0),
            Radiation = fields.Optional("radiation", Scalars.Int, 0),
            Parasite = fields.Optional("parasite", Scalars.Flag, false),
            ParasiteDamage = fields.Optional("parasite_damage", Scalars.Int, 0),
            ParasiteDelay = fields.Optional("parasite_delay", Scalars.Ticks, 0),
            ParasiteNaval = fields.Optional("parasite_naval", Scalars.Flag, false),
            Paralyzes = fields.Optional("paralyzes", Scalars.Flag, false),
            InfantryDeath = fields.Optional("infantry_death", Scalars.Id, ""),
        };
    }

    public static ProjectileDef Projectile(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, ProjectileKeys);
        return new ProjectileDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Motion = fields.Required("motion", MotionOf, ProjectileMotion.Instant),
            Speed = fields.Optional("speed", Scalars.Int, 0),
            Arc = fields.Optional("arc", Scalars.FixedNumber, Fixed.Zero),
            Inaccuracy = fields.Optional("inaccuracy", Scalars.FixedNumber, Fixed.Zero),
            TurnRate = fields.Optional("turn_rate", Scalars.Int, 0),
            Lifetime = fields.Optional("lifetime", Scalars.Ticks, 0),
            Visual = fields.Optional("visual", Scalars.Id, ""),
            Hp = fields.Optional("hp", Scalars.Int, 0),
            Armor = fields.Optional("armor", Scalars.Id, ""),
        };
    }

    public static LocomotorDef Locomotor(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, LocomotorKeys);
        return new LocomotorDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Slope = fields.Required("slope", Scalars.Percent, Fixed.Zero),
        };
    }

    public static TerrainDef Terrain(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, TerrainKeys);
        return new TerrainDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Code = fields.Required("code", Scalars.Code, ' '),
            Buildable = fields.Optional("buildable", Scalars.Flag, false),
            Speed = fields.Required("speed", Scalars.Speeds, ImmutableArray<LocomotorSpeed>.Empty),
        };
    }

    public static FactionDef Faction(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, FactionKeys);
        return new FactionDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Mcv = fields.Required("mcv", Scalars.Id, ""),
            ConstructionYard = fields.Required("construction_yard", Scalars.Id, ""),
            Refinery = fields.Required("refinery", Scalars.Id, ""),
            Harvester = fields.Required("harvester", Scalars.Id, ""),
            StartingInfantry = fields.Optional("starting_infantry", Scalars.Id, ""),
            IsDefault = fields.Optional("default", Scalars.Flag, false),
        };
    }

    public static ArmyDef Army(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, ArmyKeys);
        return new ArmyDef
        {
            Id = definition.Id,
            Faction = fields.Required("faction", Scalars.Id, ""),
            Shares = fields.Required("shares", Scalars.UnitShares, ImmutableArray<UnitShare>.Empty),
            AgainstInfantry = fields.Optional("against_infantry", Scalars.IdList, ImmutableArray<string>.Empty),
            AgainstVehicles = fields.Optional("against_vehicles", Scalars.IdList, ImmutableArray<string>.Empty),
        };
    }

    /// <summary>
    /// A level of the bots (M5 step 14, M8 step 1): every value of <see cref="DifficultyDef.Keys"/>; a word of a choice that is not in
    /// <see cref="DifficultyDef.Choices"/> is an error at it that names the words there are.
    /// </summary>
    public static DifficultyDef Difficulty(RawDefinition definition, bool complete)
    {
        var keys = new List<string>();
        foreach (var (key, _) in DifficultyDef.Keys)
        {
            keys.Add(key);
        }

        var fields = new FieldReader(definition, complete, [.. keys]);
        var values = ImmutableSortedDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        foreach (var (key, kind) in DifficultyDef.Keys)
        {
            values[key] = kind switch
            {
                DifficultyValue.Percent => WholePercent(fields.Required(key, Scalars.Percent, Fixed.Zero)),
                DifficultyValue.Duration => fields.Required(key, Scalars.Ticks, 0),
                DifficultyValue.Choice => fields.Required(key, (fileName, node) => ChoiceIndex(fileName, node, DifficultyDef.Choices[key]), 0),
                _ => fields.Required(key, Scalars.Int, 0),
            };
        }

        return new DifficultyDef { Id = definition.Id, Values = values.ToImmutable() };
    }

    /// <summary>The place of a word in <paramref name="words"/>.</summary>
    private static int ChoiceIndex(string fileName, YamlNode node, ImmutableArray<string> words) =>
        words.IndexOf(Scalars.Choice(fileName, node, [.. words]));

    /// <summary>
    /// A personality of the bots (M8 step 1): its weights, its army and build orders for every faction, its attacks and its targets. A
    /// mapping that misses a key or has one it does not know is an error at that place.
    /// </summary>
    public static PersonalityDef Personality(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, PersonalityKeys);
        var (firstMin, firstMax) = fields.Required("first_attack", FirstAttackOf, (0, 0));
        return new PersonalityDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Weights = fields.Required("weights", WeightsOf, default),
            Armies = fields.Required("armies", ArmiesOf, ImmutableArray<FactionArmy>.Empty),
            BuildOrders = fields.Required("build_orders", BuildOrdersOf, ImmutableArray<FactionBuildOrders>.Empty),
            FirstAttackMin = firstMin,
            FirstAttackMax = firstMax,
            WaveInterval = fields.Required("wave_interval", Scalars.Ticks, 0),
            RetreatThreshold = fields.Required("retreat_threshold", Scalars.Percent, Fixed.Zero),
            TargetPriority = fields.Required("target_priority", TargetPriorityOf, ImmutableArray<string>.Empty),
        };
    }

    /// <summary><c>{ economy: 1, defense: 0.5, army: 1.5, tech: 1, harassment: 0.2 }</c>: every weight there, each a number.</summary>
    private static PersonalityWeights WeightsOf(string fileName, YamlNode node)
    {
        var weights = Mapping(fileName, node, "weights", WeightKeys, "{ economy: 1, defense: 1, army: 1, tech: 1, harassment: 1 }");
        Fixed Weight(string key) => Scalars.FixedNumber(fileName, weights[key]);
        return new PersonalityWeights(Weight("economy"), Weight("defense"), Weight("army"), Weight("tech"), Weight("harassment"));
    }

    /// <summary><c>{ min: 360s, max: 480s }</c>.</summary>
    private static (int Min, int Max) FirstAttackOf(string fileName, YamlNode node)
    {
        var window = Mapping(fileName, node, "first_attack", ["min", "max"], "{ min: 360s, max: 480s }");
        return (Scalars.Ticks(fileName, window["min"]), Scalars.Ticks(fileName, window["max"]));
    }

    /// <summary><c>{ union: standard_army }</c>: an army for every faction, sorted by faction.</summary>
    private static ImmutableArray<FactionArmy> ArmiesOf(string fileName, YamlNode node)
    {
        var armies = ImmutableArray.CreateBuilder<FactionArmy>();
        foreach (var (faction, value) in ByFaction(fileName, node, "{ union: standard_army }"))
        {
            armies.Add(new FactionArmy(faction, Scalars.Id(fileName, value)));
        }

        return armies.ToImmutable();
    }

    /// <summary><c>{ union: { simple: …, standard: …, optimized: … } }</c>: the three build orders for every faction, sorted by faction.</summary>
    private static ImmutableArray<FactionBuildOrders> BuildOrdersOf(string fileName, YamlNode node)
    {
        var orders = ImmutableArray.CreateBuilder<FactionBuildOrders>();
        foreach (var (faction, value) in ByFaction(fileName, node, "{ union: { simple: standard, standard: standard, optimized: standard } }"))
        {
            var byQuality = Mapping(fileName, value, $"the build orders of '{faction}'", QualityKeys, "{ simple: standard, standard: standard, optimized: standard }");
            orders.Add(new FactionBuildOrders(
                faction, Scalars.Id(fileName, byQuality["simple"]), Scalars.Id(fileName, byQuality["standard"]), Scalars.Id(fileName, byQuality["optimized"])));
        }

        return orders.ToImmutable();
    }

    /// <summary><c>[production, army, power, harvesters, defense, core]</c>: every target once, in the order a squad goes for them.</summary>
    private static ImmutableArray<string> TargetPriorityOf(string fileName, YamlNode node)
    {
        var example = $"[{string.Join(", ", PersonalityDef.Targets)}]";
        if (node is not YamlSequenceNode sequence)
        {
            throw RulesFormatException.At(fileName, node, $"expected a list of every target in order, such as {example}");
        }

        var targets = ImmutableArray.CreateBuilder<string>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            var target = Scalars.Choice(fileName, child, [.. PersonalityDef.Targets]);
            if (targets.Contains(target))
            {
                throw RulesFormatException.At(fileName, child, $"target '{target}' repeats");
            }

            targets.Add(target);
        }

        foreach (var target in PersonalityDef.Targets)
        {
            if (!targets.Contains(target))
            {
                throw RulesFormatException.At(fileName, node, $"target_priority leaves out '{target}': every one of {example} goes in once");
            }
        }

        return targets.MoveToImmutable();
    }

    /// <summary>A mapping with exactly <paramref name="keys"/>: the value of each by key.</summary>
    private static Dictionary<string, YamlNode> Mapping(string fileName, YamlNode node, string what, string[] keys, string example)
    {
        if (node is not YamlMappingNode mapping)
        {
            throw RulesFormatException.At(fileName, node, $"expected {what} as a mapping such as {example}");
        }

        var values = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var entry in mapping)
        {
            var key = Scalars.Text(fileName, entry.Key);
            if (Array.IndexOf(keys, key) < 0)
            {
                throw RulesFormatException.At(fileName, entry.Key, $"unknown key '{key}' in {what}; it has {string.Join(", ", keys)}");
            }

            values[key] = entry.Value;
        }

        foreach (var key in keys)
        {
            if (!values.ContainsKey(key))
            {
                throw RulesFormatException.At(fileName, node, $"'{key}' is missing from {what}");
            }
        }

        return values;
    }

    /// <summary>A mapping from faction ids, at least one, sorted by faction.</summary>
    private static List<(string Faction, YamlNode Value)> ByFaction(string fileName, YamlNode node, string example)
    {
        if (node is not YamlMappingNode { Children.Count: > 0 } mapping)
        {
            throw RulesFormatException.At(fileName, node, $"expected a mapping from factions such as {example}");
        }

        var factions = new SortedDictionary<string, YamlNode>(StringComparer.Ordinal);
        foreach (var entry in mapping)
        {
            var faction = Scalars.Id(fileName, entry.Key);
            if (!factions.TryAdd(faction, entry.Value))
            {
                throw RulesFormatException.At(fileName, entry.Key, $"faction '{faction}' repeats");
            }
        }

        var list = new List<(string Faction, YamlNode Value)>(factions.Count);
        foreach (var (faction, value) in factions)
        {
            list.Add((faction, value));
        }

        return list;
    }

    /// <summary>125% read as a fraction, 1.25, back to the whole percent, 125.</summary>
    private static int WholePercent(Fixed fraction) => (int)((fraction.Raw * 100 + (1L << (Fixed.FractionalBits - 1))) >> Fixed.FractionalBits);

    public static BuildOrderDef BuildOrder(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, BuildOrderKeys);
        return new BuildOrderDef
        {
            Id = definition.Id,
            Faction = fields.Required("faction", Scalars.Id, ""),
            Steps = fields.Required("steps", Scalars.IdList, ImmutableArray<string>.Empty),
        };
    }

    public static EconomyDef Economy(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, EconomyKeys);
        return new EconomyDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            LowPowerMinRate = fields.Required("low_power_min_rate", Scalars.Percent, Fixed.Zero),
            MultipleFactoryBonus = fields.Required("multiple_factory_bonus", Scalars.Percent, Fixed.Zero),
            MaxFactoryRate = fields.Required("max_factory_rate", Scalars.Percent, Fixed.Zero),
            OreValue = fields.Required("ore_value", Scalars.Int, 0),
            GemValue = fields.Required("gem_value", Scalars.Int, 0),
            OreGrowthInterval = fields.Required("ore_growth_interval", Scalars.Ticks, 0),
            OreGrowthChance = fields.Required("ore_growth_chance", Scalars.Percent, Fixed.Zero),
            OreSpreadStage = fields.Required("ore_spread_stage", Scalars.Int, 0),
            OreSpreadChance = fields.Required("ore_spread_chance", Scalars.Percent, Fixed.Zero),
            SourceInterval = fields.Required("source_interval", Scalars.Ticks, 0),
            SourceRadius = fields.Required("source_radius", Scalars.Int, 0),
            SellRefund = fields.Required("sell_refund", Scalars.Percent, Fixed.Zero),
            RepairCost = fields.Required("repair_cost", Scalars.Percent, Fixed.Zero),
            RepairRate = fields.Required("repair_rate", Scalars.Int, 0),
        };
    }

    /// <summary>A list of production categories, <c>[building, defense]</c>; each at most once.</summary>
    private static ImmutableArray<ProductionCategory> CategoriesOf(string fileName, YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw RulesFormatException.At(fileName, node, "expected a list of categories such as [infantry]");
        }

        var categories = ImmutableArray.CreateBuilder<ProductionCategory>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            var category = Scalars.Choice(fileName, child, "building", "defense", "infantry", "vehicle", "naval", "aircraft") switch
            {
                "building" => ProductionCategory.Building,
                "defense" => ProductionCategory.Defense,
                "infantry" => ProductionCategory.Infantry,
                "naval" => ProductionCategory.Naval,
                "aircraft" => ProductionCategory.Aircraft,
                _ => ProductionCategory.Vehicle,
            };
            if (categories.Contains(category))
            {
                throw RulesFormatException.At(fileName, child, $"category '{((YamlScalarNode)child).Value}' repeats");
            }

            categories.Add(category);
        }

        return categories.MoveToImmutable();
    }

    private static UnitCategory UnitCategoryOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "infantry", "vehicle", "naval", "aircraft") switch
        {
            "infantry" => UnitCategory.Infantry,
            "naval" => UnitCategory.Naval,
            "aircraft" => UnitCategory.Aircraft,
            _ => UnitCategory.Vehicle,
        };

    private static ProductionCategory BuildingCategoryOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "building", "defense") == "building" ? ProductionCategory.Building : ProductionCategory.Defense;

    private static Stance StanceOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "aggressive", "guard", "hold") switch
        {
            "aggressive" => Stance.Aggressive,
            "guard" => Stance.Guard,
            _ => Stance.HoldPosition,
        };

    private static ProjectileMotion MotionOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "instant", "ballistic", "homing") switch
        {
            "instant" => ProjectileMotion.Instant,
            "ballistic" => ProjectileMotion.Ballistic,
            _ => ProjectileMotion.Homing,
        };

    /// <summary>What a weapon fires at: a list of <c>ground</c>, <c>air</c>, <c>naval</c> and <c>underwater</c>, each once.</summary>
    private static WeaponTargets TargetsOf(string fileName, YamlNode node)
    {
        if (node is not YamlSequenceNode { Children.Count: > 0 } sequence)
        {
            throw RulesFormatException.At(fileName, node, "expected a list of targets such as [ground] or [ground, air]");
        }

        var targets = WeaponTargets.None;
        foreach (var child in sequence.Children)
        {
            var target = Scalars.Choice(fileName, child, "ground", "air", "naval", "underwater") switch
            {
                "ground" => WeaponTargets.Ground,
                "air" => WeaponTargets.Air,
                "naval" => WeaponTargets.Naval,
                _ => WeaponTargets.Underwater,
            };
            if ((targets & target) != 0)
            {
                throw RulesFormatException.At(fileName, child, $"target '{((YamlScalarNode)child).Value}' repeats");
            }

            targets |= target;
        }

        return targets;
    }

    private static SpyEffect SpyEffectOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "none", "blackout", "reset_map", "steal_credits") switch
        {
            "blackout" => SpyEffect.Blackout,
            "reset_map" => SpyEffect.ResetMap,
            "steal_credits" => SpyEffect.StealCredits,
            _ => SpyEffect.None,
        };

    private static UnitPlacement PlacementOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "cell", "subcell") == "cell" ? UnitPlacement.Cell : UnitPlacement.Subcell;
}
