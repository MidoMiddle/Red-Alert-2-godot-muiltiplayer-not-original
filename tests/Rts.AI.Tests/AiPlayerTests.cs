using Rts.Simulation;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>One bot and its modules (docs/implementation/M5_first_bot.md, 3, 4.3).</summary>
public sealed class AiPlayerTests
{
    private static readonly PlayerId Bot = new(1);

    [Fact]
    public void The_modules_think_on_their_ticks_in_the_order_of_the_table()
    {
        var log = new List<(AiModuleKind Kind, int Tick)>();
        // Given out of order: the bot puts them in the order of the table.
        var modules = new[] { AiModuleKind.Defense, AiModuleKind.Squads, AiModuleKind.Strategist, AiModuleKind.Economy }
            .Select(kind => (IAiModule)new RecordingModule(kind, log))
            .ToList();
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);
        simulation.AttachAi(new AiPlayer(Bot, AiSettings.Medium, modules));

        for (var tick = 0; tick < 90; tick++)
        {
            simulation.Step();
        }

        var expected = Enumerable.Range(0, 90)
            .SelectMany(tick => new[] { AiModuleKind.Strategist, AiModuleKind.Economy, AiModuleKind.Squads, AiModuleKind.Defense }
                .Where(kind => AiSchedule.IsDue(AiSettings.Medium, kind, Bot, tick))
                .Select(kind => (kind, tick)))
            .ToList();
        Assert.Equal(expected, log);
        // Tick 42: the strategist (42 + 3 + 0 = 45) and the squads (45 % 15 = 0) share it, the strategist first.
        Assert.Equal([(AiModuleKind.Strategist, 42), (AiModuleKind.Squads, 42)], log.Where(entry => entry.Tick == 42));
    }

    [Fact]
    public void Two_modules_of_one_kind_are_refused()
    {
        var log = new List<(AiModuleKind Kind, int Tick)>();

        Assert.Throws<ArgumentException>(() => new AiPlayer(Bot, AiSettings.Medium,
            [new RecordingModule(AiModuleKind.Economy, log), new RecordingModule(AiModuleKind.Economy, log)]));
    }
}
