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

    private sealed record Run(BotMatch Match, int Checks, int IdleWithMoney);

    private static readonly Lazy<Run> Once = new(Play);

    /// <summary>The match, and how often the war factory stood idle while there was money to spare (every 15 ticks).</summary>
    private static Run Play()
    {
        var match = new BotMatch();
        var (checks, idle) = (0, 0);
        while (match.Simulation.CurrentTick < Minutes * 900)
        {
            match.Step();
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

        return new Run(match, checks, idle);
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
    public void The_army_keeps_near_the_shares_of_the_data()
    {
        var match = Once.Value.Match;
        var army = match.UnitsOfBot(unit => unit.Archetype.Armed && unit.Harvester is null);
        var techCenter = match.BuildingsOfBot("tech_center").Any();

        Assert.True(army.Count >= 10, $"{army.Count} units");
        foreach (var share in Army.Shares)
        {
            if (share.Unit == "tesla_tank" && !techCenter)
            {
                continue;
            }

            var percent = army.Count(unit => unit.Archetype.Id == share.Unit) * 100 / army.Count;
            var target = (int)((share.Share * Rts.Simulation.Math.Fixed.FromInt(100)).Raw >> 16);
            Assert.True(Math.Abs(percent - target) <= 15, $"{share.Unit}: {percent}% against {target}%; " +
                string.Join(", ", army.GroupBy(unit => unit.Archetype.Id).Select(group => $"{group.Key} {group.Count()}")));
        }
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
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }
}
