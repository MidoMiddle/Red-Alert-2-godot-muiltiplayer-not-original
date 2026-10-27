using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>
/// A light object of the map (docs/implementation/M7_maps.md, 4.7): a tree, boulder, fence, wall or lamp post on a cell. Not
/// an <c>Entity</c>, so thousands of trees cost little; ids come from the map's own counter, in the order the map lists
/// them. <see cref="Variant"/> picks the model, for the look only, the same for every player. A destroyed tree stays as a
/// stump; other destroyed objects no longer count. Part of the simulation state through the map.
/// </summary>
public sealed class MapObject
{
    internal MapObject(int id, MapObjectType type, CellPos cell, int variant, int hp, bool destroyed)
    {
        Id = id;
        Type = type;
        Cell = cell;
        Variant = variant;
        Hp = hp;
        Destroyed = destroyed;
    }

    public int Id { get; }

    public MapObjectKind Kind => Type.Kind;

    /// <summary>What its kind does, as the data of the map defines it (<see cref="MapTypes"/>).</summary>
    public MapObjectType Type { get; }

    public CellPos Cell { get; }

    public int Variant { get; }

    /// <summary>Hit points left; 0 for an object that cannot be destroyed (see <see cref="MapObjectType.Destructible"/>).</summary>
    public int Hp { get; internal set; }

    public bool Destroyed { get; internal set; }

    /// <summary>Standing and in the way of whoever it blocks.</summary>
    public bool IsAlive => !Destroyed;

    /// <summary>A copy of the kind <paramref name="type"/> — the same kind, as the rules of a match define it.</summary>
    internal MapObject Clone(MapObjectType type) => new(Id, type, Cell, Variant, Hp, Destroyed);

    internal bool ContentEquals(MapObject other) =>
        Id == other.Id && Kind == other.Kind && Cell == other.Cell && Variant == other.Variant && Hp == other.Hp && Destroyed == other.Destroyed;
}
