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
/// The cloning vats of RA2 (docs/ra2_buildings/soviets/cloning_vats): every soldier the barracks train comes out once more at each
/// vats, free; a vats with no free cell round it gives none; a soldier sent in leaves for half its price, an engineer does not go in.
/// By the game data; two people play, no bot.
/// </summary>
public sealed class CloningVatsTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // A vats of 2 x 2 cells; the island one stands in the water, with no free cell round it within 4 rings.
    private static readonly CellPos Vats = new(30, 20);
    private static readonly CellPos Island = new(50, 40);

    private static GameSimulation Match()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        for (var y = Island.Y - 4; y <= Island.Y + 5; y++)
        {
            for (var x = Island.X - 4; x <= Island.X + 5; x++)
            {
                var inside = x >= Island.X && x <= Island.X + 1 && y >= Island.Y && y <= Island.Y + 1;
                land[(y * Size) + x] = (byte)(inside ? LandType.Clear : LandType.Deep);
            }
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        return new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "vats",
                    31,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
    }

    private static Building Build(GameWorld world, PlayerId player, string id, CellPos cell)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, cell, true, null);
    }

    /// <summary>A base with power and barracks; then one conscript trained. The conscripts of the player afterwards.</summary>
    private static int Train(GameSimulation simulation, params CellPos[] vats)
    {
        var world = simulation.World;
        Build(world, Union, "construction_yard", new CellPos(6, 6));
        Build(world, Union, "reactor", new CellPos(12, 6));
        Build(world, Union, "reactor", new CellPos(15, 6));
        Build(world, Union, "barracks", new CellPos(12, 12));
        foreach (var cell in vats)
        {
            Build(world, Union, "cloning_vats", cell);
        }

        TechTree.Update(world);
        var conscript = world.Rules.Database.IndexOfUnit("conscript");
        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, conscript, 1));
        for (var tick = 0; tick < 1200 && !simulation.LastTickEvents.OfType<UnitProduced>().Any(); tick++)
        {
            simulation.Step();
        }

        Assert.Contains(simulation.LastTickEvents, evt => evt is UnitProduced);
        return world.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Owner == Union && unit.Type == UnitType.Conscript);
    }

    [Fact]
    public void Without_vats_one_soldier_comes_out()
    {
        Assert.Equal(1, Train(Match()));
    }

    [Fact]
    public void A_soldier_trained_comes_out_again_at_the_vats_and_again_at_a_second_one()
    {
        var simulation = Match();
        Assert.Equal(2, Train(simulation, Vats));
        Assert.Single(simulation.LastTickEvents.OfType<UnitCloned>());

        // RA2 allows one vats; a map or a mod with two gives two copies.
        Assert.Equal(3, Train(Match(), Vats, new CellPos(40, 20)));
    }

    [Fact]
    public void The_clone_stands_beside_the_vats()
    {
        var simulation = Match();
        Train(simulation, Vats);
        var cloned = simulation.LastTickEvents.OfType<UnitCloned>().Single();
        var clone = (Unit)simulation.World.Entities.First(entity => entity.Id == cloned.Unit);
        var cell = clone.Position.ToCell();

        Assert.InRange(cell.X, Vats.X - 1, Vats.X + 2);
        Assert.InRange(cell.Y, Vats.Y - 1, Vats.Y + 2);
    }

    [Fact]
    public void A_vats_with_no_free_cell_round_it_gives_no_copy()
    {
        Assert.Equal(1, Train(Match(), Island));
    }

    [Fact]
    public void A_soldier_sent_in_leaves_for_half_its_price_and_an_engineer_does_not_go_in()
    {
        var simulation = Match();
        var world = simulation.World;
        var vats = Build(world, Union, "cloning_vats", Vats);
        var conscript = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(30, 25).Center, snapToSubcell: true);
        var engineer = world.SpawnUnit(Union, UnitType.Engineer, new CellPos(31, 25).Center, snapToSubcell: true);
        var enemy = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(58, 58).Center, snapToSubcell: true);

        Assert.Equal(EntryAction.Recycle, EntrySystem.ActionOf(world, conscript, vats));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, vats));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, enemy, vats));

        var before = world.Player(Union).Credits;
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [conscript.Id], vats.Id));
        for (var tick = 0; tick < 300 && conscript.IsAlive; tick++)
        {
            simulation.Step();
        }

        Assert.False(conscript.IsAlive);
        Assert.Equal(before + (conscript.Archetype.Cost / 2), world.Player(Union).Credits);
        Assert.Equal(50, conscript.Archetype.Cost / 2);
    }
}
