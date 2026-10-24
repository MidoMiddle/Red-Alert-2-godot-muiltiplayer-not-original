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
    private readonly Visibility.VisibilityGrid[] _vision;
    private readonly int[] _allies;
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
    internal GameWorld(
        ulong seed, MapGrid map, GameRules rules, int startingCredits = SimulationSettings.DefaultStartingCredits, Matches.MatchSetup? match = null)
    {
        Map = map;
        Rules = rules;
        _players = new PlayerState[PlayerId.MaxPlayers];
        for (var i = 0; i < _players.Length; i++)
        {
            _players[i] = new PlayerState(new PlayerId(i + 1), rules.DefaultFaction, startingCredits, rules.Database);
        }

        _vision = new Visibility.VisibilityGrid[PlayerId.MaxPlayers];
        for (var i = 0; i < _vision.Length; i++)
        {
            _vision[i] = new Visibility.VisibilityGrid(map.Width, map.Height);
        }

        Random = new DeterministicRandom(seed);
        PathRequests = new PathRequestQueue(map);
        Reservations = new Reservations(map.Width, map.Height);
        Radiation = new ushort[map.Width * map.Height];
        Spatial = new SpatialIndex(map.Width, map.Height);
        foreach (var start in map.StartBuildings)
        {
            _entities.Add(new Building(AllocateId(), start.Owner, start.Kind, start.Cell, start.Facing));
        }

        // A match: the factions and the starting units of its players (M5 step 2), and the players of a team are allies (M8 step 11).
        _allies = Alliances(match);
        Match = match;
        if (match is not null)
        {
            Matches.MatchStart.Apply(this, match);
        }

        // Power and technologies as the match starts, so a command of the first tick sees them.
        Systems.PowerSystem.Update(this, null);
        Systems.TechTree.Update(this);
    }

    public int CurrentTick { get; internal set; }

    /// <summary>The match this world plays, or null in the sandbox: victory and defeat only in a match (M5 step 10). Not state.</summary>
    public Matches.MatchSetup? Match { get; }

    /// <summary>
    /// How the match ended, or null while it goes on (M5 step 10). Not hashed: it follows from <see cref="PlayerState.DefeatedTick"/>.
    /// </summary>
    public Matches.MatchResult? Result { get; internal set; }

    /// <summary>What every player did (M5 step 11), in the sandbox too. Not hashed: the game does not depend on it.</summary>
    public Matches.MatchStats Stats { get; } = new();

    public DeterministicRandom Random { get; }

    public MapGrid Map { get; }

    /// <summary>The rules of the match, from the data files (docs/implementation/M3_base_economy.md, 4.5). Not state.</summary>
    public GameRules Rules { get; }

    /// <summary>The economy of the players 1…8, in order (docs/implementation/M3_base_economy.md, 4.7).</summary>
    public PlayerList Players => new(_players);

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

    /// <summary>
    /// The shots that landed this tick (M4 step 5): <see cref="Systems.WeaponSystem"/> and <see cref="Systems.ProjectileSystem"/> add,
    /// <see cref="Systems.DamageSystem"/> applies and empties it in the same tick, so it is never state.
    /// </summary>
    internal List<Combat.Hit> Hits { get; } = [];

    /// <summary>
    /// The weapons that go off where something died this tick — a unit's death weapon (stage 3b) — launched by
    /// <see cref="Systems.BombSystem"/> after the movement, as a death may come while the entities are walked. Empty between ticks.
    /// </summary>
    internal List<(PlayerId Owner, EntityId Shooter, int Weapon, WorldPos Point)> DeathBlasts { get; } = [];

    /// <summary>The level of toxin on every cell, in row order (stage 3b, <see cref="Systems.RadiationSystem"/>). State: hashed.</summary>
    internal ushort[] Radiation { get; }

    /// <summary>The level of toxin on <paramref name="cell"/>, 0 off the map: the client shows it.</summary>
    public int RadiationAt(CellPos cell) => Map.Contains(cell) ? Radiation[cell.Y * WidthCells + cell.X] : 0;

    /// <summary>
    /// The buildings that fell this tick (M4 step 9): their cells open at the end of the tick (<see cref="OpenWrecks"/>). Emptied in
    /// the same tick: never state.
    /// </summary>
    internal List<Building> Wrecks { get; } = [];

    /// <summary>
    /// Lists the systems fill and empty within one call (M4 step 15): kept here so a tick allocates none. Never state, and no system
    /// relies on what another left in them.
    /// </summary>
    internal List<EntityId> Nearby { get; } = [];

    /// <inheritdoc cref="Nearby"/>
    internal List<EntityId> Area { get; } = [];

    /// <inheritdoc cref="Nearby"/>
    internal List<Combat.Shot> Shots { get; } = [];

    /// <summary>The ready factories of every player by category and the main one of each (production, M4 step 15): reused every tick.</summary>
    internal int[,] FactoryCounts { get; } = new int[PlayerId.MaxPlayers, Rts.Simulation.Rules.ProductionCategories.Count];

    /// <inheritdoc cref="FactoryCounts"/>
    internal Building?[,] MainFactories { get; } = new Building?[PlayerId.MaxPlayers, Rts.Simulation.Rules.ProductionCategories.Count];

    public int WidthCells => Map.Width;

    public int HeightCells => Map.Height;

    /// <summary>Id the next spawned entity will get. Part of the state hash.</summary>
    public EntityId NextEntityId => new(_nextEntityId);

    /// <summary>All entities in increasing id order, including ones marked dead during the current tick.</summary>
    public EntityList Entities => new(_entities);

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
    internal void SetHp(Unit unit, int hp, List<Events.SimEvent>? events)
    {
        var before = Combat.DamageStates.Of(unit.Hp, unit.MaxHp);
        unit.Hp = hp;
        var after = Combat.DamageStates.Of(hp, unit.MaxHp);
        if (after != before && after != Combat.DamageState.Destroyed)
        {
            events?.Add(new Events.DamageStateChanged(CurrentTick, unit.Id, after));
        }
    }

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

    /// <summary>What a player 1…8 sees (M4 step 11).</summary>
    public Visibility.VisibilityGrid VisionOf(PlayerId player) =>
        player.Value is >= 1 and <= PlayerId.MaxPlayers
            ? _vision[player.Value - 1]
            : throw new ArgumentOutOfRangeException(nameof(player), player, "Only the players 1…8 see.");

    /// <summary>
    /// The allies of a player and the player itself, as a mask of bit <c>player − 1</c> (M4, 4.14; M8 step 11): the players of its team
    /// in the match (<see cref="Matches.MatchPlayer.Team"/>). They share their sight; none of them is the enemy of another — not a target,
    /// not crushed, not chased. Without a match every player is alone. Follows from the setup, which the replay keeps: not state.
    /// </summary>
    public int AlliesOf(PlayerId player) => player.Value is >= 1 and <= PlayerId.MaxPlayers ? _allies[player.Value - 1] : 0;

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> are one player or allies; the neutral player is nobody's ally.</summary>
    public bool AreAllies(PlayerId a, PlayerId b) =>
        a == b || (b.Value is >= 1 and <= PlayerId.MaxPlayers && (AlliesOf(a) & (1 << (b.Value - 1))) != 0);

    /// <summary>Every player its own bit; the players of one team of <paramref name="match"/> each other's too.</summary>
    private static int[] Alliances(Matches.MatchSetup? match)
    {
        var allies = new int[PlayerId.MaxPlayers];
        for (var i = 0; i < allies.Length; i++)
        {
            allies[i] = 1 << i;
        }

        if (match is not null)
        {
            foreach (var a in match.Players)
            {
                foreach (var b in match.Players)
                {
                    if (a.Team == b.Team && a.Slot is >= 1 and <= PlayerId.MaxPlayers && b.Slot is >= 1 and <= PlayerId.MaxPlayers)
                    {
                        allies[a.Slot - 1] |= 1 << (b.Slot - 1);
                    }
                }
            }
        }

        return allies;
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
            if (entity is Unit { IsAlive: true } unit && !unit.Archetype.Flies && unit.Position.ToCell() == segment.Cell
                && !Map.CanEnter(segment.Cell, unit.Archetype.Locomotor))
            {
                unit.MarkDead();
                events?.Add(new Events.UnitKilled(CurrentTick, unit.Id, unit.Position));
            }
        }
    }

    /// <summary>
    /// A building falls (docs/implementation/M7_maps.md, 4.8; M4, 4.16): it dies at once — power and technologies are worked out again
    /// — and leaves the world at the end of the tick; its footprint opens then too (<see cref="OpenWrecks"/>), so nobody drives or
    /// builds into it on the tick it fell.
    /// </summary>
    internal void DestroyBuilding(Building building, List<Events.SimEvent>? events)
    {
        building.Hp = 0;
        building.MarkDead();
        building.Repairing = false;
        BuildingsChanged();
        Wrecks.Add(building);
        events?.Add(new Events.BuildingDestroyed(CurrentTick, building.Id, building.Kind, building.Cell));
    }

    /// <summary>The cells of the buildings that fell this tick open through <see cref="ChangeMap"/> (M4 step 9).</summary>
    internal void OpenWrecks(List<Events.SimEvent>? events)
    {
        foreach (var wreck in Wrecks)
        {
            ChangeMap([new ClearBuildingEdit(wreck.Cell, wreck.FarCell)], events);
        }

        Wrecks.Clear();
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
        // Commands never spawn where there is no room; a test that places two units in one cell gets an unregistered second one. A
        // flier takes no cell (stage 2b).
        if (Contains(position) && !unit.Archetype.Flies)
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

    /// <summary>A shot in flight (M4 step 4): a new entity with the next id; it starts to fly on the next tick.</summary>
    internal Projectile Launch(PlayerId owner, EntityId shooter, int weapon, EntityId? target, WorldPos origin, WorldPos aimPoint)
    {
        var projectile = new Projectile(AllocateId(), owner, shooter, weapon, target, origin, aimPoint, CurrentTick);
        if (Rules.Database.TryGetProjectile(Rules.Database.Weapons[weapon].Projectile, out var shot))
        {
            projectile.Hp = shot.Hp;
        }

        _entities.Add(projectile);
        return projectile;
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
