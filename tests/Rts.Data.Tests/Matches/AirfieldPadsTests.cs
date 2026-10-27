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
/// RA2's Airforce Command (docs/ra2_buildings/allies/air_force_command, NumberOfDocks=4): a jet built there stands on a pad of its own,
/// four to a building, and no fifth is built; sent at a target it fires its missile, flies back, lands on a free pad and rearms there.
/// By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class AirfieldPadsTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    [Fact]
    public void Four_jets_stand_on_four_pads_fly_out_fire_and_land_back_on_a_pad()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "airfield",
                    97,
                    new MatchOptions(StartingCredits: 20_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        var world = simulation.World;
        world.VisionOf(Allied).ExploreAll();
        var database = world.Rules.Database;
        database.TryGetBuilding("alliance_power_plant", out var power);
        database.TryGetBuilding("alliance_air_force_command", out var command);
        world.CreateBuilding(Allied, power, new CellPos(20, 20), true, null);
        var airfield = world.CreateBuilding(Allied, command, new CellPos(30, 30), true, null);
        var harrier = database.IndexOfUnit("alliance_harrier");
        TechTree.Update(world);

        var order = new ProduceCommand(Allied, simulation.CurrentTick, 0, ProductionKind.Unit, harrier, 5);
        simulation.Enqueue(order);
        var jets = new List<Unit>();
        for (var tick = 0; tick < 20_000 && jets.Count < 4; tick++)
        {
            simulation.Step();
            jets.AddRange(simulation.LastTickEvents.OfType<UnitProduced>().Select(made => (Unit)world.Entities.First(e => e.Id == made.Unit)));
        }

        // Four pads: four jets, each on its own, landed; the fifth was not taken into the queue.
        Assert.Equal(4, jets.Count);
        Assert.Equal(4, jets.Select(jet => jet.Position).Distinct().Count());
        Assert.All(jets, jet => Assert.True(AmmoSystem.OnPad(world, jet)));
        Assert.All(jets, jet => Assert.Equal(airfield, world.BuildingAt(jet.Position.ToCell())));
        Run(simulation, 600);
        Assert.Empty(world.Player(Allied).Queue(Rts.Simulation.Rules.ProductionCategory.Aircraft));

        // Sent at a target: one missile, back, on a pad again, and loaded once more.
        world.Rules.Database.TryGetBuilding("reactor", out var reactorDef);
        var target = world.CreateBuilding(Union, reactorDef, new CellPos(30, 50), true, null);
        var jet = jets[0];
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 1, [jet.Id], target.Id));
        Run(simulation, 900, () => jet.Ammo == 0);
        Assert.Equal(0, jet.Ammo);
        Run(simulation, 1500, () => AmmoSystem.OnPad(world, jet) && jet.Ammo == jet.Archetype.Ammo);

        Assert.True(AmmoSystem.OnPad(world, jet));
        Assert.Equal(jet.Archetype.Ammo, jet.Ammo);
        Assert.True(target.Hp < target.MaxHp);
    }

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }
}
