using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>
/// A building (docs/implementation/M7_maps.md, 4.8): for now only the minimal one of M7 — a footprint of cells that stops
/// every mover, hit points, an owner (the neutral player for the houses and tech buildings of a map). M3 adds construction,
/// power and production; M11 garrisons and capture. The footprint does not turn with <see cref="Facing"/>, which is for
/// the look only. A destroyed building leaves no ruins: its cells open at once.
/// </summary>
public sealed class Building : Entity
{
    internal Building(EntityId id, PlayerId owner, BuildingKind kind, CellPos cell, int facing)
        : base(id, owner)
    {
        Kind = kind;
        Cell = cell;
        Facing = facing;
        Hp = BuildingTypes.Get(kind).MaxHp;
    }

    public BuildingKind Kind { get; }

    public BuildingType Type => BuildingTypes.Get(Kind);

    /// <summary>The north-west cell of the footprint.</summary>
    public CellPos Cell { get; }

    /// <summary>0…7 in 45° steps, for the look only.</summary>
    public int Facing { get; }

    public int Hp { get; internal set; }

    /// <summary>True when the cell is part of the footprint.</summary>
    public bool Covers(CellPos cell) =>
        cell.X >= Cell.X && cell.Y >= Cell.Y && cell.X < Cell.X + Type.Width && cell.Y < Cell.Y + Type.Height;
}
