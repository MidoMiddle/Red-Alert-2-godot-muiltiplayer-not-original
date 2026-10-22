using Rts.Simulation;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>The turn of a bot in the tick (docs/implementation/M5_first_bot.md, 4.2): commands for the next tick, through the queue.</summary>
public sealed class AiTurnTests
{
    private static readonly PlayerId Bot = new(3);

    private static GameSimulation Simulation() => new(new SimulationSettings(1), TestRules.Standard);

    private static SpawnTestUnitCommand Spawn(PlayerId player, int tick, uint sequence, int x = 20) =>
        new(player, tick, sequence, UnitType.Conscript, new CellPos(x, 20).Center);

    [Fact]
    public void A_command_of_the_turn_after_tick_t_executes_on_tick_t_plus_1()
    {
        var simulation = Simulation();
        var issued = false;
        simulation.AttachAi(new LambdaBot(Bot, turn =>
        {
            if (turn.Tick == 4)
            {
                Assert.Equal(5, turn.ExecuteTick);
                turn.Issue(Spawn(Bot, turn.ExecuteTick, turn.NextSequence()));
                issued = true;
            }
        }));

        for (var tick = 0; tick <= 4; tick++)
        {
            simulation.Step();
            Assert.DoesNotContain(simulation.LastTickEvents, e => e is UnitSpawned);
        }

        Assert.True(issued);
        simulation.Step();
        var spawned = Assert.Single(simulation.LastTickEvents.OfType<UnitSpawned>());
        Assert.Equal((5, Bot), (spawned.Tick, spawned.Owner));
        Assert.Equal(AiTurn.FirstSequence, Assert.Single(simulation.ExecutedCommands).Sequence);
    }

    [Fact]
    public void A_command_of_another_player_or_for_another_tick_is_a_mistake_of_the_bot()
    {
        var other = Simulation();
        other.AttachAi(new LambdaBot(Bot, turn => turn.Issue(Spawn(new PlayerId(1), turn.ExecuteTick, turn.NextSequence()))));
        var late = Simulation();
        late.AttachAi(new LambdaBot(Bot, turn => turn.Issue(Spawn(Bot, turn.ExecuteTick + 1, turn.NextSequence()))));

        Assert.Throws<ArgumentException>(other.Step);
        Assert.Throws<ArgumentException>(late.Step);
    }

    [Fact]
    public void A_person_playing_the_player_of_a_bot_does_not_collide_with_its_numbers()
    {
        // The debug switch (F11): a person gives commands for the player of the bot, numbered from 0 as the client does.
        var simulation = Simulation();
        simulation.AttachAi(new LambdaBot(Bot, turn => turn.Issue(Spawn(Bot, turn.ExecuteTick, turn.NextSequence()))));
        for (var tick = 1; tick <= 3; tick++)
        {
            simulation.Enqueue(Spawn(Bot, tick, (uint)(tick - 1), x: 30));
        }

        for (var tick = 0; tick <= 3; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(6, simulation.ExecutedCommands.Count);
        Assert.Equal(6, simulation.World.Entities.OfType<Unit>().Count(unit => unit.Owner == Bot));
    }

    [Fact]
    public void One_bot_a_player_and_only_the_players_1_to_8()
    {
        var simulation = Simulation();
        simulation.AttachAi(new LambdaBot(Bot, _ => { }));

        Assert.Throws<InvalidOperationException>(() => simulation.AttachAi(new LambdaBot(Bot, _ => { })));
        Assert.Throws<ArgumentException>(() => simulation.AttachAi(new LambdaBot(PlayerId.Neutral, _ => { })));
        Assert.Throws<ArgumentException>(() => simulation.AttachAi(new LambdaBot(new PlayerId(9), _ => { })));
    }

    [Fact]
    public void The_bots_think_in_the_order_of_their_players_whatever_the_order_they_came_in()
    {
        var simulation = Simulation();
        var order = new List<int>();
        foreach (var player in new[] { 5, 2, 7 })
        {
            simulation.AttachAi(new LambdaBot(new PlayerId(player), turn => order.Add(turn.Player.Value)));
        }

        simulation.Step();

        Assert.Equal([2, 5, 7], order);
    }
}
