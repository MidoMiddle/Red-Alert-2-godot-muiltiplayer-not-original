using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The attacks of a personality (docs/implementation/M8_ai_difficulty.md, 4.7, step 8), on the scenes of <see cref="SquadManagerTests"/>:
/// the first wave not before its window and by its end with what it has, the waves apart, the targets in the order of the personality.
/// </summary>
public sealed class AttackTimingTests
{
    private static readonly AiSettings Out = AiSettings.Medium with { MinAttackArmy = 2, FirstAttackFrom = 0, WaveInterval = 0 };

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
}
