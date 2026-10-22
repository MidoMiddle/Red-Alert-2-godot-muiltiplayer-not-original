using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The damage formula (docs/implementation/M4_combat.md, 4.5): the share against the armor, the fall-off over the area, at least 1 for
/// the target, rounding down in a fixed order. Every expected number is worked out by hand in the comment next to it.
/// </summary>
public sealed class DamageCalculatorTests
{
    // As the parser of the data files reads "25%": rounded to the nearest raw value.
    private static Fixed Percent(int percent) => Fixed.FromRaw(IntMath.DivRound(percent * Fixed.OneRaw, 100));

    [Theory]
    [InlineData(90, 25, 22)]     // 22.5, rounded down
    [InlineData(90, 100, 90)]
    [InlineData(15, 80, 12)]     // 80 % reads as 52429 / 65536: 12.00004
    [InlineData(135, 85, 114)]   // 114.75
    [InlineData(25, 150, 37)]    // 37.5
    [InlineData(40, 0, 0)]
    [InlineData(0, 100, 0)]
    public void Against_armor_is_damage_times_the_share_rounded_down(int damage, int percent, int expected)
    {
        Assert.Equal(expected, DamageCalculator.AgainstArmor(damage, Percent(percent)));
    }

    [Fact]
    public void A_large_hit_does_not_overflow()
    {
        // 10 000 × 1000 % = 100 000.
        Assert.Equal(100_000, DamageCalculator.Direct(10_000, Percent(1000)));
    }

    [Fact]
    public void The_target_takes_at_least_1_while_its_armor_can_be_hurt()
    {
        // 1 × 25 % = 0.25, but a hit that can hurt always does.
        Assert.Equal(1, DamageCalculator.Direct(1, Percent(25)));
        Assert.Equal(0, DamageCalculator.Direct(1, Fixed.Zero));
        Assert.Equal(0, DamageCalculator.Direct(40, Fixed.Zero));
    }

    [Theory]
    [InlineData(0, 65536)]       // the centre: all of it
    [InlineData(256, 32768)]     // the edge of a 1-cell area at 50 %: half
    [InlineData(128, 49152)]     // halfway: 1 − 0.5 × 128 / 256 = 0.75
    [InlineData(257, 0)]         // beyond the edge: nothing
    public void The_fall_off_goes_straight_from_the_centre_to_the_edge(int distance, long raw)
    {
        Assert.Equal(raw, DamageCalculator.FallOff(Fixed.One, Percent(50), distance).Raw);
    }

    [Fact]
    public void An_area_of_no_size_hurts_only_the_target()
    {
        Assert.Equal(Fixed.Zero, DamageCalculator.FallOff(Fixed.Zero, Percent(50), 0));
        Assert.Equal(0, DamageCalculator.InArea(90, Fixed.One, Fixed.Zero, Percent(50), 0));
        Assert.Equal(90, DamageCalculator.Direct(90, Fixed.One));
    }

    [Fact]
    public void Others_in_the_area_take_the_fall_off_and_no_minimum()
    {
        // 90 × 100 % at the edge of a 1-cell area at 50 %: 45; a quarter of a cell in: 1 − 0.5 × 64 / 256 = 0.875, 78.75 → 78.
        Assert.Equal(45, DamageCalculator.InArea(90, Fixed.One, Fixed.One, Percent(50), 256));
        Assert.Equal(78, DamageCalculator.InArea(90, Fixed.One, Fixed.One, Percent(50), 64));
        // 1 × 25 % is 0 for a neighbour: no minimum outside the target.
        Assert.Equal(0, DamageCalculator.InArea(1, Percent(25), Fixed.One, Percent(50), 0));
        Assert.Equal(0, DamageCalculator.InArea(90, Fixed.One, Fixed.One, Percent(50), 300));
    }

    [Fact]
    public void The_armor_comes_before_the_fall_off_each_rounded_down()
    {
        // 15 × 80 % = 12, × 50 % = 6. The other way round it would be 15 × 50 % = 7, × 80 % = 5.
        Assert.Equal(6, DamageCalculator.InArea(15, Percent(80), Fixed.One, Percent(50), 256));
    }

    [Fact]
    public void A_fractional_spread_measures_in_leptons()
    {
        // 0.3 cells read as 19661 / 65536: 76 leptons. At 76 the edge share of 50 %: 90 × 50 % = 45; at 77 nothing.
        var spread = Fixed.FromRaw(IntMath.DivRound(3 * Fixed.OneRaw, 10));
        Assert.Equal(45, DamageCalculator.InArea(90, Fixed.One, spread, Percent(50), 76));
        Assert.Equal(0, DamageCalculator.InArea(90, Fixed.One, spread, Percent(50), 77));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 300, 400, 500)]
    [InlineData(10, 10, 11, 11, 1)]      // √2 = 1.41, rounded down
    [InlineData(-256, 0, 256, 0, 512)]
    public void The_distance_is_straight_in_leptons_rounded_down(int ax, int ay, int bx, int by, int expected)
    {
        Assert.Equal(expected, DamageCalculator.Distance(new WorldPos(ax, ay), new WorldPos(bx, by)));
    }

    [Fact]
    public void The_rules_lay_out_the_verses_by_armor_number()
    {
        var rules = TestRules.Standard;
        var database = rules.Database;
        foreach (var (warhead, w) in database.Warheads.Select((warhead, w) => (warhead, w)))
        {
            foreach (var verse in warhead.Verses)
            {
                Assert.Equal(verse.Percent, rules.Verses(w, database.IndexOfArmor(verse.Armor)));
            }
        }

        // The AP shell against heavy armor and against infantry, as warheads.yaml gives them.
        var ap = database.IndexOfWarhead("ap_shell");
        Assert.Equal(Fixed.One, rules.Verses(ap, database.IndexOfArmor("heavy")));
        Assert.Equal(Percent(25), rules.Verses(ap, database.IndexOfArmor("none")));
        Assert.Equal(database.IndexOfArmor("concrete"), rules.ArmorOfBuilding(database.IndexOfBuilding("construction_yard")));
    }
}
