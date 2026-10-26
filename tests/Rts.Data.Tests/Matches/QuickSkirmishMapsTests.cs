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
    public void The_playable_built_in_maps_are_the_ones_listed()
    {
        Assert.Equal(
        [
            "ambush-valley", "arctic-strip", "bay-city", "blizzard-basin", "city-blocks", "crate-glacier", "dense-taiga",
            "downtown-duel", "dune-sea", "facing-corners", "four-corners", "four-farms", "four-isles", "four-ports", "frost-arena", "frost-gate",
            "frozen-crossing", "frozen-outpost", "frozen-plateau", "glacier-duel", "green-valley", "harbor-district", "heaven",
            "ice-city", "ice-ridge", "lagoon-isle", "mesa-pass", "oasis-crossing", "palm-shore", "pine-haven", "polar-circle",
            "relief_test", "resort-bay", "river-meadows", "snowbound-hamlet", "snowfall-forest", "steel-tundra", "stephs-high-ground", "taiga-front",
            "tundra-lane", "twin-isles", "twin-rivers", "war-pro", "windswept-pass",
        ], Maps().Cast<object[]>().Select(row => (string)row[0]));
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

    [Theory]
    [MemberData(nameof(Maps))]
    public void Every_ready_map_but_the_relief_test_has_an_ore_drill_by_every_start_and_a_gem_drill_for_each(string id)
    {
        // M8 step 10b: the fields grow back. The relief test of M7 is a test of slopes, without them.
        var map = Read(id);
        var ore = map.Objects.Where(o => o.Type == "ore_source").ToList();
        var gems = map.Objects.Where(o => o.Type == "gem_source").ToList();
        if (id == "relief_test")
        {
            Assert.Empty(ore.Concat(gems));
            return;
        }

        Assert.Equal((map.Starts.Length, map.Starts.Length), (ore.Count, gems.Count));
        // By the edge of the flat ground of the base (radius 12), not in it.
        Assert.All(map.Starts, start => Assert.Contains(ore, drill => Distance2(drill.Cell, start.Cell) <= 16 * 16));
        Assert.All(ore.Concat(gems), drill => Assert.Equal(0, map.Ore[drill.Cell.Y * map.Width + drill.Cell.X]));
        // Heaven, Facing Corners, Steph's High Ground and WAR Pro come from RA2 (tools/ra2_import) with their authors' fields further from the bases: their ore drills
        // stand within 16 cells but not always within the 12 the editor's check asks for, so only that warning is let pass.
        var problems = MapValidator.Check(map).Where(problem => id is not ("heaven" or "facing-corners" or "stephs-high-ground" or "war-pro") || !problem.Message.StartsWith("The starts have", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, problem => problem.Message.Contains("drill") || problem.Message.Contains("under the"));
    }

    [Fact]
    public void In_a_match_the_gem_drills_of_frozen_crossing_add_gems_within_a_minute()
    {
        // Gems neither grow nor spread: only the drills add them.
        var map = Read("frozen-crossing");
        var setup = MatchSetups.OneOnOne(map, GameDataFiles.Rules.DefaultFaction.Id, seed: 3);
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, map.ToMapGrid()), GameDataFiles.Rules);
        var before = Gems(simulation.World.Map);
        for (var tick = 0; tick < 900; tick++)
        {
            simulation.Step();
        }

        Assert.True(Gems(simulation.World.Map) > before, $"gems {before} → {Gems(simulation.World.Map)}");
    }

    private static int Gems(Rts.Simulation.Map.MapGrid grid)
    {
        var sum = 0;
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var ore = grid.OreAt(new Rts.Simulation.Geometry.CellPos(x, y));
                sum += ore.Kind == Rts.Simulation.Map.OreKind.Gems ? ore.Stage + 1 : 0;
            }
        }

        return sum;
    }

    private static int Distance2(Rts.Simulation.Geometry.CellPos a, Rts.Simulation.Geometry.CellPos b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    [Fact]
    public void One_against_one_puts_the_person_on_the_first_start_and_the_bot_on_the_second()
    {
        var map = Read("taiga-front");
        var setup = MatchSetups.OneOnOne(map, "union", seed: 9);

        Assert.Equal("taiga-front", setup.MapId);
        Assert.False(setup.Options.FogOfWar);
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
