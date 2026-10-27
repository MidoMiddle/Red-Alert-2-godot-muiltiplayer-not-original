using Rts.AI.Modules;
using Rts.Data.Tests;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The army of the «Medium» bot in a match by the real data (docs/implementation/M5_first_bot.md, step 6): its factories do not stand
/// idle while there is money, its army keeps near the shares of the data, and its base still gets built.
/// </summary>
[Trait("Category", "Slow")]
public sealed class ArmyScenarioTests
{
    private const int Minutes = 15;

    private static readonly ArmyDef Army = GameDataFiles.Rules.Database.Armies.Single(army => army.Id == "standard_army");

    private sealed record Run(BotMatch Match, int Checks, int IdleWithMoney, SortedSet<string> Targeted);

    private static readonly Lazy<Run> Once = new(Play);

    /// <summary>The match, and how often the war factory stood idle while there was money to spare (every 15 ticks).</summary>
    private static Run Play()
    {
        var match = new BotMatch();
        var (checks, idle) = (0, 0);
        var targeted = new SortedSet<string>(StringComparer.Ordinal);
        while (match.Simulation.CurrentTick < Minutes * 900)
        {
            match.Step();

            // The target of the army once a minute (M11): the units the bot means to have as its technology grows.
            if (match.Simulation.CurrentTick % 900 == 0 && match.Brain?.Context is { } context)
            {
                var production = new ProductionManager(match.Brain.Settings);
                production.Target(context);
                targeted.UnionWith(production.Shares.Select(share => GameDataFiles.Rules.Database.Units[share.Unit].Id));
            }

            if (match.Simulation.CurrentTick % 15 != 0)
            {
                continue;
            }

            var player = match.World.Player(BotMatch.Bot);
            var promised = 0;
            foreach (var category in new[] { ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Building, ProductionCategory.Defense })
            {
                promised += player.Queue(category).Sum(item => ProductionRules.CostOf(GameDataFiles.Rules.Database, item.Kind, item.DefIndex) - item.Paid);
            }

            // The war factory makes the slowest part of the army: with both factories up and money for any unit and any building,
            // it should be at work. The barracks may wait for the tanks: the mix of the data holds.
            if (match.BuildingsOfBot("war_factory").Any(b => b.BuildUpLeft == 0) && match.BuildingsOfBot("barracks").Any(b => b.BuildUpLeft == 0)
                && player.Credits - promised >= 3000)
            {
                checks++;
                idle += player.Queue(ProductionCategory.Vehicle).Count == 0 ? 1 : 0;
            }
        }

        return new Run(match, checks, idle, targeted);
    }

    [Fact]
    public void The_war_factory_does_not_stand_idle_while_there_is_money_and_the_barracks_work_too()
    {
        var run = Once.Value;
        var infantry = run.Match.Timeline.Count(line => line.Text is "produced conscript" or "produced rocketeer");

        Assert.True(run.IdleWithMoney * 10 <= run.Checks, $"idle with money {run.IdleWithMoney} of {run.Checks} checks");
        Assert.True(infantry >= 3, $"{infantry} infantry made");
    }

    [Fact]
    public void The_army_is_of_the_units_the_bot_means_to_have_and_of_many_kinds()
    {
        // M11: the army of the data is one of roles, and the units of a role change as the technology grows — the shares at the end
        // are not those the army was built by. Every kind of the army was in the target of some minute; no kind is most of it.
        var run = Once.Value;
        var army = run.Match.UnitsOfBot(unit => unit.Archetype.Armed && unit.Harvester is null);
        var kinds = army.GroupBy(unit => unit.Archetype.Id).ToDictionary(group => group.Key, group => group.Count());
        var report = string.Join(", ", kinds.Select(kind => $"{kind.Key} {kind.Value}")) + "; targeted " + string.Join(", ", run.Targeted);

        Assert.True(army.Count >= 10, $"{army.Count} units");
        Assert.NotEmpty(Army.Roles);
        Assert.True(kinds.Count >= 4, report);
        Assert.All(kinds.Keys, kind => Assert.True(run.Targeted.Contains(kind), $"{kind} was never in the target; {report}"));
        Assert.All(kinds.Values, count => Assert.True(count * 100 <= army.Count * 60, report));
    }

    [Fact]
    public void The_base_still_gets_built_and_no_command_is_refused()
    {
        var match = Once.Value.Match;

        // Every step was built; with the ore gone the base may have sold what earns or only opens technologies since (step 14).
        foreach (var step in GameDataFiles.Rules.Database.BuildOrders.Single(order => order.Id == "standard").Steps.Distinct())
        {
            Assert.NotNull(match.When($"placed {step} "));
        }

        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void The_match_plays_the_same_twice_and_its_replay_with_the_bot_run_again()
    {
        var match = Once.Value.Match;
        var again = Play().Match;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()), GameDataFiles.Rules.MapTypes);

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }
}
