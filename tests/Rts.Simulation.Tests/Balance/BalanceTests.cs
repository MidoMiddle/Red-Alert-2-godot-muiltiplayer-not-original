using Rts.MapTool;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Balance;

/// <summary>
/// Balance v0 (docs/implementation/M4_combat.md, step 14): the fights of the plan, on the rules of the tests — their combat is that of
/// game/data, a test of the data makes sure. The counters on equal credits need the real costs: Rts.Data.Tests, <c>CounterTests</c>.
/// </summary>
public sealed class BalanceTests
{
    private static readonly Rules.GameRules Rules = TestRules.Standard;

    private static SkirmishResult Fight(UnitType first, int firstCount, UnitType second, int secondCount) =>
        Skirmish.Fight(Rules, Skirmish.Repeat(first, firstCount), Skirmish.Repeat(second, secondCount));

    private static string Describe(SkirmishResult result) =>
        $"winner {result.Winner} after {result.Ticks} ticks; left {result.UnitsLeft[0]} / {result.UnitsLeft[1]}, value {result.PercentLeft(1)}% / {result.PercentLeft(2)}%";

    [Fact]
    public void Five_rhinos_against_five_end_with_a_winner_the_same_way_twice()
    {
        var first = Fight(UnitType.HeavyTank, 5, UnitType.HeavyTank, 5);
        var second = Fight(UnitType.HeavyTank, 5, UnitType.HeavyTank, 5);

        Assert.True(first.Winner != 0, Describe(first));
        Assert.True(first.Ticks < Skirmish.MaxTicks, Describe(first));
        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void A_rhino_beats_ten_conscripts_by_crushing_and_its_cannon()
    {
        var crushed = 0;
        var result = Skirmish.Fight(
            Rules, Skirmish.Repeat(UnitType.Conscript, 10), Skirmish.Repeat(UnitType.HeavyTank, 1),
            afterTick: simulation => crushed += simulation.LastTickEvents.Count(e => e is EntityDestroyed { Cause: DeathCause.Crushed }));

        Assert.True(result.Winner == 2, Describe(result));
        Assert.True(crushed > 0, $"no conscript was crushed; {Describe(result)}");
    }

    [Fact]
    public void Three_rocket_troopers_beat_a_rhino()
    {
        var result = Fight(UnitType.Rocketeer, 3, UnitType.HeavyTank, 1);

        Assert.True(result.Winner == 1, Describe(result));
    }

    [Fact]
    public void A_powered_tesla_coil_hurts_four_rhinos_and_one_without_power_does_not()
    {
        SkirmishResult Against(bool powered) => Skirmish.Fight(
            Rules, Skirmish.Repeat(UnitType.HeavyTank, 4), [], setup: simulation =>
            {
                simulation.World.CreateBuilding(new PlayerId(2), TestRules.BuildingDef("tesla_coil"), new CellPos(Skirmish.SecondColumn, Skirmish.Height / 2 - 1), true, null);
                if (powered)
                {
                    simulation.World.CreateBuilding(new PlayerId(2), TestRules.BuildingDef("reactor"), new CellPos(Skirmish.Width - 4, 2), true, null);
                }
            });

        var withPower = Against(powered: true);
        var withoutPower = Against(powered: false);

        Assert.True(withPower.UnitsLeft[0] < 4 || withPower.ValueStart[0] - withPower.ValueLeft[0] >= 900, Describe(withPower));
        Assert.True(withoutPower.ValueLeft[0] == withoutPower.ValueStart[0], Describe(withoutPower));
        Assert.Equal(1, withoutPower.Winner);
    }

    [Fact]
    public void A_sentry_bunker_beats_a_small_raid_of_conscripts()
    {
        var result = Skirmish.Fight(
            Rules, Skirmish.Repeat(UnitType.Conscript, 3), [], setup: simulation =>
                simulation.World.CreateBuilding(new PlayerId(2), TestRules.BuildingDef("sentry_bunker"), new CellPos(Skirmish.SecondColumn, Skirmish.Height / 2), true, null));

        Assert.True(result.Winner == 2, Describe(result));
    }
}
