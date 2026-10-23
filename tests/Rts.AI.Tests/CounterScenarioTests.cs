using Rts.AI.Modules;
using Rts.Data.Tests;
using Rts.Simulation.Events;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.AI.Tests;

/// <summary>
/// The counters of the levels at work (docs/implementation/M8_ai_difficulty.md, 4.6, step 7): a base with barracks and a war factory and
/// money enough sees masses of infantry, and its production runs for three minutes with the level's share picked against them — «Hard»
/// builds more of what beats infantry than «Medium», and «Medium» more than «Easy».
/// </summary>
public sealed class CounterScenarioTests(ITestOutputHelper output)
{
    private static int CounterPick(string level) =>
        AiSettings.From(GameDataFiles.Rules.Database.TryGetDifficulty(level, out var def) ? def : throw new ArgumentException(level)).CounterPickPercent;

    /// <summary>The units the production of the level built in three minutes, by kind, with eight enemy conscripts in sight.</summary>
    private List<string> Built(string level)
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 30, 30);
        scene.Build(Scene.Me, "reactor", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 18, 6);
        scene.Build(Scene.Me, "barracks", 24, 6);
        scene.Build(Scene.Me, "war_factory", 30, 6);
        scene.World.Player(Scene.Me).Credits = 100_000;
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 50, 40);
        for (var i = 0; i < 8; i++)
        {
            scene.Spawn(Scene.Enemy, UnitType.Conscript, 52 + i % 4, 42 + i / 4);
        }

        var settings = AiSettings.Medium with { CounterPickPercent = CounterPick(level) };
        var production = new ProductionManager(settings);
        var context = scene.Context();
        var built = new List<string>();
        for (var tick = 0; tick < 3 * 900; tick++)
        {
            if (AiSchedule.IsDue(settings, AiModuleKind.Production, Scene.Me, tick))
            {
                scene.Look(tick);
                context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
                production.Update(context);
                context.End();
            }

            scene.Simulation.Step();
            foreach (var produced in scene.Simulation.LastTickEvents.OfType<UnitProduced>().Where(e => e.Player == Scene.Me))
            {
                built.Add(scene.World.TryGet(produced.Unit, out var unit) ? ((Unit)unit).Archetype.Id : "?");
            }
        }

        var against = built.Count(unit => unit is "heavy_tank" or "support_vehicle");
        output.WriteLine($"{level} ({CounterPick(level)} %): {against} of {built.Count} against infantry — {string.Join(", ", built)}");
        return built;
    }

    private int AgainstInfantry(string level)
    {
        var built = Built(level);
        return built.Count == 0 ? 0 : 100 * built.Count(unit => unit is "heavy_tank" or "support_vehicle") / built.Count;
    }

    [Fact]
    public void Against_masses_of_infantry_hard_builds_more_of_what_beats_it_than_medium_and_medium_than_easy()
    {
        var (easy, medium, hard) = (AgainstInfantry("ai_easy"), AgainstInfantry("ai_medium"), AgainstInfantry("ai_hard"));

        Assert.True(hard > medium && medium > easy, $"«Hard» {hard} %, «Medium» {medium} %, «Easy» {easy} %");
    }
}
