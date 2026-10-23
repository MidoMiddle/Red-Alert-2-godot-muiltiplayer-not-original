using Rts.Client.Matches;
using Rts.Data.Maps;
using Rts.Data.Tests.Maps;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// One against a bot on every ready map of the game (docs/implementation/M5_first_bot.md, step 2): the quick skirmish, the map editor's
/// «Play» and <c>--map</c> set these matches up (<see cref="MatchSetups.OneOnOne"/>), by the real data.
/// </summary>
public sealed class QuickSkirmishMapsTests
{
    public static TheoryData<string> Maps()
    {
        var maps = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps"), "*.rtsmap").Order(StringComparer.Ordinal))
        {
            if (MatchSetups.IsPlayable(RtsMapReader.Read(File.ReadAllText(path))))
            {
                maps.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        return maps;
    }

    private static MapDefinition Read(string id) =>
        RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", id + ".rtsmap")));

    [Fact]
    public void The_ready_maps_with_starts_are_the_four_of_m7_and_the_relief_test()
    {
        Assert.Equal(["four-corners", "frozen-crossing", "relief_test", "taiga-front", "twin-rivers"], Maps().Cast<object[]>().Select(row => (string)row[0]));
    }

    [Theory]
    [MemberData(nameof(Maps))]
    public void On_every_ready_map_both_sides_start_with_an_mcv_and_three_conscripts_and_deploy_where_it_is_flat(string id)
    {
        var map = Read(id);
        var setup = MatchSetups.OneOnOne(map, GameDataFiles.Rules.DefaultFaction.Id, seed: 5);
        var grid = map.ToMapGrid();
        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, grid), GameDataFiles.Rules);

        uint sequence = 0;
        foreach (var player in setup.Players)
        {
            var units = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == player.Id).ToList();
            Assert.Equal([UnitType.Mcv, UnitType.Conscript, UnitType.Conscript, UnitType.Conscript], units.Select(unit => unit.Type));
            Assert.Equal(map.Starts[player.Slot - 1].Cell.Center, units[0].Position);
            simulation.Enqueue(new DeployCommand(player.Id, 0, sequence++, units[0].Id));
        }

        simulation.Step();

        // The relief test of M7 puts its starts on slopes: there the MCV has to drive to flat ground first.
        var deployed = simulation.LastTickEvents.OfType<Deployed>().Count();
        Assert.True(deployed == (id == "relief_test" ? 0 : 2), string.Join("; ", simulation.LastTickEvents));
        Assert.All(simulation.LastTickEvents.OfType<CannotDeploy>(), refusal => Assert.Equal(PlacementProblem.NotFlat, refusal.Problem));
    }

    [Fact]
    public void One_against_one_puts_the_person_on_the_first_start_and_the_bot_on_the_second()
    {
        var map = Read("taiga-front");
        var setup = MatchSetups.OneOnOne(map, "union", seed: 9);

        Assert.Equal("taiga-front", setup.MapId);
        Assert.Equal(9UL, setup.Seed);
        Assert.Collection(
            setup.Players,
            person => Assert.Equal((1, PlayerKind.Human, map.Starts[0].Cell, 0), (person.Slot, person.Kind, person.Start, person.Color)),
            bot => Assert.Equal((2, PlayerKind.Ai, map.Starts[1].Cell, 1), (bot.Slot, bot.Kind, bot.Start, bot.Color)));
    }

    [Fact]
    public void Two_against_two_on_four_corners_puts_the_person_and_a_bot_ally_on_the_top_corners()
    {
        // M6, A6: the quick skirmish's «Mode: 2 vs 2», the setup the fog of the whole team is seen in.
        var map = Read("four-corners");
        var setup = MatchSetups.TwoOnTwo(map, GameDataFiles.Rules.DefaultFaction.Id, seed: 9);
        var grid = map.ToMapGrid();

        Assert.True(MatchSetups.IsPlayableTwoOnTwo(map));
        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        Assert.Equal(
            [(1, PlayerKind.Human, 1, 0), (2, PlayerKind.Ai, 1, 1), (3, PlayerKind.Ai, 2, 2), (4, PlayerKind.Ai, 2, 3)],
            setup.Players.Select(player => (player.Slot, player.Kind, player.Team, player.Color)));
        Assert.Equal(map.Starts.Take(4).Select(start => start.Cell), setup.Players.Select(player => player.Start));

        var world = new GameSimulation(SimulationSettings.ForMatch(setup, grid), GameDataFiles.Rules).World;
        Assert.Equal(0b0011, world.AlliesOf(new PlayerId(1)));
        Assert.Equal(0b1100, world.AlliesOf(new PlayerId(3)));
        Assert.All(setup.Players, player => Assert.Equal(4, world.Entities.OfType<Unit>().Count(unit => unit.Owner == player.Id)));
    }

    [Fact]
    public void A_map_of_two_starts_is_no_two_against_two()
    {
        var map = Read("twin-rivers");

        Assert.False(MatchSetups.IsPlayableTwoOnTwo(map));
        Assert.Throws<ArgumentException>(() => MatchSetups.TwoOnTwo(map, "union", 1));
    }

    [Fact]
    public void A_map_without_two_starts_is_not_a_match()
    {
        var sandbox = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        Assert.False(MatchSetups.IsPlayable(sandbox));
        Assert.Throws<ArgumentException>(() => MatchSetups.OneOnOne(sandbox, "union", 1));
    }
}
