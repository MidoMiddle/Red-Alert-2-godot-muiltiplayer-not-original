using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The prerequisite groups of RA2 (docs/ra2_buildings/tech_tree.md: POWER, PROC, RADAR, BARRACKS, TECH): a building names a group, and
/// any ready building of the player in it fulfils it — the nuclear reactor is a power plant, a building taken from the other faction
/// counts as its own. By the game data; two people play, no bot.
/// </summary>
public sealed class PrerequisiteGroupsTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameWorld World()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "groups",
                    7,
                    new MatchOptions(StartingCredits: 10_000, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        return simulation.World;
    }

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static bool CanBuild(GameWorld world, PlayerId player, string id)
    {
        TechTree.Update(world);
        return world.Player(player).CanBuildBuilding(world.Rules.Database.IndexOfBuilding(id));
    }

    [Fact]
    public void The_atomic_plant_is_a_power_plant()
    {
        var world = World();
        Build(world, Union, "construction_yard", 4, 4);
        Assert.False(CanBuild(world, Union, "barracks"));

        Build(world, Union, "nuclear_reactor", 20, 4);
        Assert.True(CanBuild(world, Union, "barracks"));
        Assert.True(CanBuild(world, Union, "refinery"));
    }

    [Fact]
    public void A_power_plant_taken_from_the_allies_opens_the_soviet_base()
    {
        var world = World();
        Build(world, Union, "construction_yard", 4, 4);
        var plant = Build(world, Allied, "alliance_power_plant", 20, 20);
        Assert.False(CanBuild(world, Union, "barracks"));

        EntrySystem.Capture(world, plant, Union, null);
        Assert.True(CanBuild(world, Union, "barracks"));
    }

    [Fact]
    public void The_tesla_coil_needs_power_and_a_radar_and_the_war_factory_a_refinery_and_barracks()
    {
        var world = World();
        Build(world, Union, "construction_yard", 4, 4);
        Build(world, Union, "reactor", 10, 4);
        Build(world, Union, "refinery", 14, 4);
        Assert.False(CanBuild(world, Union, "war_factory"));
        Build(world, Union, "barracks", 20, 4);
        Assert.True(CanBuild(world, Union, "war_factory"));
        Assert.True(CanBuild(world, Union, "flak_cannon"));

        Assert.False(CanBuild(world, Union, "tesla_coil"));
        // The Allies' air force command is a radar of RA2's RADAR group: taken, it opens the coil.
        var command = Build(world, Allied, "alliance_air_force_command", 30, 30);
        EntrySystem.Capture(world, command, Union, null);
        Assert.True(CanBuild(world, Union, "tesla_coil"));
    }
}
