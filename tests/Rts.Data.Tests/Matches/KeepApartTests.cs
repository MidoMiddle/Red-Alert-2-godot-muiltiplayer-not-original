using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The hovering fliers keep apart (rules v103, <see cref="FlightSystem.KeepApart"/>): the owner looked into the base of a bot and saw one
/// Kirov where there were tens, one in another, and they flew so. By the game data; two people play, no bot.
/// </summary>
public sealed class KeepApartTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    /// <summary>A cell, less what the integer steps leave short.</summary>
    private const int ApartLeptons = FlightSystem.SpacingLeptons - 16;

    [Fact]
    public void Kirovs_in_one_place_spread_a_cell_apart()
    {
        var simulation = Match();
        var kirovs = Enumerable.Range(0, 6).Select(_ => Spawn(simulation.World, Union, UnitType.KirovAirship, 30, 30)).ToList();

        Run(simulation, 300);

        Assert.True(Closest(kirovs) >= ApartLeptons, $"the nearest two {Closest(kirovs)} leptons apart");
        Assert.All(kirovs, kirov => Assert.InRange(Distance(kirov.Position, new CellPos(30, 30).Center), 0, 3 * WorldPos.LeptonsPerCell));
    }

    [Fact]
    public void Kirovs_sent_one_by_one_to_one_point_fly_there_apart_and_hang_apart()
    {
        // As from their factory to its rally point: each its own order to the same point.
        var simulation = Match();
        var kirovs = Enumerable.Range(0, 4).Select(_ => Spawn(simulation.World, Union, UnitType.KirovAirship, 10, 30)).ToList();
        var point = new CellPos(40, 30).Center;
        for (var i = 0; i < kirovs.Count; i++)
        {
            simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, (uint)i, [kirovs[i].Id], point));
        }

        Run(simulation, 60);
        Assert.True(Closest(kirovs) >= ApartLeptons / 2, $"on the way, the nearest two {Closest(kirovs)} leptons apart");
        Assert.All(kirovs, kirov => Assert.True(kirov.Position.X > new CellPos(12, 30).Center.X, "on its way"));

        Run(simulation, 600);
        Assert.True(Closest(kirovs) >= ApartLeptons, $"there, the nearest two {Closest(kirovs)} leptons apart");
        Assert.All(kirovs, kirov => Assert.InRange(Distance(kirov.Position, point), 0, 3 * WorldPos.LeptonsPerCell));
    }

    [Fact]
    public void Jetpack_troopers_keep_half_a_cell_apart()
    {
        var simulation = Match();
        var troopers = Enumerable.Range(0, 2).Select(_ => Spawn(simulation.World, Allied, UnitType.AllianceRocketeer, 30, 30)).ToList();

        Run(simulation, 120);

        Assert.InRange(Closest(troopers), FlightSystem.SpacingLeptons / 2 - 16, FlightSystem.SpacingLeptons / 2);
    }

    [Fact]
    public void Two_helicopters_do_not_set_down_on_one_cell()
    {
        var simulation = Match();
        var hawks = Enumerable.Range(0, 2).Select(_ => Spawn(simulation.World, Allied, UnitType.AllianceNighthawk, 30, 30)).ToList();

        Run(simulation, 120);

        Assert.All(hawks, hawk => Assert.True(hawk.Grounded));
        Assert.NotEqual(hawks[0].Position.ToCell(), hawks[1].Position.ToCell());
    }

    [Fact]
    public void Jets_do_not_keep_apart()
    {
        // A jet flies its runs at a target and goes back to rearm: not over a cell of its own.
        var simulation = Match();
        var jets = Enumerable.Range(0, 2).Select(_ => Spawn(simulation.World, Allied, UnitType.AllianceHarrier, 30, 30)).ToList();

        Run(simulation, 30);

        Assert.Equal(jets[0].Position, jets[1].Position);
    }

    private static long Closest(IReadOnlyList<Unit> units)
    {
        var closest = long.MaxValue;
        for (var i = 0; i < units.Count; i++)
        {
            for (var j = i + 1; j < units.Count; j++)
            {
                closest = System.Math.Min(closest, Distance(units[i].Position, units[j].Position));
            }
        }

        return closest;
    }

    private static long Distance(WorldPos a, WorldPos b) => (long)System.Math.Sqrt((a - b).LengthSquared);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "keep apart",
                    103,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Allied).ExploreAll();
        simulation.World.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: false);

    private static void Run(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }
}
