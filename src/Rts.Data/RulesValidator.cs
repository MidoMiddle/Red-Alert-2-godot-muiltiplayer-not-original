using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Yaml;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;
using YamlDotNet.RepresentationModel;

namespace Rts.Data;

/// <summary>
/// Checks the meaning of rules that have been read (docs/implementation/M3_base_economy.md, 4.3): numbers in their ranges, references to
/// definitions that exist and have the right kind, no cycle of prerequisites, and every faction with its construction yard, MCV, refinery
/// and harvester. Since M4 step 1 also the combat: armor of every unit and building, weapons with their projectile and warhead, and
/// warheads with a share for every armor type. Every mistake is collected, at the place where its value is written: an inherited field points into the template, an id
/// in a list points at that id. The schema itself (unknown keys, missing fields, values that do not parse) and inheritance cycles are found
/// earlier, while the files are read; the assets are checked by the client, since the simulation does not need them.
/// </summary>
internal static class RulesValidator
{
    /// <summary>The largest side of a building footprint, in cells.</summary>
    public const int MaxFootprint = 6;

    /// <summary>The widest veil of a veil generator, in cells (RA2's gap generator: 10).</summary>
    public const int MaxVeilRadius = 20;

    /// <summary>The most soldiers a building holds.</summary>
    public const int MaxGarrison = 10;

    /// <summary>The most towers that support one shot.</summary>
    public const int MaxChain = 16;

    /// <param name="sources">The resolved entry of every definition in <paramref name="rules"/>, by id.</param>
    public static void Check(RulesDatabase rules, IReadOnlyDictionary<string, RawDefinition> sources, List<RulesError> errors)
    {
        var checker = new Checker(rules, sources, errors);
        foreach (var unit in rules.Units)
        {
            checker.Unit(unit);
        }

        foreach (var building in rules.Buildings)
        {
            checker.Building(building);
        }

        foreach (var weapon in rules.Weapons)
        {
            checker.Weapon(weapon);
        }

        foreach (var locomotor in rules.Locomotors)
        {
            checker.Locomotor(locomotor);
        }

        checker.Terrains();
        foreach (var faction in rules.Factions)
        {
            checker.Faction(faction);
        }

        foreach (var country in rules.Countries)
        {
            checker.Country(country);
        }

        foreach (var crates in rules.Crates)
        {
            checker.Crates(crates, rules.Crates.Length);
        }

        foreach (var economy in rules.Economies)
        {
            checker.Economy(economy);
        }

        foreach (var order in rules.BuildOrders)
        {
            checker.BuildOrder(order);
        }

        foreach (var army in rules.Armies)
        {
            checker.Army(army);
        }

        foreach (var difficulty in rules.Difficulties)
        {
            checker.Difficulty(difficulty);
        }

        foreach (var personality in rules.Personalities)
        {
            checker.Personality(personality);
        }

        foreach (var warhead in rules.Warheads)
        {
            checker.Warhead(warhead);
        }

        foreach (var projectile in rules.Projectiles)
        {
            checker.Projectile(projectile);
        }

        checker.PrerequisiteCycles();
    }

    /// <summary>
    /// An error at the value of <paramref name="key"/> in the definition <paramref name="id"/>; at its id when the key is empty or not
    /// written; for the whole data, without a place, when the id is empty.
    /// </summary>
    public static RulesError At(IReadOnlyDictionary<string, RawDefinition> sources, string id, string key, string reason)
    {
        if (id.Length == 0)
        {
            return new RulesError("", 0, 0, reason);
        }

        var source = sources[id];
        return source.Fields.TryGetValue(key, out var field)
            ? RulesFormatException.At(field.FileName, field.Value, reason).Error
            : RulesFormatException.At(source.FileName, source.IdNode, reason).Error;
    }

    private sealed class Checker(RulesDatabase rules, IReadOnlyDictionary<string, RawDefinition> sources, List<RulesError> errors)
    {
        public void Unit(UnitDef unit)
        {
            Positive(unit.Id, "cost", unit.Cost);
            Positive(unit.Id, "hp", unit.Hp);
            Positive(unit.Id, "build_time", unit.BuildTime);
            NotNegative(unit.Id, "speed", unit.Speed);
            NotNegative(unit.Id, "turn_rate", unit.TurnRate);
            NotNegative(unit.Id, "turret_turn_rate", unit.TurretTurnRate ?? 0);
            NotNegative(unit.Id, "threat", unit.Threat);
            if (unit.Crusher && unit.Placement != UnitPlacement.Cell)
            {
                Error(unit.Id, "crusher", $"'{unit.Id}' cannot crush: only a vehicle (placement: cell) crushes infantry");
            }

            if (unit.Crushable && unit.Placement != UnitPlacement.Subcell)
            {
                Error(unit.Id, "crushable", $"'{unit.Id}' cannot be crushed: only infantry (placement: subcell) can be");
            }

            if (unit.Jumpjet && (unit.Category != UnitCategory.Infantry || unit.Crushable))
            {
                Error(unit.Id, "jumpjet", $"'{unit.Id}' cannot have a jet pack: only infantry that cannot be crushed flies with one");
            }

            NotNegative(unit.Id, "sight", unit.Sight);
            NotNegative(unit.Id, "harvest_capacity", unit.HarvestCapacity);
            if (unit.HarvestCapacity > 0)
            {
                Positive(unit.Id, "harvest_time", unit.HarvestTime);
                Positive(unit.Id, "unload_rate", unit.UnloadRate);
            }
            FactionOf(unit.Id, unit.Faction);
            CountriesOf(unit.Id, "countries", unit.Countries);
            CountriesOf(unit.Id, "forbidden_countries", unit.ForbiddenCountries);
            if (!rules.TryGetLocomotor(unit.Locomotor, out _))
            {
                Error(unit.Id, "locomotor", Missing(unit.Id, "locomotor", unit.Locomotor));
            }

            ArmorOf(unit.Id, unit.Armor);
            WeaponOf(unit.Id, "primary", unit.Primary);
            WeaponOf(unit.Id, "secondary", unit.Secondary);
            WeaponOf(unit.Id, "death_weapon", unit.DeathWeapon);
            WeaponOf(unit.Id, "occupy_weapon", unit.OccupyWeapon);
            WeaponOf(unit.Id, "deploy_weapon", unit.DeployWeapon);
            WeaponOf(unit.Id, "deployed_weapon", unit.DeployedWeapon);
            WeaponOf(unit.Id, "ifv_weapon", unit.IfvWeapon);
            NotNegative(unit.Id, "passengers", unit.PassengerSlots);
            NotNegative(unit.Id, "size", unit.Size);
            NotNegative(unit.Id, "sensors", unit.Sensors);
            NotNegative(unit.Id, "ammo", unit.Ammo);
            NotNegative(unit.Id, "self_heal", unit.SelfHeal);
            if (unit.Spawns.Length > 0)
            {
                if (!rules.TryGetUnit(unit.Spawns, out _))
                {
                    Error(unit.Id, "spawns", $"'{unit.Id}' carries '{unit.Spawns}', which is no unit");
                }

                if (unit.SpawnCount <= 0 || unit.SpawnRegen <= 0)
                {
                    Error(unit.Id, "spawn_count", $"'{unit.Id}' carries units: 'spawn_count' and 'spawn_regen' must be above zero");
                }
            }
            if (unit.Submerged && unit.Category != UnitCategory.Naval)
            {
                Error(unit.Id, "submerged", $"'{unit.Id}' cannot keep under the water: only a ship can");
            }
            if (unit.PassengerSlots > 0 && unit.PassengerSizeLimit <= 0)
            {
                Error(unit.Id, "passenger_size", $"'{unit.Id}' carries passengers but takes none: 'passenger_size' must be above zero");
            }
            foreach (var (key, has) in new[] { ("engineer", unit.Engineer), ("infiltrator", unit.Infiltrator), ("teleports", unit.Teleports) })
            {
                if (has && unit.Category != UnitCategory.Infantry)
                {
                    Error(unit.Id, key, $"'{key}' of '{unit.Id}' is for infantry only");
                }
            }
            if (unit.Secondary.Length > 0 && unit.Primary.Length == 0)
            {
                Error(unit.Id, "secondary", $"'{unit.Id}' has a secondary weapon but no primary one");
            }

            Prerequisites(unit.Id, unit.Prerequisites);
        }

        public void Building(BuildingDef building)
        {
            Positive(building.Id, "cost", building.Cost);
            Positive(building.Id, "hp", building.Hp);
            Positive(building.Id, "build_time", building.BuildTime);
            NotNegative(building.Id, "build_up", building.BuildUp);
            NotNegative(building.Id, "threat", building.Threat);
            NotNegative(building.Id, "sight", building.Sight);
            if (building.Adjacent is < 0 or > BuildingDef.MaxAdjacent)
            {
                Error(building.Id, "adjacent", $"'adjacent' of '{building.Id}' must be 0 to {BuildingDef.MaxAdjacent} cells");
            }

            if (building.Wall && (building.Footprint != new CellSize(1, 1) || building.ExtendsBase))
            {
                Error(building.Id, "wall", $"wall '{building.Id}' must have a footprint of [1, 1] and extends_base: false");
            }
            var (width, height) = (building.Footprint.Width, building.Footprint.Height);
            if (width is < 1 or > MaxFootprint || height is < 1 or > MaxFootprint)
            {
                Error(
                    building.Id,
                    "footprint",
                    $"the footprint of '{building.Id}' must be 1 to {MaxFootprint} cells a side, not [{width}, {height}]");
            }

            if (building.ExitCell is { } exit && (exit.X < -1 || exit.X > width || exit.Y < -1 || exit.Y > height))
            {
                Error(
                    building.Id,
                    "exit_cell",
                    $"the exit cell [{exit.X}, {exit.Y}] of '{building.Id}' must be in its footprint or next to it");
            }

            if (building.DockCell is { } dock && (dock.X < -1 || dock.X > width || dock.Y < -1 || dock.Y > height))
            {
                Error(
                    building.Id,
                    "dock_cell",
                    $"the dock cell [{dock.X}, {dock.Y}] of '{building.Id}' must be in its footprint or next to it");
            }

            FactionOf(building.Id, building.Faction);
            CountriesOf(building.Id, "countries", building.Countries);
            CountriesOf(building.Id, "forbidden_countries", building.ForbiddenCountries);
            CountriesOf(building.Id, "superweapon_countries", building.SuperweaponCountries);
            if (building.Paradrop.Length > 0 && !rules.TryGetUnit(building.Paradrop, out _))
            {
                Error(building.Id, "paradrop", Missing(building.Id, "unit", building.Paradrop));
            }
            Prerequisites(building.Id, building.Prerequisites);
            ArmorOf(building.Id, building.Armor);
            WeaponOf(building.Id, "primary", building.Primary);
            WeaponOf(building.Id, "death_weapon", building.DeathWeapon);
            WeaponOf(building.Id, "overcharge_weapon", building.OverchargeWeapon);
            WeaponOf(building.Id, "superweapon_weapon", building.SuperweaponWeapon);
            NotNegative(building.Id, "build_limit", building.BuildLimit);
            if (building.Superweapon != SuperweaponKind.None && building.Recharge <= 0)
            {
                Error(building.Id, "recharge", $"the superweapon '{building.Id}' needs a 'recharge' above zero");
            }

            if (building.Superweapon is SuperweaponKind.Missile or SuperweaponKind.Storm && building.SuperweaponWeapon.Length == 0)
            {
                Error(building.Id, "superweapon_weapon", $"the superweapon '{building.Id}' needs its 'superweapon_weapon'");
            }

            if (building.IncomeBonusPercent is < 0 or > 100)
            {
                Error(building.Id, "income_bonus", $"'income_bonus' of '{building.Id}' must be 0% to 100%");
            }

            if (building.VeilRadius is < 0 or > MaxVeilRadius)
            {
                Error(building.Id, "veil_radius", $"'veil_radius' of '{building.Id}' must be 0 to {MaxVeilRadius} cells");
            }

            if (building.GarrisonCapacity is < 0 or > MaxGarrison)
            {
                Error(building.Id, "garrison", $"'garrison' of '{building.Id}' must be 0 to {MaxGarrison} soldiers");
            }

            if (building.ChainBonusPercent is < 0 or > 200)
            {
                Error(building.Id, "chain_bonus", $"'chain_bonus' of '{building.Id}' must be 0% to 200%");
            }

            if (building.ChainMax is < 0 or > MaxChain)
            {
                Error(building.Id, "chain_max", $"'chain_max' of '{building.Id}' must be 0 to {MaxChain} towers");
            }

            if (building.ChainBonusPercent > 0 && building.Primary.Length == 0)
            {
                Error(building.Id, "chain_bonus", $"'{building.Id}' supports the shots of its kind but has no 'primary' weapon");
            }
        }

        /// <summary>
        /// Damage at least 0, a reload and a range above 0, the nearest range below the range, a volley of at least one shot; the projectile
        /// and the warhead exist.
        /// </summary>
        public void Weapon(WeaponDef weapon)
        {
            NotNegative(weapon.Id, "damage", weapon.Damage);
            NotNegative(weapon.Id, "fragments", weapon.Fragments);
            WeaponOf(weapon.Id, "fragment_weapon", weapon.FragmentWeapon);
            if (weapon.Fragments > 0 && (weapon.FragmentWeapon.Length == 0 || weapon.FragmentRange <= Fixed.Zero))
            {
                Error(weapon.Id, "fragments", $"the splinters of '{weapon.Id}' need a 'fragment_weapon' and a 'fragment_range' above zero");
            }

            Positive(weapon.Id, "reload", weapon.Reload);
            if (weapon.Range <= Fixed.Zero)
            {
                Error(weapon.Id, "range", $"'range' of '{weapon.Id}' must be above zero");
            }

            if (weapon.MinRange < Fixed.Zero || (weapon.Range > Fixed.Zero && weapon.MinRange >= weapon.Range))
            {
                Error(weapon.Id, "min_range", $"'min_range' of '{weapon.Id}' must be 0 or more and below its range");
            }

            Positive(weapon.Id, "burst", weapon.Burst);
            NotNegative(weapon.Id, "burst_delay", weapon.BurstDelay);
            if (!rules.TryGetProjectile(weapon.Projectile, out _))
            {
                Error(weapon.Id, "projectile", Missing(weapon.Id, "projectile", weapon.Projectile));
            }

            if (!rules.TryGetWarhead(weapon.Warhead, out _))
            {
                Error(weapon.Id, "warhead", Missing(weapon.Id, "warhead", weapon.Warhead));
            }
        }

        /// <summary>A share of 0 to 1000 % for known armor, one for every armor type; the area and the share at its edge in range.</summary>
        public void Warhead(WarheadDef warhead)
        {
            var thousand = Fixed.FromInt(10);
            foreach (var verse in warhead.Verses)
            {
                if (!rules.TryGetArmor(verse.Armor, out _))
                {
                    MapKeyError(warhead.Id, "verses", verse.Armor, Missing(warhead.Id, "armor", verse.Armor));
                }
                else if (verse.Percent < Fixed.Zero || verse.Percent > thousand)
                {
                    MapKeyError(warhead.Id, "verses", verse.Armor, $"the share against '{verse.Armor}' of '{warhead.Id}' must be 0% to 1000%");
                }
            }

            foreach (var armor in rules.Armors)
            {
                if (!warhead.Verses.Any(verse => verse.Armor == armor.Id))
                {
                    Error(warhead.Id, "verses", $"the verses of '{warhead.Id}' have no share against armor '{armor.Id}'");
                }
            }

            if (warhead.Spread < Fixed.Zero)
            {
                Error(warhead.Id, "spread", $"'spread' of '{warhead.Id}' must not be negative");
            }

            if (warhead.PercentAtMax < Fixed.Zero || warhead.PercentAtMax > Fixed.One)
            {
                Error(warhead.Id, "percent_at_max", $"'percent_at_max' of '{warhead.Id}' must be 0% to 100%");
            }

            WeaponOf(warhead.Id, "bomb", warhead.Bomb);
            if (warhead.Bomb.Length > 0 && warhead.BombDelay <= 0)
            {
                Error(warhead.Id, "bomb_delay", $"the bomb of '{warhead.Id}' needs a 'bomb_delay' above 0");
            }

            NotNegative(warhead.Id, "radiation", warhead.Radiation);
            if (warhead.Parasite && (warhead.ParasiteDamage <= 0 || warhead.ParasiteDelay <= 0))
            {
                Error(warhead.Id, "parasite", $"the parasite of '{warhead.Id}' needs a 'parasite_damage' and a 'parasite_delay' above zero");
            }
        }

        /// <summary>Nothing negative; a projectile that flies needs a speed, a homing one also a turn rate and a lifetime.</summary>
        public void Projectile(ProjectileDef projectile)
        {
            NotNegative(projectile.Id, "speed", projectile.Speed);
            NotNegative(projectile.Id, "hp", projectile.Hp);
            if (projectile.Hp > 0)
            {
                ArmorOf(projectile.Id, projectile.Armor);
            }

            NotNegative(projectile.Id, "turn_rate", projectile.TurnRate);
            NotNegative(projectile.Id, "lifetime", projectile.Lifetime);
            foreach (var (key, value) in new[] { ("arc", projectile.Arc), ("inaccuracy", projectile.Inaccuracy) })
            {
                if (value < Fixed.Zero)
                {
                    Error(projectile.Id, key, $"'{key}' of '{projectile.Id}' must not be negative");
                }
            }

            if (projectile.Motion != ProjectileMotion.Instant)
            {
                Positive(projectile.Id, "speed", projectile.Speed);
            }

            if (projectile.Motion == ProjectileMotion.Homing)
            {
                Positive(projectile.Id, "turn_rate", projectile.TurnRate);
                Positive(projectile.Id, "lifetime", projectile.Lifetime);
            }
        }

        /// <summary>
        /// The slowest rate under low power 0–100 %, the bonus of a factory at least 0, the fastest rate at least 100 %, a source
        /// erupts now and then within a radius of 1 to <see cref="OreSourceSystem.MaxRadius"/>.
        /// </summary>
        public void Economy(EconomyDef economy)
        {
            if (economy.LowPowerMinRate < Fixed.Zero || economy.LowPowerMinRate > Fixed.One)
            {
                Error(economy.Id, "low_power_min_rate", $"'low_power_min_rate' of '{economy.Id}' must be 0% to 100%");
            }

            if (economy.LowPowerMaxRate > Fixed.One
                || (economy.LowPowerMinRate >= Fixed.Zero && economy.LowPowerMinRate <= Fixed.One && economy.LowPowerMaxRate < economy.LowPowerMinRate))
            {
                Error(economy.Id, "low_power_max_rate", $"'low_power_max_rate' of '{economy.Id}' must be from 'low_power_min_rate' to 100%");
            }

            if (economy.MultipleFactory <= Fixed.Zero || economy.MultipleFactory > Fixed.One)
            {
                Error(economy.Id, "multiple_factory", $"'multiple_factory' of '{economy.Id}' must be more than 0% and at most 100%");
            }

            Positive(economy.Id, "ore_value", economy.OreValue);
            Positive(economy.Id, "gem_value", economy.GemValue);
            Positive(economy.Id, "ore_growth_interval", economy.OreGrowthInterval);
            Positive(economy.Id, "repair_rate", economy.RepairRate);
            Positive(economy.Id, "repair_delay", economy.RepairDelay);
            foreach (var (key, chance) in new[]
            {
                ("ore_growth_chance", economy.OreGrowthChance), ("ore_spread_chance", economy.OreSpreadChance),
                ("sell_refund", economy.SellRefund), ("repair_cost", economy.RepairCost),
            })
            {
                if (chance < Fixed.Zero || chance > Fixed.One)
                {
                    Error(economy.Id, key, $"'{key}' of '{economy.Id}' must be 0% to 100%");
                }
            }

            if (economy.OreSpreadStage is < 0 or > OreCell.MaxStage)
            {
                Error(economy.Id, "ore_spread_stage", $"'ore_spread_stage' of '{economy.Id}' must be 0 to {OreCell.MaxStage}");
            }

            Positive(economy.Id, "source_interval", economy.SourceInterval);
            if (economy.SourceRadius is < 1 or > OreSourceSystem.MaxRadius)
            {
                Error(economy.Id, "source_radius", $"'source_radius' of '{economy.Id}' must be 1 to {OreSourceSystem.MaxRadius}");
            }
        }

        public void Locomotor(LocomotorDef locomotor)
        {
            if (locomotor.Slope < Fixed.Zero || locomotor.Slope > Fixed.One)
            {
                Error(locomotor.Id, "slope", $"'slope' of '{locomotor.Id}' must be 0% to 100%");
            }
        }

        /// <summary>Speeds for known locomotors, at least zero, one for every locomotor; and no land code used twice.</summary>
        public void Terrains()
        {
            var codes = new SortedDictionary<char, string>();
            foreach (var terrain in rules.Terrains)
            {
                foreach (var speed in terrain.Speed)
                {
                    if (!rules.TryGetLocomotor(speed.Locomotor, out _))
                    {
                        SpeedError(terrain.Id, speed.Locomotor, Missing(terrain.Id, "locomotor", speed.Locomotor));
                    }
                    else if (speed.Multiplier < Fixed.Zero)
                    {
                        SpeedError(terrain.Id, speed.Locomotor, $"the speed of '{speed.Locomotor}' on '{terrain.Id}' must not be negative");
                    }
                }

                foreach (var locomotor in rules.Locomotors)
                {
                    if (!terrain.Speed.Any(speed => speed.Locomotor == locomotor.Id))
                    {
                        Error(terrain.Id, "speed", $"'{terrain.Id}' has no speed for locomotor '{locomotor.Id}'");
                    }
                }

                if (!codes.TryAdd(terrain.Code, terrain.Id))
                {
                    Error(terrain.Id, "code", $"code '{terrain.Code}' of '{terrain.Id}' is already used by '{codes[terrain.Code]}'");
                }
            }
        }

        /// <summary>A country belongs to a faction of the data (loose ends, stage 5).</summary>
        public void Country(CountryDef country) => FactionOf(country.Id, country.Faction);

        // The crates (M11 a6): one record; sane numbers; units of the ground that exist.
        public void Crates(CratesDef crates, int records)
        {
            if (records > 1)
            {
                Error(crates.Id, "kind", $"there is more than one 'crates' record ('{crates.Id}')");
            }

            Positive(crates.Id, "regen", crates.Regen);
            if (crates.PerPlayer < 0 || crates.AtLeast < 0 || crates.PerPlayer + crates.AtLeast == 0)
            {
                Error(crates.Id, "per_player", $"'per_player' and 'at_least' of '{crates.Id}' must not be negative, nor both 0");
            }

            if (crates.Radius is < 0 or > 16)
            {
                Error(crates.Id, "radius", $"'radius' of '{crates.Id}' must be 0 to 16");
            }

            if (crates.Money < 0 || crates.Weights.Any(weight => weight < 0) || crates.Weights.Sum() <= 0)
            {
                Error(crates.Id, "weight_money", $"the money and the weights of '{crates.Id}' must not be negative, and some weight above 0");
            }

            foreach (var (key, value) in new[] { ("armor", crates.Armor), ("firepower", crates.Firepower), ("speed", crates.Speed) })
            {
                if (value < Fixed.One || value > Fixed.FromInt(4))
                {
                    Error(crates.Id, key, $"'{key}' of '{crates.Id}' must be 100% to 400%");
                }
            }

            foreach (var id in crates.Units)
            {
                if (!rules.TryGetUnit(id, out var unit))
                {
                    Error(crates.Id, "units", Missing(crates.Id, "unit", id));
                }
                else if (unit.Category is UnitCategory.Naval or UnitCategory.Aircraft)
                {
                    Error(crates.Id, "units", $"'{crates.Id}' gives '{id}', which is not a unit of the ground");
                }
            }
        }

        /// <summary>The countries a unit or building names are countries of the data.</summary>
        private void CountriesOf(string id, string key, ImmutableArray<string> countries)
        {
            foreach (var country in countries)
            {
                if (!rules.TryGetCountry(country, out _))
                {
                    Error(id, key, Missing(id, key, country));
                }
            }
        }

        /// <summary>The faction's MCV and harvester are its units, its construction yard and refinery are its buildings.</summary>
        public void Faction(FactionDef faction)
        {
            UnitOf(faction.Id, "mcv", faction.Mcv);
            UnitOf(faction.Id, "harvester", faction.Harvester);
            BuildingOf(faction.Id, "construction_yard", faction.ConstructionYard);
            BuildingOf(faction.Id, "refinery", faction.Refinery);
            if (faction.StartingInfantry.Length > 0)
            {
                UnitOf(faction.Id, "starting_infantry", faction.StartingInfantry);
            }

            if (faction.Paradrop.Length > 0)
            {
                UnitOf(faction.Id, "paradrop", faction.Paradrop);
                Positive(faction.Id, "paradrop_count", faction.ParadropCount);
            }
        }

        /// <summary>
        /// A level of the bots (M5 step 14, M8 steps 1–2): every period, the actions a second and the squads at once are above zero, no
        /// other number is below zero, a chance, a share or a bonus is 100% at most, a squad turns back below a share of its strength under 100%, and
        /// every wave is at least as big as the one before. The words of a choice are checked as the file is read.
        /// </summary>
        public void Difficulty(DifficultyDef difficulty)
        {
            foreach (var (key, value) in difficulty.Values)
            {
                if (key.EndsWith("_period", StringComparison.Ordinal) || key is "actions_per_second" or "parallel_squads")
                {
                    Positive(difficulty.Id, key, value);
                }
                else if (value < 0)
                {
                    Error(difficulty.Id, key, $"'{key}' of '{difficulty.Id}' must not be below zero");
                }
                else if (value > 100 && key is "power_reserve" or "wounded" or "mistake_chance" or "counter_pick")
                {
                    Error(difficulty.Id, key, $"'{key}' of '{difficulty.Id}' is a share: 100% at most");
                }
                else if (value > 100 && key is "income_bonus" or "build_speed_bonus")
                {
                    Error(difficulty.Id, key, $"'{key}' of '{difficulty.Id}' would more than double: 100% at most");
                }
            }

            if (difficulty.Values.TryGetValue("retreat_at_strength", out var retreat) && retreat >= 100)
            {
                Error(difficulty.Id, "retreat_at_strength", $"'{difficulty.Id}' would turn back before it lost anything: retreat_at_strength must be below 100%");
            }

            if (difficulty.Values.TryGetValue("attack_growth_per_wave", out var growth) && growth < 100)
            {
                Error(difficulty.Id, "attack_growth_per_wave", $"the waves of '{difficulty.Id}' would shrink: attack_growth_per_wave must be 100% or more");
            }
        }

        /// <summary>
        /// A personality of the bots (M8 step 1): no weight below zero; for every faction of the data an army and three build orders, each in
        /// the data and of that faction, and none for a faction there is not; the first attack not later than its window allows; waves
        /// apart; a squad that turns back before it is outmatched completely.
        /// </summary>
        public void Personality(PersonalityDef personality)
        {
            var id = personality.Id;
            var weights = personality.Weights;
            foreach (var (key, weight) in new[]
            {
                ("economy", weights.Economy), ("defense", weights.Defense), ("army", weights.Army), ("tech", weights.Tech), ("harassment", weights.Harassment),
            })
            {
                if (weight < Fixed.Zero)
                {
                    PathError(id, "weights", [key], $"the weight '{key}' of '{id}' must not be below zero");
                }
            }

            foreach (var faction in rules.Factions)
            {
                if (!personality.Armies.Any(army => army.Faction == faction.Id))
                {
                    Error(id, "armies", $"'{id}' has no army for faction '{faction.Id}'");
                }

                if (!personality.BuildOrders.Any(orders => orders.Faction == faction.Id))
                {
                    Error(id, "build_orders", $"'{id}' has no build orders for faction '{faction.Id}'");
                }
            }

            foreach (var (faction, army) in personality.Armies)
            {
                if (!rules.TryGetFaction(faction, out _))
                {
                    PathError(id, "armies", [faction], Missing(id, "faction", faction), atValue: false);
                }
                else if (!rules.TryGetArmy(army, out var definition))
                {
                    PathError(id, "armies", [faction], Missing(id, "army", army));
                }
                else if (definition.Faction != faction && rules.TryGetFaction(definition.Faction, out _))
                {
                    PathError(id, "armies", [faction], $"'{id}' gives '{faction}' the army '{army}', which belongs to '{definition.Faction}'");
                }
            }

            foreach (var orders in personality.BuildOrders)
            {
                if (!rules.TryGetFaction(orders.Faction, out _))
                {
                    PathError(id, "build_orders", [orders.Faction], Missing(id, "faction", orders.Faction), atValue: false);
                    continue;
                }

                foreach (var (quality, order) in new[] { ("simple", orders.Simple), ("standard", orders.Standard), ("optimized", orders.Optimized) })
                {
                    if (!rules.TryGetBuildOrder(order, out var definition))
                    {
                        PathError(id, "build_orders", [orders.Faction, quality], Missing(id, "build order", order));
                    }
                    else if (definition.Faction != orders.Faction && rules.TryGetFaction(definition.Faction, out _))
                    {
                        PathError(
                            id, "build_orders", [orders.Faction, quality],
                            $"'{id}' gives '{orders.Faction}' the build order '{order}', which belongs to '{definition.Faction}'");
                    }
                }
            }

            if (personality.FirstAttackMin > personality.FirstAttackMax)
            {
                PathError(id, "first_attack", ["min"], $"the first attack of '{id}' would start after its window ends: min must not be above max");
            }

            Positive(id, "wave_interval", personality.WaveInterval);
            if (personality.RetreatThreshold <= Fixed.Zero || personality.RetreatThreshold > Fixed.One)
            {
                Error(id, "retreat_threshold", $"'retreat_threshold' of '{id}' must be above 0% and 100% at most");
            }
        }

        /// <summary>
        /// An army of the bots (M5 step 6): of a known faction, its shares — units of that faction — make a whole, and the units that
        /// gain against infantry or vehicles are among them.
        /// </summary>
        public void Army(ArmyDef army)
        {
            FactionOf(army.Id, army.Faction);
            var known = rules.TryGetFaction(army.Faction, out _);
            var total = Fixed.Zero;
            foreach (var share in army.Shares)
            {
                total += share.Share;
                if (!rules.TryGetUnit(share.Unit, out var unit))
                {
                    Error(army.Id, "shares", Missing(army.Id, "unit", share.Unit));
                }
                else if (known && unit.Faction != army.Faction && rules.TryGetFaction(unit.Faction, out _))
                {
                    Error(army.Id, "shares", $"'{army.Id}' has '{share.Unit}', which belongs to '{unit.Faction}', not '{army.Faction}'");
                }
            }

            if (total != Fixed.One)
            {
                Error(army.Id, "shares", $"the shares of army '{army.Id}' do not make 100%");
            }

            foreach (var (key, units) in new[] { ("against_infantry", army.AgainstInfantry), ("against_vehicles", army.AgainstVehicles) })
            {
                for (var i = 0; i < units.Length; i++)
                {
                    if (!army.Shares.Any(share => share.Unit == units[i]))
                    {
                        ItemError(army.Id, key, i, $"'{army.Id}' gives '{units[i]}' more against the enemy, but it has no share of the army");
                    }
                }
            }
        }

        /// <summary>A build order of the bots (M5 step 5): of a known faction, one step or more, each a building of that faction.</summary>
        public void BuildOrder(BuildOrderDef order)
        {
            FactionOf(order.Id, order.Faction);
            if (order.Steps.IsEmpty)
            {
                Error(order.Id, "steps", $"build order '{order.Id}' has no steps");
            }

            for (var i = 0; i < order.Steps.Length; i++)
            {
                if (!rules.TryGetBuilding(order.Steps[i], out var building))
                {
                    ItemError(order.Id, "steps", i, Missing(order.Id, "building", order.Steps[i]));
                }
                else if (building.Faction != order.Faction && rules.TryGetFaction(order.Faction, out _))
                {
                    ItemError(order.Id, "steps", i, $"'{order.Id}' builds '{order.Steps[i]}', which belongs to '{building.Faction}', not '{order.Faction}'");
                }
            }
        }

        /// <summary>
        /// Walks the prerequisites of every building, in id order. A prerequisite that leads back to a building whose walk is under way
        /// closes a cycle; it is reported there, with the cycle, once.
        /// </summary>
        public void PrerequisiteCycles()
        {
            var done = new SortedSet<string>(StringComparer.Ordinal);
            var path = new List<string>();
            foreach (var building in rules.Buildings)
            {
                Visit(building, done, path);
            }
        }

        private void Visit(BuildingDef building, SortedSet<string> done, List<string> path)
        {
            if (done.Contains(building.Id))
            {
                return;
            }

            path.Add(building.Id);
            for (var i = 0; i < building.Prerequisites.Length; i++)
            {
                var id = building.Prerequisites[i];
                var start = path.IndexOf(id);
                if (start >= 0)
                {
                    var cycle = path.Skip(start).Append(id);
                    ItemError(building.Id, "prerequisites", i, $"prerequisite cycle: {string.Join(" -> ", cycle)}");
                }
                else if (rules.TryGetBuilding(id, out var prerequisite))
                {
                    Visit(prerequisite, done, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            done.Add(building.Id);
        }

        /// <summary>A unit or a building has an armor type that exists.</summary>
        private void ArmorOf(string id, string armor)
        {
            if (armor.Length == 0)
            {
                Error(id, "armor", $"'{id}' has no armor");
            }
            else if (!rules.TryGetArmor(armor, out _))
            {
                Error(id, "armor", Missing(id, "armor", armor));
            }
        }

        /// <summary>A weapon slot is empty or names a weapon.</summary>
        private void WeaponOf(string id, string key, string weapon)
        {
            if (weapon.Length > 0 && !rules.TryGetWeapon(weapon, out _))
            {
                Error(id, key, Missing(id, "weapon", weapon));
            }
        }

        private void FactionOf(string id, string faction)
        {
            if (!rules.TryGetFaction(faction, out _))
            {
                Error(id, "faction", Missing(id, "faction", faction));
            }
        }

        private void Prerequisites(string id, ImmutableArray<string> prerequisites)
        {
            for (var i = 0; i < prerequisites.Length; i++)
            {
                // A building, or a group of RA2 that a building provides.
                if (!rules.TryGetBuilding(prerequisites[i], out _) && !rules.Buildings.Any(building => building.Provides.Contains(prerequisites[i])))
                {
                    ItemError(id, "prerequisites", i, Missing(id, "building", prerequisites[i]));
                }
            }
        }

        private void UnitOf(string faction, string key, string id)
        {
            if (!rules.TryGetUnit(id, out var unit))
            {
                Error(faction, key, Missing(faction, "unit", id));
            }
            else if (unit.Faction != faction)
            {
                Error(faction, key, $"'{faction}' refers to unit '{id}', but it belongs to '{unit.Faction}'");
            }
        }

        private void BuildingOf(string faction, string key, string id)
        {
            if (!rules.TryGetBuilding(id, out var building))
            {
                Error(faction, key, Missing(faction, "building", id));
            }
            else if (building.Faction != faction)
            {
                Error(faction, key, $"'{faction}' refers to building '{id}', but it belongs to '{building.Faction}'");
            }
        }

        /// <summary>The reason for a reference that does not lead to a definition of the expected kind.</summary>
        private string Missing(string id, string kind, string target)
        {
            var actual = KindOf(target);
            return actual is null
                ? $"'{id}' refers to {kind} '{target}', which is not defined"
                : $"'{id}' refers to {kind} '{target}', but '{target}' is {actual}";
        }

        private string? KindOf(string id) =>
            rules.TryGetUnit(id, out _) ? "unit"
            : rules.TryGetBuilding(id, out _) ? "building"
            : rules.TryGetWeapon(id, out _) ? "weapon"
            : rules.TryGetLocomotor(id, out _) ? "locomotor"
            : rules.TryGetTerrain(id, out _) ? "terrain"
            : rules.TryGetFaction(id, out _) ? "faction"
            : rules.TryGetEconomy(id, out _) ? "economy"
            : rules.TryGetArmor(id, out _) ? "armor"
            : rules.TryGetWarhead(id, out _) ? "warhead"
            : rules.TryGetProjectile(id, out _) ? "projectile"
            : rules.TryGetBuildOrder(id, out _) ? "build order"
            : rules.TryGetArmy(id, out _) ? "army"
            : rules.TryGetDifficulty(id, out _) ? "difficulty"
            : rules.TryGetPersonality(id, out _) ? "personality"
            : null;

        private void Positive(string id, string key, int value)
        {
            if (value <= 0)
            {
                Error(id, key, $"'{key}' of '{id}' must be above zero");
            }
        }

        private void NotNegative(string id, string key, int value)
        {
            if (value < 0)
            {
                Error(id, key, $"'{key}' of '{id}' must not be negative");
            }
        }

        /// <summary>An error at the value of a field; at the id when the field is not written and its default is wrong.</summary>
        private void Error(string id, string key, string reason) => errors.Add(At(sources, id, key, reason));

        /// <summary>An error at one item of a list field.</summary>
        private void ItemError(string id, string key, int index, string reason)
        {
            var (fileName, node) = Place(id, key);
            if (node is YamlSequenceNode sequence && index < sequence.Children.Count)
            {
                node = sequence.Children[index];
            }

            Add(fileName, node, reason);
        }

        /// <summary>An error at the locomotor key of a speed in a land type.</summary>
        private void SpeedError(string id, string locomotor, string reason) => MapKeyError(id, "speed", locomotor, reason);

        /// <summary>An error at one key of a mapping field, such as an armor of <c>verses</c>.</summary>
        private void MapKeyError(string id, string field, string mapKey, string reason) => PathError(id, field, [mapKey], reason, atValue: false);

        /// <summary>
        /// An error inside a mapping field, by the keys down to it — <c>build_orders</c>, <c>union</c>, <c>simple</c>: at the value of the
        /// last key, or at the key itself; as deep as the file goes.
        /// </summary>
        private void PathError(string id, string field, string[] path, string reason, bool atValue = true)
        {
            var (fileName, node) = Place(id, field);
            foreach (var step in path)
            {
                if (node is not YamlMappingNode mapping)
                {
                    break;
                }

                foreach (var entry in mapping)
                {
                    if (entry.Key is YamlScalarNode { Value: { } key } && key == step)
                    {
                        node = atValue || step != path[^1] ? entry.Value : entry.Key;
                        break;
                    }
                }
            }

            Add(fileName, node, reason);
        }

        private (string FileName, YamlNode Node) Place(string id, string key)
        {
            var source = sources[id];
            return source.Fields.TryGetValue(key, out var field) ? (field.FileName, field.Value) : (source.FileName, source.IdNode);
        }

        private void Add(string fileName, YamlNode node, string reason) => errors.Add(RulesFormatException.At(fileName, node, reason).Error);
    }
}
