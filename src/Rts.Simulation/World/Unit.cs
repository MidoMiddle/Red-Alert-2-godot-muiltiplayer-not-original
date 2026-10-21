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

    /// <summary>Index in the <see cref="CellPath.Waypoints"/> of <see cref="Path"/> that the unit drives to next.</summary>
    public int PathIndex { get; internal set; }

    /// <summary>Ticks in a row the unit had an order but did not move (docs/implementation/M2_unit_control.md, 4.7).</summary>
    public int WaitTicks { get; internal set; }

    /// <summary>Paths asked for around other units since the unit last entered a new cell; after 3 it gives up.</summary>
    public int Replans { get; internal set; }

    /// <summary>Where the unit goes again after giving way in a side pocket; null when it is not giving way.</summary>
    public WorldPos? ResumeTarget { get; internal set; }

    /// <summary>Ticks left to wait in the pocket before heading for <see cref="ResumeTarget"/> again.</summary>
    public int HoldTicks { get; internal set; }

    /// <summary>
    /// True while the unit only goes back to the place it was moved off to make room. Such a move is polite: it never asks
    /// others to make room in turn, so two units cannot keep moving each other off their places.
    /// </summary>
    public bool Returning { get; internal set; }
}
