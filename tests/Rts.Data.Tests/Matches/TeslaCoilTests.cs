using Rts.Simulation;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The tesla coil charged by tesla troopers (RA2, docs/ra2_buildings/soviets/tesla_coil): with power one trooper gives the charged
/// shot (300 at 8 cells instead of 200 at 7); low on power the coil is silent with none or one, fires its own shot with two and the
/// charged one with three. A trooper given another order stops charging. By the game data, without the fog of war; two people play,
/// no bot.
/// </summary>
public sealed class TeslaCoilTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // A coil of 2 x 2 cells at (20, 20); its centre is the corner of (21, 21). The target stands 6 cells east, the troopers west.
    private static readonly CellPos Coil = new(20, 20);

    private static GameSimulation Match()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "coil",
                    37,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Building Build(GameWorld world, PlayerId player, string id, CellPos cell)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, cell, true, null);
    }

    /// <summary>The coil, with or without power and with <paramref name="troopers"/> charging it; the damage of its first shot at an MCV.</summary>
    private static (int Damage, Building Coil) FirstShot(bool power, int troopers)
    {
        var simulation = Match();
        var world = simulation.World;
        if (power)
        {
            Build(world, Union, "reactor", new CellPos(6, 6));
        }

        var coil = Build(world, Union, "tesla_coil", Coil);
        var charging = Enumerable.Range(0, troopers)
            .Select(i => world.SpawnUnit(Union, UnitType.TeslaTrooper, new CellPos(18, 19 + i).Center, snapToSubcell: true)).ToList();
        if (charging.Count > 0)
        {
            simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [.. charging.Select(unit => unit.Id)], coil.Id));
        }

        for (var tick = 0; tick < 30; tick++)
        {
            simulation.Step();
        }

        var mcv = world.SpawnUnit(Allied, UnitType.AllianceMcv, new CellPos(27, 21).Center);
        for (var tick = 0; tick < 200 && mcv.Hp == mcv.MaxHp; tick++)
        {
            simulation.Step();
        }

        return (mcv.MaxHp - mcv.Hp, coil);
    }

    [Theory]
    [InlineData(true, 0, 200)]
    [InlineData(true, 1, 300)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 0)]
    [InlineData(false, 2, 200)]
    [InlineData(false, 3, 300)]
    public void The_troopers_charging_it_decide_its_shot(bool power, int troopers, int damage)
    {
        var (dealt, coil) = FirstShot(power, troopers);

        Assert.Equal(troopers, coil.Chargers);
        Assert.Equal(damage, dealt);
    }

    [Fact]
    public void A_trooper_given_another_order_stops_charging()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "reactor", new CellPos(6, 6));
        var coil = Build(world, Union, "tesla_coil", Coil);
        var trooper = world.SpawnUnit(Union, UnitType.TeslaTrooper, new CellPos(18, 20).Center, snapToSubcell: true);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [trooper.Id], coil.Id));
        for (var tick = 0; tick < 30; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(coil.Id, trooper.Charging);
        Assert.True(Defenses.Overcharged(world, coil));

        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 0, [trooper.Id], new CellPos(10, 30).Center));
        for (var tick = 0; tick < 10; tick++)
        {
            simulation.Step();
        }

        Assert.Null(trooper.Charging);
        Assert.Equal(0, coil.Chargers);
        Assert.False(Defenses.Overcharged(world, coil));
    }
}
