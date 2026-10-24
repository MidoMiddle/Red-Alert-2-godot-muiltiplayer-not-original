namespace Rts.Simulation.World;

/// <summary>
/// Anything that lives in the simulation and has an id: units, buildings, projectiles.
/// A dead entity stays in the world until the end of the tick, so lists never change mid-tick.
/// </summary>
public abstract class Entity
{
    protected Entity(EntityId id, PlayerId owner)
    {
        Id = id;
        Owner = owner;
    }

    public EntityId Id { get; }

    /// <summary>Whose it is; a building changes hands when an engineer takes it (stage 3b).</summary>
    public PlayerId Owner { get; internal set; }

    public bool IsAlive { get; private set; } = true;

    internal void MarkDead() => IsAlive = false;

    /// <summary>
    /// The cell whose sight circle this entity added to its owner's <see cref="Visibility.VisibilityGrid"/> (M4 step 11); null while it
    /// adds none. Derived from where it stands, so not hashed: the grids are.
    /// </summary>
    internal Geometry.CellPos? SightCell { get; set; }

    /// <summary>The radius of that circle.</summary>
    internal int SightRadius { get; set; }

    /// <summary>
    /// The shooter whose temporal beam holds this unit or building (stage 3a: the time trooper); null while none does. Meanwhile it
    /// stands still, does not fire and takes no other damage (<see cref="Systems.ErasureSystem"/>). State: hashed.
    /// </summary>
    public EntityId? ErasedBy { get; internal set; }

    /// <summary>The temporal weapon of that shooter, by its place in the rules; -1 while none holds it. State: hashed.</summary>
    public int EraseWeapon { get; internal set; } = -1;

    /// <summary>The erasure gathered so far; at 10 × its hit points the entity is gone. State: hashed.</summary>
    public int ErasePoints { get; internal set; }

    /// <summary>
    /// The ticks until the bomb planted on it goes off (stage 3b: Ivan's bomb, the commando's C4); 0 without one. State: hashed with
    /// the three below.
    /// </summary>
    public int BombTicks { get; internal set; }

    /// <summary>The weapon of the bomb, by its place in the rules; -1 without one.</summary>
    public int BombWeapon { get; internal set; } = -1;

    /// <summary>Who planted it, for the kills.</summary>
    public EntityId BombShooter { get; internal set; }

    /// <summary>The player of whoever planted it.</summary>
    public PlayerId BombOwner { get; internal set; }

    /// <summary>The tick until which nothing hurts it: the Bastion Field Projector covers it (stage 2d). State: hashed.</summary>
    public int InvulnerableUntil { get; internal set; }

    /// <summary>Whether nothing hurts it now.</summary>
    public bool IsInvulnerable(int tick) => tick < InvulnerableUntil;
}
