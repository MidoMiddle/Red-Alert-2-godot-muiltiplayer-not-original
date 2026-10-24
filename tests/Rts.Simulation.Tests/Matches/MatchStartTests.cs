using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>The starting state of a match (docs/implementation/M5_first_bot.md, 4.11, decision 1): an MCV and three conscripts at every start.</summary>
public sealed class MatchStartTests
{
    private static GameSimulation Start(MatchSetup setup, MapGrid? map = null) =>
        new(SimulationSettings.ForMatch(setup, map ?? new MapGrid(64, 64)), TestRules.Standard);

    private static List<Unit> UnitsOf(GameWorld world, PlayerId player) =>
        world.Entities.OfType<Unit>().Where(unit => unit.Owner == player).ToList();

    [Fact]
    public void Every_player_gets_an_mcv_on_its_start_and_three_conscripts_nearby()
    {
        var simulation = Start(MatchSetupTests.OneOnOne(credits: 5000));
        var world = simulation.World;

        foreach (var player in simulation.Settings.Match!.Players)
        {
            var units = UnitsOf(world, player.Id);
            var mcv = Assert.Single(units, unit => unit.Type == UnitType.Mcv);
            Assert.Equal(player.Start.Center, mcv.Position);
            var conscripts = units.Where(unit => unit.Type == UnitType.Conscript).ToList();
            Assert.Equal(MatchStart.StartingInfantry, conscripts.Count);
            Assert.Equal(4, units.Count);
            // Outside the 3×3 yard the MCV deploys to, within a few cells of it.
            Assert.All(conscripts, conscript =>
            {
                var cell = conscript.Position.ToCell();
                var distance = System.Math.Max(System.Math.Abs(cell.X - player.Start.X), System.Math.Abs(cell.Y - player.Start.Y));
                Assert.InRange(distance, 2, 4);
            });
            Assert.Equal(5000, world.Player(player.Id).Credits);
        }

        Assert.Empty(UnitsOf(world, new PlayerId(3)));
    }

    [Fact]
    public void The_conscripts_go_around_what_infantry_cannot_enter()
    {
        var map = new MapGrid(64, 64);
        // Rock on the whole first ring outside the yard to the left and above: the conscripts stand where they can.
        for (var i = -2; i <= 2; i++)
        {
            map.SetLand(new CellPos(10 + i, 8), LandType.Rock);
            map.SetLand(new CellPos(8, 10 + i), LandType.Rock);
        }

        var world = Start(MatchSetupTests.OneOnOne(), map).World;

        var conscripts = UnitsOf(world, new PlayerId(1)).Where(unit => unit.Type == UnitType.Conscript).ToList();
        Assert.Equal(MatchStart.StartingInfantry, conscripts.Count);
        Assert.All(conscripts, conscript => Assert.True(world.Map.CanEnter(conscript.Position.ToCell(), Locomotor.Foot)));
    }

    [Fact]
    public void The_mcv_of_the_start_deploys_where_it_stands()
    {
        var simulation = Start(MatchSetupTests.OneOnOne());
        var mcv = UnitsOf(simulation.World, new PlayerId(1)).Single(unit => unit.Type == UnitType.Mcv);

        simulation.Enqueue(new DeployCommand(new PlayerId(1), 0, 0, mcv.Id));
        simulation.Step();

        Assert.Contains(simulation.LastTickEvents, e => e is Deployed);
        Assert.DoesNotContain(simulation.LastTickEvents, e => e is CannotDeploy);
    }

    [Fact]
    public void The_same_setup_makes_the_same_world_and_another_seed_another_one()
    {
        var first = StateHasher.Compute(Start(MatchSetupTests.OneOnOne()).World);
        var second = StateHasher.Compute(Start(MatchSetupTests.OneOnOne()).World);
        var otherSeed = StateHasher.Compute(Start(MatchSetupTests.OneOnOne(seed: 8)).World);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherSeed);
    }
}
