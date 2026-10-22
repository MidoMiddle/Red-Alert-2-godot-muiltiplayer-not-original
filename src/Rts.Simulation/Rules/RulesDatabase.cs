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
        ImmutableArray<FactionDef> factions)
    {
        Locomotors = locomotors;
        Terrains = terrains;
        Weapons = weapons;
        Units = units;
        Buildings = buildings;
        Factions = factions;
    }

    public ImmutableArray<LocomotorDef> Locomotors { get; }

    public ImmutableArray<TerrainDef> Terrains { get; }

    public ImmutableArray<WeaponDef> Weapons { get; }

    public ImmutableArray<UnitDef> Units { get; }

    public ImmutableArray<BuildingDef> Buildings { get; }

    public ImmutableArray<FactionDef> Factions { get; }

    public bool TryGetLocomotor(string id, out LocomotorDef definition) => TryFind(Locomotors, id, out definition);

    public bool TryGetTerrain(string id, out TerrainDef definition) => TryFind(Terrains, id, out definition);

    public bool TryGetWeapon(string id, out WeaponDef definition) => TryFind(Weapons, id, out definition);

    public bool TryGetUnit(string id, out UnitDef definition) => TryFind(Units, id, out definition);

    public bool TryGetBuilding(string id, out BuildingDef definition) => TryFind(Buildings, id, out definition);

    public bool TryGetFaction(string id, out FactionDef definition) => TryFind(Factions, id, out definition);

    /// <summary>Binary search over a list sorted by id. The comparison is ordinal, so every platform finds the same entry.</summary>
    private static bool TryFind<T>(ImmutableArray<T> sorted, string id, out T definition)
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
                definition = sorted[middle];
                return true;
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

        definition = default!;
        return false;
    }
}
