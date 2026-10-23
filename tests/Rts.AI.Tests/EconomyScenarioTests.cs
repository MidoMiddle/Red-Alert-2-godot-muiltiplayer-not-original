using Rts.Data.Tests;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The economy of the «Medium» bot in a match by the real data (docs/implementation/M5_first_bot.md, step 4): it deploys, builds a
/// reactor, a refinery, a war factory, keeps two harvesters a refinery and builds again what it loses — without a command that changes
/// nothing.
/// </summary>
[Trait("Category", "Slow")]
public sealed class EconomyScenarioTests
{
    private static readonly Lazy<BotMatch> TenMinutes = new(() => new BotMatch().PlayTo(10));

    [Fact]
    public void In_four_minutes_the_bot_has_a_yard_a_reactor_a_refinery_and_brings_ore_in()
    {
        var match = TenMinutes.Value;

        Assert.True(match.When("deployed") <= 60, match.Report());
        Assert.True(match.When("placed reactor") < match.When("placed refinery"), match.Report());
        Assert.True(match.When("unload") <= 4 * 900, match.Report());
    }

    [Fact]
    public void In_ten_minutes_it_has_a_war_factory_and_two_harvesters_a_refinery_without_a_refused_command()
    {
        var match = TenMinutes.Value;

        Assert.NotEmpty(match.BuildingsOfBot("war_factory"));
        // The build order gives two refineries (step 5).
        Assert.Equal(2, match.BuildingsOfBot("refinery").Count);
        Assert.Equal(4, match.UnitsOfBot(unit => unit.Harvester is not null).Count);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void The_match_plays_the_same_twice_and_its_replay_with_or_without_the_bot()
    {
        var match = TenMinutes.Value;
        var again = new BotMatch().PlayTo(10);
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, GameDataFiles.Rules).Status);
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }

    [Fact]
    public void A_lost_harvester_is_made_again()
    {
        var match = new BotMatch().PlayTo(8);
        var before = match.UnitsOfBot(unit => unit.Harvester is not null).Count;
        match.UnitsOfBot(unit => unit.Harvester is not null)[0].MarkDead();
        match.Simulation.Step();
        Assert.Equal(before - 1, match.UnitsOfBot(unit => unit.Harvester is not null).Count);

        // One tank queued at most ahead of it, then the harvester itself.
        match.PlayTo(11);

        Assert.Equal(before, match.UnitsOfBot(unit => unit.Harvester is not null).Count);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void A_lost_refinery_is_the_next_building_ordered()
    {
        var match = new BotMatch().PlayTo(6);
        var data = GameDataFiles.Rules.Database;
        // What is in the building queue as the refinery falls is finished first; defenses have a queue of their own.
        var inProduction = match.World.Player(BotMatch.Bot).Queue(ProductionCategory.Building).Select(item => data.Buildings[item.DefIndex].Id).ToList();
        var refineries = match.BuildingsOfBot("refinery");
        match.World.DestroyBuilding(refineries[0], null);
        DeathSystem.Update(match.World, null);

        match.PlayTo(10);

        Assert.Equal(refineries.Count, match.BuildingsOfBot("refinery").Count);
        var placed = match.Timeline.Where(line => line.Tick >= 6 * 900 && line.Text.StartsWith("placed", StringComparison.Ordinal))
            .Select(line => line.Text.Split(' ')[1])
            .Where(id => data.Buildings[data.IndexOfBuilding(id)].Category == ProductionCategory.Building)
            .ToList();
        foreach (var id in inProduction)
        {
            placed.Remove(id);
        }

        Assert.Equal("refinery", placed[0]);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Theory]
    [InlineData("twin-rivers")]
    [InlineData("taiga-front")]
    [InlineData("four-corners")]
    public void On_the_other_maps_the_bot_builds_its_economy_too(string map)
    {
        var match = new BotMatch(map).PlayTo(5);

        Assert.True(match.When("placed refinery") is not null && match.When("unload") is not null, match.Report());
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void On_the_slopes_of_the_relief_test_the_mcv_drives_to_flat_ground_and_deploys()
    {
        var match = new BotMatch("relief_test").PlayTo(2);

        Assert.NotNull(match.When("deployed"));
        Assert.Empty(match.RefusedBotCommands);
        Assert.DoesNotContain(match.Timeline, line => line.Text.StartsWith("cannot", StringComparison.Ordinal));
    }
}
