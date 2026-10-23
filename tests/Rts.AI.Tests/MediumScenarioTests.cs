using Rts.AI.Squads;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// «Medium» as tuned in the data (docs/implementation/M5_first_bot.md, sections 6–7, step 14) on the ready maps against a person who does
/// nothing: when its first wave sets out, that it beats a person with a base and a tesla coil, that it rebuilds a lost refinery, and
/// that with no money and no ore it does nothing wrong.
/// </summary>
public sealed class MediumScenarioTests
{
    /// <summary>The tick the first squad of the bot sets out, or -1 before <paramref name="minutes"/> are over.</summary>
    private static int FirstWave(BotMatch match, int minutes)
    {
        while (match.Simulation.CurrentTick < minutes * 900)
        {
            match.Step();
            if (match.Brain!.Context?.Plan.Squads.Any(squad => squad.State == SquadState.Moving) == true)
            {
                return match.Simulation.CurrentTick;
            }
        }

        return -1;
    }

    [Theory]
    [InlineData("frozen-crossing", 8.5)]
    [InlineData("twin-rivers", 8.5)]
    [InlineData("four-corners", 8.5)]
    // The biggest map: its starts lie farther apart, its fields too.
    [InlineData("taiga-front", 12)]
    public void The_first_wave_sets_out_around_the_eighth_minute(string map, double minutes)
    {
        var match = new BotMatch(map);

        var tick = FirstWave(match, 15);

        Assert.InRange(tick, 6 * 900, (int)(minutes * 900));
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void It_beats_a_passive_person_with_a_base_and_a_tesla_coil_before_the_25th_minute()
    {
        var match = new BotMatch("four-corners");
        match.GivePersonABase();
        while (match.World.Result is null && match.Simulation.CurrentTick < 25 * 900)
        {
            match.Step();
        }

        Assert.NotNull(match.World.Result);
        Assert.Equal([BotMatch.Bot], match.World.Result.Winners.AsEnumerable());
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void A_lost_refinery_is_built_again()
    {
        var match = new BotMatch("four-corners").PlayTo(3);
        var refinery = match.BuildingsOfBot("refinery").Single();
        match.World.DestroyBuilding(refinery, null);
        match.Simulation.Step();
        Assert.Empty(match.BuildingsOfBot("refinery"));

        match.PlayTo(6);

        Assert.NotEmpty(match.BuildingsOfBot("refinery"));
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void With_no_money_and_no_ore_it_gives_no_wrong_command_in_ten_minutes()
    {
        var match = new BotMatch(credits: 0, noOre: true).PlayTo(10);

        Assert.Empty(match.RefusedBotCommands);
        // It still deploys its MCV — that is free — and builds nothing more.
        Assert.NotNull(match.When("deployed"));
        Assert.Null(match.When("produced "));
    }
}
