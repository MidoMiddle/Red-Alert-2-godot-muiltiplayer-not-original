using Rts.Client.Matches;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests.Matches;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The game speed of RA2 (docs/implementation/M6_menus_skirmish.md, 4.4, step 8): 8 to 25 ticks a second, speed 4 the simulation's own
/// rate; the keys step along the scale and stop at its ends; and the match itself does not depend on it.
/// </summary>
public sealed class GameSpeedsTests
{
    [Fact]
    public void Every_speed_is_its_ticks_a_second_and_4_is_the_rate_of_the_simulation()
    {
        Assert.Equal([8, 10, 12, 15, 20, 25], Enumerable.Range(1, 6).Select(GameSpeeds.TicksOf));
        Assert.Equal(1.0, GameSpeeds.Multiplier(GameSpeeds.Normal, SimulationSettings.DefaultTicksPerSecond));
        Assert.Equal(25.0 / 15, GameSpeeds.Multiplier(6, 15));
        Assert.Equal((GameSpeeds.Slowest, GameSpeeds.Fastest), (MatchSetup.MinGameSpeed, MatchSetup.MaxGameSpeed));
    }

    [Fact]
    public void The_keys_step_along_the_scale_and_stop_at_its_ends()
    {
        Assert.Equal(5, GameSpeeds.Step(4, +1));
        Assert.Equal(3, GameSpeeds.Step(4, -1));
        Assert.Equal(6, GameSpeeds.Step(6, +1));
        Assert.Equal(1, GameSpeeds.Step(1, -1));
        Assert.Equal(8, GameSpeeds.TicksOf(0));
    }

    [Fact]
    public void The_speed_of_a_match_does_not_change_its_ticks()
    {
        // The speed is the client's clock only: matches of speed 1 and 6 computed tick by tick are the same match.
        var slow = MatchSetupTests.OneOnOne() with { Options = new MatchOptions(GameSpeed: 1) };
        var fast = slow with { Options = new MatchOptions(GameSpeed: 6) };
        var a = new GameSimulation(SimulationSettings.ForMatch(slow, new MapGrid(64, 64)), TestRules.Standard);
        var b = new GameSimulation(SimulationSettings.ForMatch(fast, new MapGrid(64, 64)), TestRules.Standard);
        for (var tick = 0; tick < 150; tick++)
        {
            a.Step();
            b.Step();
        }

        Assert.Equal(a.LastChecksum, b.LastChecksum);
    }
}
