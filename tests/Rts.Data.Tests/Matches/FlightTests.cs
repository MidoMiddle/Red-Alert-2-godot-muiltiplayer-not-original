using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Take-off (rules v102, the owner's wish of 2026-10-26, <see cref="FlightSystem"/>): the helicopter set down, a jet on its pad and a
/// carrier's Hornet climb to their flying height before they set out. By the game data; two people play, no bot. The sea of the
/// carrier lies from row 20 to 44.
/// </summary>
public sealed class FlightTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    /// <summary>Ticks enough for a flier to turn toward its way and set out on it: a second.</summary>
    private const int TurnAndGo = 15;

    [Fact]
    public void A_helicopter_set_down_climbs_before_it_moves()
    {
        var simulation = Match();
        var world = simulation.World;
        var hawk = Spawn(world, Allied, UnitType.AllianceNighthawk, 10, 10);
        Run(simulation, 2);
        Assert.True(hawk.Grounded, "set down over open ground");

        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [hawk.Id], new CellPos(14, 10).Center));
        var start = hawk.Position;
        Run(simulation, FlightSystem.ClimbTicks);
        Assert.False(hawk.Grounded);
        Assert.Equal(start, hawk.Position);

        Run(simulation, TurnAndGo);
        Assert.NotEqual(start, hawk.Position);
    }

    [Fact]
    public void A_helicopter_hanging_over_the_water_sets_out_at_once()
    {
        var simulation = Match(sea: true);
        var world = simulation.World;
        var hawk = Spawn(world, Allied, UnitType.AllianceNighthawk, 30, 30);
        Run(simulation, 2);
        Assert.False(hawk.Grounded);

        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [hawk.Id], new CellPos(34, 30).Center));
        var start = hawk.Position;
        Run(simulation, TurnAndGo);
        Assert.NotEqual(start, hawk.Position);
    }

    [Fact]
    public void A_jet_on_its_pad_climbs_before_it_flies_to_strike_and_fires_nothing_meanwhile()
    {
        var simulation = Match();
        var world = simulation.World;
        var command = Build(world, Allied, "alliance_air_force_command", 10, 10);
        var pad = AmmoSystem.PadCell(command, 0);
        var jet = world.SpawnUnit(Allied, UnitType.AllianceHarrier, pad.Center);
        var target = Build(world, Union, "war_factory", 40, 40);
        Run(simulation, 2);
        Assert.True(jet.Grounded, "on its pad");

        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [jet.Id], target.Id));
        var (start, ammo) = (jet.Position, jet.Ammo);
        Run(simulation, FlightSystem.ClimbTicks);
        Assert.Equal((start, ammo), (jet.Position, jet.Ammo));

        Run(simulation, TurnAndGo);
        Assert.NotEqual(start, jet.Position);
    }

    [Fact]
    public void A_hornet_climbs_off_the_deck_before_it_flies()
    {
        var simulation = Match(sea: true);
        var world = simulation.World;
        var carrier = Spawn(world, Allied, UnitType.AllianceAircraftCarrier, 12, 30);
        var target = Spawn(world, Union, UnitType.Dreadnought, 30, 30);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [carrier.Id], target.Id));

        var events = Run(simulation, 120, () => carrier.SpawnsDocked < 3);
        var launched = events.OfType<SpawnLaunched>().First();
        Assert.True(world.TryGet(launched.Unit, out var entity) && entity is Unit hornet);
        var jet = (Unit)entity!;
        // Launched in the tick's weapons, before its movement: a tick of the climb is gone already.
        Assert.InRange(jet.ClimbTicks, FlightSystem.ClimbTicks - 1, FlightSystem.ClimbTicks);
        var deck = jet.Position;

        Run(simulation, jet.ClimbTicks);
        Assert.Equal(deck, jet.Position);
        Run(simulation, TurnAndGo);
        Assert.NotEqual(deck, jet.Position);
    }

    private static GameSimulation Match(bool sea = false)
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "flight",
                    102,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        var world = simulation.World;
        if (sea)
        {
            for (var y = 20; y <= 44; y++)
            {
                for (var x = 6; x <= 58; x++)
                {
                    world.Map.SetLand(new CellPos(x, y), LandType.Deep);
                }
            }
        }

        world.VisionOf(Allied).ExploreAll();
        world.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }
}
