using System.Collections.Immutable;

namespace Rts.Simulation.Rules;

/// <summary>
/// The rules of a match: every definition of the data, each kind sorted by id (ordinal), so the order of the files does not matter.
/// Immutable once <see cref="RulesBuilder.Build"/> has made it. A definition is referred to by its position in the list of its kind:
/// the <c>u16</c> index that commands use (docs/implementation/M3_base_economy.md, 4.14).
/// </summary>
public sealed class RulesDatabase
{
    internal RulesDatabase(
        ImmutableArray<LocomotorDef> locomotors,
        ImmutableArray<TerrainDef> terrains,
        ImmutableArray<WeaponDef> weapons,
        ImmutableArray<UnitDef> units,
        ImmutableArray<BuildingDef> buildings,
        ImmutableArray<FactionDef> factions,
        ImmutableArray<EconomyDef> economies,
        ImmutableArray<ArmorDef> armors,
        ImmutableArray<WarheadDef> warheads,
        ImmutableArray<ProjectileDef> projectiles,
        ImmutableArray<BuildOrderDef> buildOrders,
        ImmutableArray<ArmyDef> armies)
    {
        Locomotors = locomotors;
        Terrains = terrains;
        Weapons = weapons;
        Units = units;
        Buildings = buildings;
        Factions = factions;
        Economies = economies;
        Armors = armors;
        Warheads = warheads;
        Projectiles = projectiles;
        BuildOrders = buildOrders;
        Armies = armies;
    }

    public ImmutableArray<LocomotorDef> Locomotors { get; }

    public ImmutableArray<TerrainDef> Terrains { get; }

    public ImmutableArray<WeaponDef> Weapons { get; }

    public ImmutableArray<UnitDef> Units { get; }

    public ImmutableArray<BuildingDef> Buildings { get; }

    public ImmutableArray<FactionDef> Factions { get; }

    /// <summary>The general rules of the economy; the engine needs exactly one (<see cref="GameRules.Problems"/>).</summary>
    public ImmutableArray<EconomyDef> Economies { get; }

    /// <summary>The armor types (M4, 4.2); a type's number is its place here.</summary>
    public ImmutableArray<ArmorDef> Armors { get; }

    public ImmutableArray<WarheadDef> Warheads { get; }

    public ImmutableArray<ProjectileDef> Projectiles { get; }

    /// <summary>The build orders of the bots (M5 step 5).</summary>
    public ImmutableArray<BuildOrderDef> BuildOrders { get; }

    /// <summary>The armies the bots build (M5 step 6).</summary>
    public ImmutableArray<ArmyDef> Armies { get; }

    public bool TryGetLocomotor(string id, out LocomotorDef definition) => TryFind(Locomotors, id, out definition);

    public bool TryGetBuildOrder(string id, out BuildOrderDef definition) => TryFind(BuildOrders, id, out definition);

    public bool TryGetArmy(string id, out ArmyDef definition) => TryFind(Armies, id, out definition);

    public bool TryGetTerrain(string id, out TerrainDef definition) => TryFind(Terrains, id, out definition);

    public bool TryGetWeapon(string id, out WeaponDef definition) => TryFind(Weapons, id, out definition);

    public bool TryGetUnit(string id, out UnitDef definition) => TryFind(Units, id, out definition);

    public bool TryGetBuilding(string id, out BuildingDef definition) => TryFind(Buildings, id, out definition);

    public bool TryGetFaction(string id, out FactionDef definition) => TryFind(Factions, id, out definition);

    public bool TryGetEconomy(string id, out EconomyDef definition) => TryFind(Economies, id, out definition);

    public bool TryGetArmor(string id, out ArmorDef definition) => TryFind(Armors, id, out definition);

    public bool TryGetWarhead(string id, out WarheadDef definition) => TryFind(Warheads, id, out definition);

    public bool TryGetProjectile(string id, out ProjectileDef definition) => TryFind(Projectiles, id, out definition);

    /// <summary>The number of an armor type: its position in <see cref="Armors"/>; -1 when it is not defined.</summary>
    public int IndexOfArmor(string id) => IndexOf(Armors, id);

    /// <summary>The position of a weapon in <see cref="Weapons"/>; -1 when it is not defined.</summary>
    public int IndexOfWeapon(string id) => IndexOf(Weapons, id);

    /// <summary>The position of a warhead in <see cref="Warheads"/>; -1 when it is not defined.</summary>
    public int IndexOfWarhead(string id) => IndexOf(Warheads, id);

    /// <summary>The position of a unit in <see cref="Units"/>: the index commands and tech sets use; -1 when it is not defined.</summary>
    public int IndexOfUnit(string id) => IndexOf(Units, id);

    /// <summary>The position of a building in <see cref="Buildings"/>: the index commands and tech sets use; -1 when it is not defined.</summary>
    public int IndexOfBuilding(string id) => IndexOf(Buildings, id);

    private static bool TryFind<T>(ImmutableArray<T> sorted, string id, out T definition)
        where T : IDefinition
    {
        var index = IndexOf(sorted, id);
        definition = index >= 0 ? sorted[index] : default!;
        return index >= 0;
    }

    /// <summary>Binary search over a list sorted by id. The comparison is ordinal, so every platform finds the same entry.</summary>
    private static int IndexOf<T>(ImmutableArray<T> sorted, string id)
        where T : IDefinition
    {
        var low = 0;
        var high = sorted.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = string.CompareOrdinal(sorted[middle].Id, id);
            if (order == 0)
            {
                return middle;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }
}
