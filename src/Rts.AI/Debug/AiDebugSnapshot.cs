using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Rts.AI.Squads;
using Rts.AI.Team;
using Rts.AI.View;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Debug;

/// <summary>A squad as the overlay shows it: its number, what it does, how many units, where it is and goes, its strength now and when it set out.</summary>
public sealed record SquadView(
    int Number, SquadState State, int Units, CellPos Centre, CellPos? Target, CellPos? Regroup, bool Recalled, int Strength, int StartStrength)
{
    /// <summary>Its task for the debug window (M8 step 12): attack, raid, ambush, defense, help, gathering, home.</summary>
    public string Task { get; init; } = "";

    /// <summary>Its units by type, the most first: <c>heavy_tank 4, rocketeer 2</c> (M8 step 12).</summary>
    public string Composition { get; init; } = "";
}

/// <summary>
/// What a bot thinks, copied out for the debug overlay of the client (docs/implementation/M5_first_bot.md, 4.13, step 13): the
/// strategist and its priorities, the threat to the base, the money and power as the bot sees them, the economy's needs and the next
/// building, the squads, the scout, the gathering point and the memory of the enemy. Immutable — the client only reads it, the bot
/// goes on — and made on request (<see cref="AiPlayer.Snapshot"/>) from what the bot already holds, never from the world.
/// </summary>
public sealed record AiDebugSnapshot(
    PlayerId Player,
    int Tick,
    StrategyState Strategy,
    bool AttackAllowed,
    bool ArmyFirst,
    AiThreat? Threat,
    CellPos? AttackedFrom,
    int Credits,
    int PowerOutput,
    int PowerDrain,
    int Harvesters,
    bool HarvestersWanted,
    ImmutableArray<string> Wants,
    string? NextBuilding,
    (string Building, CellPos Cell)? LastPlacement,
    ImmutableArray<SquadView> Squads,
    EntityId? Scout,
    CellPos? ScoutErrand,
    CellPos? GatherPoint,
    ImmutableArray<RememberedBuilding> EnemyBuildings,
    ImmutableArray<ArmySighting> EnemyArmies,
    int EnemyStrength,
    ImmutableArray<CellPos> PossibleEnemyStarts,
    CellPos? LastEnemyCell,
    ImmutableArray<EnemyPicture> Enemies)
{
    /// <summary>The level and the personality, <c>ai_hard:balanced</c> (M8 step 12); null when unknown.</summary>
    public string? Profile { get; init; }

    /// <summary>The settings the bot plays with — its level with its personality applied (M8 step 12).</summary>
    public AiSettings? Settings { get; init; }

    /// <summary>The next steps of the build order not done yet (M8 step 12); empty while the journal is off.</summary>
    public ImmutableArray<string> Upcoming { get; init; } = [];

    /// <summary>The army it has, by type, the most first (M8 step 12).</summary>
    public ImmutableArray<(string Unit, int Count)> Army { get; init; } = [];

    /// <summary>The army it builds toward, the shares in hundredths of a percent with the counters in (M8 step 12).</summary>
    public ImmutableArray<(string Unit, long Share)> ArmyTarget { get; init; } = [];

    /// <summary>The attacks announced on the board of its team and the calls for help (M8 step 12); empty alone.</summary>
    public ImmutableArray<AttackIntent> TeamAttacks { get; init; } = [];

    public ImmutableArray<DefenseRequest> TeamRequests { get; init; } = [];

    /// <summary>The attack of the team, the strongest announced; null for none.</summary>
    public AttackIntent? TeamAttack { get; init; }

    /// <summary>What the team asks of the bot: the attack it joins, the ally it helps (step 11).</summary>
    public CellPos? JoinAttack { get; init; }

    public CellPos? HelpAt { get; init; }

    /// <summary>Until this tick it hits back after an attack beaten at its base (step 10a).</summary>
    public int CounterUntil { get; init; }

    /// <summary>Whether a squad waits to know the enemy and the scout goes to look (step 10).</summary>
    public bool LookAtTheEnemy { get; init; }

    /// <summary>The last decisions with their reasons (M8 step 12); empty while the journal is off.</summary>
    public ImmutableArray<AiDecision> Decisions { get; init; } = [];

    /// <summary>The snapshot of the bot of <paramref name="context"/> after its last turn.</summary>
    internal static AiDebugSnapshot Of(AiContext context)
    {
        var (view, memory, plan) = (context.View, context.Memory, context.Plan);
        var rules = view.Rules.Database;
        var harvesters = 0;
        foreach (var unit in view.OwnUnits)
        {
            harvesters += unit.Harvester is not null ? 1 : 0;
        }

        var wants = ImmutableArray.CreateBuilder<string>();
        foreach (var want in plan.Wants)
        {
            wants.Add(rules.Buildings[want].Id);
        }

        var types = new Dictionary<int, string>();
        var army = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        foreach (var unit in view.OwnUnits)
        {
            types[unit.Id.Value] = unit.Archetype.Id;
            if (unit.Archetype.Armed && unit.Harvester is null)
            {
                army[unit.Archetype.Id] = army.GetValueOrDefault(unit.Archetype.Id) + 1;
            }
        }

        var squads = ImmutableArray.CreateBuilder<SquadView>();
        foreach (var squad in plan.Squads)
        {
            var kinds = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            foreach (var id in squad.Units)
            {
                if (types.TryGetValue(id.Value, out var type))
                {
                    kinds[type] = kinds.GetValueOrDefault(type) + 1;
                }
            }

            squads.Add(new SquadView(
                squad.Number, squad.State, squad.Units.Count, squad.Centre, squad.TargetCell, squad.Regroup, squad.Recalled, squad.Strength, squad.StartStrength)
            {
                Task = TaskOf(squad, context.Settings),
                Composition = string.Join(", ", kinds.OrderByDescending(kind => kind.Value).ThenBy(kind => kind.Key, System.StringComparer.Ordinal)
                    .Select(kind => $"{kind.Key} {kind.Value}")),
            });
        }

        return new AiDebugSnapshot(
            view.Me,
            view.Tick,
            plan.Strategy,
            plan.AttackAllowed,
            plan.ArmyFirst,
            plan.Threat,
            plan.AttackedFrom,
            view.Player.Credits,
            view.Player.PowerOutput,
            view.Player.PowerDrain,
            harvesters,
            plan.HarvestersWanted,
            wants.ToImmutable(),
            plan.NextBuilding >= 0 ? rules.Buildings[plan.NextBuilding].Id : null,
            plan.LastPlacement is { } placed ? (rules.Buildings[placed.DefIndex].Id, placed.Cell) : null,
            squads.ToImmutable(),
            plan.Scout,
            plan.ScoutErrand,
            plan.GatherPoint,
            [.. memory.Buildings],
            [.. memory.Armies],
            memory.EnemyStrength(view),
            [.. memory.PossibleEnemyStarts],
            memory.LastEnemyCell,
            [.. memory.Estimate.Players])
        {
            Settings = context.Settings,
            Upcoming = [.. plan.Upcoming],
            Army = [.. army.OrderByDescending(kind => kind.Value).ThenBy(kind => kind.Key, System.StringComparer.Ordinal).Select(kind => (kind.Key, kind.Value))],
            ArmyTarget = [.. plan.ArmyTarget],
            TeamAttacks = plan.Team is { } team ? [.. team.Intents] : [],
            TeamRequests = plan.Team is { } board ? [.. board.Requests] : [],
            TeamAttack = plan.Team?.TeamAttack(),
            JoinAttack = plan.JoinAttack,
            HelpAt = plan.HelpAt,
            CounterUntil = plan.CounterUntil,
            LookAtTheEnemy = plan.LookAtTheEnemy,
            Decisions = context.Log is { } log ? [.. log.Recent] : [],
        };
    }

    private static string TaskOf(Squad squad, AiSettings settings) =>
        squad.HelpCell is not null ? "help"
        : squad.Recalled || squad.State == SquadState.Defending ? "defense"
        : squad.AmbushUntil > 0 ? "ambush"
        : squad.State is SquadState.Moving or SquadState.Fighting ? (settings.Raids ? "raid" : "attack")
        : squad.State == SquadState.Retreating ? "home"
        : "gathering";

    /// <summary>
    /// The sections of the debug window (docs/implementation/M8_ai_difficulty.md, 4.9, step 12), in its order: Summary, Economy, Army,
    /// Enemies, Squads, Team, Decisions — each a title and its lines.
    /// </summary>
    public ImmutableArray<(string Title, string Text)> Sections()
    {
        var clock = Tick / 15;
        var summary = new StringBuilder();
        Line(summary, $"{Player} · {Profile ?? "bot"} · {clock / 60}:{clock % 60:00}");
        Line(summary, $"strategy {Strategy}{(AttackAllowed ? "" : " · attack held")}{(ArmyFirst ? " · army first" : "")}"
            + (CounterUntil > Tick ? $" · hitting back {(CounterUntil - Tick) / 15} s" : "") + (LookAtTheEnemy ? " · looking at the enemy" : ""));
        if (Threat is { } threat)
        {
            Line(summary, $"threat {threat.Strength} at {Cell(threat.Cell)}");
        }

        if (Settings is { } s)
        {
            Line(summary, $"micro {s.Micro} · scouting {s.Scouting} · counter-pick {s.CounterPickPercent} % · mistakes {s.MistakePercent} % · {s.ActionsPerSecond} actions/s");
            Line(summary, $"attack: {s.MinAttackArmy} units, {s.AttackThresholdPercent} % of the enemy, unseen taken for {s.AssumedEnemyPercent} %, knowledge {s.Knowledge}");
            Line(summary, $"first attack {Time(s.FirstAttackFrom)}" + (s.FirstAttackBy > 0 ? $"–{Time(s.FirstAttackBy)}" : "") + $" · waves every {Time(s.WaveInterval)}"
                + $" · retreat below {s.RetreatThresholdPercent} %{(s.Raids ? " · raids" : "")}");
            Line(summary, $"weights: refineries {s.MaxRefineries} · defenses {s.MaxDefenses} · second factory at {s.SecondFactoryCredits} · targets {s.TargetPriority}");
        }

        var economy = new StringBuilder();
        Line(economy, $"credits {Credits} · power {PowerOutput}/{PowerDrain} · harvesters {Harvesters}{(HarvestersWanted ? " (more wanted)" : "")}"
            + (Settings is { HarvestersByOre: true } ? " · by the ore" : ""));
        Line(economy, $"wants {(Wants.IsEmpty ? "-" : string.Join(", ", Wants))} · next {NextBuilding ?? "-"}");
        Line(economy, $"build order ahead: {(Upcoming.IsEmpty ? "-" : string.Join(", ", Upcoming))}");
        if (LastPlacement is { } placed)
        {
            Line(economy, $"placed last {placed.Building} at {Cell(placed.Cell)}");
        }

        var armySection = new StringBuilder();
        Line(armySection, $"has: {(Army.IsEmpty ? "-" : string.Join(", ", Army.Select(kind => $"{kind.Unit} {kind.Count}")))}");
        Line(armySection, $"builds toward: {(ArmyTarget.IsEmpty ? "-" : string.Join(", ", ArmyTarget.Select(share => $"{share.Unit} {share.Share / 100} %")))}");
        if (Settings is { } settings)
        {
            Line(armySection, $"army {settings.Army} · counter-pick {settings.CounterPickPercent} % · gathering at {Cell(GatherPoint)}");
        }

        var enemies = new StringBuilder();
        Line(enemies, $"strength {EnemyStrength} · buildings {EnemyBuildings.Length} · armies {EnemyArmies.Length} · last seen {Cell(LastEnemyCell)}"
            + $" · starts left {PossibleEnemyStarts.Length}");
        foreach (var enemy in Enemies)
        {
            var seen = enemy.LastSeenTick / 15;
            Line(enemies, $"{enemy.Player}: units {Count(enemy.Units)} (inf {Count(enemy.Infantry)}, veh {Count(enemy.Vehicles)}) · strength {enemy.ArmyStrength}"
                + $" · harvesters {Count(enemy.Harvesters)} · refineries {enemy.Refineries} · tech {enemy.TechLevel} · seen {seen / 60}:{seen % 60:00}"
                + $" ({(Tick - enemy.LastSeenTick) / 15} s ago)");
        }

        var squadsSection = new StringBuilder();
        Line(squadsSection, $"scout {(Scout is { } scout ? $"#{scout.Value} to {Cell(ScoutErrand)}" : "-")}");
        foreach (var squad in Squads)
        {
            Line(squadsSection, $"squad {squad.Number} · {squad.Task} · {squad.State} · strength {squad.Strength}"
                + (squad.StartStrength > 0 ? $"/{squad.StartStrength}" : "") + $" · at {Cell(squad.Centre)} → {Cell(squad.Target)}");
            Line(squadsSection, $"    {squad.Units} units: {(squad.Composition.Length > 0 ? squad.Composition : "-")}");
        }

        var team = new StringBuilder();
        if (TeamAttacks.IsEmpty && TeamRequests.IsEmpty && JoinAttack is null && HelpAt is null)
        {
            Line(team, "no allied bot, nothing asked");
        }

        foreach (var attack in TeamAttacks)
        {
            Line(team, $"{attack.Player} attacks {Cell(attack.Target)} with {attack.Strength}{(TeamAttack == attack ? " — the attack of the team" : "")}");
        }

        foreach (var request in TeamRequests)
        {
            Line(team, $"{request.Player} calls for help at {Cell(request.Cell)}: {request.Strength} at its base");
        }

        if (JoinAttack is { } join)
        {
            Line(team, $"joins the attack at {Cell(join)}");
        }

        if (HelpAt is { } help)
        {
            Line(team, $"helps at {Cell(help)}");
        }

        var decisions = new StringBuilder();
        if (Decisions.IsEmpty)
        {
            Line(decisions, "the journal is off");
        }

        for (var i = Decisions.Length - 1; i >= 0; i--)
        {
            Line(decisions, Decisions[i].ToString());
        }

        return
        [
            ("Summary", summary.ToString().TrimEnd()), ("Economy", economy.ToString().TrimEnd()), ("Army", armySection.ToString().TrimEnd()),
            ("Enemies", enemies.ToString().TrimEnd()), ("Squads", squadsSection.ToString().TrimEnd()), ("Team", team.ToString().TrimEnd()),
            ("Decisions", decisions.ToString().TrimEnd()),
        ];
    }

    private static string Time(int ticks) => $"{ticks / 900}:{ticks % 900 / 15:00}";

    /// <summary>The lines of the overlay's panel, the building ids of the data as they are.</summary>
    public string ToText()
    {
        var text = new StringBuilder();
        // At 15 ticks a second, as the match plays.
        var clock = Tick / 15;
        Line(text, $"AI {Player} · {clock / 60}:{clock % 60:00} · {Strategy}{(AttackAllowed ? "" : " · attack held")}{(ArmyFirst ? " · army first" : "")}");
        if (Threat is { } threat)
        {
            Line(text, $"threat {threat.Strength} at {Cell(threat.Cell)} from {Cell(threat.Middle)}");
        }

        Line(text, $"credits {Credits} · power {PowerOutput}/{PowerDrain} · harvesters {Harvesters}{(HarvestersWanted ? " (more wanted)" : "")}");
        Line(text, $"wants {(Wants.IsEmpty ? "-" : string.Join(", ", Wants))} · next {NextBuilding ?? "-"}"
            + (LastPlacement is { } placed ? $" · placed {placed.Building} at {Cell(placed.Cell)}" : ""));
        Line(text, $"gather {Cell(GatherPoint)} · scout {(Scout is { } scout ? $"#{scout.Value} to {Cell(ScoutErrand)}" : "-")}");
        foreach (var squad in Squads)
        {
            Line(text, $"squad {squad.Number} {squad.State}{(squad.Recalled ? " (recalled)" : "")} · {squad.Units} units · strength {squad.Strength}"
                + (squad.StartStrength > 0 ? $"/{squad.StartStrength}" : "") + $" · at {Cell(squad.Centre)} → {Cell(squad.Target)}"
                + (squad.Regroup is { } regroup ? $" · regroup {Cell(regroup)}" : ""));
        }

        Line(text, $"enemy strength {EnemyStrength} · buildings {EnemyBuildings.Length} · armies {EnemyArmies.Length}"
            + $" · last seen {Cell(LastEnemyCell)} · starts left {PossibleEnemyStarts.Length}");
        foreach (var enemy in Enemies)
        {
            // M8 step 6: what the bot makes of the enemy, the units weighted by how fresh they are.
            var seen = enemy.LastSeenTick / 15;
            Line(text, $"{enemy.Player}: units {Count(enemy.Units)} (inf {Count(enemy.Infantry)}, veh {Count(enemy.Vehicles)})"
                + $" · harvesters {Count(enemy.Harvesters)} · refineries {enemy.Refineries} · tech {enemy.TechLevel} · seen {seen / 60}:{seen % 60:00}");
        }

        if (AttackedFrom is { } from)
        {
            Line(text, $"attacked from {Cell(from)}");
        }

        return text.ToString().TrimEnd();
    }

    private static string Cell(CellPos? cell) => cell is { } at ? string.Create(CultureInfo.InvariantCulture, $"{at.X},{at.Y}") : "-";

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');

    /// <summary>Hundredths of a unit as a whole number, rounded: 250 — 3.</summary>
    private static int Count(int hundredths) => (hundredths + 50) / 100;
}
