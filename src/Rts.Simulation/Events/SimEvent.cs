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

/// <summary>An engineer mended a bridge through its repair hut (loose ends, stage 2): its segments are whole again.</summary>
public sealed record BridgeRepaired(int Tick, int BridgeId) : SimEvent(Tick);

/// <summary>The unit died: for now only on a bridge segment that fell under it (weapons come in M4).</summary>
public sealed record UnitKilled(int Tick, EntityId Unit, WorldPos Position) : SimEvent(Tick);

/// <summary>A building took a hit and stands, with <paramref name="Hp"/> hit points left.</summary>
public sealed record BuildingDamaged(int Tick, EntityId Building, int Hp) : SimEvent(Tick);

/// <summary>
/// A unit or a building crossed a threshold of its hit points (M4, 4.4): healthy, damaged, critical. The client changes its health bar,
/// smoke and fire.
/// </summary>
public sealed record DamageStateChanged(int Tick, EntityId Entity, Combat.DamageState State) : SimEvent(Tick);

/// <summary>
/// A unit or a defense fired at its target (M4, 4.7) with the weapon in <paramref name="Slot"/>: 0 the primary, 1 the secondary.
/// <paramref name="Target"/> is null for a forced attack on the ground (step 7).
/// </summary>
public sealed record WeaponFired(int Tick, EntityId Shooter, EntityId? Target, int Slot) : SimEvent(Tick);

/// <summary>
/// A shot landed (M4, 4.8) at <paramref name="Point"/>: an instant one on the tick it was fired (no <paramref name="Projectile"/>),
/// a ballistic one at its aim point, a homing one on its target or where it fell. <paramref name="Target"/> is the living target it
/// was fired at, or none when that died or a homing shot ran out of time. <paramref name="Weapon"/> is the place of the weapon in the
/// rules. The damage comes from step 5 on.
/// </summary>
public sealed record ProjectileImpact(int Tick, EntityId? Projectile, EntityId Shooter, int Weapon, WorldPos Point, EntityId? Target)
    : SimEvent(Tick);

/// <summary>A unit or a building lost <paramref name="Amount"/> hit points to a shot of <paramref name="Attacker"/> (M4, 4.9).</summary>
public sealed record EntityDamaged(int Tick, EntityId Entity, EntityId Attacker, int Amount, int Hp) : SimEvent(Tick);

/// <summary>
/// <paramref name="Killer"/> destroyed a unit or a building (M4, 4.9): with a shot of <paramref name="Weapon"/> (its place in the
/// rules; its warhead tells the client the kind of death, step 9), or by driving over it (<see cref="DeathCause.Crushed"/>, step 8,
/// weapon -1). It leaves the world at the end of the tick. <paramref name="KillerOwner"/> is the player of the killer, kept from the shot
/// itself: the shooter may be gone before its shell lands (M5 step 11, the statistics); neutral without a killer.
/// </summary>
public sealed record EntityDestroyed(
    int Tick, EntityId Entity, EntityId Killer, DeathCause Cause = DeathCause.Weapon, int Weapon = -1, PlayerId KillerOwner = default)
    : SimEvent(Tick);

/// <summary>How a unit or a building died (M4).</summary>
public enum DeathCause
{
    /// <summary>Hit points taken by shots.</summary>
    Weapon,

    /// <summary>Driven over by a crushing vehicle (step 8).</summary>
    Crushed,

    /// <summary>What was left of a player who lost the match (M5 step 10).</summary>
    Defeat,
}

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

/// <summary>
/// Another player's shot hit a building of <paramref name="Player"/> at <paramref name="Point"/> (M4, 4.19, step 13): "our base is under
/// attack". Once in <see cref="Systems.DamageSystem.UnderAttackTicks"/> per player at most, so the notice does not repeat.
/// </summary>
public sealed record UnderAttack(int Tick, PlayerId Player, WorldPos Point) : SimEvent(Tick);

/// <summary><paramref name="Player"/> lost the match, by <see cref="Matches.VictorySystem"/> or a surrender (M5, 4.8, step 10).</summary>
public sealed record PlayerDefeated(int Tick, PlayerId Player) : SimEvent(Tick);

/// <summary>The match is over: <paramref name="Winners"/> are the players of one team still standing; none for a draw (M5 step 10).</summary>
public sealed record MatchEnded(int Tick, System.Collections.Immutable.ImmutableArray<PlayerId> Winners) : SimEvent(Tick);

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

/// <summary>The construction yard <paramref name="Building"/> packed up into the MCV <paramref name="Unit"/> (RA2 <c>UndeploysInto</c>).</summary>
public sealed record Undeployed(int Tick, EntityId Building, EntityId Unit) : SimEvent(Tick);

/// <summary>The MCV could not turn into its construction yard where it stands, for <paramref name="Problem"/>.</summary>
public sealed record CannotDeploy(int Tick, PlayerId Player, EntityId Unit, PlacementProblem Problem) : SimEvent(Tick);

/// <summary>A building has been produced and waits to be placed (docs/implementation/M3_base_economy.md, 4.10).</summary>
public sealed record ProductionReady(int Tick, PlayerId Player, int DefIndex) : SimEvent(Tick);

/// <summary>A unit came out of <paramref name="Factory"/>.</summary>
public sealed record UnitProduced(int Tick, PlayerId Player, EntityId Unit, EntityId Factory) : SimEvent(Tick);

/// <summary>Production of a category stopped for want of credits; it goes on by itself when they come.</summary>
public sealed record InsufficientFunds(int Tick, PlayerId Player, Rules.ProductionCategory Category) : SimEvent(Tick);

/// <summary>An item left its queue, and <paramref name="Refund"/> credits, all that was paid for it, went back.</summary>
public sealed record ProductionCancelled(int Tick, PlayerId Player, ProductionKind Kind, int DefIndex, int Refund) : SimEvent(Tick);

/// <summary>A harvester took the last stage of ore off <paramref name="Cell"/> (docs/implementation/M3_base_economy.md, 4.13).</summary>
public sealed record OreDepleted(int Tick, CellPos Cell) : SimEvent(Tick);

/// <summary>
/// The ore or gem source of map object id <paramref name="Source"/> threw a stage of <paramref name="Kind"/> onto <paramref name="Cell"/>
/// (docs/implementation/M3_base_economy.md, 4.12, step 16): for the client's drill and particles, not kept in the simulation.
/// </summary>
public sealed record OreSourceErupted(int Tick, int Source, CellPos Cell, Map.OreKind Kind) : SimEvent(Tick);

/// <summary>A harvester stands in the dock of <paramref name="Refinery"/> and starts to unload.</summary>
public sealed record HarvesterUnloading(int Tick, EntityId Harvester, EntityId Refinery) : SimEvent(Tick);

/// <summary>The player sold a building and got <paramref name="Refund"/> credits back (docs/implementation/M3_base_economy.md, 4.8).</summary>
public sealed record BuildingSold(int Tick, EntityId Building, PlayerId Player, int DefIndex, int Refund) : SimEvent(Tick);

/// <summary>The player turned the repair of a building on or off.</summary>
public sealed record RepairToggled(int Tick, EntityId Building, bool On) : SimEvent(Tick);

/// <summary>A repaired building is whole again; its repair turned off.</summary>
public sealed record RepairDone(int Tick, EntityId Building) : SimEvent(Tick);

/// <summary>A soldier went into <paramref name="Building"/> and left the map (stage 2e).</summary>
public sealed record UnitGarrisoned(int Tick, EntityId Unit, EntityId Building) : SimEvent(Tick);

/// <summary>A soldier came out of <paramref name="Building"/> as <paramref name="Unit"/>, a new unit (stage 2e).</summary>
public sealed record UnitLeftGarrison(int Tick, EntityId Unit, EntityId Building) : SimEvent(Tick);

/// <summary><paramref name="Supporter"/>, an idle tower, sent its charge to <paramref name="Tower"/>'s shot (stage 2e: the lens towers).</summary>
public sealed record TowerSupported(int Tick, EntityId Supporter, EntityId Tower) : SimEvent(Tick);

/// <summary>An engineer took <paramref name="Building"/> from <paramref name="From"/> for <paramref name="To"/> (stage 3b).</summary>
public sealed record BuildingCaptured(int Tick, EntityId Building, PlayerId From, PlayerId To) : SimEvent(Tick);

/// <summary>An engineer of the owner mended <paramref name="Building"/> whole (stage 3b).</summary>
public sealed record BuildingMended(int Tick, EntityId Building) : SimEvent(Tick);

/// <summary>A crate came onto <paramref name="Cell"/> (M11 a6).</summary>
public sealed record CrateAppeared(int Tick, int Crate, CellPos Cell) : SimEvent(Tick);

/// <summary><paramref name="Unit"/> of <paramref name="Player"/> took the crate on <paramref name="Cell"/>, which gave <paramref name="Kind"/> (M11 a6).</summary>
public sealed record CratePicked(int Tick, int Crate, PlayerId Player, EntityId Unit, Rules.CrateKind Kind, CellPos Cell) : SimEvent(Tick);

/// <summary>A unit rose to <paramref name="Rank"/> by its kills, or came out a veteran (loose ends, stage 3).</summary>
public sealed record UnitPromoted(int Tick, EntityId Unit, PlayerId Owner, Systems.Rank Rank) : SimEvent(Tick);

/// <summary>A hurt soldier was healed whole at a hospital (loose ends, stage 1).</summary>
public sealed record UnitHealed(int Tick, EntityId Unit, EntityId Hospital) : SimEvent(Tick);

/// <summary>Yuri's mind control took a unit: it is <paramref name="To"/>'s now, held by <paramref name="Yuri"/> (RA2).</summary>
public sealed record UnitControlled(int Tick, EntityId Yuri, EntityId Unit, PlayerId From, PlayerId To) : SimEvent(Tick);

/// <summary>A mind control broke: the unit is <paramref name="To"/>'s again, or nobody's when its player is out (RA2).</summary>
public sealed record UnitReleased(int Tick, EntityId Unit, PlayerId To) : SimEvent(Tick);

/// <summary>A soldier just trained came out once more, free, at the player's cloning vats (RA2).</summary>
public sealed record UnitCloned(int Tick, PlayerId Player, EntityId Unit, EntityId Vats) : SimEvent(Tick);

/// <summary>A soldier went into its owner's cloning vats and left for <paramref name="Credits"/> (RA2).</summary>
public sealed record UnitRecycled(int Tick, EntityId Unit, EntityId Vats, int Credits) : SimEvent(Tick);

/// <summary>
/// An infiltrator of <paramref name="Player"/> went into <paramref name="Building"/> and did <paramref name="Effect"/>; the credits it
/// took for a theft (stage 3b).
/// </summary>
public sealed record BuildingInfiltrated(int Tick, EntityId Building, PlayerId Player, Rules.SpyEffect Effect, int Credits) : SimEvent(Tick);

/// <summary>A bomb was planted on <paramref name="Target"/>; it goes off in <paramref name="Ticks"/> (stage 3b).</summary>
public sealed record BombPlanted(int Tick, EntityId Target, EntityId Shooter, int Ticks) : SimEvent(Tick);

/// <summary><paramref name="Unit"/> jumped from <paramref name="From"/> to <paramref name="To"/> and phases in for a while (stage 3b).</summary>
public sealed record UnitTeleported(int Tick, EntityId Unit, WorldPos From, WorldPos To) : SimEvent(Tick);

/// <summary><paramref name="Unit"/> boarded <paramref name="Transport"/> and left the map (stage 3d).</summary>
public sealed record UnitBoarded(int Tick, EntityId Unit, EntityId Transport) : SimEvent(Tick);

/// <summary>A passenger came out of <paramref name="Transport"/> as <paramref name="Unit"/>, a new unit (stage 3d).</summary>
public sealed record UnitUnloaded(int Tick, EntityId Unit, EntityId Transport) : SimEvent(Tick);

/// <summary>The parasite <paramref name="Parasite"/> went into <paramref name="Host"/> (stage 3d).</summary>
public sealed record ParasiteEntered(int Tick, EntityId Parasite, EntityId Host) : SimEvent(Tick);

/// <summary>A workshop mending <paramref name="Host"/> killed the parasite inside (stage 3d).</summary>
public sealed record ParasiteKilled(int Tick, EntityId Host) : SimEvent(Tick);

/// <summary>A hit freed <paramref name="Host"/> from the parasite that held it (the dolphin's wave on the squid's ship).</summary>
public sealed record ParasiteFreed(int Tick, EntityId Host) : SimEvent(Tick);

/// <summary><paramref name="Mother"/>, a carrier, launched <paramref name="Unit"/> at its target.</summary>
public sealed record SpawnLaunched(int Tick, EntityId Unit, EntityId Mother) : SimEvent(Tick);

/// <summary>The superweapon <paramref name="Building"/> of <paramref name="Player"/> is charged (stage 2d): every player hears it.</summary>
public sealed record SuperweaponReady(int Tick, EntityId Building, PlayerId Player, Rules.SuperweaponKind Kind) : SimEvent(Tick);

/// <summary>The superweapon <paramref name="Building"/> of <paramref name="Player"/> fired at <paramref name="Target"/> (stage 2d).</summary>
public sealed record SuperweaponFired(int Tick, EntityId Building, PlayerId Player, Rules.SuperweaponKind Kind, CellPos Target) : SimEvent(Tick);

/// <summary>A lightning of a storm struck <paramref name="Point"/> (stage 2d).</summary>
public sealed record LightningStrike(int Tick, WorldPos Point) : SimEvent(Tick);
