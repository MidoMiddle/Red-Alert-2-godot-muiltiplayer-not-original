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

    public PlayerId Owner { get; }

    public bool IsAlive { get; private set; } = true;

    internal void MarkDead() => IsAlive = false;

    /// <summary>
    /// The cell whose sight circle this entity added to its owner's <see cref="Visibility.VisibilityGrid"/> (M4 step 11); null while it
    /// adds none. Derived from where it stands, so not hashed: the grids are.
    /// </summary>
    internal Geometry.CellPos? SightCell { get; set; }

    /// <summary>The radius of that circle.</summary>
    internal int SightRadius { get; set; }
}
