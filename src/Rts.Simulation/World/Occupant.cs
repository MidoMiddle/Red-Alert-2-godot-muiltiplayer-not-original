namespace Rts.Simulation.World;

/// <summary>
/// A soldier inside a building (stage 2e: the fortified bunker): it left the map and fires from inside with its weapons, until it comes
/// out again with its hit points. State: hashed with its building.
/// </summary>
public sealed class Occupant
{
    internal Occupant(UnitArchetype archetype, int hp)
    {
        Type = archetype.Type;
        Hp = hp;
        Weapons = archetype.OccupyWeapon >= 0 ? [new()] : !archetype.Armed ? [] : archetype.Secondary >= 0 ? [new(), new()] : [new()];
    }

    public UnitType Type { get; }

    /// <summary>The hit points it had when it went in, and has when it comes out.</summary>
    public int Hp { get; }

    /// <summary>The state of its weapons, as a unit's: they reload inside.</summary>
    public Combat.WeaponInstance[] Weapons { get; }
}
