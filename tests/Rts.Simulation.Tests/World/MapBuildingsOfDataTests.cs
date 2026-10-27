using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// A building of a faction's data on the map, for nobody (2026-10-27): RA2's maps place the Neutral house's defenses and tech — tesla
/// coils, Patriots, nuclear reactors, construction yards. The map writes its id and footprint; the match makes it from its rules.
/// </summary>
public sealed class MapBuildingsOfDataTests
{
    private const int Width = 16;
    private const int Height = 10;
    private static readonly CellPos Coil = new(5, 4);

    private static MapGrid Map(params BuildingState[] buildings) =>
        MapGrid.FromLayers(Width, Height, Enumerable.Repeat((byte)LandType.Clear, Width * Height).ToArray(), new byte[Width * Height], default,
            null, null, buildings, types: TestRules.Standard.MapTypes);

    private static BuildingState Of(string id, int width, int height, CellPos cell) => BuildingState.OfData(id, new CellSize(width, height), cell, 0);

    [Fact]
    public void A_tesla_coil_of_the_map_stands_ready_for_nobody_on_the_cells_the_map_took()
    {
        var world = new GameWorld(1, Map(Of("tesla_coil", 1, 1, Coil)), TestRules.Standard);

        var coil = Assert.Single(world.Entities.OfType<Building>());
        Assert.Equal(("tesla_coil", PlayerId.Neutral, Coil, 0), (coil.Def!.Id, coil.Owner, coil.Cell, coil.BuildUpLeft));
        Assert.True(coil.IsReady);
        Assert.True(world.Map.HasBuilding(Coil));
    }

    [Fact]
    public void The_test_map_names_it_as_it_names_a_building_of_the_map()
    {
        var map = TestMapParser.Parse(string.Concat(Enumerable.Repeat(". . . . . . .\n", 6)) + "building tesla_coil 5 1\nbuilding construction_yard 0 0", TestRules.Standard);

        Assert.Equal(["tesla_coil", "construction_yard"], map.Grid.StartBuildings.Select(building => building.Def));
        Assert.Equal((4, 4), (map.Grid.StartBuildings[1].Width, map.Grid.StartBuildings[1].Height));
    }

    [Fact]
    public void Its_id_and_footprint_are_in_the_digest_and_a_map_without_one_keeps_its_digest()
    {
        var plain = Map().Digest;
        var coil = Map(Of("tesla_coil", 1, 1, Coil)).Digest;

        Assert.NotEqual(plain, coil);
        Assert.NotEqual(coil, Map(Of("flak_cannon", 1, 1, Coil)).Digest);
        Assert.NotEqual(coil, Map(Of("tesla_coil", 1, 2, Coil)).Digest);
        Assert.Equal(coil, Map(Of("tesla_coil", 1, 1, Coil)).Digest);
    }

    [Fact]
    public void A_replay_keeps_it()
    {
        var settings = new SimulationSettings(7, Map(Of("tesla_coil", 1, 1, Coil), Of("construction_yard", 4, 4, new CellPos(10, 2))));
        var simulation = new GameSimulation(settings, TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        for (var tick = 0; tick < 3; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var read = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);

        Assert.Equal(settings.Map!.StartBuildings, read.Settings.Map!.StartBuildings);
        Assert.Equal(settings.Map.Digest, read.Settings.Map.Digest);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }

    [Theory]
    [InlineData(1, "tesla_coil is a building of a faction: a map places it for nobody (neutral) only")]
    public void A_map_places_one_for_nobody_only(int owner, string problem)
    {
        var error = Assert.Throws<ArgumentException>(() => Map(Of("tesla_coil", 1, 1, Coil) with { Owner = new PlayerId(owner) }));
        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void A_wall_or_an_id_the_rules_do_not_know_is_no_building_of_the_map()
    {
        Assert.False(TestRules.Standard.MapTypes.TryGetBuildingOfData("wall", out _));
        Assert.Contains("unknown building 'war_fortress'", Assert.ThrowsAny<Exception>(() =>
            TestMapParser.Parse(". . .\nbuilding war_fortress 0 0", TestRules.Standard)).Message);
    }

    [Fact]
    public void The_match_refuses_one_its_rules_make_another_size_or_do_not_define()
    {
        Assert.Contains("the rules make it 1x1", Assert.Throws<ArgumentException>(() =>
            new GameWorld(1, Map(Of("tesla_coil", 2, 2, Coil)), TestRules.Standard)).Message);
        Assert.Contains("which the rules of this match do not define", Assert.Throws<ArgumentException>(() =>
            new GameWorld(1, Map(Of("prism_tower", 1, 1, Coil)), TestRules.Standard)).Message);
    }

    [Fact]
    public void Destroyed_it_opens_its_cells_and_leaves_no_ruins()
    {
        var world = new GameWorld(1, Map(Of("tesla_coil", 1, 1, Coil)), TestRules.Standard);
        var coil = world.Entities.OfType<Building>().Single();

        world.DestroyBuilding(coil, null);
        world.OpenWrecks(null);

        Assert.False(world.Map.HasBuilding(Coil));
    }
}
