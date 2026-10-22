using Rts.AI.View;
using Rts.Simulation;
using Rts.Simulation.Tests;
using Rts.Simulation.Tests.Determinism;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>What the bots see and remember is the same in two runs of the reference match (docs/implementation/M5_first_bot.md, 4.1).</summary>
public sealed class ViewDeterminismTests
{
    private const int Ticks = 1000;

    /// <summary>A strategist that only writes down what its bot sees and remembers.</summary>
    private sealed class Diary(List<string> lines) : IAiModule
    {
        public AiModuleKind Kind => AiModuleKind.Strategist;

        public void Update(AiContext context)
        {
            var view = context.View;
            var memory = context.Memory;
            lines.Add($"{context.Tick} {view.Me}: enemies {string.Join(",", view.VisibleEnemies.Select(e => e.Id.Value))}; " +
                      $"armies {string.Join(",", memory.Armies.Select(a => $"{a.BlockX}/{a.BlockY}/{a.Units}/{a.Strength}"))}; " +
                      $"buildings {string.Join(",", memory.Buildings.Select(b => b.Id.Value))}");
        }
    }

    private static List<string> Run()
    {
        var lines = new List<string>();
        var simulation = new GameSimulation(ReferenceScenario.Settings(), TestRules.Standard);
        simulation.AttachAi(new AiPlayer(new PlayerId(1), AiSettings.Medium, [new Diary(lines)]));
        simulation.AttachAi(new AiPlayer(new PlayerId(2), AiSettings.Medium, [new Diary(lines)]));
        foreach (var command in ReferenceScenario.Commands(Ticks))
        {
            simulation.Enqueue(command);
        }

        for (var tick = 0; tick < Ticks; tick++)
        {
            simulation.Step();
        }

        return lines;
    }

    [Fact]
    public void The_bots_see_and_remember_the_same_twice()
    {
        var first = Run();

        Assert.Equal(first, Run());
        Assert.Contains(first, line => !line.Contains("enemies ;"));
        Assert.Contains(first, line => !line.Contains("armies ;"));
    }
}
