using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The buildings of sight (stage 2d of filling the game): the orbital uplink explores its owner's whole map while it stands ready and
/// online, and the veil generator hides the cells round it from the enemy — shroud, and nothing seen there from memory — unless the
/// enemy sees them now. With the fog of war and without it. By the game data; two people play, no bot.
/// </summary>
public sealed class IntelBuildingsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    // Far from both starts: no starting unit sees it.
    private static readonly CellPos Far = new(40, 30);

    private static GameSimulation Match(bool fog) =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "intel",
                11,
                new MatchOptions(StartingCredits: 50_000, FogOfWar: fog),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(10, 10)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(55, 55)),
                ],
                [new CellPos(10, 10), new CellPos(55, 55)]),
            new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static void Step(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void The_uplink_explores_the_whole_map_once_it_has_power_and_under_the_fog_shows_the_ground_only()
    {
        var simulation = Match(fog: true);
        var world = simulation.World;
        Build(world, Allied, "alliance_spy_satellite_uplink", 14, 6);
        // Placed between ticks: the power is worked out at once, as for a building that grows ready in a tick.
        PowerSystem.Update(world, null);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, Far.Center);
        Step(simulation, 2);

        Assert.True(world.Player(Allied).LowPower);
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Allied, Far));

        Build(world, Allied, "alliance_power_plant", 17, 6);
        Step(simulation, 2);

        Assert.Equal(CellVisibility.Fog, Vision.StateAt(world, Allied, Far));
        Assert.Equal(CellVisibility.Fog, Vision.StateAt(world, Allied, new CellPos(63, 63)));
        Assert.False(Vision.Sees(world, Allied, tank), "under the fog only what is in sight now");
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Union, new CellPos(14, 6)));
    }

    [Fact]
    public void Without_the_fog_the_uplink_shows_the_enemy_anywhere()
    {
        var simulation = Match(fog: false);
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 17, 6);
        Build(world, Allied, "alliance_spy_satellite_uplink", 14, 6);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, Far.Center);
        Step(simulation, 2);

        Assert.True(Vision.Sees(world, Allied, tank));
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Allied, new CellPos(63, 0)));
    }

    [Fact]
    public void Without_the_fog_the_veil_hides_an_explored_base_until_the_generator_is_sold()
    {
        var simulation = Match(fog: false);
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 14, 6);
        var barracks = Build(world, Allied, "alliance_barracks", 18, 10);
        world.VisionOf(Union).ExploreAll();
        Step(simulation, 2);
        Assert.True(Vision.Sees(world, Union, barracks), "explored, no fog: seen");

        var generator = Build(world, Allied, "alliance_gap_generator", 16, 12);
        Step(simulation, 2);

        Assert.False(Vision.Sees(world, Union, barracks));
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Union, barracks.Cell));
        Assert.True(Vision.IsVeiled(world, Union, barracks.Cell));
        Assert.False(Vision.IsVeiled(world, Allied, barracks.Cell), "not from its own team");
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Union, Far));

        simulation.Enqueue(new SellCommand(Allied, simulation.CurrentTick, 0, generator.Id));
        Step(simulation, 60);

        Assert.False(generator.IsAlive);
        Assert.True(Vision.Sees(world, Union, barracks));
    }

    [Fact]
    public void Under_the_fog_a_veiled_cell_is_shroud_and_one_who_comes_near_sees_in()
    {
        var simulation = Match(fog: true);
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 14, 6);
        var generator = Build(world, Allied, "alliance_gap_generator", 16, 12);
        world.VisionOf(Union).ExploreAll();
        Step(simulation, 2);

        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Union, new CellPos(20, 12)));
        Assert.Equal(CellVisibility.Fog, Vision.StateAt(world, Union, Far));
        Assert.False(Vision.Sees(world, Union, generator));

        world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(19, 14).Center);
        Step(simulation, 1);

        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Union, new CellPos(18, 13)));
        Assert.True(Vision.Sees(world, Union, generator));
    }
}
