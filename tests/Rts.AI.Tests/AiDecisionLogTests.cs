using Rts.AI.Debug;
using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The journal of the decisions of a bot (docs/implementation/M8_ai_difficulty.md, 4.9, step 12): the last 50 in order, each decision with
/// its reason, the sections of the debug window — and nothing the bot does changes with it.
/// </summary>
public sealed class AiDecisionLogTests
{
    private static readonly AiSettings Out = AiSettings.Medium with { MinAttackArmy = 2, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };

    private static void Turn(Scene scene, IAiModule module, AiContext context, int tick, Action? then = null)
    {
        while (scene.Simulation.CurrentTick < tick)
        {
            scene.Simulation.Step();
        }

        then?.Invoke();
        scene.Look(tick);
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
        context.Module = module.Kind;
        module.Update(context);
        context.End();
    }

    private static string Journal(AiContext context) => string.Join("\n", context.Log!.Recent);

    [Fact]
    public void The_journal_keeps_the_last_fifty_in_order_and_hands_every_one_on()
    {
        var log = new AiDecisionLog();
        var handed = 0;
        log.Recorded += _ => handed++;

        for (var tick = 0; tick < 60; tick++)
        {
            log.Add(tick, AiModuleKind.Squads, $"decision {tick}");
        }

        Assert.Equal(60, handed);
        Assert.Equal(Enumerable.Range(10, AiDecisionLog.Capacity), log.Recent.Select(decision => decision.Tick));
        Assert.Equal("0:03 Squads: decision 59", log.Recent.Last().ToString());
    }

    [Fact]
    public void A_squad_that_sets_out_says_why_and_without_a_journal_nothing_is_kept()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var context = scene.Context();
        Assert.Null(context.Log);
        context.Log = new AiDecisionLog();

        Turn(scene, new SquadManager(Out), context, 0);

        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
        Assert.Contains("squad 1 sets out, 2 units", Journal(context));
        Assert.Contains("stronger: 100 % of the enemy", Journal(context));
    }

    [Fact]
    public void An_attack_beaten_back_at_the_base_says_it_hits_back()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 7, 7);
        for (var i = 0; i < 3; i++)
        {
            scene.Spawn(Scene.Me, UnitType.HeavyTank, 10 + i, 12);
        }

        var enemies = Enumerable.Range(0, 2).Select(i => scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 8 + i)).ToList();
        var context = scene.Context();
        context.Log = new AiDecisionLog();
        var defense = new DefenseManager(AiSettings.Medium);
        Turn(scene, defense, context, 0);
        foreach (var enemy in enemies)
        {
            enemy.MarkDead();
        }

        scene.World.RemoveDead();
        Turn(scene, defense, context, AiSettings.Medium.ThreatClearTicks + 1);

        Assert.Contains("enemies at the base", Journal(context));
        Assert.Contains("beaten back by an army", Journal(context));
        Assert.Contains("hitting back", Journal(context));
    }

    [Fact]
    public void The_sections_of_the_window_are_the_seven_of_the_plan_and_a_squad_shows_its_task_and_units()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.Rocketeer, 15, 14);
        var context = scene.Context();
        context.Log = new AiDecisionLog();
        Turn(scene, new SquadManager(Out), context, 0);

        var snapshot = AiDebugSnapshot.Of(context);

        Assert.Equal(["Summary", "Economy", "Army", "Enemies", "Squads", "Team", "Decisions"], snapshot.Sections().Select(section => section.Title));
        var squad = snapshot.Squads.Single();
        Assert.Equal(("attack", "heavy_tank 1, rocketeer 1"), (squad.Task, squad.Composition));
        Assert.Contains("sets out", snapshot.Sections().Single(section => section.Title == "Decisions").Text);
        Assert.Equal(new[] { ("heavy_tank", 1), ("rocketeer", 1) }, snapshot.Army.ToArray());
    }

    [Fact]
    [Trait("Category", "Slow")]
    public void A_match_plays_the_same_with_the_journal_on_and_off()
    {
        var off = new BotMatch().PlayTo(8);
        var on = new BotMatch();
        on.Brain!.Log = new AiDecisionLog();
        on.PlayTo(8);

        Assert.NotEmpty(on.Brain.Log.Recent);
        Assert.Equal(StateHasher.Compute(off.World), StateHasher.Compute(on.World));
    }
}
