using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.AI.Team;

/// <summary>A person on the team of a bot: who and where they started.</summary>
public readonly record struct HumanAlly(PlayerId Player, CellPos Start);

/// <summary>
/// The team of a bot (docs/implementation/M8_ai_difficulty.md, 4.8, step 11), on the period of the squads; it gives no commands, it
/// tells the squads what the team does (<see cref="AiPlan.JoinAttack"/>, <see cref="AiPlan.HelpAt"/>):
/// <list type="bullet">
/// <item><b>an attack of the team</b> — the strongest one announced on the <see cref="TeamBlackboard"/>: the bot answers, and joins it
/// when its squad at home has half the units it needs to set out or more and its own base is not threatened;</item>
/// <item><b>a call for help</b> — of an allied bot whose base is attacked, within <see cref="HelpCells"/> of the bot's start: the bot helps
/// when its own base is not threatened;</item>
/// <item><b>a person of the team</b> writes nothing on the board; the bot reads them as it sees them: armed enemies at their buildings are
/// a call for help, <see cref="HumanAttackUnits"/> of their armed units or more out of their base and on the move an attack of the team,
/// toward the enemy the bot knows of.</item>
/// </list>
/// </summary>
public sealed class TeamCoordinator(TeamBlackboard? board, ImmutableArray<HumanAlly> humans, AiSettings settings) : IAiModule
{
    /// <summary>
    /// An allied base farther than this from the bot's start, in cells, gets no help from it: on a map of 128 cells every corner is in
    /// reach — those of Four Corners are 87 apart.
    /// </summary>
    public const int HelpCells = 96;

    /// <summary>The armed units a person moves out of their base that make an attack of the team.</summary>
    public const int HumanAttackUnits = 6;

    /// <summary>Units of a person this far from their start, in cells, are out of their base.</summary>
    public const int HumanOutCells = 20;

    public AiModuleKind Kind => AiModuleKind.Team;

    public void Update(AiContext context)
    {
        var plan = context.Plan;
        var me = context.View.Me;
        board?.Forget(context.Tick);
        (plan.JoinAttack, plan.HelpAt) = (null, null);
        if (plan.Threat is not null)
        {
            return;
        }

        var home = context.View.Start ?? new CellPos(context.View.Width / 2, context.View.Height / 2);
        var free = plan.GatheringUnits > 0 && plan.GatheringUnits * 2 >= plan.NeededUnits;
        if (board?.TeamAttack() is { } attack && attack.Player != me)
        {
            board.Answer(attack.Player, me, free, plan.GatheringStrength);
            plan.JoinAttack = free ? attack.Target : null;
        }
        else if (free && HumanAttack(context) && context.Memory.LikelyEnemyCell() is { } enemy)
        {
            plan.JoinAttack = enemy;
        }

        if (board is not null)
        {
            foreach (var request in board.Requests)
            {
                if (request.Player != me && Distance(request.Cell, home) <= HelpCells)
                {
                    plan.HelpAt = request.Cell;
                    return;
                }
            }
        }

        if (HumanThreat(context) is { } threatened && Distance(threatened, home) <= HelpCells)
        {
            plan.HelpAt = threatened;
        }
    }

    /// <summary>The middle of the armed enemies the bot sees at the buildings of a person of the team; null with none.</summary>
    private CellPos? HumanThreat(AiContext context)
    {
        var view = context.View;
        var (x, y, count) = (0L, 0L, 0);
        foreach (var human in humans)
        {
            foreach (var enemy in view.VisibleEnemies)
            {
                if (enemy is not Unit { Archetype.Armed: true, Harvester: null } unit)
                {
                    continue;
                }

                var at = unit.Position.ToCell();
                foreach (var ally in view.Allied)
                {
                    if (ally is Building building && building.Owner == human.Player && Distance(at, building.Cell) <= settings.BaseCells)
                    {
                        (x, y, count) = (x + at.X, y + at.Y, count + 1);
                        break;
                    }
                }
            }
        }

        return count == 0 ? null : new CellPos((int)(x / count), (int)(y / count));
    }

    /// <summary>Whether a person of the team has <see cref="HumanAttackUnits"/> armed units or more out of their base, on the move.</summary>
    private bool HumanAttack(AiContext context)
    {
        foreach (var human in humans)
        {
            var away = 0;
            foreach (var ally in context.View.Allied)
            {
                if (ally is Unit { Archetype.Armed: true, Harvester: null, MoveTarget: not null } unit && unit.Owner == human.Player
                    && Distance(unit.Position.ToCell(), human.Start) > HumanOutCells)
                {
                    away++;
                }
            }

            if (away >= HumanAttackUnits)
            {
                return true;
            }
        }

        return false;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
