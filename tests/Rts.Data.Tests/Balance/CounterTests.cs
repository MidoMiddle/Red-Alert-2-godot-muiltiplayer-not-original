using Rts.MapTool;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Balance;

/// <summary>
/// The counters of docs/05_factions_content.md, section 3, on the real game/data (M4 step 14): on equal credits, each side as many
/// units as its money buys, the counter wins whichever side it starts on.
/// </summary>
public sealed class CounterTests
{
    private const int Credits = 1800;

    private static SkirmishResult EqualMoney(UnitType first, UnitType second)
    {
        var rules = GameDataFiles.Rules;
        return Skirmish.Fight(
            rules, Skirmish.Repeat(first, Skirmish.CountFor(rules, first, Credits)), Skirmish.Repeat(second, Skirmish.CountFor(rules, second, Credits)));
    }

    private static string Describe(SkirmishResult result) =>
        $"winner {result.Winner} after {result.Ticks} ticks; left {result.UnitsLeft[0]} / {result.UnitsLeft[1]}, value {result.PercentLeft(1)}% / {result.PercentLeft(2)}%";

    [Theory]
    [InlineData(UnitType.Conscript, UnitType.Rocketeer)]           // basic infantry beats anti-tank infantry
    [InlineData(UnitType.Rocketeer, UnitType.HeavyTank)]           // anti-tank infantry beats tanks
    [InlineData(UnitType.HeavyTank, UnitType.Conscript)]           // tanks beat basic infantry
    [InlineData(UnitType.HeavyTank, UnitType.SupportVehicle)]      // the main tank beats the light vehicle
    [InlineData(UnitType.SupportVehicle, UnitType.Conscript)]      // flak beats basic infantry
    public void The_counter_wins_whichever_side_it_starts_on(UnitType counter, UnitType countered)
    {
        var asFirst = EqualMoney(counter, countered);
        var asSecond = EqualMoney(countered, counter);

        Assert.True(asFirst.Winner == 1, Describe(asFirst));
        Assert.True(asSecond.Winner == 2, Describe(asSecond));
    }

    [Fact]
    public void Equal_credits_buy_round_numbers_of_units()
    {
        var rules = GameDataFiles.Rules;

        Assert.Equal((18, 6, 3, 2, 1), (
            Skirmish.CountFor(rules, UnitType.Conscript, Credits), Skirmish.CountFor(rules, UnitType.Rocketeer, Credits),
            Skirmish.CountFor(rules, UnitType.SupportVehicle, Credits), Skirmish.CountFor(rules, UnitType.HeavyTank, Credits),
            Skirmish.CountFor(rules, UnitType.TeslaTank, Credits)));
    }
}
