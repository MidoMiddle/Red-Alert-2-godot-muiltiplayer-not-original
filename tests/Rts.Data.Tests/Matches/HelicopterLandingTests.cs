using Rts.Client.Units;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The helicopter sets down when it has nothing to do over open flat ground and hangs in the air over water, a slope, a building or
/// a soldier (the owner, 2026-10-25); since rules v102 the simulation decides it and makes it climb before it goes (the owner,
/// 2026-10-26), the look (<see cref="UnitAltitude"/>) follows. Two people play, no bot.
/// </summary>
public sealed class HelicopterLandingTests
{
    private static readonly PlayerId Allied = new(1);

    private static GameSimulation Match()
    {
        var map = new MapGrid(64, 64);
        map.SetLand(new CellPos(40, 40), LandType.Deep);
        map.SetCornerLevel(45, 45, 1);
        return new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "helicopter",
                    31,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
    }

    private static Unit Spawn(GameWorld world, UnitType type, int x, int y) =>
        world.SpawnUnit(Allied, type, new CellPos(x, y).Center, snapToSubcell: false);

    [Fact]
    public void It_lands_on_open_ground_when_idle_and_hovers_elsewhere()
    {
        var simulation = Match();
        var world = simulation.World;
        var open = Spawn(world, UnitType.AllianceNighthawk, 30, 30);
        var water = Spawn(world, UnitType.AllianceNighthawk, 40, 40);
        var slope = Spawn(world, UnitType.AllianceNighthawk, 45, 45);
        Spawn(world, UnitType.AllianceRifleman, 20, 20);
        var soldier = Spawn(world, UnitType.AllianceNighthawk, 20, 20);
        world.Rules.Database.TryGetBuilding("alliance_power_plant", out var plant);
        world.CreateBuilding(Allied, plant, new CellPos(10, 30), true, null);
        var building = Spawn(world, UnitType.AllianceNighthawk, 10, 30);

        // A jet off its pad never sets down.
        var jet = Spawn(world, UnitType.AllianceHarrier, 32, 32);

        // The simulation sets it down (rules v102, FlightSystem); the look follows.
        simulation.Step();
        simulation.Step();

        Assert.Equal(0f, UnitAltitude.Target(world, open));
        Assert.All([water, slope, soldier, building, jet], hovering => Assert.Equal(UnitAltitude.Aircraft, UnitAltitude.Target(world, hovering)));
    }

    [Fact]
    public void Ordered_away_it_climbs_before_it_goes_and_its_look_moves_at_its_rate()
    {
        var simulation = Match();
        var world = simulation.World;
        var helicopter = Spawn(world, UnitType.AllianceNighthawk, 30, 30);
        simulation.Step();
        simulation.Step();
        Assert.Equal(0f, UnitAltitude.Target(world, helicopter));

        // Climbing (FlightSystem.ClimbTicks): on its way up, then at its flying height.
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [helicopter.Id], new CellPos(50, 30).Center, false));
        simulation.Step();
        simulation.Step();
        var climbing = UnitAltitude.Target(world, helicopter);
        Assert.InRange(climbing, 0.01f, UnitAltitude.Aircraft - 0.01f);
        for (var tick = 0; tick < Rts.Simulation.Systems.FlightSystem.ClimbTicks; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(UnitAltitude.Aircraft, UnitAltitude.Target(world, helicopter));

        // The look starts where the unit goes, then moves at ClimbRate: half a second takes 0.6 of a cell off.
        Assert.Equal(UnitAltitude.Aircraft, UnitAltitude.Step(helicopter.Id, UnitAltitude.Aircraft, 0f));
        var height = UnitAltitude.Step(helicopter.Id, 0f, 0.5f);
        Assert.Equal(UnitAltitude.Aircraft - (UnitAltitude.ClimbRate * 0.5f), height, 3);
        Assert.Equal(height, UnitAltitude.Of(helicopter));
        Assert.Equal(0f, UnitAltitude.Step(helicopter.Id, 0f, 5f));
        UnitAltitude.Forget(helicopter.Id);
        Assert.Equal(UnitAltitude.Aircraft, UnitAltitude.Of(helicopter));
    }
}
