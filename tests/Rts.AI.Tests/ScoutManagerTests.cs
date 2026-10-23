using System.Collections.Immutable;
using Rts.AI.Modules;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>What the scout of a bot does (docs/implementation/M5_first_bot.md, 4.6, step 7), on scenes.</summary>
public sealed class ScoutManagerTests
{
    /// <summary>The bot's start in the north-west, one start near it, one far.</summary>
    private static readonly ImmutableArray<CellPos> Starts = [new(8, 8), new(20, 8), new(56, 56)];

    /// <summary>
    /// One turn of the scouting after <paramref name="tick"/>: the world plays on to it, <paramref name="then"/> changes the scene; the cells
    /// of the move orders the turn gave, once they ran.
    /// </summary>
    private static List<CellPos> Turn(Scene scene, ScoutManager scouting, AiContext context, int tick, Action? then = null)
    {
        while (scene.Simulation.CurrentTick < tick)
        {
            scene.Simulation.Step();
        }

        then?.Invoke();
        scene.Look(tick);
        var before = scene.Simulation.ExecutedCommands.Count;
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
        scouting.Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();
        return scene.Simulation.ExecutedCommands.Skip(before).OfType<MoveCommand>().Select(command => command.Target.ToCell()).ToList();
    }

    [Fact]
    public void The_cheapest_fighter_goes_to_the_nearest_start_where_the_enemy_may_be_then_to_the_next()
    {
        var scene = new Scene(Starts);
        // The rules of the tests give every unit the same cost: the first of the cheapest scouts.
        var conscript = scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 6, 6);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);

        Assert.Equal([new CellPos(20, 8)], Turn(scene, scouting, context, 0));
        Assert.Equal(conscript.Id, context.Plan.Scout);

        // The near start explored: on to the far one.
        Assert.Equal([new CellPos(56, 56)], Turn(scene, scouting, context, 45, () => Scene.Move(conscript, 19, 8)));
    }

    [Fact]
    public void The_same_order_is_not_given_twice()
    {
        var scene = new Scene(Starts);
        scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);

        Assert.Single(Turn(scene, scouting, context, 0));
        Assert.Empty(Turn(scene, scouting, context, 45));
    }

    [Fact]
    public void An_armed_enemy_near_the_scout_sends_it_home()
    {
        var scene = new Scene(Starts);
        var conscript = scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);
        Turn(scene, scouting, context, 0);

        var home = Turn(scene, scouting, context, 45, () =>
        {
            Scene.Move(conscript, 40, 40);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 44, 40);
        });

        Assert.Equal([new CellPos(8, 8)], home);
        Assert.Null(scouting.Errand);
    }

    [Fact]
    public void Once_the_enemy_is_seen_it_looks_into_the_nearest_unexplored_block()
    {
        var scene = new Scene(Starts);
        // The bot's own corner is seen, as in a match.
        scene.Build(Scene.Me, "construction_yard", 7, 7);
        var conscript = scene.Spawn(Scene.Me, UnitType.Conscript, 20, 8);
        // An unarmed enemy in sight: found, and no danger.
        scene.Spawn(Scene.Enemy, UnitType.Engineer, 23, 8);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);

        var orders = Turn(scene, scouting, context, 0);

        // The bot's start is (8, 8); the blocks of 16 cells have their middles at 8, 24, 40, 56. Around the scout everything is seen
        // but the block of (8, 24), the nearest one still in the shroud.
        Assert.Equal([new CellPos(8, 24)], orders);
        Assert.Equal(conscript.Id, context.Plan.Scout);
    }

    [Fact]
    public void With_everything_explored_it_looks_at_the_enemy_building_seen_longest_ago()
    {
        var scene = new Scene(Starts);
        scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var old = scene.Build(Scene.Enemy, "reactor", 50, 50);
        var vision = scene.World.VisionOf(Scene.Me);
        // Everything seen at tick 0, then in the fog; a second reactor seen alone at tick 1000.
        vision.Reveal(new CellPos(32, 32), 50);
        scene.Look(0);
        vision.Hide(new CellPos(32, 32), 50);
        var fresh = scene.Build(Scene.Enemy, "reactor", 50, 20);
        vision.Reveal(new CellPos(51, 21), 3);
        scene.Look(1000);
        vision.Hide(new CellPos(51, 21), 3);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);

        // 2000 ticks on: the old reactor was seen 2000 ticks ago, the fresh one 1000 — only the old one is due for a look.
        Assert.Equal([old.Cell], Turn(scene, scouting, context, 2000));
        Assert.NotEqual(old.Cell, fresh.Cell);
    }

    [Fact]
    public void A_fallen_scout_is_followed_by_another()
    {
        var scene = new Scene(Starts);
        var first = scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var second = scene.Spawn(Scene.Me, UnitType.Rocketeer, 6, 8);
        var context = scene.Context();
        var scouting = new ScoutManager(AiSettings.Medium);
        Turn(scene, scouting, context, 0);
        Assert.Equal(first.Id, context.Plan.Scout);

        first.MarkDead();
        scene.World.RemoveDead();
        Turn(scene, scouting, context, 45);

        Assert.Equal(second.Id, context.Plan.Scout);
    }
}
