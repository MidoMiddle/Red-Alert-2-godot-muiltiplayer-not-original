using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Rts.Simulation.Rules;

/// <summary>
/// Builds a <see cref="RulesDatabase"/> in code (docs/implementation/M3_base_economy.md, 4.5). Simulation tests that need no YAML use it
/// directly, and the loader of the data files ends with it. Ids must be unique across all kinds; the order of the calls does not matter.
/// </summary>
public sealed class RulesBuilder
{
    private readonly List<LocomotorDef> _locomotors = [];
    private readonly List<TerrainDef> _terrains = [];
    private readonly List<WeaponDef> _weapons = [];
    private readonly List<UnitDef> _units = [];
    private readonly List<BuildingDef> _buildings = [];
    private readonly List<FactionDef> _factions = [];
    private readonly List<EconomyDef> _economies = [];
    private readonly List<ArmorDef> _armors = [];
    private readonly List<WarheadDef> _warheads = [];
    private readonly List<ProjectileDef> _projectiles = [];

    public RulesBuilder Add(LocomotorDef definition)
    {
        _locomotors.Add(definition);
        return this;
    }

    public RulesBuilder Add(TerrainDef definition)
    {
        _terrains.Add(definition);
        return this;
    }

    public RulesBuilder Add(WeaponDef definition)
    {
        _weapons.Add(definition);
        return this;
    }

    public RulesBuilder Add(UnitDef definition)
    {
        _units.Add(definition);
        return this;
    }

    public RulesBuilder Add(BuildingDef definition)
    {
        _buildings.Add(definition);
        return this;
    }

    public RulesBuilder Add(FactionDef definition)
    {
        _factions.Add(definition);
        return this;
    }

    public RulesBuilder Add(EconomyDef definition)
    {
        _economies.Add(definition);
        return this;
    }

    public RulesBuilder Add(ArmorDef definition)
    {
        _armors.Add(definition);
        return this;
    }

    public RulesBuilder Add(WarheadDef definition)
    {
        _warheads.Add(definition);
        return this;
    }

    public RulesBuilder Add(ProjectileDef definition)
    {
        _projectiles.Add(definition);
        return this;
    }

    /// <summary>Sorts every kind by id and checks that no id is used twice, in any kind.</summary>
    /// <exception cref="ArgumentException">Two definitions have the same id.</exception>
    public RulesDatabase Build()
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var id in AllIds())
        {
            if (!ids.Add(id))
            {
                throw new ArgumentException($"Two definitions have the id '{id}'.");
            }
        }

        return new RulesDatabase(
            Sorted(_locomotors),
            Sorted(_terrains),
            Sorted(_weapons),
            Sorted(_units),
            Sorted(_buildings),
            Sorted(_factions),
            Sorted(_economies),
            Sorted(_armors),
            Sorted(_warheads),
            Sorted(_projectiles));
    }

    private IEnumerable<string> AllIds()
    {
        foreach (var definition in _locomotors)
        {
            yield return definition.Id;
        }

        foreach (var definition in _terrains)
        {
            yield return definition.Id;
        }

        foreach (var definition in _weapons)
        {
            yield return definition.Id;
        }

        foreach (var definition in _units)
        {
            yield return definition.Id;
        }

        foreach (var definition in _buildings)
        {
            yield return definition.Id;
        }

        foreach (var definition in _factions)
        {
            yield return definition.Id;
        }

        foreach (var definition in _economies)
        {
            yield return definition.Id;
        }

        foreach (var definition in _armors)
        {
            yield return definition.Id;
        }

        foreach (var definition in _warheads)
        {
            yield return definition.Id;
        }

        foreach (var definition in _projectiles)
        {
            yield return definition.Id;
        }
    }

    private static ImmutableArray<T> Sorted<T>(List<T> definitions)
        where T : IDefinition =>
        definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal).ToImmutableArray();
}
