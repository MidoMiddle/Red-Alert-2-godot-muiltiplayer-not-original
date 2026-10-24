using System.Collections.Immutable;
using System.Text;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Rules;

/// <summary>
/// The hash of the rules a match is played with (docs/implementation/M3_base_economy.md, 4.4): FNV-1a 64 over the definitions, not over the
/// bytes of the files, so spaces, comments and the order of entries and files do not change it. The kinds go in a fixed order, each sorted
/// by id as <see cref="RulesDatabase"/> keeps it. Only what a match uses goes in: the names, models and icons may differ between players
/// without a desync, so they are left out. A replay keeps the hash next to the rules version; in M9 players compare it before a match.
/// </summary>
public static class DataHasher
{
    /// <summary>The hash a replay records when the match had no data files: the rules of M2 that are still written in code.</summary>
    public const ulong NoData = 0;

    public static ulong Compute(RulesDatabase rules)
    {
        var hash = new Fnv1a64();
        hash.Add(rules.Locomotors.Length);
        foreach (var locomotor in rules.Locomotors)
        {
            Add(hash, locomotor.Id);
            hash.Add(locomotor.Slope.Raw);
        }

        hash.Add(rules.Terrains.Length);
        foreach (var terrain in rules.Terrains)
        {
            Add(hash, terrain.Id);
            hash.Add((int)terrain.Code);
            hash.Add(terrain.Buildable);
            hash.Add(terrain.Speed.Length);
            foreach (var speed in terrain.Speed)
            {
                Add(hash, speed.Locomotor);
                hash.Add(speed.Multiplier.Raw);
            }
        }

        hash.Add(rules.Weapons.Length);
        foreach (var weapon in rules.Weapons)
        {
            Add(hash, weapon.Id);
            hash.Add(weapon.Damage);
            hash.Add(weapon.Range.Raw);
            hash.Add(weapon.Reload);
            hash.Add(weapon.MinRange.Raw);
            Add(hash, weapon.Projectile);
            Add(hash, weapon.Warhead);
            hash.Add(weapon.Burst);
            hash.Add(weapon.BurstDelay);
            hash.Add((byte)weapon.Targets);
            hash.Add(weapon.Suicide);
            hash.Add(weapon.Fragments);
            Add(hash, weapon.FragmentWeapon);
            hash.Add(weapon.FragmentRange.Raw);
        }

        hash.Add(rules.Units.Length);
        foreach (var unit in rules.Units)
        {
            Add(hash, unit.Id);
            Add(hash, unit.Faction);
            hash.Add((byte)unit.Category);
            hash.Add(unit.Cost);
            hash.Add(unit.BuildTime);
            hash.Add(unit.Hp);
            Add(hash, unit.Armor);
            hash.Add(unit.Speed);
            hash.Add(unit.TurnRate);
            hash.Add(unit.Sight);
            Add(hash, unit.Locomotor);
            hash.Add((byte)unit.Placement);
            hash.Add(unit.Turret);
            hash.Add(unit.HarvestCapacity);
            hash.Add(unit.HarvestTime);
            hash.Add(unit.UnloadRate);
            Add(hash, unit.Prerequisites);
            Add(hash, unit.Primary);
            Add(hash, unit.Secondary);
            hash.Add(unit.TurretTurnRate.HasValue);
            hash.Add(unit.TurretTurnRate ?? 0);
            hash.Add(unit.Threat);
            hash.Add((byte)unit.Stance);
            hash.Add(unit.Crusher);
            hash.Add(unit.Crushable);
            hash.Add(unit.Jumpjet);
            hash.Add(unit.Engineer);
            hash.Add(unit.Infiltrator);
            hash.Add(unit.Disguised);
            hash.Add(unit.DetectsDisguise);
            hash.Add(unit.Passive);
            Add(hash, unit.DeathWeapon);
            hash.Add(unit.Teleports);
            Add(hash, unit.DeployWeapon);
            hash.Add(unit.RadiationImmune);
            hash.Add(unit.DisguiseWhenStill);
            hash.Add(unit.PassengerSlots);
            hash.Add(unit.PassengerSizeLimit);
            hash.Add(unit.Size);
            hash.Add(unit.PassengerWeapon);
            Add(hash, unit.IfvWeapon);
            hash.Add(unit.Robotic);
            hash.Add(unit.Submerged);
            hash.Add(unit.Sensors);
            hash.Add(unit.AvailableLater);
        }

        hash.Add(rules.Buildings.Length);
        foreach (var building in rules.Buildings)
        {
            Add(hash, building.Id);
            Add(hash, building.Faction);
            hash.Add((byte)building.Category);
            hash.Add(building.Cost);
            hash.Add(building.BuildTime);
            hash.Add(building.Hp);
            hash.Add(building.Footprint.Width);
            hash.Add(building.Footprint.Height);
            hash.Add(building.Power);
            hash.Add(building.BuildUp);
            hash.Add(building.DockCell.HasValue);
            if (building.DockCell is { } dock)
            {
                hash.Add(dock.X);
                hash.Add(dock.Y);
            }

            hash.Add(building.Adjacent);
            hash.Add(building.ExtendsBase);
            hash.Add(building.Wall);
            hash.Add(building.Powered);
            hash.Add(building.Radar);
            hash.Add(building.Produces.Length);
            foreach (var category in building.Produces)
            {
                hash.Add((byte)category);
            }
            hash.Add(building.ExitCell.HasValue);
            if (building.ExitCell is { } exit)
            {
                hash.Add(exit.X);
                hash.Add(exit.Y);
            }

            Add(hash, building.Prerequisites);
            Add(hash, building.Armor);
            Add(hash, building.Primary);
            hash.Add(building.Threat);
            hash.Add(building.Sight);
            hash.Add(building.Water);
            hash.Add(building.RepairsVehicles);
            hash.Add(building.VehicleDiscountPercent);
            hash.Add(building.IncomeBonusPercent);
            Add(hash, building.DeathWeapon);
            hash.Add(building.RevealsMap);
            hash.Add(building.VeilRadius);
            hash.Add(building.GarrisonCapacity);
            hash.Add(building.ChainBonusPercent);
            hash.Add(building.ChainMax);
            hash.Add(building.Capturable);
            hash.Add((byte)building.SpyEffect);
            hash.Add(building.ControlsRobots);
        }

        hash.Add(rules.Factions.Length);
        foreach (var faction in rules.Factions)
        {
            Add(hash, faction.Id);
            Add(hash, faction.Mcv);
            Add(hash, faction.ConstructionYard);
            Add(hash, faction.Refinery);
            Add(hash, faction.Harvester);
            Add(hash, faction.StartingInfantry);
            hash.Add(faction.IsDefault);
        }

        // The build orders of the bots (M5 step 5): a bot of other data builds otherwise.
        hash.Add(rules.BuildOrders.Length);
        foreach (var order in rules.BuildOrders)
        {
            Add(hash, order.Id);
            Add(hash, order.Faction);
            Add(hash, order.Steps);
        }

        // The armies of the bots (M5 step 6).
        hash.Add(rules.Armies.Length);
        foreach (var army in rules.Armies)
        {
            Add(hash, army.Id);
            Add(hash, army.Faction);
            hash.Add(army.Shares.Length);
            foreach (var share in army.Shares)
            {
                Add(hash, share.Unit);
                hash.Add(share.Share.Raw);
            }

            Add(hash, army.AgainstInfantry);
            Add(hash, army.AgainstVehicles);
        }

        // The levels of the bots (M5 step 14; M8 step 1), every number in the order of the keys, a choice by the place of its word.
        hash.Add(rules.Difficulties.Length);
        foreach (var difficulty in rules.Difficulties)
        {
            Add(hash, difficulty.Id);
            hash.Add(difficulty.Values.Count);
            foreach (var (key, _) in DifficultyDef.Keys)
            {
                hash.Add(difficulty.Values.TryGetValue(key, out var value) ? value : int.MinValue);
            }
        }

        // The personalities of the bots (M8 step 1); the name is the screen's.
        hash.Add(rules.Personalities.Length);
        foreach (var personality in rules.Personalities)
        {
            Add(hash, personality.Id);
            var weights = personality.Weights;
            hash.Add(weights.Economy.Raw);
            hash.Add(weights.Defense.Raw);
            hash.Add(weights.Army.Raw);
            hash.Add(weights.Tech.Raw);
            hash.Add(weights.Harassment.Raw);
            hash.Add(personality.Armies.Length);
            foreach (var army in personality.Armies)
            {
                Add(hash, army.Faction);
                Add(hash, army.Army);
            }

            hash.Add(personality.BuildOrders.Length);
            foreach (var orders in personality.BuildOrders)
            {
                Add(hash, orders.Faction);
                Add(hash, orders.Simple);
                Add(hash, orders.Standard);
                Add(hash, orders.Optimized);
            }

            hash.Add(personality.FirstAttackMin);
            hash.Add(personality.FirstAttackMax);
            hash.Add(personality.WaveInterval);
            hash.Add(personality.RetreatThreshold.Raw);
            Add(hash, personality.TargetPriority);
        }

        hash.Add(rules.Economies.Length);
        foreach (var economy in rules.Economies)
        {
            Add(hash, economy.Id);
            hash.Add(economy.LowPowerMinRate.Raw);
            hash.Add(economy.MultipleFactoryBonus.Raw);
            hash.Add(economy.MaxFactoryRate.Raw);
            hash.Add(economy.OreValue);
            hash.Add(economy.GemValue);
            hash.Add(economy.OreGrowthInterval);
            hash.Add(economy.OreGrowthChance.Raw);
            hash.Add(economy.OreSpreadStage);
            hash.Add(economy.OreSpreadChance.Raw);
            hash.Add(economy.SourceInterval);
            hash.Add(economy.SourceRadius);
            hash.Add(economy.SellRefund.Raw);
            hash.Add(economy.RepairCost.Raw);
            hash.Add(economy.RepairRate);
        }

        // M4 step 1: armor, warheads and projectiles; the death of infantry and the look of a projectile are the client's.
        hash.Add(rules.Armors.Length);
        foreach (var armor in rules.Armors)
        {
            Add(hash, armor.Id);
        }

        hash.Add(rules.Warheads.Length);
        foreach (var warhead in rules.Warheads)
        {
            Add(hash, warhead.Id);
            hash.Add(warhead.Verses.Length);
            foreach (var verse in warhead.Verses)
            {
                Add(hash, verse.Armor);
                hash.Add(verse.Percent.Raw);
            }

            hash.Add(warhead.Spread.Raw);
            hash.Add(warhead.PercentAtMax.Raw);
            hash.Add(warhead.WallDamage);
            hash.Add(warhead.AffectsAir);
            hash.Add(warhead.Temporal);
            Add(hash, warhead.Bomb);
            hash.Add(warhead.BombDelay);
            hash.Add(warhead.Radiation);
            hash.Add(warhead.Parasite);
            hash.Add(warhead.ParasiteDamage);
            hash.Add(warhead.ParasiteDelay);
            hash.Add(warhead.ParasiteNaval);
            hash.Add(warhead.Paralyzes);
        }

        hash.Add(rules.Projectiles.Length);
        foreach (var projectile in rules.Projectiles)
        {
            Add(hash, projectile.Id);
            hash.Add((byte)projectile.Motion);
            hash.Add(projectile.Speed);
            hash.Add(projectile.Arc.Raw);
            hash.Add(projectile.Inaccuracy.Raw);
            hash.Add(projectile.TurnRate);
            hash.Add(projectile.Lifetime);
            hash.Add(projectile.Hp);
            Add(hash, projectile.Armor);
        }

        return hash.Value;
    }

    /// <summary>A text as its length and its UTF-8 bytes, so that two texts never run into each other.</summary>
    private static void Add(Fnv1a64 hash, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        hash.Add(bytes.Length);
        hash.Add(bytes);
    }

    /// <summary>A list in its order, with its length first.</summary>
    private static void Add(Fnv1a64 hash, ImmutableArray<string> list)
    {
        hash.Add(list.Length);
        foreach (var item in list)
        {
            Add(hash, item);
        }
    }
}
