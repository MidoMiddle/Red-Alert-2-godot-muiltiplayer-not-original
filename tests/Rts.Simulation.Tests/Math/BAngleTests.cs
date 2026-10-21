using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Math;

public sealed class BAngleTests
{
    private static BAngle Deg(long degrees) => BAngle.FromDegrees(degrees);

    [Fact]
    public void Cardinal_directions_split_the_turn_in_quarters()
    {
        Assert.Equal(0, BAngle.North.Value);
        Assert.Equal(16384, BAngle.East.Value);
        Assert.Equal(32768, BAngle.South.Value);
        Assert.Equal(49152, BAngle.West.Value);
        Assert.Equal(BAngle.East, Deg(90));
        Assert.Equal(BAngle.West, Deg(-90));
    }

    [Fact]
    public void Arithmetic_wraps_around_north()
    {
        // Multiples of 45° are whole numbers of units, so these sums are exact.
        Assert.Equal(Deg(45), Deg(315) + Deg(90));
        Assert.Equal(Deg(315), Deg(45) - Deg(90));
        Assert.Equal(BAngle.North, BAngle.West + BAngle.East);
        Assert.Equal(65535, BAngle.FromUnits(-1).Value);
        Assert.Equal(5, BAngle.FromUnits(65541).Value);
    }

    [Fact]
    public void Difference_is_the_shortest_signed_turn()
    {
        // From 350° to 10° is +20° (3640 units after rounding both ends), from 10° to 350° is −20°.
        Assert.Equal(3640, BAngle.Difference(Deg(350), Deg(10)));
        Assert.Equal(-3640, BAngle.Difference(Deg(10), Deg(350)));
        Assert.Equal(200, BAngle.Difference(BAngle.FromUnits(-100), BAngle.FromUnits(100)));
        Assert.Equal(16384, BAngle.Difference(BAngle.North, BAngle.East));
        Assert.Equal(-16384, BAngle.Difference(BAngle.North, BAngle.West));
        Assert.Equal(0, BAngle.Difference(Deg(123), Deg(123)));
        // Exactly half a turn has no shorter side; the value is fixed so turning is deterministic.
        Assert.Equal(short.MinValue, BAngle.Difference(BAngle.North, BAngle.South));
    }

    [Fact]
    public void Difference_inverts_addition_for_every_offset()
    {
        var random = new TestRandom(7);
        for (var i = 0; i < 10_000; i++)
        {
            var start = BAngle.FromUnits(random.NextInt(65536));
            var offset = random.NextInt(65536) - 32768;

            Assert.Equal(offset, BAngle.Difference(start, BAngle.FromUnits(start.Value + offset)));
        }
    }

    [Fact]
    public void Rotate_towards_takes_the_short_way_and_stops_on_target()
    {
        Assert.Equal(BAngle.FromUnits(Deg(350).Value + 100), BAngle.RotateTowards(Deg(350), Deg(10), 100));
        Assert.Equal(BAngle.FromUnits(Deg(10).Value - 100), BAngle.RotateTowards(Deg(10), Deg(350), 100));
        Assert.Equal(Deg(10), BAngle.RotateTowards(Deg(350), Deg(10), 5000));
        Assert.Equal(Deg(45), BAngle.RotateTowards(Deg(45), Deg(45), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BAngle.RotateTowards(Deg(0), Deg(1), -1));
    }

    [Fact]
    public void Rotating_step_by_step_reaches_any_target()
    {
        var random = new TestRandom(11);
        for (var i = 0; i < 1000; i++)
        {
            var current = BAngle.FromUnits(random.NextInt(65536));
            var target = BAngle.FromUnits(random.NextInt(65536));
            const int Step = 1000;

            var steps = 0;
            while (current != target)
            {
                var next = BAngle.RotateTowards(current, target, Step);
                Assert.True(IntMath.Abs(BAngle.Difference(current, next)) <= Step);
                current = next;
                Assert.True(++steps <= 33, "Half a turn takes at most 33 steps of 1000 units.");
            }
        }
    }

    [Fact]
    public void Text_shows_units_and_degrees()
    {
        Assert.Equal("16384 (90°)", BAngle.East.ToString());
    }
}
