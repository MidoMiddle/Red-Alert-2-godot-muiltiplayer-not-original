using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// RA2's Night Hawk (docs/ra2_units/allies/nighthawk, SHAD Prerequisite=GAWEAP): built at the war factory in the queue of the tanks, no
/// air force command needed, and it takes off from the war factory. By the game data; two people play, no bot.
/// </summary>
public sealed class NightHawkTests
{
    private const int Size = 64;
    private static readonly PlayerId Allied = new(2);

    [Fact]
    public void The_night_hawk_is_built_at_the_war_factory_with_the_tanks_and_takes_off_from_it()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "night-hawk",
                    93,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        var world = simulation.World;
        var database = world.Rules.Database;
        database.TryGetBuilding("alliance_power_plant", out var power);
        database.TryGetBuilding("alliance_war_factory", out var factoryDef);
        world.CreateBuilding(Allied, power, new CellPos(30, 30), true, null);
        var factory = world.CreateBuilding(Allied, factoryDef, new CellPos(36, 36), true, null);
        var hawk = database.IndexOfUnit("alliance_nighthawk");
        TechTree.Update(world);

        Assert.Equal(ProductionCategory.Vehicle, ProductionRules.CategoryOf(database, ProductionKind.Unit, hawk));
        Assert.True(world.Player(Allied).CanBuildUnit(hawk));

        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 0, ProductionKind.Unit, hawk, 1));
        UnitProduced? made = null;
        for (var tick = 0; tick < 3000 && made is null; tick++)
        {
            simulation.Step();
            made = simulation.LastTickEvents.OfType<UnitProduced>().FirstOrDefault();
        }

        Assert.NotNull(made);
        Assert.Equal(factory.Id, made!.Factory);
        Assert.True(world.TryGet(made.Unit, out var unit) && unit is Unit { Archetype.Flies: true });
    }
}
