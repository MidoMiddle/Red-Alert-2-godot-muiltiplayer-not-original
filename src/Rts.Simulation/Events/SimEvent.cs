using Rts.Simulation.Geometry;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Events;

/// <summary>
/// Something that happened during a tick. The client plays sounds and effects from events;
/// events never feed back into the simulation and are not part of the state hash.
/// </summary>
public abstract record SimEvent(int Tick);

public sealed record UnitSpawned(int Tick, EntityId Unit, PlayerId Owner, UnitType Type, WorldPos Position) : SimEvent(Tick);

public sealed record UnitMoveStarted(int Tick, EntityId Unit, WorldPos Target) : SimEvent(Tick);

public sealed record UnitArrived(int Tick, EntityId Unit, WorldPos Position) : SimEvent(Tick);

/// <summary>The path search for the unit finished; it starts driving along the path.</summary>
public sealed record UnitPathFound(int Tick, EntityId Unit, PathStatus Status, int Cells) : SimEvent(Tick);

/// <summary>The unit stopped in front of a cell it cannot enter and plans its way again from there.</summary>
public sealed record UnitBlocked(int Tick, EntityId Unit, WorldPos Position) : SimEvent(Tick);

/// <summary>A map object fell: a tree to a stump, a fence crushed by tracks, a post knocked over, a wall shot down.</summary>
public sealed record MapObjectDestroyed(int Tick, int ObjectId, Map.MapObjectKind Kind, CellPos Cell) : SimEvent(Tick);

/// <summary>A bridge segment took a hit and stands, with <paramref name="Hp"/> hit points left.</summary>
public sealed record BridgeSegmentDamaged(int Tick, int BridgeId, CellPos Cell, int Hp) : SimEvent(Tick);

/// <summary>A bridge segment fell into the water; its neighbours stand.</summary>
public sealed record BridgeSegmentDestroyed(int Tick, int BridgeId, CellPos Cell) : SimEvent(Tick);

/// <summary>The unit died: for now only on a bridge segment that fell under it (weapons come in M4).</summary>
public sealed record UnitKilled(int Tick, EntityId Unit, WorldPos Position) : SimEvent(Tick);

/// <summary>A building took a hit and stands, with <paramref name="Hp"/> hit points left.</summary>
public sealed record BuildingDamaged(int Tick, EntityId Building, int Hp) : SimEvent(Tick);

/// <summary>A building fell; its footprint from <paramref name="Cell"/> is open now.</summary>
public sealed record BuildingDestroyed(int Tick, EntityId Building, BuildingKind Kind, CellPos Cell) : SimEvent(Tick);

/// <summary>The map changed in the cells from <paramref name="From"/> to <paramref name="To"/> (inclusive): the client redraws them.</summary>
public sealed record MapChanged(int Tick, CellPos From, CellPos To) : SimEvent(Tick);

/// <summary>
/// <paramref name="Unit"/>, blocked by <paramref name="Other"/> of the same player standing nearer its place, gave it that place
/// and takes the other's spot instead, so a group fills its places from the inside.
/// </summary>
public sealed record UnitPlacesSwapped(int Tick, EntityId Unit, EntityId Other) : SimEvent(Tick);

/// <summary>The player drains more power than it produces from now on (docs/implementation/M3_base_economy.md, 4.11).</summary>
public sealed record LowPower(int Tick, PlayerId Player) : SimEvent(Tick);

/// <summary>The player produces enough power again.</summary>
public sealed record PowerRestored(int Tick, PlayerId Player) : SimEvent(Tick);

/// <summary>A building of a player was placed (or came from an MCV) and starts to grow (docs/implementation/M3_base_economy.md, 4.8).</summary>
public sealed record BuildingPlaced(int Tick, EntityId Building, PlayerId Player, int DefIndex, CellPos Cell) : SimEvent(Tick);

/// <summary>A building has grown: it gives power and counts as a prerequisite from now on.</summary>
public sealed record BuildUpComplete(int Tick, EntityId Building) : SimEvent(Tick);

/// <summary>A building could not be placed at <paramref name="Cell"/> (north-west cell), for <paramref name="Problem"/>.</summary>
public sealed record CannotPlace(int Tick, PlayerId Player, int DefIndex, CellPos Cell, PlacementProblem Problem) : SimEvent(Tick);

/// <summary>The MCV turned into <paramref name="Building"/>, its construction yard (4.9).</summary>
public sealed record Deployed(int Tick, EntityId Unit, EntityId Building) : SimEvent(Tick);

/// <summary>The MCV could not turn into its construction yard where it stands, for <paramref name="Problem"/>.</summary>
public sealed record CannotDeploy(int Tick, PlayerId Player, EntityId Unit, PlacementProblem Problem) : SimEvent(Tick);
