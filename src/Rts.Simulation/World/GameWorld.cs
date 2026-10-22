using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Random;
using Rts.Simulation.Rules;
using Rts.Simulation.Spatial;

namespace Rts.Simulation.World;

/// <summary>
/// Complete simulation state: tick number, entities, id counter, random generator and the map.
/// Entities are kept sorted by id; iterating <see cref="Entities"/> is always in id order,
/// which keeps every system deterministic.
/// </summary>
public sealed class GameWorld
{
    private readonly List<Entity> _entities = new();
    private readonly PlayerState[] _players;
    private int _nextEntityId = 1;

    /// <summary>
    /// A world on a map of clear land that goes by the land speeds of <paramref name="rules"/>; every player starts with
    /// <paramref name="startingCredits"/>.
    /// </summary>
    public GameWorld(ulong seed, int widthCells, int heightCells, GameRules rules, int startingCredits = SimulationSettings.DefaultStartingCredits)
        : this(seed, new MapGrid(widthCells, heightCells, rules.Land), rules, startingCredits)
    {
    }

    /// <summary>
    /// A world on the given map, which should go by the land speeds of <paramref name="rules"/> (<see cref="MapGrid.Clone"/>). The
    /// buildings the map places at the start (M7 step 7) become its first entities, in the order of <see cref="MapGrid.StartBuildings"/>,
    /// at full hit points; the map already marks their cells as taken. Every player 1…8 starts with <paramref name="startingCredits"/>
    /// and the first faction of the data.
    /// </summary>
    internal GameWorld(ulong seed, MapGrid map, GameRules rules, int startingCredits = SimulationSettings.DefaultStartingCredits)
    {
        Map = map;
        Rules = rules;
        _players = new PlayerState[PlayerId.MaxPlayers];
        for (var i = 0; i < _players.Length; i++)
        {
            _players[i] = new PlayerState(new PlayerId(i + 1), rules.DefaultFaction, startingCredits, rules.Database);
        }

        Random = new DeterministicRandom(seed);
        PathRequests = new PathRequestQueue(map);
        Reservations = new Reservations(map.Width, map.Height);
        Spatial = new SpatialIndex(map.Width, map.Height);
        foreach (var start in map.StartBuildings)
        {
            _entities.Add(new Building(AllocateId(), start.Owner, start.Kind, start.Cell, start.Facing));
        }

        // Power and technologies as the match starts, so a command of the first tick sees them.
        Systems.PowerSystem.Update(this, null);
        Systems.TechTree.Update(this);
    }

    public int CurrentTick { get; internal set; }

    public DeterministicRandom Random { get; }

    public MapGrid Map { get; }

    /// <summary>The rules of the match, from the data files (docs/implementation/M3_base_economy.md, 4.5). Not state.</summary>
    public GameRules Rules { get; }

    /// <summary>The economy of the players 1…8, in order (docs/implementation/M3_base_economy.md, 4.7).</summary>
    public IReadOnlyList<PlayerState> Players => _players;

    /// <summary>
    /// Set when the buildings of a player changed (one is placed, finished growing or destroyed); <see cref="Systems.PowerSystem"/>
    /// works the power out again on its turn of the tick and clears it. Derived; also worked out once when the world is made.
    /// </summary>
    internal bool PowerDirty { get; set; } = true;

    /// <summary>The same for <see cref="Systems.TechTree"/>, which runs late in the tick (4.15).</summary>
    internal bool TechDirty { get; set; } = true;

    /// <summary>Path searches waiting for their share of the per-tick node budget.</summary>
    public PathRequestQueue PathRequests { get; }

    /// <summary>Which unit stands in which cell: one unit per cell.</summary>
    public Reservations Reservations { get; }

    /// <summary>Units by area, for "who is near" queries. Derived from positions, not part of the state hash.</summary>
    public SpatialIndex Spatial { get; }

    public int WidthCells => Map.Width;

    public int HeightCells => Map.Height;

    /// <summary>Id the next spawned entity will get. Part of the state hash.</summary>
    public EntityId NextEntityId => new(_nextEntityId);

    /// <summary>All entities in increasing id order, including ones marked dead during the current tick.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>True when the position is inside the map.</summary>
    public bool Contains(WorldPos position) =>
        position.X >= 0 && position.Y >= 0
        && position.X < WidthCells * WorldPos.LeptonsPerCell
        && position.Y < HeightCells * WorldPos.LeptonsPerCell;

    /// <summary>
    /// Finds an entity by id (binary search). An entity marked dead this tick is still found,
    /// with <see cref="Entity.IsAlive"/> == false, until <see cref="RemoveDead"/> runs.
    /// </summary>
    public bool TryGet(EntityId id, out Entity entity)
    {
        var low = 0;
        var high = _entities.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var candidate = _entities[middle];
            var comparison = candidate.Id.CompareTo(id);
            if (comparison == 0)
            {
                entity = candidate;
                return true;
            }

            if (comparison < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        entity = null!;
        return false;
    }

    /// <param name="snapToSubcell">Put a soldier on the spot it takes (commands); tests keep the exact position.</param>
    /// <summary>
    /// Changes the map during a match (docs/implementation/M7_maps.md, 4.4): applies the edits, patches the A* step costs in the
    /// changed area only, restarts path searches already under way, and sends every unit whose remaining path now crosses a
    /// cell it cannot enter to look for a new one (not counted as a replan). A cell that opens up does not disturb units on
    /// their way, as in RA2: the next search finds it. Reports <see cref="Events.MapChanged"/> with the changed area.
    /// </summary>
    internal void ChangeMap(IReadOnlyList<MapEdit> edits, List<Events.SimEvent>? events)
    {
        if (edits.Count == 0)
        {
            return;
        }

        var versionBefore = Map.Version;
        var (from, to) = edits[0].Area(Map);
        foreach (var edit in edits)
        {
            var (editFrom, editTo) = edit.Area(Map);
            from = new CellPos(Math.IntMath.Min(from.X, editFrom.X), Math.IntMath.Min(from.Y, editFrom.Y));
            to = new CellPos(Math.IntMath.Max(to.X, editTo.X), Math.IntMath.Max(to.Y, editTo.Y));
            edit.Apply(Map);
        }

        PathRequests.Pathfinder.MapChanged(versionBefore, from, to);
        PathRequests.MapChanged();
        foreach (var entity in _entities)
        {
            if (entity is Unit { IsAlive: true, Path: { } path, MoveTarget: { } target } unit && CrossesClosedCell(unit, path, from, to))
            {
                Movement.UnitOrders.RequestPath(this, unit, target, default);
            }
        }

        events?.Add(new Events.MapChanged(CurrentTick, from, to));
    }

    /// <summary>
    /// Damages what stands on the cell (debug command now, weapons in M4): the first destructible standing map object, or else a
    /// standing bridge segment (M7 step 6), or else the standing building whose footprint covers it (step 7). At 0 hit points
    /// it is destroyed through <see cref="ChangeMap"/>, so paths learn about the change; reports
    /// <see cref="Events.MapObjectDestroyed"/>, <see cref="Events.BridgeSegmentDamaged"/> and
    /// <see cref="Events.BridgeSegmentDestroyed"/>, or <see cref="Events.BuildingDamaged"/> and <see cref="Events.BuildingDestroyed"/>.
    /// Returns false when there is nothing on the cell that can be damaged.
    /// </summary>
    /// <summary>
    /// Sets the hit points of a building (M4, 4.4) and reports <see cref="Events.DamageStateChanged"/> when that crosses a threshold:
    /// the one way damage and repair change them.
    /// </summary>
    internal void SetHp(Building building, int hp, List<Events.SimEvent>? events)
    {
        var before = Combat.DamageStates.Of(building.Hp, building.MaxHp);
        building.Hp = hp;
        var after = Combat.DamageStates.Of(hp, building.MaxHp);
        if (after != before && after != Combat.DamageState.Destroyed)
        {
            events?.Add(new Events.DamageStateChanged(CurrentTick, building.Id, after));
        }
    }

    internal bool DamageAtCell(CellPos cell, int amount, List<Events.SimEvent>? events)
    {
        foreach (var mapObject in Map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive && mapObject.Type.Destructible)
            {
                if (mapObject.Hp > amount)
                {
                    Map.SetObjectHp(mapObject.Id, mapObject.Hp - amount);
                }
                else
                {
                    DestroyMapObject(mapObject, events);
                }

                return true;
            }
        }

        if (Map.BridgeAt(cell) is { IsIntact: true } segment)
        {
            if (segment.Hp > amount)
            {
                Map.SetSegmentHp(segment, segment.Hp - amount);
                events?.Add(new Events.BridgeSegmentDamaged(CurrentTick, segment.Bridge.Id, segment.Cell, segment.Hp));
            }
            else
            {
                DestroyBridgeSegment(segment, events);
            }

            return true;
        }

        if (Map.HasBuilding(cell) && BuildingAt(cell) is { } building)
        {
            if (building.Hp > amount)
            {
                SetHp(building, building.Hp - amount, events);
                events?.Add(new Events.BuildingDamaged(CurrentTick, building.Id, building.Hp));
            }
            else
            {
                DestroyBuilding(building, events);
            }

            return true;
        }

        return false;
    }

    /// <summary>The economy of a player 1…8.</summary>
    public PlayerState Player(PlayerId player) =>
        player.Value is >= 1 and <= PlayerId.MaxPlayers
            ? _players[player.Value - 1]
            : throw new ArgumentOutOfRangeException(nameof(player), player, "Only the players 1…8 have an economy.");

    /// <summary>
    /// Creates a building of <paramref name="owner"/> from the data and takes its cells through <see cref="ChangeMap"/>, so paths learn
    /// about it (docs/implementation/M3_base_economy.md, 4.8). Where a building may stand is the business of the placement rules and
    /// commands of M3 steps 6–7; here only the footprint must be on the map and free of other buildings.
    /// </summary>
    /// <param name="ready">False for a building that grows for the <c>build_up</c> of its definition first: it gives no power and
    /// opens nothing until <see cref="CompleteBuildUp"/>.</param>
    internal Building CreateBuilding(PlayerId owner, BuildingDef def, CellPos cell, bool ready, List<Events.SimEvent>? events)
    {
        var index = Rules.Database.IndexOfBuilding(def.Id);
        if (index < 0 || !ReferenceEquals(Rules.Database.Buildings[index], def))
        {
            throw new ArgumentException($"'{def.Id}' is not a building of the rules of this match.", nameof(def));
        }

        var far = new CellPos(cell.X + def.Footprint.Width - 1, cell.Y + def.Footprint.Height - 1);
        if (!Map.Contains(cell) || !Map.Contains(far))
        {
            throw new ArgumentException($"{def.Id} at ({cell.X}, {cell.Y}) reaches outside the map.", nameof(cell));
        }

        for (var y = cell.Y; y <= far.Y; y++)
        {
            for (var x = cell.X; x <= far.X; x++)
            {
                if (Map.HasBuilding(new CellPos(x, y)))
                {
                    throw new ArgumentException($"{def.Id} at ({cell.X}, {cell.Y}) covers another building at ({x}, {y}).", nameof(cell));
                }
            }
        }

        var building = new Building(AllocateId(), owner, def, index, cell, ready ? 0 : def.BuildUp);
        _entities.Add(building);
        ChangeMap([new TakeBuildingEdit(cell, far)], events);
        BuildingsChanged();
        return building;
    }

    /// <summary>A building has grown: from now on it gives power and counts as a prerequisite.</summary>
    internal void CompleteBuildUp(Building building)
    {
        building.BuildUpLeft = 0;
        BuildingsChanged();
    }

    /// <summary>Power and technologies are worked out again on their next turn.</summary>
    internal void BuildingsChanged()
    {
        PowerDirty = true;
        TechDirty = true;
    }

    /// <summary>The standing building whose footprint covers the cell, or null.</summary>
    public Building? BuildingAt(CellPos cell)
    {
        foreach (var entity in _entities)
        {
            if (entity is Building { IsAlive: true } building && building.Covers(cell))
            {
                return building;
            }
        }

        return null;
    }

    /// <summary>
    /// A unit entered <paramref name="cell"/>: it crushes the standing objects there that its locomotor flattens (a fence under
    /// tracks, a lamp post under anyone).
    /// </summary>
    internal void CrushMapObjects(CellPos cell, Locomotor locomotor, List<Events.SimEvent>? events)
    {
        foreach (var mapObject in Map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive && mapObject.Type.CrushedBy(locomotor))
            {
                DestroyMapObject(mapObject, events);
            }
        }
    }

    private void DestroyMapObject(MapObject mapObject, List<Events.SimEvent>? events)
    {
        ChangeMap([new DestroyObjectEdit(mapObject.Id)], events);
        events?.Add(new Events.MapObjectDestroyed(CurrentTick, mapObject.Id, mapObject.Kind, mapObject.Cell));
    }

    /// <summary>
    /// A bridge segment falls (docs/implementation/M7_maps.md, 4.6): its cell is water again, paths are rebuilt through
    /// <see cref="ChangeMap"/>, and the ground units on it drown. Who can stay on water (amphibious, hover) stays. The dead keep
    /// their place in the cell until the end of the tick, like every unit that dies.
    /// </summary>
    private void DestroyBridgeSegment(BridgeSegment segment, List<Events.SimEvent>? events)
    {
        ChangeMap([new DestroyBridgeSegmentEdit(segment.Bridge.Id, segment.Index)], events);
        events?.Add(new Events.BridgeSegmentDestroyed(CurrentTick, segment.Bridge.Id, segment.Cell));
        foreach (var entity in _entities)
        {
            if (entity is Unit { IsAlive: true } unit && unit.Position.ToCell() == segment.Cell && !Map.CanEnter(segment.Cell, unit.Archetype.Locomotor))
            {
                unit.MarkDead();
                events?.Add(new Events.UnitKilled(CurrentTick, unit.Id, unit.Position));
            }
        }
    }

    /// <summary>
    /// A building falls (docs/implementation/M7_maps.md, 4.8): it dies (leaves the world at the end of the tick) and its
    /// footprint opens at once through <see cref="ChangeMap"/>. No ruins in M7.
    /// </summary>
    private void DestroyBuilding(Building building, List<Events.SimEvent>? events)
    {
        building.Hp = 0;
        TakeDown(building, events);
        events?.Add(new Events.BuildingDestroyed(CurrentTick, building.Id, building.Kind, building.Cell));
    }

    /// <summary>
    /// The player sells a building (docs/implementation/M3_base_economy.md, 4.8): <paramref name="refund"/> credits come back, the
    /// building leaves the world at the end of the tick and its cells open at once.
    /// </summary>
    internal void SellBuilding(Building building, int refund, List<Events.SimEvent>? events)
    {
        Player(building.Owner).Credits += refund;
        TakeDown(building, events);
        events?.Add(new Events.BuildingSold(CurrentTick, building.Id, building.Owner, building.DefIndex, refund));
    }

    /// <summary>A building goes: it dies, its cells open through <see cref="ChangeMap"/>, power and technologies are worked out again.</summary>
    private void TakeDown(Building building, List<Events.SimEvent>? events)
    {
        building.MarkDead();
        building.Repairing = false;
        ChangeMap([new ClearBuildingEdit(building.Cell, building.FarCell)], events);
        BuildingsChanged();
    }

    /// <summary>True when a cell of the path still ahead of the unit lies in the area and is closed to it now.</summary>
    private bool CrossesClosedCell(Unit unit, Pathfinding.CellPath path, CellPos from, CellPos to)
    {
        var here = unit.Position.ToCell();
        var start = path.Cells.IndexOf(here);
        for (var i = start < 0 ? 0 : start; i < path.Cells.Length; i++)
        {
            var cell = path.Cells[i];
            if (cell.X >= from.X && cell.X <= to.X && cell.Y >= from.Y && cell.Y <= to.Y && !Map.CanEnter(cell, unit.Archetype.Locomotor))
            {
                return true;
            }
        }

        return false;
    }

    internal Unit SpawnUnit(PlayerId owner, UnitType type, WorldPos position, bool snapToSubcell = false)
    {
        var unit = new Unit(AllocateId(), owner, Rules.Unit(type), position);
        _entities.Add(unit);
        Spatial.Add(unit);
        // Commands never spawn where there is no room; a test that places two units in one cell gets an unregistered second one.
        if (Contains(position))
        {
            var cell = position.ToCell();
            var subcell = Reservations.TryEnter(cell, unit, position);
            if (subcell >= 0)
            {
                unit.Subcell = subcell;
                if (snapToSubcell && unit.Archetype.Placement == UnitPlacement.Subcell)
                {
                    unit.Position = Reservations.SubcellPoint(cell, subcell);
                }
            }
        }

        return unit;
    }

    /// <summary>Physically removes entities marked dead and frees their cells. Called once at the end of a tick.</summary>
    internal void RemoveDead()
    {
        foreach (var entity in _entities)
        {
            if (entity is Unit { IsAlive: false } unit)
            {
                Spatial.Remove(unit.Id);
                if (Contains(unit.Position))
                {
                    Reservations.Leave(unit.Position.ToCell(), unit);
                }
            }
        }

        _entities.RemoveAll(entity => !entity.IsAlive);
    }

    private EntityId AllocateId() => new(_nextEntityId++);
}
