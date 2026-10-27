using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.View;

/// <summary>An enemy building as the bot saw it last.</summary>
/// <param name="DefIndex">Its kind, an index of the buildings of the rules; -1 for a building of the map (a tech or civilian building).</param>
/// <param name="SeenTick">The last tick it was seen on.</param>
public sealed record RememberedBuilding(EntityId Id, PlayerId Owner, int DefIndex, CellPos Cell, CellPos FarCell, int Hp, int SeenTick);

/// <summary>
/// A building of the map as the bot saw it last (M11 step 3): a tech building an engineer may take, a civilian one soldiers may hold —
/// whose it was, how hurt, how many soldiers in it.
/// </summary>
public sealed record RememberedMapBuilding(EntityId Id, BuildingKind Kind, PlayerId Owner, CellPos Cell, CellPos FarCell, int Hp, int MaxHp,
    int Occupants, int SeenTick);

/// <summary>
/// A building of a faction the map placed for nobody, as the bot knows it (2026-10-27, the maps of RA2): from the map before the match, then
/// as it saw it last. <paramref name="Reach"/> is the longest range of its weapons in whole cells, 0 for one that does not fire;
/// <paramref name="HitsGround"/> whether one of them hits what goes on the ground.
/// </summary>
public sealed record RememberedNobodysBuilding(EntityId Id, int DefIndex, CellPos Cell, CellPos FarCell, int Hp, int Reach, bool HitsGround,
    int SeenTick)
{
    /// <summary>Whether it fires at the bot's units: a defense of nobody, which fires at every player (<see cref="Rts.Simulation.Combat.Hostility"/>).</summary>
    public bool Fires => Reach > 0;
}

/// <summary>
/// Enemy units seen together in one block of <see cref="AiMemory.BlockCells"/> × <see cref="AiMemory.BlockCells"/> cells: where they stood on
/// average, how strong they were (cost × the share of hit points left, as the strength of a squad in 4.6), how many and how many of them
/// infantry (step 6), when.
/// </summary>
public sealed record ArmySighting(int BlockX, int BlockY, WorldPos Centre, int Strength, int Units, int Infantry, int SeenTick)
{
    /// <summary>Of the units the armed ones but the harvesters (M8 step 10a): a harvester alone guards nothing.</summary>
    public int Armed { get; init; }
}

/// <summary>An enemy harvester where the bot saw it last (M8 step 10): what a raid goes for.</summary>
public sealed record HarvesterSighting(EntityId Id, CellPos Cell, int SeenTick);

/// <summary>
/// What a bot remembers of what it saw (docs/implementation/M5_first_bot.md, 4.5), brought up to date on every turn from its
/// <see cref="AiWorldView"/>:
/// <list type="bullet">
/// <item>enemy buildings — kept in the fog; forgotten once a cell of theirs is in sight and they are not there;</item>
/// <item>enemy armies by block — forgotten after <see cref="ArmyForgetTicks"/> unseen, or once the centre of the block is in sight
/// without them;</item>
/// <item>the starts of the map where an enemy may be — every start but the bot's own, until it is explored;</item>
/// <item>enemy harvesters where they were seen last (M8 step 10) — forgotten after <see cref="EnemyEstimate.ForgetTicks"/>, or once
/// their cell is in sight without them;</item>
/// <item>the buildings of the factions the map placed for nobody (2026-10-27) — known from the map on the first turn, as its preview
/// shows them; forgotten once a cell of theirs is in sight and they are not there, or no longer nobody's.</item>
/// </list>
/// The state of the bot only, not of the match: it is not hashed, and it is the same on every machine because the bot is deterministic.
/// </summary>
public sealed class AiMemory
{
    public const int BlockCells = 8;

    /// <summary>60 seconds.</summary>
    public const int ArmyForgetTicks = 900;

    private readonly SortedList<int, RememberedBuilding> _buildings = new();
    private readonly SortedList<int, ArmySighting> _armies = new();
    private readonly SortedList<int, HarvesterSighting> _harvesters = new();
    private readonly SortedList<int, (long X, long Y, int Units, int Infantry, int Strength, int Armed)> _blocks = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<CellPos> _starts = new();
    private readonly SortedDictionary<int, int> _exploredSince = new();
    private readonly List<int> _forgotten = new();
    private int[]? _costs;
    private bool _startsKnown;

    /// <summary>What the bot makes of every enemy from all it has seen (M8 step 6).</summary>
    public EnemyEstimate Estimate { get; } = new();

    /// <summary>The enemy buildings remembered, by id.</summary>
    public IList<RememberedBuilding> Buildings => _buildings.Values;

    /// <summary>The enemy armies remembered, by block (row by row).</summary>
    public IList<ArmySighting> Armies => _armies.Values;

    /// <summary>The enemy harvesters remembered, by id.</summary>
    public IList<HarvesterSighting> Harvesters => _harvesters.Values;

    /// <summary>The starts of the map not explored yet, other than the bot's own, in the order of the map.</summary>
    public IReadOnlyList<CellPos> PossibleEnemyStarts => _starts;

    /// <summary>Whether the bot has ever seen an enemy unit or building (step 7): the first scouting is over then.</summary>
    public bool EnemySeen { get; private set; }

    /// <summary>
    /// The last tick the bot saw the core of an enemy — a construction yard or a construction vehicle — where its army waits at home (M8
    /// step 10); -1 for never.
    /// </summary>
    public int CoreSeenTick { get; private set; } = -1;

    /// <summary>Where the bot saw the core of an enemy last (M8 step 10); null for never.</summary>
    public CellPos? CoreSeenCell { get; private set; }

    /// <summary>
    /// Where the bot last saw an enemy away from home — the cell of the first one it saw then, in id order, farther than
    /// <see cref="HomeCells"/> from its start — never forgotten: once the starts are explored and the sightings old, the side of the
    /// enemy is still known (step 7). A raid on its base does not move it (step 9): squads go there. Null before the first.
    /// </summary>
    public CellPos? LastEnemyCell { get; private set; }

    /// <summary>Enemies this near the bot's start, in cells, are at its home, not where the enemy is.</summary>
    public const int HomeCells = 16;

    /// <summary>
    /// The side of the enemy as far as the bot knows: an enemy building it remembers, else an army, else where it last saw an enemy,
    /// else the first start where an enemy may be; null with nothing known. Defenses face it (step 5), squads gather toward it (step 8).
    /// </summary>
    public CellPos? LikelyEnemyCell() =>
        Buildings.Count > 0 ? Buildings[0].Cell
        : Armies.Count > 0 ? Armies[0].Centre.ToCell()
        : LastEnemyCell is { } last ? last
        : PossibleEnemyStarts.Count > 0 ? PossibleEnemyStarts[0]
        : null;

    /// <summary>
    /// The strength of the enemy the bot knows of (4.6, step 8): its armies and its defenses, cost × the share of hit points left. A squad
    /// sets out only stronger than this. The armies are those remembered where they stood or, if more, the units of the
    /// <see cref="Estimate"/>, fading over 3 minutes (M8 step 10): an army that left the place it was seen at is not gone, and a bot
    /// that took it for nothing set out against an enemy it had not seen for minutes.
    /// </summary>
    public int EnemyStrength(AiWorldView view)
    {
        var strength = 0;
        foreach (var army in Armies)
        {
            strength += army.Strength;
        }

        strength = IntMath.Max(strength, Estimate.Total.ArmyStrength);

        foreach (var building in Buildings)
        {
            // A building of the map (a taken tech building, a held civilian one) has no definition to weigh.
            if (building.DefIndex < 0)
            {
                continue;
            }

            var def = view.Rules.Database.Buildings[building.DefIndex];
            if (def.Primary.Length > 0 && def.Hp > 0)
            {
                strength += def.Cost * building.Hp / def.Hp;
            }
        }

        return strength;
    }

    /// <summary>The strength of a unit as of a squad (4.6): its cost × the share of its hit points left.</summary>
    public int StrengthOf(Unit unit) => unit.MaxHp > 0 && _costs is { } costs ? costs[(int)unit.Type] * unit.Hp / unit.MaxHp : 0;

    /// <summary>The buildings of the map the bot has seen, by id (M11 step 3); one seen gone where it stood is forgotten.</summary>
    public IReadOnlyCollection<RememberedMapBuilding> MapBuildings => _mapBuildings.Values;

    private readonly SortedDictionary<int, RememberedMapBuilding> _mapBuildings = new();

    /// <summary>The buildings of the factions the map placed for nobody, by id: its defenses and what an engineer may take (2026-10-27).</summary>
    public IReadOnlyCollection<RememberedNobodysBuilding> NobodysBuildings => _nobodys.Values;

    private readonly SortedDictionary<int, RememberedNobodysBuilding> _nobodys = new();
    private bool _nobodysKnown;

    /// <summary>
    /// The defense of nobody that hits the ground and reaches within <paramref name="margin"/> cells of <paramref name="cell"/> — by its
    /// reach from the nearest cell of its footprint, more than the rules allow it, so that the bot stays clear; null for none.
    /// </summary>
    public RememberedNobodysBuilding? NobodysDefenseReaching(CellPos cell, int margin = 0) => NobodysDefenseReaching(cell, cell, margin);

    /// <summary>As <see cref="NobodysDefenseReaching(CellPos, int)"/>, for the cells from <paramref name="from"/> to <paramref name="to"/>: a footprint.</summary>
    public RememberedNobodysBuilding? NobodysDefenseReaching(CellPos from, CellPos to, int margin)
    {
        foreach (var building in _nobodys.Values)
        {
            if (!building.Fires || !building.HitsGround)
            {
                continue;
            }

            var dx = (long)IntMath.Max(IntMath.Max(building.Cell.X - to.X, from.X - building.FarCell.X), 0);
            var dy = (long)IntMath.Max(IntMath.Max(building.Cell.Y - to.Y, from.Y - building.FarCell.Y), 0);
            var reach = (long)building.Reach + margin;
            if (dx * dx + dy * dy <= reach * reach)
            {
                return building;
            }
        }

        return null;
    }

    internal void Update(AiWorldView view)
    {
        RememberBuildings(view);
        RememberMapBuildings(view);
        RememberNobodysBuildings(view);
        RememberArmies(view);
        RememberHarvesters(view);
        RememberStarts(view);
        Estimate.Update(view, this);
        EnemySeen |= view.VisibleEnemies.Count > 0;
        foreach (var enemy in view.VisibleEnemies)
        {
            if ((enemy is Unit unit && view.Rules.IsMcv(unit.Type)) || (enemy is Building { DefIndex: >= 0 } core
                && view.Rules.Database.Buildings[core.DefIndex].Produces.Contains(ProductionCategory.Building)))
            {
                (CoreSeenTick, CoreSeenCell) = (view.Tick, enemy is Building yard ? yard.Cell : ((Unit)enemy).Position.ToCell());
                break;
            }
        }
        foreach (var enemy in view.VisibleEnemies)
        {
            var cell = enemy is Building building ? building.Cell : ((Unit)enemy).Position.ToCell();
            if (view.Start is not { } start || IntMath.Max(IntMath.Abs(cell.X - start.X), IntMath.Abs(cell.Y - start.Y)) > HomeCells)
            {
                LastEnemyCell = cell;
                break;
            }
        }
    }

    private void RememberBuildings(AiWorldView view)
    {
        _seen.Clear();
        foreach (var entity in view.VisibleEnemies)
        {
            if (entity is Building building)
            {
                _seen.Add(building.Id.Value);
                _buildings[building.Id.Value] = new RememberedBuilding(
                    building.Id, building.Owner, building.DefIndex, building.Cell, building.FarCell, building.Hp, view.Tick);
            }
        }

        _forgotten.Clear();
        foreach (var remembered in _buildings.Values)
        {
            // A building in sight that the bot is still noticing (M8 step 3) is not gone.
            if (!_seen.Contains(remembered.Id.Value) && !view.Perception.IsNoticing(remembered.Id.Value, view.Tick)
                && AnyVisible(view, remembered.Cell, remembered.FarCell))
            {
                _forgotten.Add(remembered.Id.Value);
            }
        }

        foreach (var id in _forgotten)
        {
            _buildings.Remove(id);
        }
    }

    private void RememberMapBuildings(AiWorldView view)
    {
        _seen.Clear();
        foreach (var building in view.MapBuildingsInSight)
        {
            _seen.Add(building.Id.Value);
            _mapBuildings[building.Id.Value] = new RememberedMapBuilding(
                building.Id, building.Kind, building.Owner, building.Cell, building.FarCell, building.Hp, building.MaxHp, building.Occupants.Count, view.Tick);
        }

        _forgotten.Clear();
        foreach (var remembered in _mapBuildings.Values)
        {
            if (!_seen.Contains(remembered.Id.Value) && AnyVisible(view, remembered.Cell, remembered.FarCell))
            {
                _forgotten.Add(remembered.Id.Value);
            }
        }

        foreach (var id in _forgotten)
        {
            _mapBuildings.Remove(id);
        }
    }

    private void RememberNobodysBuildings(AiWorldView view)
    {
        if (!_nobodysKnown)
        {
            foreach (var building in view.NobodysBuildingsOfTheMap())
            {
                _nobodys[building.Id.Value] = Nobodys(view, building);
            }

            _nobodysKnown = true;
        }

        _seen.Clear();
        foreach (var building in view.NobodysBuildingsInSight)
        {
            _seen.Add(building.Id.Value);
            _nobodys[building.Id.Value] = Nobodys(view, building);
        }

        _forgotten.Clear();
        foreach (var remembered in _nobodys.Values)
        {
            // Destroyed, or taken by an engineer: not nobody's any more.
            if (!_seen.Contains(remembered.Id.Value) && AnyVisible(view, remembered.Cell, remembered.FarCell))
            {
                _forgotten.Add(remembered.Id.Value);
            }
        }

        foreach (var id in _forgotten)
        {
            _nobodys.Remove(id);
        }
    }

    private static RememberedNobodysBuilding Nobodys(AiWorldView view, Building building)
    {
        var (reach, ground) = (0, false);
        if (Rts.Simulation.Combat.Hostility.IsHostileNeutral(building))
        {
            foreach (var index in view.Rules.WeaponsOfBuilding(building.DefIndex))
            {
                if (index >= 0 && view.Rules.Database.Weapons[index] is var weapon)
                {
                    reach = IntMath.Max(reach, (weapon.Range + Fixed.One - Fixed.Epsilon).ToIntFloor());
                    ground |= (weapon.Targets & WeaponTargets.Ground) != 0;
                }
            }
        }

        return new RememberedNobodysBuilding(building.Id, building.DefIndex, building.Cell, building.FarCell, building.Hp, reach, ground, view.Tick);
    }

    private void RememberHarvesters(AiWorldView view)
    {
        foreach (var entity in view.VisibleEnemies)
        {
            if (entity is Unit { Harvester: not null } unit)
            {
                _harvesters[unit.Id.Value] = new HarvesterSighting(unit.Id, unit.Position.ToCell(), view.Tick);
            }
        }

        _forgotten.Clear();
        foreach (var (key, sighting) in _harvesters)
        {
            if (sighting.SeenTick != view.Tick && (view.Tick - sighting.SeenTick >= EnemyEstimate.ForgetTicks || view.IsVisible(sighting.Cell)))
            {
                _forgotten.Add(key);
            }
        }

        foreach (var key in _forgotten)
        {
            _harvesters.Remove(key);
        }
    }

    private void RememberArmies(AiWorldView view)
    {
        _costs ??= Costs(view);
        _blocks.Clear();
        foreach (var entity in view.VisibleEnemies)
        {
            if (entity is not Unit unit)
            {
                continue;
            }

            var cell = unit.Position.ToCell();
            var key = Key(cell.X / BlockCells, cell.Y / BlockCells);
            _blocks.TryGetValue(key, out var block);
            var strength = StrengthOf(unit);
            var infantry = unit.Archetype.Category == UnitCategory.Infantry ? 1 : 0;
            _blocks[key] = (block.X + unit.Position.X, block.Y + unit.Position.Y, block.Units + 1, block.Infantry + infantry, block.Strength + strength,
                block.Armed + (unit.Archetype.Armed && unit.Harvester is null ? 1 : 0));
        }

        foreach (var (key, block) in _blocks)
        {
            var centre = new WorldPos((int)(block.X / block.Units), (int)(block.Y / block.Units));
            _armies[key] = new ArmySighting(key % 1024, key / 1024, centre, block.Strength, block.Units, block.Infantry, view.Tick) { Armed = block.Armed };
        }

        _forgotten.Clear();
        foreach (var (key, sighting) in _armies)
        {
            if (sighting.SeenTick == view.Tick)
            {
                continue;
            }

            var centre = new CellPos(sighting.BlockX * BlockCells + BlockCells / 2, sighting.BlockY * BlockCells + BlockCells / 2);
            if (view.Tick - sighting.SeenTick >= ArmyForgetTicks || view.IsVisible(centre))
            {
                _forgotten.Add(key);
            }
        }

        foreach (var key in _forgotten)
        {
            _armies.Remove(key);
        }
    }

    private void RememberStarts(AiWorldView view)
    {
        if (!_startsKnown)
        {
            foreach (var start in view.MapStarts)
            {
                if (start != view.Start)
                {
                    _starts.Add(start);
                }
            }

            _startsKnown = true;
        }

        // A start is crossed off once it has been in view as long as the bot needs to notice what is there (M8 step 3); a scout that
        // turned away the moment it came into view would leave the enemy there unnoticed.
        foreach (var start in _starts)
        {
            if (view.IsExplored(start))
            {
                _exploredSince.TryAdd(Key(start.X, start.Y), view.Tick);
            }
        }

        _starts.RemoveAll(start => _exploredSince.TryGetValue(Key(start.X, start.Y), out var since) && view.Tick - since >= view.Perception.Delay);
    }

    private static bool AnyVisible(AiWorldView view, CellPos from, CellPos to)
    {
        for (var y = from.Y; y <= to.Y; y++)
        {
            for (var x = from.X; x <= to.X; x++)
            {
                if (view.IsVisible(new CellPos(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The cost of every unit type, by its value.</summary>
    private static int[] Costs(AiWorldView view)
    {
        var costs = new int[UnitTypes.Highest + 1];
        foreach (var type in UnitTypes.All)
        {
            costs[(int)type] = view.Rules.Database.TryGetUnit(UnitTypes.IdOf(type), out var unit) ? unit.Cost : 0;
        }

        return costs;
    }

    private static int Key(int blockX, int blockY) => blockY * 1024 + blockX;
}
