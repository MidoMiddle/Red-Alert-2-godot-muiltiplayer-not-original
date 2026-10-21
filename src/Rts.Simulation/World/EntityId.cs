using System;

namespace Rts.Simulation.World;

/// <summary>
/// Stable entity identifier. Issued by <see cref="GameWorld"/> in increasing order starting at 1
/// and never reused, so "sorted by id" is also "sorted by creation".
/// </summary>
public readonly record struct EntityId(int Value) : IComparable<EntityId>
{
    /// <summary>No entity.</summary>
    public static readonly EntityId None = new(0);

    public int CompareTo(EntityId other) => Value.CompareTo(other.Value);

    public override string ToString() => "#" + Value;
}
