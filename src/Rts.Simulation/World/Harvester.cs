using Rts.Simulation.Geometry;

namespace Rts.Simulation.World;

/// <summary>What a harvester is busy with (docs/implementation/M3_base_economy.md, 4.13). Values go into the state hash: never renumber.</summary>
public enum HarvesterMode : byte
{
    /// <summary>Looks for ore, from its last field first.</summary>
    SeekOre = 1,

    /// <summary>Drives to the cell of ore it has claimed.</summary>
    MovingToOre = 2,

    /// <summary>Stands on its cell and takes the ore stage by stage.</summary>
    Harvesting = 3,

    /// <summary>Chooses a refinery to unload at.</summary>
    SeekRefinery = 4,

    /// <summary>Drives toward the dock of its refinery.</summary>
    MovingToRefinery = 5,

    /// <summary>Waits near a busy dock for its turn.</summary>
    Queued = 6,

    /// <summary>Drives into the free dock.</summary>
    Docking = 7,

    /// <summary>Stands in the dock and unloads.</summary>
    Unloading = 8,

    /// <summary>Found no ore or no refinery, or got stuck: tries again after a while.</summary>
    Waiting = 9,

    /// <summary>Follows an order of the player; goes back to work by itself after a while (decision 3 of the M3 plan).</summary>
    Manual = 10,
}

/// <summary>
/// The work of a unit that gathers ore (docs/implementation/M3_base_economy.md, 4.13): what it is doing, its field, the cell it has
/// claimed, its refinery and its load. Driven by <see cref="Systems.HarvesterSystem"/>. State: hashed, except <see cref="Searches"/>.
/// </summary>
public sealed class Harvester
{
    public HarvesterMode Mode { get; internal set; } = HarvesterMode.SeekOre;

    /// <summary>The field it works: ore searches start here. Null until it has found ore.</summary>
    public CellPos? Field { get; internal set; }

    /// <summary>The cell of ore it drives to or takes: claimed, so no other harvester goes there.</summary>
    public CellPos? Target { get; internal set; }

    /// <summary>The refinery it drives to, waits at or unloads at; none otherwise.</summary>
    public EntityId Refinery { get; internal set; }

    /// <summary>A refinery the player sent it to (a move order onto the refinery); chosen before any other while it stands.</summary>
    public EntityId Preferred { get; internal set; }

    /// <summary>Credits of ore it carries.</summary>
    public int Load { get; internal set; }

    /// <summary>Ticks spent on the stage of ore it is taking.</summary>
    public int Timer { get; internal set; }

    /// <summary>Ticks left to wait (<see cref="HarvesterMode.Waiting"/>) or to follow the player's order (<see cref="HarvesterMode.Manual"/>).</summary>
    public int WaitTicks { get; internal set; }

    /// <summary>After waiting, whether it looks for a refinery again rather than for ore.</summary>
    public bool WaitingForRefinery { get; internal set; }

    /// <summary>The tick it took its place in the queue of a dock: the earliest goes in first.</summary>
    public int QueueTick { get; internal set; }

    /// <summary>How many times it searched the map for ore: for the tests that it does not search every tick. Not state.</summary>
    public int Searches { get; internal set; }
}
