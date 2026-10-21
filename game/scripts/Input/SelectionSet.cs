using System;
using System.Collections.Generic;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>The units the player has selected. Client state: the simulation never sees it, only the orders it produces.</summary>
public sealed class SelectionSet
{
    private readonly List<EntityId> _units = new();

    public IReadOnlyList<EntityId> Units => _units;

    public bool IsEmpty => _units.Count == 0;

    public bool Contains(EntityId unit) => _units.Contains(unit);

    /// <summary>Selects one unit and drops the rest of the selection.</summary>
    public void Replace(EntityId unit)
    {
        _units.Clear();
        _units.Add(unit);
    }

    /// <summary>Selects exactly <paramref name="units"/>, in the given order, without repeats.</summary>
    public void ReplaceAll(IEnumerable<EntityId> units)
    {
        _units.Clear();
        foreach (var unit in units)
        {
            if (!_units.Contains(unit))
            {
                _units.Add(unit);
            }
        }
    }

    /// <summary>Adds the unit, or removes it if it is selected already (Shift + click).</summary>
    public void Toggle(EntityId unit)
    {
        if (!_units.Remove(unit))
        {
            _units.Add(unit);
        }
    }

    /// <summary>Adds the units that are not selected yet, keeping the current ones first (Shift + box).</summary>
    public void AddAll(IEnumerable<EntityId> units)
    {
        foreach (var unit in units)
        {
            if (!_units.Contains(unit))
            {
                _units.Add(unit);
            }
        }
    }

    public void Clear() => _units.Clear();

    /// <summary>Drops the units for which <paramref name="gone"/> is true, for example the dead ones.</summary>
    public void RemoveWhere(Func<EntityId, bool> gone) => _units.RemoveAll(unit => gone(unit));
}
