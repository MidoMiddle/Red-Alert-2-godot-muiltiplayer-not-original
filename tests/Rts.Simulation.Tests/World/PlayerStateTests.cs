using Rts.Simulation.Hashing;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>The economy of the players (docs/implementation/M3_base_economy.md, 4.7): credits from the match settings, the faction.</summary>
public sealed class PlayerStateTests
{
    [Fact]
    public void Every_player_starts_with_the_credits_of_the_settings_and_the_union()
    {
        var simulation = new GameSimulation(new SimulationSettings(1), TestRules.Standard);

        Assert.Equal(PlayerId.MaxPlayers, simulation.World.Players.Count);
        Assert.All(simulation.World.Players, player =>
        {
            Assert.Equal(10_000, player.Credits);
            Assert.Equal("union", player.Faction.Id);
        });
        Assert.Equal(Enumerable.Range(1, 8), simulation.World.Players.Select(player => player.Id.Value));
    }

    [Fact]
    public void The_settings_choose_the_starting_credits()
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { StartingCredits = 2500 }, TestRules.Standard);

        Assert.Equal(2500, simulation.World.Player(new PlayerId(3)).Credits);
    }

    [Fact]
    public void The_credits_are_part_of_the_state_hash()
    {
        var rich = new GameSimulation(new SimulationSettings(1), TestRules.Standard);
        var poor = new GameSimulation(new SimulationSettings(1) { StartingCredits = 9999 }, TestRules.Standard);

        Assert.NotEqual(StateHasher.Compute(rich.World), StateHasher.Compute(poor.World));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Only_the_players_one_to_eight_have_an_economy(int player)
    {
        var world = new GameWorld(seed: 1, widthCells: 16, heightCells: 16, TestRules.Standard);

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Player(new PlayerId(player)));
    }

    [Fact]
    public void Negative_starting_credits_are_not_valid_settings()
    {
        Assert.Throws<ArgumentException>(() => new GameSimulation(new SimulationSettings(1) { StartingCredits = -1 }, TestRules.Standard));
    }
}
