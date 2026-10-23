using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Rts.AI.Squads;
using Rts.AI.View;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Debug;

/// <summary>A squad as the overlay shows it: its number, what it does, how many units, where it is and goes, its strength now and when it set out.</summary>
public sealed record SquadView(
    int Number, SquadState State, int Units, CellPos Centre, CellPos? Target, CellPos? Regroup, bool Recalled, int Strength, int StartStrength);

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
    CellPos? LastEnemyCell)
{
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

        var squads = ImmutableArray.CreateBuilder<SquadView>();
        foreach (var squad in plan.Squads)
        {
            squads.Add(new SquadView(
                squad.Number, squad.State, squad.Units.Count, squad.Centre, squad.TargetCell, squad.Regroup, squad.Recalled, squad.Strength, squad.StartStrength));
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
            memory.LastEnemyCell);
    }

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
        if (AttackedFrom is { } from)
        {
            Line(text, $"attacked from {Cell(from)}");
        }

        return text.ToString().TrimEnd();
    }

    private static string Cell(CellPos? cell) => cell is { } at ? string.Create(CultureInfo.InvariantCulture, $"{at.X},{at.Y}") : "-";

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
