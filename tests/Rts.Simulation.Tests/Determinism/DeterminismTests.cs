using Rts.Simulation.Commands;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;

namespace Rts.Simulation.Tests.Determinism;

/// <summary>
/// The simulation must produce the same state no matter how often, in which arrival order,
/// or through which transport the same commands reach it (docs/implementation/M0_foundation.md, section 6).
/// </summary>
public sealed class DeterminismTests
{
    private const int Ticks = 1000;

    private static readonly IReadOnlyList<Command> Commands = ReferenceScenario.Commands(Ticks);

    [Fact]
    public void Scenario_is_not_trivial()
    {
        Assert.InRange(Commands.Count, 300, 700);
        Assert.Contains(Commands, c => c is SpawnTestUnitCommand);
        Assert.Contains(Commands, c => c is MoveCommand { Queue: false });
        Assert.Contains(Commands, c => c is MoveCommand { Queue: true });
        Assert.Contains(Commands, c => c is AttackMoveCommand);
        Assert.Contains(Commands, c => c is WaypointsCommand { Loop: true });
        Assert.Contains(Commands, c => c is WaypointsCommand { Loop: false });
        Assert.Contains(Commands, c => c is StopCommand);
        Assert.Contains(Commands, c => c is ScatterCommand);
    }

    [Fact]
    public void Two_runs_give_the_same_checksum_on_every_tick()
    {
        var first = ReferenceScenario.Run(Commands, Ticks);
        var second = ReferenceScenario.Run(Commands, Ticks);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Arrival_order_of_commands_does_not_matter()
    {
        var shuffleRandom = new TestRandom(5);
        var shuffled = Commands.OrderBy(_ => shuffleRandom.NextULong()).ToList();

        var inOrder = ReferenceScenario.Run(Commands, Ticks);
        var reversed = ReferenceScenario.Run(Commands.Reverse(), Ticks);
        var mixed = ReferenceScenario.Run(shuffled, Ticks);

        Assert.Equal(inOrder, reversed);
        Assert.Equal(inOrder, mixed);
    }

    [Fact]
    public void Commands_passed_through_bytes_give_the_same_result()
    {
        var direct = ReferenceScenario.Run(Commands, Ticks);
        var viaBytes = ReferenceScenario.Run(CommandSerializer.ReadAll(CommandSerializer.WriteAll(Commands)), Ticks);

        Assert.Equal(direct, viaBytes);
    }

    [Fact]
    public void Replay_of_the_scenario_reproduces_the_live_match()
    {
        var live = new GameSimulation(ReferenceScenario.Settings());
        var recorder = new ReplayRecorder(live);
        foreach (var command in Commands)
        {
            live.Enqueue(command);
        }

        for (var i = 0; i < Ticks; i++)
        {
            live.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));
        var result = ReplayPlayer.Verify(replay);

        Assert.Equal(ReplayStatus.Match, result.Status);
        Assert.Equal(new StateChecksum(Ticks, StateHasher.Compute(live.World)), result.Final);
    }

    [Fact]
    public void Different_match_seed_gives_a_different_state()
    {
        var seed42 = ReferenceScenario.Run(Commands, ticks: 10, seed: 42);
        var seed43 = ReferenceScenario.Run(Commands, ticks: 10, seed: 43);

        Assert.NotEqual(seed42[^1], seed43[^1]);
    }
}
