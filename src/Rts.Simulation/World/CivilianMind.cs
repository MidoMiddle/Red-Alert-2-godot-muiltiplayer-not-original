using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>What a civilian of the map is doing (docs/map_elements/13_civilians, 4.1).</summary>
public enum CivilianState : byte
{
    /// <summary>Stands by its gathering point; strolls when its time is up.</summary>
    Rest = 0,

    /// <summary>Walks to another gathering point of its place.</summary>
    Stroll = 1,

    /// <summary>Runs from danger, to a house to hide in or away.</summary>
    Flee = 2,

    /// <summary>Crouches where it ran to; strolls again when all is quiet.</summary>
    Cover = 3,
}

/// <summary>
/// The mind of a civilian of the map (docs/map_elements/13_civilians; <see cref="Systems.CivilianSystem"/>): its state, the ticks left
/// in it, the cell it lives by and strolls around, the house it runs to and the danger that frightened it this tick. State: hashed.
/// </summary>
public sealed class CivilianMind
{
    internal CivilianMind(CellPos anchor, int ticks)
    {
        Anchor = anchor;
        Timer = ticks;
    }

    public CivilianState State { get; internal set; }

    /// <summary>The ticks left at rest or in cover.</summary>
    public int Timer { get; internal set; }

    /// <summary>The cell it came out at: it strolls within <see cref="Systems.CivilianSystem.WanderCells"/> of it.</summary>
    public CellPos Anchor { get; internal set; }

    /// <summary>The house it runs to, to hide in; null when it runs away.</summary>
    public EntityId? Shelter { get; internal set; }

    /// <summary>Where the danger that frightened it this tick was; null when nothing did.</summary>
    public WorldPos? Danger { get; internal set; }
}
