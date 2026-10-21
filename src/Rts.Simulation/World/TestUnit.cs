using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>
/// The only entity type of M0: a unit that moves toward a target point.
/// State is changed only by the simulation; the client reads it.
/// </summary>
public sealed class TestUnit : Entity
{
    /// <summary>Speed of spawned test units, leptons per tick (~1.9 cells per second at 15 ticks per second).</summary>
    public const int DefaultSpeed = 32;

    internal TestUnit(EntityId id, PlayerId owner, WorldPos position, int speed)
        : base(id, owner)
    {
        Position = position;
        Speed = speed;
    }

    public WorldPos Position { get; internal set; }

    /// <summary>Point the unit is moving to, or null when it stands still.</summary>
    public WorldPos? MoveTarget { get; internal set; }

    /// <summary>Movement speed in leptons per tick.</summary>
    public int Speed { get; internal set; }
}
