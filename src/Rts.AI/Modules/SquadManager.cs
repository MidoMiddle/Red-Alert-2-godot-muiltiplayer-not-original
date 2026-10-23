using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The squads of a bot (docs/implementation/M5_first_bot.md, 4.6, step 8). Every fighting unit but the scout and the harvesters is in a
/// squad; a squad goes through <see cref="SquadState"/>:
/// <list type="bullet">
/// <item><b>gathering</b> — new units come to the gathering point, <see cref="AiSettings.GatherCells"/> from the start toward the
/// enemy; <b>ready</b> once it has <see cref="AiSettings.MinAttackArmy"/> units (more by <see cref="AiSettings.AttackGrowthPercent"/>
/// for every wave sent) and <see cref="AiSettings.AttackThresholdPercent"/> of the enemy strength the bot knows of — or, with
/// <see cref="AiSettings.AllInUnits"/> at least, once it has not grown for <see cref="AiSettings.AllInTicks"/> since the ore ran out
/// (<see cref="AiPlan.OreGone"/>): an army that stopped growing for want of money goes with what it has; it sets out when the plan lets it (<see cref="AiPlan.AttackAllowed"/>), and a new
/// squad gathers;</item>
/// <item><b>moving</b> — attack-move to the target, the units ahead waiting for those behind; the target is an enemy building of the
/// memory — refineries, defenses, factories, the construction yard, the rest, the nearest of the first kind there is — else where
/// the enemy was last seen or may be; unarmed enemy units near it — harvesters, an MCV — and the target building in sight are
/// attacked;</item>
/// <item><b>fighting</b> — while an armed enemy is near: every unit that can hurt the nearest one fires at it, the wounded fall back
/// behind the squad;</item>
/// <item><b>defending</b> — called back by <see cref="DefenseManager"/> (<see cref="Squad.Recalled"/>): it goes for the threat at the
/// base and fights it; the squad at home does the same while there is a threat (<see cref="AiPlan.Threat"/>), and does not set out;
/// once the base is safe the squads called back go home;</item>
/// <item><b>retreating</b> — below <see cref="AiSettings.RetreatAtStrengthPercent"/> of its strength when it set out, or with nothing
/// left to attack: a plain move back to the gathering point, where it joins the squad gathering there.</item>
/// </list>
/// No unit is given the order it has and still carries out: a command of the bot always changes something.
/// </summary>
public sealed class SquadManager(AiSettings settings) : IAiModule
{
    /// <summary>A unit this near its point, in cells, is there.</summary>
    public const int ArrivedCells = 4;

    /// <summary>A waiting squad is together when every unit is this near the point it waits at, in cells.</summary>
    public const int RegroupCells = 3;

    /// <summary>The longest a squad waits for its laggards, in ticks (20 s); half of it passes before it waits again.</summary>
    public const int RegroupTicks = 300;

    /// <summary>A unit of a squad called back comes by a plain move while it is farther than this from the threat, in cells.</summary>
    public const int RecallCells = 16;

    /// <summary>How far behind the middle of the squad a wounded unit falls back, in cells.</summary>
    public const int BehindCells = 3;

    private enum OrderKind
    {
        Move,
        AttackMove,
        Attack,
    }

    private readonly List<Squad> _squads = new();
    private readonly Dictionary<int, Unit> _units = new();
    private readonly Dictionary<int, (OrderKind Kind, CellPos Cell, EntityId Target)> _orders = new();
    private readonly HashSet<int> _inSquads = new();
    private readonly HashSet<int> _fellBack = new();
    private readonly List<Unit> _members = new();
    private readonly List<EntityId> _batch = new();
    private readonly List<(RememberedBuilding Building, int Rank, int Distance)> _targets = new();
    private int _formed;
    private int _waves;

    // The gathering squad at its largest so far and when it last grew: one that stopped growing sets out with what it has (step 14).
    private int _gathered;
    private int _grewAt;
    private int? _oreGoneAt;

    public AiModuleKind Kind => AiModuleKind.Squads;

    /// <summary>The waves sent so far.</summary>
    internal int Waves => _waves;

    public void Update(AiContext context)
    {
        var view = context.View;
        context.Plan.Squads = _squads;
        _units.Clear();
        foreach (var unit in view.OwnUnits)
        {
            _units[unit.Id.Value] = unit;
        }

        var gathering = Account(context);
        var gather = GatherPoint(view, context.Memory);
        context.Plan.GatherPoint = gather;
        Squad? merged = null;
        foreach (var squad in _squads)
        {
            Members(squad);
            squad.Strength = Strength(context.Memory);
            squad.Centre = Centre().ToCell();
            if (squad.Recalled)
            {
                Defend(context, squad, gather);
            }
            else if (squad.State is SquadState.Gathering or SquadState.Ready)
            {
                Gather(context, squad, gather);
            }
            else if (squad.State is SquadState.Moving or SquadState.Fighting)
            {
                Attack(context, squad, gather);
            }

            if (squad.State == SquadState.Retreating && Retreat(context, squad, gather))
            {
                merged = squad;
            }
        }

        // One a turn is enough: a squad that came back waits for the next turn of the squads at the gathering point.
        if (merged is not null)
        {
            foreach (var id in merged.Units)
            {
                _fellBack.Remove(id.Value);
            }

            Gathering(gathering).AddAll(merged);
            _squads.Remove(merged);
        }
    }

    /// <summary>
    /// Takes the fallen and the scout out of the squads and drops the empty ones; puts every fighting unit in no squad into the one
    /// gathering. Returns that one, null when there is none and nobody to gather.
    /// </summary>
    private Squad? Account(AiContext context)
    {
        var scout = context.Plan.Scout;
        _inSquads.Clear();
        foreach (var squad in _squads)
        {
            squad.RemoveAll(id =>
            {
                if (_units.ContainsKey(id.Value) && id != scout)
                {
                    _inSquads.Add(id.Value);
                    return false;
                }

                _orders.Remove(id.Value);
                _fellBack.Remove(id.Value);
                return true;
            });
        }

        _squads.RemoveAll(squad => squad.Units.Count == 0);
        Squad? gathering = null;
        foreach (var unit in context.View.OwnUnits)
        {
            if (unit.Archetype.Armed && unit.Harvester is null && unit.Id != scout && !_inSquads.Contains(unit.Id.Value))
            {
                gathering = Gathering(gathering);
                gathering.Add(unit.Id);
            }
        }

        return gathering;
    }

    /// <summary>The squad gathering or ready, a new one if there is none.</summary>
    private Squad Gathering(Squad? known)
    {
        if (known is not null && _squads.Contains(known))
        {
            return known;
        }

        foreach (var squad in _squads)
        {
            if (squad.State is SquadState.Gathering or SquadState.Ready)
            {
                return squad;
            }
        }

        var formed = new Squad(++_formed);
        _squads.Add(formed);
        return formed;
    }

    /// <summary>Sends the units far from the gathering point to it; ready when strong enough, and out when the plan lets it.</summary>
    private void Gather(AiContext context, Squad squad, CellPos? gather)
    {
        if (context.Plan.Threat is { } threat)
        {
            // Enemies at the base: the squad at home goes for them, and does not set out meanwhile.
            Engage(context, squad.Centre, threat.Cell, gather ?? squad.Centre);
            return;
        }

        foreach (var unit in _members)
        {
            _fellBack.Remove(unit.Id.Value);
        }

        if (gather is { } point)
        {
            _batch.Clear();
            foreach (var unit in _members)
            {
                if (Distance(unit.Position.ToCell(), point) > ArrivedCells && !unit.Engaged)
                {
                    Want(unit, OrderKind.AttackMove, point, default);
                }
            }

            Issue(context, OrderKind.AttackMove, point, default);
        }

        if (squad.Units.Count > _gathered)
        {
            (_gathered, _grewAt) = (squad.Units.Count, context.Tick);
        }

        _oreGoneAt = context.Plan.OreGone ? _oreGoneAt ?? context.Tick : null;

        var enemy = context.Memory.EnemyStrength(context.View);
        // Only with the ore gone, and counted from then: before, an army that does not grow is one still waiting for its factories.
        var stalled = _oreGoneAt is { } gone && squad.Units.Count >= settings.AllInUnits
            && context.Tick - IntMath.Max(_grewAt, gone) >= settings.AllInTicks;
        var strong = stalled || (squad.Units.Count >= Needed() && (long)squad.Strength * 100 >= (long)enemy * settings.AttackThresholdPercent);
        squad.State = strong ? SquadState.Ready : SquadState.Gathering;
        if (!strong || !context.Plan.AttackAllowed || !Retarget(context, squad, squad.Centre))
        {
            return;
        }

        squad.State = SquadState.Moving;
        (_gathered, _grewAt) = (0, context.Tick);
        squad.StartStrength = squad.Strength;
        squad.Beaten = false;
        squad.Regroup = null;
        squad.RegroupUntil = 0;
        _waves++;
        Attack(context, squad, gather);
    }

    /// <summary>The units a squad needs before it may set out: <see cref="AiSettings.MinAttackArmy"/>, grown for every wave sent.</summary>
    private int Needed()
    {
        var needed = settings.MinAttackArmy;
        for (var wave = 0; wave < _waves; wave++)
        {
            needed = needed * settings.AttackGrowthPercent / 100;
        }

        return needed;
    }

    /// <summary>A moving or fighting squad: turns back when beaten or with nothing to attack, fights the armed enemies near, else goes on.</summary>
    private void Attack(AiContext context, Squad squad, CellPos? gather)
    {
        var centre = squad.Centre;
        if ((long)squad.Strength * 100 < (long)squad.StartStrength * settings.RetreatAtStrengthPercent)
        {
            squad.State = SquadState.Retreating;
            squad.Beaten = true;
            return;
        }

        if (!Retarget(context, squad, centre))
        {
            // Nothing left to attack: the wave is over.
            squad.State = SquadState.Retreating;
            return;
        }

        if (squad.TargetBuilding is null && squad.TargetCell is { } place && Distance(centre, place) <= ArrivedCells
            && Nearest(context, centre, armed: true) is null && Nearest(context, centre, armed: false) is null)
        {
            // No enemy where it might be: on to the nearest block of the map unseen now, a few of them, then home (step 14).
            squad.Swept.Add(place);
            if (squad.Swept.Count > settings.SweepPlaces || Sweep(context.View, squad, centre) is not { } next)
            {
                squad.State = SquadState.Retreating;
                return;
            }

            squad.TargetCell = next;
        }

        if (Nearest(context, centre, armed: true) is { } enemy)
        {
            squad.State = SquadState.Fighting;
            Fight(context, enemy, centre, gather ?? centre);
            return;
        }

        squad.State = SquadState.Moving;
        Move(context, squad, centre);
    }

    /// <summary>
    /// A squad called back to the base (step 9): its units farther than <see cref="RecallCells"/> from the threat come by a plain move —
    /// a unit on an attack-move would stay to fight whatever it met on the way, at the enemy's base too; the nearer ones fight at it.
    /// Once the base is safe again it goes home, not beaten, and joins the squad gathering there.
    /// </summary>
    private void Defend(AiContext context, Squad squad, CellPos? gather)
    {
        if (context.Plan.Threat is not { } threat)
        {
            (squad.Recalled, squad.Beaten, squad.State) = (false, false, SquadState.Retreating);
            return;
        }

        squad.State = SquadState.Defending;
        _batch.Clear();
        foreach (var unit in _members)
        {
            if (Distance(unit.Position.ToCell(), threat.Cell) > RecallCells)
            {
                Want(unit, OrderKind.Move, threat.Cell, default);
            }
        }

        Issue(context, OrderKind.Move, threat.Cell, default);
        _members.RemoveAll(unit => Distance(unit.Position.ToCell(), threat.Cell) > RecallCells);
        if (_members.Count > 0)
        {
            Engage(context, threat.Cell, threat.Cell, gather ?? threat.Cell);
        }
    }

    /// <summary>Fights the armed enemy nearest the squad, if one is near, else goes for <paramref name="threat"/> by attack-move.</summary>
    private void Engage(AiContext context, CellPos centre, CellPos threat, CellPos home)
    {
        if (Nearest(context, centre, armed: true) is { } enemy)
        {
            Fight(context, enemy, centre, home);
        }
        else
        {
            AttackMoveAll(context, threat);
        }
    }

    /// <summary>Every unit that can hurt <paramref name="focus"/> fires at it; the wounded fall back behind the squad, once.</summary>
    private void Fight(AiContext context, Entity focus, CellPos centre, CellPos home)
    {
        var behind = Toward(centre, home, BehindCells);
        _batch.Clear();
        foreach (var unit in _members)
        {
            if (unit.Hp * 100 < unit.MaxHp * settings.WoundedPercent && _fellBack.Add(unit.Id.Value))
            {
                Want(unit, OrderKind.Move, behind, default);
            }
        }

        Issue(context, OrderKind.Move, behind, default);
        Strike(context, focus, wounded: false);
    }

    /// <summary>
    /// A moving squad: waits for its laggards; else attacks an unarmed enemy unit near it — a harvester, an MCV — or its target
    /// building in sight, else goes on to its target by attack-move.
    /// </summary>
    private void Move(AiContext context, Squad squad, CellPos centre)
    {
        if (squad.Regroup is { } point)
        {
            if (context.Tick < squad.RegroupUntil && Spread(point) > RegroupCells)
            {
                AttackMoveAll(context, point);
                return;
            }

            squad.Regroup = null;
            squad.RegroupUntil = context.Tick + RegroupTicks / 2;
        }
        else if (context.Tick >= squad.RegroupUntil && Spread(centre) > settings.SquadSpreadCells)
        {
            // The unit nearest the middle stands where the squad can stand.
            squad.Regroup = NearestMember(centre).Position.ToCell();
            squad.RegroupUntil = context.Tick + RegroupTicks;
            AttackMoveAll(context, squad.Regroup.Value);
            return;
        }

        if (Nearest(context, centre, armed: false) is { } unarmed)
        {
            Strike(context, unarmed, wounded: true);
            return;
        }

        if (squad.TargetBuilding is { } id && Visible(context.View, id) is { } building && Distance(centre, building.Cell) <= settings.EngageCells)
        {
            Strike(context, building, wounded: true);
            return;
        }

        AttackMoveAll(context, squad.TargetCell!.Value);
    }

    /// <summary>A beaten squad, or one done: a plain move to the gathering point. True once it is there.</summary>
    private bool Retreat(AiContext context, Squad squad, CellPos? gather)
    {
        if (gather is not { } point)
        {
            return true;
        }

        _batch.Clear();
        foreach (var unit in _members)
        {
            Want(unit, OrderKind.Move, point, default);
        }

        Issue(context, OrderKind.Move, point, default);
        return Distance(squad.Centre, point) <= ArrivedCells;
    }

    /// <summary>
    /// Keeps the target of the squad while the memory has it; else the building of the most important kind nearest the squad — or, as
    /// often as the level errs, one of the three first (M8 step 3) — else the place it went to, else where the enemy was last seen or the
    /// nearest start where it may be. False with none.
    /// </summary>
    private bool Retarget(AiContext context, Squad squad, CellPos centre)
    {
        var memory = context.Memory;
        var rules = context.View.Rules.Database;
        _targets.Clear();
        foreach (var building in memory.Buildings)
        {
            if (building.Id == squad.TargetBuilding)
            {
                return true;
            }

            _targets.Add((building, Rank(rules.Buildings[building.DefIndex]), Distance(centre, building.Cell)));
        }

        if (_targets.Count > 0)
        {
            _targets.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank)
                : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Building.Id.Value.CompareTo(b.Building.Id.Value));
            var chosen = context.Choose(_targets).Building;
            (squad.TargetBuilding, squad.TargetCell) = (chosen.Id, chosen.Cell);
            return true;
        }

        if (squad.TargetBuilding is not null)
        {
            (squad.TargetBuilding, squad.TargetCell) = (null, null);
        }

        if (squad.TargetCell is not null)
        {
            return true;
        }

        squad.TargetCell = memory.LastEnemyCell ?? NearestStart(memory, centre);
        return squad.TargetCell is not null;
    }

    /// <summary>
    /// The middle of the block of <see cref="AiSettings.ScoutBlockCells"/> cells nearest the squad that the bot does not see now and the
    /// squad has not swept; null for none.
    /// </summary>
    private CellPos? Sweep(AiWorldView view, Squad squad, CellPos centre)
    {
        var size = settings.ScoutBlockCells;
        CellPos? best = null;
        for (var y = size / 2; y < view.Height; y += size)
        {
            for (var x = size / 2; x < view.Width; x += size)
            {
                var middle = new CellPos(x, y);
                if (!view.IsVisible(middle) && !squad.Swept.Contains(middle)
                    && (best is null || Distance(middle, centre) < Distance(best.Value, centre)))
                {
                    best = middle;
                }
            }
        }

        return best;
    }

    /// <summary>Which enemy buildings a squad attacks first (4.6): refineries, defenses, factories, the construction yard, the rest.</summary>
    internal static int Rank(BuildingDef def) =>
        def.DockCell is not null ? 0
        : def.Primary.Length > 0 ? 1
        : def.Produces.Contains(ProductionCategory.Building) ? 3
        : def.Produces.Length > 0 ? 2
        : 4;

    private static CellPos? NearestStart(AiMemory memory, CellPos from)
    {
        CellPos? best = null;
        foreach (var start in memory.PossibleEnemyStarts)
        {
            if (best is null || Distance(start, from) < Distance(best.Value, from))
            {
                best = start;
            }
        }

        return best;
    }

    /// <summary>
    /// The visible enemy nearest <paramref name="centre"/> within <see cref="AiSettings.EngageCells"/>: an armed unit or a building with
    /// weapons, or — with <paramref name="armed"/> false — an unarmed unit, a harvester or an MCV; equal ones to the smaller id. Null for
    /// none.
    /// </summary>
    private Entity? Nearest(AiContext context, CellPos centre, bool armed)
    {
        Entity? best = null;
        var bestDistance = settings.EngageCells + 1;
        foreach (var enemy in context.View.VisibleEnemies)
        {
            var (fits, at) = enemy switch
            {
                Unit unit => (unit.Archetype.Armed == armed, unit.Position.ToCell()),
                Building building => (armed && building.Weapons.Length > 0, building.Cell),
                _ => (false, centre),
            };
            var distance = Distance(centre, at);
            if (fits && distance < bestDistance)
            {
                (best, bestDistance) = (enemy, distance);
            }
        }

        return best;
    }

    /// <summary>Orders every unit that can hurt <paramref name="target"/> to fire at it — the wounded that fell back too, when asked.</summary>
    private void Strike(AiContext context, Entity target, bool wounded)
    {
        var rules = context.View.Rules;
        var cell = target is Building building ? building.Cell : ((Unit)target).Position.ToCell();
        _batch.Clear();
        foreach (var unit in _members)
        {
            if ((wounded || !_fellBack.Contains(unit.Id.Value)) && TargetingSystem.CanHurt(rules, unit.Type, target))
            {
                Want(unit, OrderKind.Attack, cell, target.Id);
            }
        }

        if (_batch.Count > 0)
        {
            context.Issue(new AttackCommand(context.View.Me, context.ExecuteTick, context.NextSequence(), [.. _batch], target.Id));
        }
    }

    private void AttackMoveAll(AiContext context, CellPos point)
    {
        _batch.Clear();
        foreach (var unit in _members)
        {
            Want(unit, OrderKind.AttackMove, point, default);
        }

        Issue(context, OrderKind.AttackMove, point, default);
    }

    /// <summary>Puts the unit in the batch of this order unless it has it already and still carries it out.</summary>
    private void Want(Unit unit, OrderKind kind, CellPos cell, EntityId target)
    {
        if (_orders.TryGetValue(unit.Id.Value, out var last) && last == (kind, cell, target) && Carries(unit, kind, cell, target))
        {
            return;
        }

        _orders[unit.Id.Value] = (kind, cell, target);
        _batch.Add(unit.Id);
    }

    private static bool Carries(Unit unit, OrderKind kind, CellPos cell, EntityId target) => kind == OrderKind.Attack
        ? unit.Target == target && unit.TargetOrder == TargetOrder.Attack
        : unit.MoveTarget is not null || unit.Engaged || Distance(unit.Position.ToCell(), cell) <= ArrivedCells;

    private void Issue(AiContext context, OrderKind kind, CellPos cell, EntityId target)
    {
        if (_batch.Count == 0)
        {
            return;
        }

        var (me, at, units) = (context.View.Me, context.ExecuteTick, System.Collections.Immutable.ImmutableArray.CreateRange(_batch));
        context.Issue(kind switch
        {
            OrderKind.Move => new MoveCommand(me, at, context.NextSequence(), units, cell.Center),
            OrderKind.AttackMove => new AttackMoveCommand(me, at, context.NextSequence(), units, cell.Center),
            _ => new AttackCommand(me, at, context.NextSequence(), units, target),
        });
    }

    /// <summary>
    /// <see cref="AiSettings.GatherCells"/> from the start toward the enemy the bot knows of, or the cell nearest it that a tank and a
    /// soldier can enter; null for a bot with no start.
    /// </summary>
    private CellPos? GatherPoint(AiWorldView view, AiMemory memory)
    {
        if (view.Start is not { } start)
        {
            return null;
        }

        var aim = Toward(start, memory.LikelyEnemyCell() ?? start, settings.GatherCells);
        for (var ring = 0; ring <= settings.GatherCells; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(aim.X + dx, aim.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.CanEnter(cell, Locomotor.Track) && view.CanEnter(cell, Locomotor.Foot))
                    {
                        return cell;
                    }
                }
            }
        }

        return start;
    }

    private void Members(Squad squad)
    {
        _members.Clear();
        foreach (var id in squad.Units)
        {
            _members.Add(_units[id.Value]);
        }
    }

    private int Strength(AiMemory memory)
    {
        var strength = 0;
        foreach (var unit in _members)
        {
            strength += memory.StrengthOf(unit);
        }

        return strength;
    }

    /// <summary>The middle of the members: the average of their positions.</summary>
    private WorldPos Centre()
    {
        var (x, y) = (0L, 0L);
        foreach (var unit in _members)
        {
            (x, y) = (x + unit.Position.X, y + unit.Position.Y);
        }

        return new WorldPos((int)(x / _members.Count), (int)(y / _members.Count));
    }

    /// <summary>How far the farthest member stands from <paramref name="point"/>, in cells.</summary>
    private int Spread(CellPos point)
    {
        var spread = 0;
        foreach (var unit in _members)
        {
            spread = IntMath.Max(spread, Distance(unit.Position.ToCell(), point));
        }

        return spread;
    }

    private Unit NearestMember(CellPos point)
    {
        var best = _members[0];
        foreach (var unit in _members)
        {
            if (Distance(unit.Position.ToCell(), point) < Distance(best.Position.ToCell(), point))
            {
                best = unit;
            }
        }

        return best;
    }

    private static Building? Visible(AiWorldView view, EntityId id)
    {
        foreach (var enemy in view.VisibleEnemies)
        {
            if (enemy.Id == id)
            {
                return enemy as Building;
            }
        }

        return null;
    }

    /// <summary>The cell <paramref name="cells"/> from <paramref name="from"/> toward <paramref name="to"/> (or <paramref name="from"/> itself when they are one).</summary>
    private static CellPos Toward(CellPos from, CellPos to, int cells)
    {
        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        var length = IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy));
        return length == 0 ? from : new CellPos(from.X + dx * cells / length, from.Y + dy * cells / length);
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
