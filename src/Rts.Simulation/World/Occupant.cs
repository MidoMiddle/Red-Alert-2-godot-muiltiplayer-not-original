namespace Rts.Simulation.World;

/// <summary>
/// A soldier inside a building (stage 2e: the fortified bunker), or a unit inside a transport (stage 3d): it left the map and fires from
/// inside with its weapons, until it comes out again with its hit points. State: hashed with its building or transport.
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

    /// <summary>
    /// The weapon of the bomb it carried in (RA2: Ivan's bomb waits inside a transport), -1 for none; the bomb goes off when it comes
    /// out, or with the transport.
    /// </summary>
    public int BombWeapon { get; internal set; } = -1;

    /// <summary>Who planted the bomb it carries.</summary>
    public EntityId BombShooter { get; internal set; }

    /// <summary>The player of the bomb it carries.</summary>
    public PlayerId BombOwner { get; internal set; }
}
