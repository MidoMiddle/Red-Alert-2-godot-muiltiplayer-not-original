using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The defense of the base of a bot and its strategist (docs/implementation/M5_first_bot.md, 4.6, step 9), on scenes: the bot's yard at
/// (7, 7), its start (8, 8). The rules of the tests give every unit and building the same cost, so strengths are counts.
/// </summary>
public sealed class DefenseManagerTests
{
    /// <summary>One turn of the defense on <paramref name="tick"/>, after <paramref name="then"/> changed the scene.</summary>
    private static void Turn(Scene scene, DefenseManager defense, AiContext context, int tick, Action? then = null)
    {
        then?.Invoke();
        scene.Look(tick);
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
        defense.Update(context);
    }

    private static (Scene Scene, DefenseManager Defense, AiContext Context) Base(int perceptionDelay = 0)
    {
        var scene = new Scene(perceptionDelay: perceptionDelay);
        scene.Build(Scene.Me, "construction_yard", 7, 7);
        return (scene, new DefenseManager(AiSettings.Medium), scene.Context());
    }

    private static int Strength(Scene scene, UnitType type) => scene.Memory.StrengthOf(scene.World.SpawnUnit(Scene.Me, type, new CellPos(60, 60).Center));

    [Fact]
    public void Enemies_at_the_base_are_a_threat_once_the_bot_has_noticed_them()
    {
        // The delay is the bot's perception (M8 step 3): the tank is news to the bot only so many ticks after it came into sight.
        var (scene, defense, context) = Base(AiSettings.Medium.PerceptionDelay);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 10);

        Turn(scene, defense, context, 0);
        Assert.Null(context.Plan.Threat);

        Turn(scene, defense, context, 10);
        Assert.Null(context.Plan.Threat);

        Turn(scene, defense, context, AiSettings.Medium.PerceptionDelay);
        var threat = context.Plan.Threat;
        Assert.NotNull(threat);
        Assert.Equal(new CellPos(14, 10), threat.Cell);
        Assert.True(threat.Strength > 0);
        Assert.Equal(StrategyState.Developing, context.Plan.Strategy);
    }

    [Fact]
    public void A_threat_stronger_than_the_defense_on_a_base_hit_calls_back_the_nearest_squad()
    {
        var (scene, defense, context) = Base();
        for (var i = 0; i < 4; i++)
        {
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 8 + i);
        }

        Turn(scene, defense, context, 0);
        var tank = Strength(scene, UnitType.HeavyTank);
        var near = new Squad(1) { State = SquadState.Moving, Strength = 5 * tank, Centre = new CellPos(24, 24) };
        var far = new Squad(2) { State = SquadState.Fighting, Strength = 5 * tank, Centre = new CellPos(50, 50) };
        var home = new Squad(3) { State = SquadState.Gathering, Strength = tank, Centre = new CellPos(12, 12) };
        context.Plan.Squads = [far, near, home];
        context.Plan.Strategy = StrategyState.Attacking;
        scene.World.Player(Scene.Me).LastUnderAttack = 10;

        Turn(scene, defense, context, 15);

        Assert.Equal(StrategyState.Defending, context.Plan.Strategy);
        Assert.False(context.Plan.AttackAllowed);
        Assert.True(context.Plan.ArmyFirst);
        // Four tanks against one at home: the near squad of five covers them, the far one stays out.
        Assert.True(near.Recalled);
        Assert.False(far.Recalled);
        Assert.Equal(new CellPos(14, 9), context.Plan.AttackedFrom);
    }

    [Fact]
    public void A_threat_the_defense_holds_or_a_base_not_hit_does_not_make_it_defend()
    {
        var (scene, defense, context) = Base();
        scene.Build(Scene.Me, "tesla_coil", 12, 7);
        scene.Spawn(Scene.Enemy, UnitType.Conscript, 15, 8);
        context.Plan.Squads = [new Squad(1) { State = SquadState.Moving, Strength = 1000, Centre = new CellPos(30, 30) }];
        scene.World.Player(Scene.Me).LastUnderAttack = 0;
        Turn(scene, defense, context, 0);

        // One conscript against the tesla coil: a threat for the squad at home, nothing more.
        Turn(scene, defense, context, 15);
        Assert.NotNull(context.Plan.Threat);
        Assert.NotEqual(StrategyState.Defending, context.Plan.Strategy);

        // Five tanks, but no building hit lately: no defense either.
        var (other, otherDefense, otherContext) = Base();
        for (var i = 0; i < 5; i++)
        {
            other.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 8 + i);
        }

        Turn(other, otherDefense, otherContext, 0);
        Turn(other, otherDefense, otherContext, 15);
        Assert.NotNull(otherContext.Plan.Threat);
        Assert.NotEqual(StrategyState.Defending, otherContext.Plan.Strategy);
    }

    [Fact]
    public void Ten_seconds_with_no_enemy_at_the_base_end_the_defense_back_to_what_it_was()
    {
        var (scene, defense, context) = Base();
        var tanks = Enumerable.Range(0, 3).Select(i => scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 8 + i)).ToList();
        context.Plan.Strategy = StrategyState.Attacking;
        scene.World.Player(Scene.Me).LastUnderAttack = 0;
        Turn(scene, defense, context, 0);
        Turn(scene, defense, context, 15);
        Assert.Equal(StrategyState.Defending, context.Plan.Strategy);

        Turn(scene, defense, context, 20, () =>
        {
            foreach (var tank in tanks)
            {
                tank.MarkDead();
            }

            scene.World.RemoveDead();
        });
        // The enemies were last at the base on tick 15.
        Turn(scene, defense, context, 15 + AiSettings.Medium.ThreatClearTicks - 5);
        Assert.Equal(StrategyState.Defending, context.Plan.Strategy);

        Turn(scene, defense, context, 15 + AiSettings.Medium.ThreatClearTicks);
        Assert.Null(context.Plan.Threat);
        Assert.Equal(StrategyState.Attacking, context.Plan.Strategy);
        Assert.True(context.Plan.AttackAllowed);
        Assert.False(context.Plan.ArmyFirst);
    }

    [Fact]
    public void The_strategist_attacks_while_a_squad_is_out_and_does_not_leave_the_defense_itself()
    {
        var scene = new Scene();
        var context = scene.Context();
        var strategist = new Strategist();
        var squad = new Squad(1) { State = SquadState.Gathering };
        context.Plan.Squads = [squad];

        strategist.Update(context);
        Assert.Equal(StrategyState.Developing, context.Plan.Strategy);

        squad.State = SquadState.Moving;
        strategist.Update(context);
        Assert.Equal(StrategyState.Attacking, context.Plan.Strategy);
        Assert.True(context.Plan.AttackAllowed);

        // The wave is over.
        squad.State = SquadState.Retreating;
        strategist.Update(context);
        Assert.Equal(StrategyState.Developing, context.Plan.Strategy);

        context.Plan.Defend();
        strategist.Update(context);
        Assert.Equal(StrategyState.Defending, context.Plan.Strategy);
        Assert.False(context.Plan.AttackAllowed);
        Assert.True(context.Plan.ArmyFirst);
    }
}
