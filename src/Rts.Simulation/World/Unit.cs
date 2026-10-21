using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.World;

/// <summary>
/// A unit of one of the <see cref="UnitArchetypes"/>: position, move target, hull and turret direction.
/// State is changed only by the simulation; the client reads it.
/// </summary>
public sealed class Unit : Entity
{
    internal Unit(EntityId id, PlayerId owner, UnitArchetype archetype, WorldPos position)
        : base(id, owner)
    {
        Archetype = archetype;
        Position = position;
        Speed = archetype.Speed;
        TurnRate = archetype.TurnRate;
    }

    /// <summary>Rules of the unit's type. Only <see cref="UnitArchetype.Type"/> is state; the rest is fixed data.</summary>
    public UnitArchetype Archetype { get; }

    public UnitType Type => Archetype.Type;

    public WorldPos Position { get; internal set; }

    /// <summary>Point the unit is moving to, or null when it stands still.</summary>
    public WorldPos? MoveTarget { get; internal set; }

    /// <summary>Movement speed in leptons per tick. Starts at the type's value; veterancy and damage may change it later.</summary>
    public int Speed { get; internal set; }

    /// <summary>Hull turn speed in <c>BAngle</c> units per tick; 0 turns instantly (infantry).</summary>
    public int TurnRate { get; internal set; }

    /// <summary>Where the hull points. A new unit faces north.</summary>
    public BAngle Facing { get; internal set; }

    /// <summary>
    /// Where the turret points. Until targeting arrives in M4 it follows the hull; units without a turret
    /// (<see cref="UnitArchetype.HasTurret"/>) keep it too, the client just does not draw it.
    /// </summary>
    public BAngle TurretFacing { get; internal set; }

    /// <summary>True while the unit waits for its path search (docs/implementation/M2_unit_control.md, 4.4).</summary>
    public bool PathPending { get; internal set; }

    /// <summary>The path the unit drives along, or null when it has none (standing, or moving straight in tests).</summary>
    public CellPath? Path { get; internal set; }

    /// <summary>Index in <see cref="Path"/> of the cell the unit drives to next.</summary>
    public int PathIndex { get; internal set; }
}
