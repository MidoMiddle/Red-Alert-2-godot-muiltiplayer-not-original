using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>
/// The maps from RA2 whole (2026-10-27, tools/ra2_import/ra2_to_rtsmap.py): the buildings of the factions they give to nobody — the
/// defenses of WAR Pro and The Light of Night, the yards of Facing Corners, the reactors and the bunkers of Route 66 — stand on them, a
/// base deploys at every start, and no defense of nobody reaches a start. By the game data; nobody plays, no bot.
/// </summary>
public sealed class Ra2NeutralBasesTests
{
    public static TheoryData<string> Maps() => ["facing-corners", "light-of-night", "route-66", "war-pro"];

    private static MapDefinition Read(string id) =>
        RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", id + ".rtsmap")));

    [Theory]
    [InlineData("facing-corners", new[] { "alliance_construction_yard 4", "construction_yard 4" })]
    [InlineData("light-of-night", new[]
    {
        "alliance_air_force_command 1", "alliance_battle_lab 1", "alliance_construction_yard 1", "alliance_grand_cannon 3",
        "alliance_patriot_missile_system 8", "alliance_pillbox 7", "alliance_prism_tower 6", "alliance_war_factory 1",
        "neutral_civilian_bunker 4", "nuclear_reactor 2",
    })]
    [InlineData("route-66", new[] { "neutral_civilian_bunker 48", "nuclear_reactor 4" })]
    [InlineData("war-pro", new[]
    {
        // RA2 has 4 flak cannons: one stands where our larger footprints of its neighbours leave it no room.
        "alliance_chronosphere 1", "alliance_grand_cannon 12", "alliance_patriot_missile_system 24", "alliance_prism_tower 24",
        "alliance_weather_control_device 1", "barracks 2", "cloning_vats 12", "flak_cannon 3", "iron_curtain 1",
        "neutral_civilian_bunker 28", "nuclear_missile_silo 1", "tesla_coil 12",
    })]
    public void The_map_has_the_buildings_of_the_factions_and_the_bunkers_of_RA2_for_nobody(string id, string[] counts)
    {
        var map = Read(id);
        var types = GameDataFiles.Rules.MapTypes;
        var placed = map.Structures
            .Where(structure => structure.Type == "neutral_civilian_bunker" || !types.TryGetBuilding(structure.Type, out _))
            .GroupBy(structure => structure.Type)
            .Select(group => $"{group.Key} {group.Count()}")
            .Order(StringComparer.Ordinal);

        Assert.Equal(counts, placed);
        Assert.All(map.Structures, structure => Assert.Equal(0, structure.Owner));
    }

    [Theory]
    [InlineData("facing-corners", new string[0])]
    [InlineData("light-of-night", new string[0])]
    [InlineData("route-66", new string[0])]
    // The owner (2026-10-27): as in RA2. Eight Grand Cannons of the middle (range 15) reach the bases of the inner starts from the
    // first second — there RA2's Neutral house never fired.
    [InlineData("war-pro", new[]
    {
        "alliance_grand_cannon 154,166 > 5", "alliance_grand_cannon 159,82 > 6", "alliance_grand_cannon 160,167 > 5",
        "alliance_grand_cannon 167,90 > 6", "alliance_grand_cannon 71,154 > 7", "alliance_grand_cannon 83,168 > 7",
        "alliance_grand_cannon 85,81 > 8", "alliance_grand_cannon 94,78 > 8",
    })]
    public void An_mcv_of_either_faction_deploys_at_every_start_and_the_defenses_of_nobody_that_reach_it_are_these(string id, string[] reaching)
    {
        var map = Read(id);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);
        var setup = new MatchSetup(
            id,
            5,
            new MatchOptions(FogOfWar: false),
            [
                .. map.Starts.Select((start, i) => new MatchPlayer(
                    i + 1, PlayerKind.Human, AiDifficulty.Medium, i % 2 == 0 ? "union" : "alliance", Color: i, Team: i + 1, start.Slot, start.Cell)),
            ],
            [.. map.Starts.Select(start => start.Cell)]);
        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, grid), GameDataFiles.Rules);
        var world = simulation.World;
        var towers = world.Entities.OfType<Building>().Where(Hostility.IsHostileNeutral).ToList();
        Assert.Equal(id is "war-pro" or "light-of-night", towers.Count > 0);

        uint sequence = 0;
        foreach (var player in setup.Players)
        {
            var mcv = world.Entities.OfType<Unit>().First(unit => unit.Owner == player.Id && unit.Archetype.Id.EndsWith("mcv", StringComparison.Ordinal));
            simulation.Enqueue(new DeployCommand(player.Id, 0, sequence++, mcv.Id));
        }

        simulation.Step();

        Assert.Equal(setup.Players.Length, simulation.LastTickEvents.OfType<Deployed>().Count());
        // The yard of every start and the soldiers by it.
        var bases = world.Entities.Where(entity => entity.IsAlive && !entity.Owner.IsNeutral).ToList();
        Assert.Equal(setup.Players.Length, bases.OfType<Building>().Count());
        var reached = towers
            .SelectMany(tower => bases.Where(start => TargetingSystem.CanFireAt(world, tower, start))
                .Select(start => $"{tower.Def!.Id} {tower.Cell.X},{tower.Cell.Y} > {start.Owner.Value}"))
            .Distinct()
            .Order(StringComparer.Ordinal);
        Assert.Equal(reaching, reached);
    }
}
