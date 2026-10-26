using Rts.AI.Modules;
using Rts.AI.Profiles;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The attacks of a personality (docs/implementation/M8_ai_difficulty.md, 4.7, step 8), on the scenes of <see cref="SquadManagerTests"/>:
/// the first wave not before its window and by its end with what it has, the waves apart, the targets in the order of the personality.
/// And what a squad takes the enemy for (step 10): an unseen enemy as strong as half again its own army, a glimpse of a few units no proof
/// of a weaker one.
/// </summary>
public sealed class AttackTimingTests
{
    private static readonly AiSettings Out = AiSettings.Medium with { MinAttackArmy = 2, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };

    private static List<Command> Turn(Scene scene, SquadManager squads, AiContext context, int tick, Action? then = null)
    {
        while (scene.Simulation.CurrentTick < tick)
        {
            scene.Simulation.Step();
        }

        then?.Invoke();
        scene.Look(tick);
        var before = scene.Simulation.ExecutedCommands.Count;
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
        squads.Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();
        return scene.Simulation.ExecutedCommands.Skip(before).Where(command => command.Player == Scene.Me).ToList();
    }

    /// <summary><paramref name="tanks"/> tanks of the bot at its gathering point (14, 14); the enemy unseen, likely at (56, 56).</summary>
    private static (Scene Scene, AiContext Context) Gathered(int tanks)
    {
        var scene = new Scene();
        for (var i = 0; i < tanks; i++)
        {
            scene.Spawn(Scene.Me, UnitType.HeavyTank, 13 + i % 3, 14 + i / 3);
        }

        return (scene, scene.Context());
    }

    [Fact]
    public void A_ready_squad_waits_for_the_first_attack_of_its_level()
    {
        var (scene, context) = Gathered(2);
        var squads = new SquadManager(Out with { FirstAttackFrom = 300 });

        Turn(scene, squads, context, 0);
        Assert.Equal(SquadState.Ready, context.Plan.Squads.Single().State);
        Turn(scene, squads, context, 285);
        Assert.Equal(SquadState.Ready, context.Plan.Squads.Single().State);
        Turn(scene, squads, context, 300);
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
    }

    [Fact]
    public void By_the_end_of_the_window_of_the_personality_the_squad_sets_out_with_what_it_has()
    {
        // Twelve units needed, three there: not ready in the window, out at its end.
        var (scene, context) = Gathered(3);
        var squads = new SquadManager(Out with { MinAttackArmy = 12, FirstAttackFrom = 100, FirstAttackBy = 200, AllInUnits = 3 });

        Turn(scene, squads, context, 150);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
        Turn(scene, squads, context, 200);
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
    }

    [Fact]
    public void The_next_wave_waits_the_interval_of_the_personality()
    {
        var (scene, context) = Gathered(2);
        var squads = new SquadManager(Out with { WaveInterval = 300 });
        Turn(scene, squads, context, 0);

        // Two more tanks gather while the first wave is out.
        Turn(scene, squads, context, 15, () =>
        {
            scene.Spawn(Scene.Me, UnitType.HeavyTank, 13, 18);
            scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 18);
        });
        var second = context.Plan.Squads.Single(squad => squad.State != SquadState.Moving && squad.State != SquadState.Fighting);
        Assert.Equal(SquadState.Ready, second.State);
        Turn(scene, squads, context, 285);
        Assert.Equal(SquadState.Ready, second.State);
        Turn(scene, squads, context, 300);
        Assert.Equal(SquadState.Moving, second.State);
    }

    [Theory]
    // «Aggressor»: production, army, power, harvesters…; «Balanced»: harvesters, defense, production, core.
    [InlineData("production,army,power,harvesters,defense,core", "war_factory")]
    [InlineData("harvesters,defense,production,core,army,power", "refinery")]
    public void A_squad_goes_for_the_enemy_buildings_in_the_order_of_its_personality(string priority, string first)
    {
        var (scene, context) = Gathered(2);
        // A scout sees an enemy refinery and a war factory.
        scene.Spawn(Scene.Me, UnitType.Conscript, 40, 40);
        scene.Build(Scene.Enemy, "refinery", 42, 40);
        var factory = scene.Build(Scene.Enemy, "war_factory", 36, 41);
        var squads = new SquadManager(Out with { TargetPriority = priority });

        Turn(scene, squads, context, 0);

        var squad = context.Plan.Squads.Single(s => s.State == SquadState.Moving);
        var target = scene.World.BuildingAt(squad.TargetCell!.Value);
        Assert.Equal(first, target!.Def!.Id);
        Assert.Equal(first == "war_factory", target == factory);
    }

    /// <summary>A squad of four tanks at its gathering point with <paramref name="threshold"/>, the enemy unseen: does it set out?</summary>
    private static SquadState Unseen(int threshold, Action<Scene>? enemy = null, AttackKnowledge knowledge = AttackKnowledge.Base)
    {
        var (scene, context) = Gathered(4);
        var squads = new SquadManager(Out with { AssumedEnemyPercent = 150, AttackThresholdPercent = threshold, Knowledge = knowledge });
        Turn(scene, squads, context, 0, () => enemy?.Invoke(scene));
        return context.Plan.Squads.Single().State;
    }

    [Theory]
    // The enemy unseen is taken for 150 % of the bot's army (M8 step 10): «Medium» (100 %) and «Hard» (130 %) wait with all they have,
    // «Easy» (60 %, 90 % of 150) sets out.
    [InlineData(60, SquadState.Moving)]
    [InlineData(100, SquadState.Gathering)]
    [InlineData(130, SquadState.Gathering)]
    public void Against_an_enemy_unseen_a_squad_sets_out_only_with_the_threshold_of_easy(int threshold, SquadState state)
    {
        Assert.Equal(state, Unseen(threshold));
    }

    [Fact]
    public void A_glimpse_of_one_enemy_unit_is_no_proof_of_a_weaker_enemy_and_a_look_at_its_army_is()
    {
        // One conscript in sight: still 150 % of the bot's army. A tank in sight: the bot sees half or more of what it assumes — two thirds of
        // the strength of its four tanks — and takes the enemy for what it sees, weaker than its squad.
        void Seen(Scene scene, UnitType type, int count)
        {
            for (var i = 0; i < count; i++)
            {
                scene.Spawn(Scene.Enemy, type, 18 + i, 14);
            }
        }

        Assert.Equal(SquadState.Gathering, Unseen(100, scene => Seen(scene, UnitType.Conscript, 1)));
        // Out, and at once in a fight with the tanks it sees.
        Assert.Contains(Unseen(100, scene => Seen(scene, UnitType.HeavyTank, 3)), new[] { SquadState.Moving, SquadState.Fighting });
    }

    [Fact]
    public void By_the_end_of_the_window_a_squad_does_not_set_out_against_an_enemy_it_takes_for_stronger()
    {
        // M8 step 10: by the clock into the army waiting at home, the squads of «Hard» were lost at the end of the window, match after match.
        var (scene, context) = Gathered(3);
        var squads = new SquadManager(Out with
        {
            MinAttackArmy = 12, FirstAttackFrom = 100, FirstAttackBy = 200, AllInUnits = 3, AssumedEnemyPercent = 150,
        });

        Turn(scene, squads, context, 200);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
    }

    [Fact]
    public void Having_looked_into_the_base_of_the_enemy_a_squad_trusts_what_it_saw()
    {
        // M8 step 10: the core of the enemy seen, where its army waits at home, and no army there: the enemy is what was seen.
        Assert.Contains(Unseen(100, scene => scene.Build(Scene.Enemy, "construction_yard", 18, 14)), new[] { SquadState.Moving, SquadState.Fighting });
    }

    [Fact]
    public void A_squad_half_ready_held_by_not_knowing_the_enemy_asks_for_a_look()
    {
        var (scene, context) = Gathered(2);
        var squads = new SquadManager(Out with { MinAttackArmy = 4, AssumedEnemyPercent = 150 });

        Turn(scene, squads, context, 0);

        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
        Assert.True(context.Plan.LookAtTheEnemy);
    }

    [Fact]
    public void A_squad_held_by_not_knowing_the_enemy_sets_out_after_three_minutes()
    {
        // The owner's decision of 2026-10-26: on War Pro the scouts of «Balanced» were killed, and its squad waited half an hour to know.
        var (scene, context) = Gathered(4);
        var squads = new SquadManager(Out with { AssumedEnemyPercent = 150 });
        context.Log = new Rts.AI.Debug.AiDecisionLog();

        Turn(scene, squads, context, 0);
        Turn(scene, squads, context, SquadManager.KnowledgeWaitTicks - 15);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);

        Turn(scene, squads, context, SquadManager.KnowledgeWaitTicks);
        Assert.Contains(context.Plan.Squads.Single().State, new[] { SquadState.Moving, SquadState.Fighting });
        Assert.Contains(context.Log!.Recent, line => line.Text.Contains("waited 3 min to know the enemy"));
    }

    [Fact]
    public void A_squad_short_of_the_units_it_needs_does_not_set_out_however_long_it_waits_to_know()
    {
        var (scene, context) = Gathered(3);
        var squads = new SquadManager(Out with { MinAttackArmy = 4, AssumedEnemyPercent = 150 });

        Turn(scene, squads, context, 0);
        Turn(scene, squads, context, SquadManager.KnowledgeWaitTicks + 15);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
    }

    [Fact]
    public void A_bot_that_must_see_the_army_does_not_trust_a_look_into_a_base_without_it()
    {
        // M8 step 10a, attack_knowledge: army — «Hard» saw the yard of «Medium», not its army beside it, and lost its own attacking.
        void Yard(Scene scene) => scene.Build(Scene.Enemy, "construction_yard", 18, 14);

        Assert.Contains(Unseen(100, Yard), new[] { SquadState.Moving, SquadState.Fighting });
        Assert.Equal(SquadState.Gathering, Unseen(100, Yard, AttackKnowledge.Army));
    }

    [Fact]
    public void Hitting_back_a_squad_takes_the_enemy_for_what_it_sees()
    {
        // M8 step 10a: an attack beaten at the base (DefenseManager) — for a minute the enemy is what the bot sees.
        var (scene, context) = Gathered(4);
        var squads = new SquadManager(Out with { AssumedEnemyPercent = 150, Knowledge = AttackKnowledge.Army });
        Turn(scene, squads, context, 0);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);

        context.Plan.CounterUntil = 15 + 900;
        Turn(scene, squads, context, 15);
        Assert.Contains(context.Plan.Squads.Single().State, new[] { SquadState.Moving, SquadState.Fighting });
    }
}
