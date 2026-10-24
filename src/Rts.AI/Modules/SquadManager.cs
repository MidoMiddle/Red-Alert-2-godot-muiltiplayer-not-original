using System.Collections.Generic;
using Rts.AI.Profiles;
using Rts.AI.Squads;
using Rts.AI.Team;
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
/// squad gathers. By the personality (M8, 4.7, step 8): the first wave not before a tick of the window of the first attack, drawn once
/// (<see cref="AiSettings.FirstAttackFrom"/>), and by its end (<see cref="AiSettings.FirstAttackBy"/>) with what it has, if that is
/// <see cref="AiSettings.AllInUnits"/> or more; every next one <see cref="AiSettings.WaveInterval"/> after the last at the soonest;</item>
/// <item><b>moving</b> — attack-move to the target, the units ahead waiting for those behind; the target is an enemy building of the
/// memory — in the order of <see cref="AiSettings.TargetPriority"/> (<see cref="Rank"/>), the nearest of the first kind there is — else where
/// the enemy was last seen or may be; unarmed enemy units near it — harvesters, an MCV — and the target building in sight are
/// attacked;</item>
/// <item><b>fighting</b> — while an armed enemy is near, by the micro of the level (M8, 4.4, step 5, <see cref="AiSettings.Micro"/>):
/// «Easy» attack-moves at the nearest and lets its units choose; from «Medium» on every unit that can hurt it fires at the enemy the squad
/// kills soonest (<see cref="Focus"/>); «Hard» also pulls the wounded back behind the squad; «Brutal» also keeps a longer-ranged unit out of
/// the reach of what it fights (<see cref="KeepRange"/>). From «Medium» on a squad that loses the trade — weaker than
/// <see cref="AiSettings.RetreatThresholdPercent"/> of the enemy near it — turns back, «Brutal» to a defense of its own;</item>
/// <item><b>defending</b> — called back by <see cref="DefenseManager"/> (<see cref="Squad.Recalled"/>): it goes for the threat at the
/// base and fights it; the squad at home does the same while there is a threat (<see cref="AiPlan.Threat"/>), and does not set out;
/// once the base is safe the squads called back go home;</item>
/// <item><b>retreating</b> — below <see cref="AiSettings.RetreatAtStrengthPercent"/> of its strength when it set out, or with nothing
/// left to attack: a plain move back to the gathering point, where it joins the squad gathering there.</item>
/// </list>
/// <item><b>the team</b> (M8, 4.8, step 11) — with allied bots a ready squad announces its attack on the <see cref="TeamBlackboard"/> and
/// waits for their answers, <see cref="WaitForAllies"/> at the most, then sets out for the attack of the team, the strongest announced;
/// the squad at home joins an attack of the team (<see cref="AiPlan.JoinAttack"/>) with half the units it needs, whatever its own window;
/// to help an ally (<see cref="AiPlan.HelpAt"/>) up to <see cref="HelpPercent"/> of the army goes there as a squad of its own, as a
/// squad called back does, and comes home once the ally has looked safe for <see cref="HelpHoldTicks"/>.</item>
/// No unit is given the order it has and still carries out: a command of the bot always changes something.
/// </summary>
public sealed class SquadManager(AiSettings settings, TeamBlackboard? team = null) : IAiModule
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

    /// <summary>How long a squad that announced its attack waits for the answers of its allies, in ticks: 30 s (M8, 4.8).</summary>
    public const int WaitForAllies = 450;

    /// <summary>A harvester seen within this many ticks is a target of a raid: 30 s (M8 step 10a).</summary>
    public const int RaidFreshTicks = 450;

    /// <summary>How long a raid waits for a harvester at a field, in ticks: 30 s (M8 step 10a).</summary>
    public const int AmbushTicks = 450;

    /// <summary>The field of an enemy refinery is the explored ore nearest it within this many cells (M8 step 10a).</summary>
    public const int RaidFieldCells = 16;

    /// <summary>The share of its army, in percent, a bot sends to help an ally (M8, 4.8).</summary>
    public const int HelpPercent = 40;

    /// <summary>
    /// How long a squad that helps an ally stays once the ally looks safe, in ticks: 30 s. The threat at an ally's base is read from what
    /// the bot sees there, and that comes and goes as buildings fall and the enemy moves.
    /// </summary>
    public const int HelpHoldTicks = 450;


    /// <summary>How far a unit of «Brutal» steps back from an enemy it outranges, in cells (M8 step 5).</summary>
    public const int KiteCells = 2;

    /// <summary>The enemies this much beyond <see cref="AiSettings.EngageCells"/> count in the trade of a fight too, in cells (step 5).</summary>
    public const int TradeCells = 4;

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
    private readonly List<(Entity Enemy, long Kill, int Distance)> _foes = new();
    private readonly HashSet<int> _kiting = new();
    private readonly List<Unit> _far = new();
    private int _formed;
    private int _waves;

    // The tick the first wave may set out on, drawn in the window of the personality on the first turn (M8 step 8), and when the last went.
    private int? _attackAt;

    // The tick the bot announced its attack to its team on, while it waits for the answers or is out (M8 step 11).
    private int? _announcedAt;

    // Since when the ally its squad helps has looked safe: the squad goes home after HelpHoldTicks (step 11).
    private int? _allySafeSince;
    private int _lastWave;
    private readonly string[] _priority = settings.TargetPriority.Split(',');

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

        context.Plan.LookAtTheEnemy = false;
        var gathering = Account(context);
        var gather = GatherPoint(view, context.Memory);
        context.Plan.GatherPoint = gather;
        Squad? merged = null;
        foreach (var squad in _squads)
        {
            Members(squad);
            squad.Strength = Strength(context.Memory);
            squad.Centre = Centre().ToCell();
            if (squad.HelpCell is { } help)
            {
                Help(context, squad, help, gather);
            }
            else if (squad.Recalled)
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

        Team(context);
    }

    /// <summary>
    /// The team (step 11): tells it about the squad at home, sends help to an ally or calls it home, and withdraws the attack it
    /// announced once no squad of the bot is out any more.
    /// </summary>
    private void Team(AiContext context)
    {
        var plan = context.Plan;
        var home = _squads.Find(squad => squad.State is SquadState.Gathering or SquadState.Ready);
        (plan.GatheringUnits, plan.GatheringStrength, plan.NeededUnits) = (home?.Units.Count ?? 0, home?.Strength ?? 0, Needed());

        var helping = _squads.Find(squad => squad.HelpCell is not null);
        if (plan.HelpAt is { } cell)
        {
            _allySafeSince = null;
            if (helping is not null)
            {
                helping.HelpCell = cell;
            }
        }

        if (plan.HelpAt is { } at && helping is null && home is not null)
        {
            var army = 0;
            foreach (var squad in _squads)
            {
                army += squad.Units.Count;
            }

            var count = IntMath.Min(home.Units.Count, army * HelpPercent / 100);
            if (count > 0)
            {
                var help = new Squad(++_formed) { State = SquadState.Defending, HelpCell = at };
                context.Log?.Add(context.Tick, context.Module, $"squad {help.Number} of {count} units goes to help an ally at {at.X},{at.Y}");
                var sent = 0;
                home.RemoveAll(id =>
                {
                    if (sent >= count)
                    {
                        return false;
                    }

                    help.Add(id);
                    sent++;
                    return true;
                });
                _squads.Add(help);
            }
        }
        else if (plan.HelpAt is null && helping is not null)
        {
            _allySafeSince ??= context.Tick;
            if (context.Tick - _allySafeSince.Value >= HelpHoldTicks)
            {
                (helping.HelpCell, helping.Beaten, helping.State, _allySafeSince) = (null, false, SquadState.Retreating, null);
                context.Log?.Add(context.Tick, context.Module, $"squad {helping.Number} comes home: the ally has looked safe for 30 s");
            }
        }

        if (team is not null && _announcedAt is not null && !_squads.Exists(squad => squad.State is SquadState.Moving or SquadState.Fighting)
            && context.Tick - _announcedAt.Value > WaitForAllies)
        {
            team.Withdraw(context.View.Me);
            _announcedAt = null;
        }
    }

    /// <summary>A squad that helps an ally (step 11): as a squad called back to the bot's own base, but to the ally's.</summary>
    private void Help(AiContext context, Squad squad, CellPos cell, CellPos? gather)
    {
        squad.State = SquadState.Defending;
        _batch.Clear();
        foreach (var unit in _members)
        {
            if (Distance(unit.Position.ToCell(), cell) > RecallCells)
            {
                Want(unit, OrderKind.Move, cell, default);
            }
        }

        Issue(context, OrderKind.Move, cell, default);
        _members.RemoveAll(unit => Distance(unit.Position.ToCell(), cell) > RecallCells);
        if (_members.Count > 0)
        {
            Engage(context, cell, cell, gather ?? cell);
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

        var known = context.Memory.EnemyStrength(context.View);
        var enemy = IntMath.Max(known, Assumed(context));
        // Only with the ore gone, and counted from then: before, an army that does not grow is one still waiting for its factories.
        var stalled = _oreGoneAt is { } gone && squad.Units.Count >= settings.AllInUnits
            && context.Tick - IntMath.Max(_grewAt, gone) >= settings.AllInTicks;
        // The end of the window of the first attack (step 8): out with what it has.
        // Not against an enemy it knows to be stronger (M8 step 10): set out by the clock into the army waiting at home, the squads of
        // «Hard» were lost at the end of the window, match after match.
        var due = _waves == 0 && settings.FirstAttackBy > 0 && context.Tick >= settings.FirstAttackBy && squad.Units.Count >= settings.AllInUnits
            && squad.Strength >= enemy;
        var strong = due || stalled
            || (squad.Units.Count >= Needed() && (long)squad.Strength * 100 >= (long)enemy * settings.AttackThresholdPercent);
        squad.State = strong ? SquadState.Ready : SquadState.Gathering;
        // Half ready by its own count, to be held by not knowing the enemy: the scout goes to look meanwhile (step 10).
        context.Plan.LookAtTheEnemy |= !strong && squad.Units.Count * 2 >= Needed()
            && (long)squad.Strength * 100 >= (long)known * settings.AttackThresholdPercent;
        // The attack of the team (step 11): with half the units it needs, whatever its own window.
        if (context.Plan.JoinAttack is { } joined && squad.Units.Count * 2 >= Needed() && squad.Units.Count > 0 && context.Plan.AttackAllowed)
        {
            (squad.TargetBuilding, squad.TargetCell) = (null, joined);
        }
        else if (!strong || Early(context) || !context.Plan.AttackAllowed || !Retarget(context, squad, squad.Centre) || WaitsForAllies(context, squad))
        {
            return;
        }

        if (context.Log is { } log)
        {
            var why = context.Plan.JoinAttack is not null && squad.TargetCell == context.Plan.JoinAttack ? "the attack of the team"
                : due ? "the end of the window of the personality"
                : stalled ? "the ore gone, the army no longer growing"
                : context.Tick < context.Plan.CounterUntil ? "hitting back after an attack beaten at the base"
                : $"stronger: {settings.AttackThresholdPercent} % of the enemy";
            var assumed = Assumed(context);
            log.Add(context.Tick, context.Module,
                $"squad {squad.Number} sets out, {squad.Units.Count} units of strength {squad.Strength}, for {Where(squad)} — {why}; the enemy "
                + $"{known} as seen{(assumed > known ? $", taken for {assumed} unseen" : "")}");
        }

        squad.State = SquadState.Moving;
        (_gathered, _grewAt) = (0, context.Tick);
        squad.StartStrength = squad.Strength;
        squad.Beaten = false;
        squad.Regroup = null;
        squad.RegroupUntil = 0;
        _waves++;
        _lastWave = context.Tick;
        Attack(context, squad, gather);
    }

    /// <summary>Where a squad goes, for the journal: the building it attacks, else the cell.</summary>
    private static string Where(Squad squad) =>
        squad.TargetBuilding is { } building ? $"the building #{building.Value} at {squad.TargetCell?.X},{squad.TargetCell?.Y}"
        : squad.TargetCell is { } cell ? $"{cell.X},{cell.Y}" : "nowhere";

    /// <summary>
    /// Whether a ready squad still waits for its allies (step 11): it announces its attack first, then waits until every allied bot has
    /// answered, <see cref="WaitForAllies"/> at the most; it sets out for the attack of the team, the strongest announced. Alone it never
    /// waits.
    /// </summary>
    private bool WaitsForAllies(AiContext context, Squad squad)
    {
        if (team is null || team.Bots.Length < 2)
        {
            return false;
        }

        var me = context.View.Me;
        if (_announcedAt is null)
        {
            team.Announce(new AttackIntent(me, squad.TargetCell!.Value, squad.Strength, context.Tick));
            context.Log?.Add(context.Tick, context.Module, $"squad {squad.Number} announces its attack to the team and waits for the answers");
            _announcedAt = context.Tick;
            return true;
        }

        if (!team.AllAnswered(me) && context.Tick - _announcedAt.Value < WaitForAllies)
        {
            return true;
        }

        if (team.TeamAttack() is { } attack && attack.Player != me)
        {
            (squad.TargetBuilding, squad.TargetCell) = (null, attack.Target);
        }

        return false;
    }

    /// <summary>
    /// Whether it is too soon to set out (step 8): before the tick of the first attack — drawn once in the window of the personality — or
    /// within <see cref="AiSettings.WaveInterval"/> of the last wave.
    /// </summary>
    private bool Early(AiContext context)
    {
        if (_waves > 0)
        {
            return context.Tick < _lastWave + settings.WaveInterval;
        }

        var window = settings.FirstAttackBy - settings.FirstAttackFrom;
        _attackAt ??= settings.FirstAttackFrom + (window > 0 ? context.Roll(window + 1) : 0);
        return context.Tick < _attackAt;
    }

    /// <summary>
    /// The army the bot assumes the enemy has (M8 step 10): <see cref="AiSettings.AssumedEnemyPercent"/> of its own — the same map and the
    /// same ore — unless it saw the enemy's army within <see cref="EnemyEstimate.FreshTicks"/> and that was half of it or more. Taken for
    /// nothing, an enemy unseen for minutes let any squad set out, and the squads of «Medium» and «Hard» ran into the whole army of «Easy»
    /// waiting at its base; a scout that sees a third of an army at best made «Hard» take «Medium» for a fourth of what it was.
    /// </summary>
    private int Assumed(AiContext context)
    {
        // Hitting back after an attack beaten at home (step 10a): the enemy is what the bot sees.
        if (context.Tick < context.Plan.CounterUntil)
        {
            return 0;
        }

        var own = 0;
        foreach (var unit in context.View.OwnUnits)
        {
            if (unit.Archetype.Armed && unit.Harvester is null)
            {
                own += context.Memory.StrengthOf(unit);
            }
        }

        var assumed = own * settings.AssumedEnemyPercent / 100;
        var estimate = context.Memory.Estimate.Total;
        var fresh = estimate.LastSeenTick >= 0 && context.Tick - estimate.LastSeenTick < EnemyEstimate.FreshTicks;
        // A look that shows a few units at the gate of the base does not show the army: it counts showing half of what is assumed, or
        // having seen the core of the enemy (AiMemory.CoreSeenTick) as long as the estimate keeps what it saw — where its army waits at
        // home — and next to nothing there, a fourth of what is assumed at most: a base with an army shows part of it to a scout that
        // keeps out of the reach of the defenders.
        var core = context.Memory.CoreSeenTick;
        // Not for a bot that must see the army itself (step 10a, attack_knowledge: army): a scout of «Hard» saw the yard of «Medium» and
        // not its army beside it, and «Hard» lost its own attacking into it.
        var emptyBase = settings.Knowledge == AttackKnowledge.Base
            && core >= 0 && context.Tick - core < EnemyEstimate.ForgetTicks && (long)estimate.ArmyStrength * 4 <= assumed;
        return (fresh && (long)estimate.ArmyStrength * 2 >= assumed) || emptyBase ? 0 : assumed;
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
            context.Log?.Add(context.Tick, context.Module,
                $"squad {squad.Number} turns back beaten: strength {squad.Strength} of {squad.StartStrength}, below {settings.RetreatAtStrengthPercent} %");
            squad.State = SquadState.Retreating;
            squad.Beaten = true;
            return;
        }

        if (!Retarget(context, squad, centre))
        {
            // Nothing left to attack: the wave is over.
            context.Log?.Add(context.Tick, context.Module, $"squad {squad.Number} turns back: nothing left to attack");
            squad.State = SquadState.Retreating;
            return;
        }

        if (settings.Raids && squad.TargetBuilding is null && squad.TargetCell is { } field && Distance(centre, field) <= ArrivedCells
            && Nearest(context, centre, armed: true) is null && Nearest(context, centre, armed: false) is null)
        {
            // A raid at a field with no harvester (step 10a): it waits for one, then on to the next field.
            if (squad.AmbushUntil == 0)
            {
                squad.AmbushUntil = context.Tick + AmbushTicks;
                context.Log?.Add(context.Tick, context.Module, $"raid {squad.Number} waits for a harvester at the field {field.X},{field.Y}");
            }

            if (context.Tick < squad.AmbushUntil)
            {
                return;
            }

            (squad.AmbushUntil, squad.TargetCell) = (0, null);
            squad.Swept.Add(field);
            context.Log?.Add(context.Tick, context.Module, $"raid {squad.Number} leaves the field {field.X},{field.Y}: no harvester came");
            if (!Retarget(context, squad, centre))
            {
                squad.State = SquadState.Retreating;
                return;
            }
        }

        if (settings.Raids && NearestHarvester(context, centre) is { } harvester)
        {
            // A raid kills the harvester within reach first, under fire too: what it came for (step 10a).
            squad.State = SquadState.Fighting;
            Strike(context, harvester, wounded: true);
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
            if (settings.Micro != MicroLevel.None && Losing(context, squad, centre, out var near))
            {
                context.Log?.Add(context.Tick, context.Module,
                    $"squad {squad.Number} turns back, losing the trade: strength {squad.Strength} against {near} near, below {settings.RetreatThresholdPercent} %");
                (squad.State, squad.Beaten) = (SquadState.Retreating, true);
                squad.RetreatPoint = settings.Micro == MicroLevel.KeepRange ? NearestDefense(context.View, centre) : null;
                return;
            }

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

    /// <summary>
    /// A fight by the micro of the level (step 5): «Easy» attack-moves at <paramref name="nearest"/>; else every unit that can hurt it and
    /// reaches it fires at the enemy of <see cref="Focus"/>, the others attack-move to it — firing on the way, not walking past enemies
    /// under fire (M8 step 10); «Hard» and «Brutal» pull the wounded back behind the squad, once; «Brutal» keeps its longer-ranged units
    /// out of reach (<see cref="KeepRange"/>), and they fire again on its next turn.
    /// </summary>
    private void Fight(AiContext context, Entity nearest, CellPos centre, CellPos home)
    {
        if (settings.Micro == MicroLevel.None)
        {
            _batch.Clear();
            AttackMoveAll(context, CellOf(nearest));
            return;
        }

        var focus = Focus(context, centre) ?? nearest;
        if (settings.Micro >= MicroLevel.RetreatWounded)
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
        }

        _kiting.Clear();
        if (settings.Micro == MicroLevel.KeepRange)
        {
            KeepRange(context, centre);
        }

        Strike(context, focus, wounded: false, reachOnly: true);
        _kiting.Clear();
    }

    /// <summary>
    /// The armed enemy in <see cref="AiSettings.EngageCells"/> the squad kills soonest — its hit points over the shares of damage the
    /// squad does to its armor, Σ <see cref="TargetingSystem.ShareAgainst"/> — or, as often as the level errs, one of the three first;
    /// of two as soon, the nearer, then the older. Null when none of them can be hurt.
    /// </summary>
    private Entity? Focus(AiContext context, CellPos centre)
    {
        var rules = context.View.Rules;
        _foes.Clear();
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (!Armed(enemy) || Distance(centre, CellOf(enemy)) > settings.EngageCells)
            {
                continue;
            }

            var (shares, reached) = (0L, false);
            foreach (var unit in _members)
            {
                var share = TargetingSystem.ShareAgainst(rules, unit.Type, enemy);
                shares += share;
                reached |= share > 0 && Reaches(rules, unit, enemy);
            }

            if (shares > 0 && reached)
            {
                _foes.Add((enemy, HpOf(enemy) * 100L * 1000 / shares, Distance(centre, CellOf(enemy))));
            }
        }

        if (_foes.Count == 0)
        {
            return null;
        }

        _foes.Sort((a, b) => a.Kill != b.Kill ? a.Kill.CompareTo(b.Kill)
            : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Enemy.Id.Value.CompareTo(b.Enemy.Id.Value));
        return context.Choose(_foes).Enemy;
    }

    /// <summary>
    /// «Brutal» (step 5): a unit whose best weapon outranges by a cell or more the nearest armed enemy that can hurt it steps
    /// <see cref="KiteCells"/> back from it while that enemy is nearer than its own range less a cell — out of the enemy's reach, still in
    /// its own. It does not fire on this turn.
    /// </summary>
    private void KeepRange(AiContext context, CellPos centre)
    {
        var view = context.View;
        foreach (var unit in _members)
        {
            if (_fellBack.Contains(unit.Id.Value))
            {
                continue;
            }

            var cell = unit.Position.ToCell();
            var reach = RangeOf(view.Rules, view.Rules.WeaponsOf(unit.Type));
            Entity? threat = null;
            var (threatDistance, threatReach) = (int.MaxValue, Fixed.Zero);
            foreach (var enemy in view.VisibleEnemies)
            {
                var distance = Distance(cell, CellOf(enemy));
                if (!Armed(enemy) || distance >= threatDistance || !Hurts(view.Rules, enemy, unit))
                {
                    continue;
                }

                (threat, threatDistance, threatReach) = (enemy, distance, RangeOf(view.Rules, WeaponsOf(view.Rules, enemy)));
            }

            if (threat is not null && reach >= threatReach + Fixed.One && Fixed.FromInt(threatDistance) < reach - Fixed.One)
            {
                var from = CellOf(threat);
                var away = new CellPos(cell.X + (cell.X - from.X), cell.Y + (cell.Y - from.Y));
                var back = Toward(cell, away, KiteCells);
                _batch.Clear();
                Want(unit, OrderKind.Move, back, default);
                Issue(context, OrderKind.Move, back, default);
                _kiting.Add(unit.Id.Value);
            }
        }
    }

    /// <summary>
    /// Whether the squad loses the trade (step 5): its strength below <see cref="AiSettings.RetreatThresholdPercent"/> of the armed enemies
    /// within <see cref="AiSettings.EngageCells"/> + <see cref="TradeCells"/> of its middle — units by their cost and hit points, defenses
    /// by theirs.
    /// </summary>
    private bool Losing(AiContext context, Squad squad, CellPos centre, out long near)
    {
        var database = context.View.Rules.Database;
        var enemies = 0L;
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (!Armed(enemy) || Distance(centre, CellOf(enemy)) > settings.EngageCells + TradeCells)
            {
                continue;
            }

            enemies += enemy switch
            {
                Unit unit => context.Memory.StrengthOf(unit),
                Building building when database.Buildings[building.DefIndex].Hp > 0 =>
                    (long)database.Buildings[building.DefIndex].Cost * building.Hp / database.Buildings[building.DefIndex].Hp,
                _ => 0,
            };
        }

        near = enemies;
        return (long)squad.Strength * 100 < enemies * settings.RetreatThresholdPercent;
    }

    /// <summary>The bot's own defense — a building with a weapon — nearest <paramref name="centre"/>; null without one.</summary>
    private static CellPos? NearestDefense(Rts.AI.View.AiWorldView view, CellPos centre)
    {
        CellPos? best = null;
        foreach (var building in view.OwnBuildings)
        {
            if (building.Weapons.Length > 0 && (best is null || Distance(centre, building.Cell) < Distance(centre, best.Value)))
            {
                best = building.Cell;
            }
        }

        return best;
    }

    private static bool Armed(Entity entity) => entity switch
    {
        Unit unit => unit.Archetype.Armed,
        Building building => building.Weapons.Length > 0,
        _ => false,
    };

    internal static System.Collections.Immutable.ImmutableArray<int> WeaponsOf(GameRules rules, Entity entity) => entity switch
    {
        Unit unit => rules.WeaponsOf(unit.Type),
        Building { DefIndex: >= 0 } building => rules.WeaponsOfBuilding(building.DefIndex),
        _ => [],
    };

    private static bool Hurts(GameRules rules, Entity enemy, Unit unit) => enemy switch
    {
        Unit attacker => TargetingSystem.CanHurt(rules, attacker.Type, unit),
        _ => true,
    };

    /// <summary>The longest range of <paramref name="weapons"/>, in cells.</summary>
    internal static Fixed RangeOf(GameRules rules, System.Collections.Immutable.ImmutableArray<int> weapons)
    {
        var longest = Fixed.Zero;
        foreach (var index in weapons)
        {
            if (index >= 0 && rules.Database.Weapons[index].Range > longest)
            {
                longest = rules.Database.Weapons[index].Range;
            }
        }

        return longest;
    }

    private static int HpOf(Entity entity) => entity switch
    {
        Unit unit => unit.Hp,
        Building building => building.Hp,
        _ => 0,
    };

    private static CellPos CellOf(Entity entity) => entity is Building building ? building.Cell : ((Unit)entity).Position.ToCell();

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

    /// <summary>A beaten squad, or one done: a plain move to the gathering point — «Brutal» to its defense (step 5). True once it is there.</summary>
    private bool Retreat(AiContext context, Squad squad, CellPos? gather)
    {
        if ((squad.RetreatPoint ?? gather) is not { } point)
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
        if (settings.Raids && Raid(context, squad, centre) is { } field)
        {
            (squad.TargetBuilding, squad.TargetCell) = (null, field);
            return true;
        }

        _targets.Clear();
        foreach (var building in memory.Buildings)
        {
            if (building.Id == squad.TargetBuilding)
            {
                return true;
            }

            _targets.Add((building, Rank(rules.Buildings[building.DefIndex], _priority), Distance(centre, building.Cell)));
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
    /// A raid (M8 steps 10, 10a): the enemy harvester seen within <see cref="RaidFreshTicks"/> nearest <paramref name="centre"/> that no
    /// armed enemy the bot knows of guards; else the field of an enemy refinery its harvesters work, the one farthest from what guards them,
    /// that the squad has not waited at yet; null for none.
    /// </summary>
    private CellPos? Raid(AiContext context, Squad squad, CellPos centre)
    {
        var memory = context.Memory;
        HarvesterSighting? best = null;
        foreach (var sighting in memory.Harvesters)
        {
            if (context.Tick - sighting.SeenTick > RaidFreshTicks || Guarded(context, sighting.Cell) < settings.DangerCells)
            {
                continue;
            }

            if (best is null || Distance(sighting.Cell, centre) < Distance(best.Cell, centre))
            {
                best = sighting;
            }
        }

        if (best is not null)
        {
            return best.Cell;
        }

        var rules = context.View.Rules.Database;
        CellPos? field = null;
        var safest = -1;
        foreach (var building in memory.Buildings)
        {
            if (rules.Buildings[building.DefIndex].DockCell is null || FieldOf(context.View, building.Cell) is not { } ore || squad.Swept.Contains(ore))
            {
                continue;
            }

            var guard = Guarded(context, ore);
            if (guard > safest)
            {
                (field, safest) = (ore, guard);
            }
        }

        return field;
    }

    /// <summary>The explored ore nearest <paramref name="refinery"/> within <see cref="RaidFieldCells"/>: where its harvesters work.</summary>
    private static CellPos? FieldOf(AiWorldView view, CellPos refinery)
    {
        CellPos? best = null;
        for (var y = refinery.Y - RaidFieldCells; y <= refinery.Y + RaidFieldCells; y++)
        {
            for (var x = refinery.X - RaidFieldCells; x <= refinery.X + RaidFieldCells; x++)
            {
                var cell = new CellPos(x, y);
                if (view.Contains(cell) && !view.OreAt(cell).IsEmpty && (best is null || Distance(cell, refinery) < Distance(best.Value, refinery)))
                {
                    best = cell;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The enemy harvester in sight nearest <paramref name="centre"/> within <see cref="AiSettings.EngageCells"/>; equal ones to the smaller id;
    /// null for none (step 10a). The War Miner is armed: the search for the unarmed never finds it.
    /// </summary>
    private Unit? NearestHarvester(AiContext context, CellPos centre)
    {
        Unit? best = null;
        var bestDistance = settings.EngageCells + 1;
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (enemy is Unit { Harvester: not null } harvester && Distance(centre, harvester.Position.ToCell()) is var distance
                && (distance < bestDistance || (distance == bestDistance && best is not null && harvester.Id.Value < best.Id.Value)))
            {
                (best, bestDistance) = (harvester, distance);
            }
        }

        return best;
    }

    /// <summary>How far the armed enemy the bot knows of nearest <paramref name="cell"/> is — an army or a defense; very far with none.</summary>
    private static int Guarded(AiContext context, CellPos cell)
    {
        var nearest = int.MaxValue;
        foreach (var army in context.Memory.Armies)
        {
            if (army.Armed > 0)
            {
                nearest = IntMath.Min(nearest, Distance(cell, army.Centre.ToCell()));
            }
        }

        var rules = context.View.Rules.Database;
        foreach (var building in context.Memory.Buildings)
        {
            if (rules.Buildings[building.DefIndex].Primary.Length > 0)
            {
                nearest = IntMath.Min(nearest, Distance(cell, building.Cell));
            }
        }

        return nearest;
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
                if (!view.IsVisible(middle) && !squad.Swept.Contains(middle) && !view.IsScenery(middle, size)
                    && (best is null || Distance(middle, centre) < Distance(best.Value, centre)))
                {
                    best = middle;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Which enemy buildings a squad attacks first: the place of the kind of <paramref name="def"/> in <paramref name="priority"/>, the
    /// <c>target_priority</c> of the personality (M8 step 8) — <c>harvesters</c> the refineries, where they unload; <c>defense</c> those with
    /// a weapon; <c>production</c> the barracks and the factories; <c>core</c> the construction yard; <c>power</c> the power plants. The
    /// rest — the radar, the battle lab — after all of them; <c>army</c> has no building.
    /// </summary>
    internal static int Rank(BuildingDef def, string[] priority)
    {
        var kind = def.DockCell is not null ? "harvesters"
            : def.Primary.Length > 0 ? "defense"
            : def.Produces.Contains(ProductionCategory.Building) ? "core"
            : def.Produces.Length > 0 ? "production"
            : def.Power > 0 ? "power"
            : null;
        var place = kind is null ? -1 : System.Array.IndexOf(priority, kind);
        return place >= 0 ? place : priority.Length;
    }

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

    /// <summary>
    /// Orders every unit that can hurt <paramref name="target"/> to fire at it — the wounded that fell back too, when asked. With
    /// <paramref name="reachOnly"/> a unit out of its reach attack-moves to it instead (M8 step 10): an order to attack drops what a unit
    /// does and walks it to its target without firing at the enemies it passes.
    /// </summary>
    private void Strike(AiContext context, Entity target, bool wounded, bool reachOnly = false)
    {
        var rules = context.View.Rules;
        var cell = target is Building building ? building.Cell : ((Unit)target).Position.ToCell();
        _batch.Clear();
        _far.Clear();
        foreach (var unit in _members)
        {
            if ((wounded || !_fellBack.Contains(unit.Id.Value)) && !_kiting.Contains(unit.Id.Value) && TargetingSystem.CanHurt(rules, unit.Type, target))
            {
                if (reachOnly && !Reaches(rules, unit, target))
                {
                    _far.Add(unit);
                }
                else
                {
                    Want(unit, OrderKind.Attack, cell, target.Id);
                }
            }
        }

        if (_batch.Count > 0)
        {
            context.Issue(new AttackCommand(context.View.Me, context.ExecuteTick, context.NextSequence(), [.. _batch], target.Id));
        }

        _batch.Clear();
        foreach (var unit in _far)
        {
            Want(unit, OrderKind.AttackMove, cell, default);
        }

        Issue(context, OrderKind.AttackMove, cell, default);
    }

    /// <summary>Whether <paramref name="target"/> is within the range of the longest weapon of <paramref name="unit"/> and a cell more.</summary>
    private static bool Reaches(GameRules rules, Unit unit, Entity target) =>
        Fixed.FromInt(Distance(unit.Position.ToCell(), CellOf(target))) <= RangeOf(rules, rules.WeaponsOf(unit.Type)) + Fixed.One;

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
