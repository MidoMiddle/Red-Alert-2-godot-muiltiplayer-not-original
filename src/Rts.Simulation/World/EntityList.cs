using System.Collections;
using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>
/// The entities of the world in id order, read-only (M4 step 15). A <c>foreach</c> over it takes the list's own struct enumerator, so the
/// systems that walk the entities every tick allocate nothing, whatever runtime runs them; through the interface it is a plain list.
/// </summary>
public readonly struct EntityList : IReadOnlyList<Entity>
{
    private readonly List<Entity> _entities;

    internal EntityList(List<Entity> entities) => _entities = entities;

    public int Count => _entities.Count;

    public Entity this[int index] => _entities[index];

    public List<Entity>.Enumerator GetEnumerator() => _entities.GetEnumerator();

    IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator() => _entities.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _entities.GetEnumerator();
}
