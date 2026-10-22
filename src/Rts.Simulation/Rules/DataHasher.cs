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
            Add(hash, unit.Prerequisites);
            Add(hash, unit.Weapons);
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
            hash.Add(building.ExitCell.HasValue);
            if (building.ExitCell is { } exit)
            {
                hash.Add(exit.X);
                hash.Add(exit.Y);
            }

            Add(hash, building.Prerequisites);
        }

        hash.Add(rules.Factions.Length);
        foreach (var faction in rules.Factions)
        {
            Add(hash, faction.Id);
            Add(hash, faction.Mcv);
            Add(hash, faction.ConstructionYard);
            Add(hash, faction.Refinery);
            Add(hash, faction.Harvester);
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
