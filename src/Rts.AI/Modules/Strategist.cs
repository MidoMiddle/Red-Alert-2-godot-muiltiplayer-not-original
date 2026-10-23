using Rts.AI.Squads;

namespace Rts.AI.Modules;

/// <summary>
/// The strategist of a bot (docs/implementation/M5_first_bot.md, 4.6, step 9): what the bot does as a whole, and the priorities that
/// follow from it for the other modules. It gives no orders to units.
/// <list type="bullet">
/// <item><b>developing</b> — no squad out; a squad of step 8 that is strong enough («the army ≥ the threshold») sets out by itself while
/// the attack is allowed, and the strategist sees it out on its next turn;</item>
/// <item><b>attacking</b> — a squad moves or fights; once none does, the wave is over: developing again;</item>
/// <item><b>defending</b> — set by <see cref="DefenseManager"/> at once when the base is attacked by more than its defense holds, and
/// left by it, back to what was before, when the base is safe again: the strategist thinks every 3 s, longer than the reaction
/// of 4.7 allows. Meanwhile no attack and the army first (<see cref="AiPlan.ArmyFirst"/>).</item>
/// </list>
/// «Medium» never holds the army back (<see cref="AiPlan.ArmyAllowed"/>).
/// </summary>
public sealed class Strategist : IAiModule
{
    public AiModuleKind Kind => AiModuleKind.Strategist;

    public void Update(AiContext context)
    {
        var plan = context.Plan;
        if (plan.Strategy == StrategyState.Defending)
        {
            return;
        }

        var was = plan.Strategy;
        plan.Strategy = StrategyState.Developing;
        foreach (var squad in plan.Squads)
        {
            if (squad.State is SquadState.Moving or SquadState.Fighting)
            {
                plan.Strategy = StrategyState.Attacking;
                break;
            }
        }

        if (plan.Strategy != was)
        {
            context.Log?.Add(context.Tick, context.Module, plan.Strategy == StrategyState.Attacking ? "attacking: a squad is out" : "developing: no squad out");
        }

        (plan.AttackAllowed, plan.ArmyFirst, plan.ArmyAllowed) = (true, false, true);
    }
}
