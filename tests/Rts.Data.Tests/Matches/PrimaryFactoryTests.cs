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
/// The primary factory of RA2: of two barracks the last built is the main one, where a soldier comes out; the player makes the other
/// its primary one and the soldiers come out there; another factory of the kind made primary takes it over. By the game data; two
/// people play, no bot.
/// </summary>
public sealed class PrimaryFactoryTests
{
    private const int Size = 64;
    private static readonly PlayerId Allied = new(2);

    [Fact]
    public void A_soldier_comes_out_of_the_primary_barracks()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "primary",
                    98,
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
        database.TryGetBuilding("alliance_barracks", out var barracksDef);
        world.CreateBuilding(Allied, power, new CellPos(20, 20), true, null);
        var first = world.CreateBuilding(Allied, barracksDef, new CellPos(30, 20), true, null);
        var second = world.CreateBuilding(Allied, barracksDef, new CellPos(40, 20), true, null);
        var gi = database.IndexOfUnit("alliance_rifleman");
        var sequence = 0u;
        TechTree.Update(world);
        Assert.True(world.Player(Allied).CanBuildUnit(gi));

        EntityId Train()
        {
            simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, sequence++, ProductionKind.Unit, gi, 1));
            for (var tick = 0; tick < 2000; tick++)
            {
                simulation.Step();
                if (simulation.LastTickEvents.OfType<UnitProduced>().FirstOrDefault() is { } made)
                {
                    return made.Factory;
                }
            }

            throw new InvalidOperationException($"No soldier came out; refused: {simulation.RefusedCommands.Count}.");
        }

        // Without a primary one: the last built.
        Assert.Equal(second.Id, Train());
        Assert.Equal(second, ProductionSystem.MainFactory(world, Allied, Rts.Simulation.Rules.ProductionCategory.Infantry, out var count));
        Assert.Equal(2, count);

        // The first made primary (D or a double click): the soldiers come out of it.
        simulation.Enqueue(new SetPrimaryCommand(Allied, simulation.CurrentTick, sequence++, first.Id));
        simulation.Step();
        Assert.True(first.Primary);
        Assert.Equal(first.Id, Train());

        // The second made primary: the first is no longer.
        simulation.Enqueue(new SetPrimaryCommand(Allied, simulation.CurrentTick, sequence++, second.Id));
        simulation.Step();
        Assert.False(first.Primary);
        Assert.Equal(second.Id, Train());
    }
}
