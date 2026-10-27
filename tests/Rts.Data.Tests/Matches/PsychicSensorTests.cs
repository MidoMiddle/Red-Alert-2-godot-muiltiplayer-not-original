using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The psychic sensor of RA2 (docs/ra2_buildings/soviets/psychic_sensor): with power, within 15 cells it shows its owner's team a
/// disguised spy for what it is — the soldiers fire at it —, finds a unit under the water and shows what enemy units aim at; without
/// power, or farther off, nothing. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class PsychicSensorTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos Sensor = new(20, 20);

    // Deep water east of x = 40.
    private const int Shore = 40;

    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                land[(y * Size) + x] = (byte)(x >= Shore ? LandType.Deep : LandType.Clear);
            }
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "sensor",
                    23,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(4, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(4, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static void Build(GameWorld world, PlayerId player, string id, CellPos cell)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        world.CreateBuilding(player, def, cell, true, null);
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static void Run(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    /// <summary>A Union conscript at (22, <paramref name="y"/>) and an Alliance spy 3 cells east; whether it is unmasked, and the spy.</summary>
    private static (Unit Spy, bool Unmasked) SpyNear(bool sensor, bool power, int y)
    {
        var simulation = Match();
        var world = simulation.World;
        if (power)
        {
            Build(world, Union, "reactor", new CellPos(10, 10));
        }

        if (sensor)
        {
            Build(world, Union, "psychic_sensor", Sensor);
        }

        Spawn(world, Union, UnitType.Conscript, 22, y);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 25, y);
        Run(simulation, 150);
        return (spy, Vision.Unmasked(world, Union, spy));
    }

    [Fact]
    public void Within_its_reach_with_power_the_spy_is_shown_and_shot()
    {
        var (spy, unmasked) = SpyNear(sensor: true, power: true, y: 26);

        Assert.True(unmasked);
        Assert.True(spy.Hp < spy.MaxHp, "the conscript fired at the spy");
    }

    [Theory]
    [InlineData(false, true, 26)]
    [InlineData(true, false, 26)]
    [InlineData(true, true, 50)] // 29 cells south of the sensor's centre (21, 21), past its 15
    public void Without_a_sensor_its_power_or_its_reach_the_spy_passes(bool sensor, bool power, int y)
    {
        var (spy, unmasked) = SpyNear(sensor, power, y);

        Assert.False(unmasked);
        Assert.Equal(spy.MaxHp, spy.Hp);
    }

    [Fact]
    public void A_unit_under_the_water_in_its_reach_is_found_and_one_farther_off_is_not()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "reactor", new CellPos(10, 10));
        Build(world, Union, "psychic_sensor", new CellPos(28, 20));
        // The sensor's centre is (29, 21): 12 cells to (41, 21), 30 to (59, 21).
        var near = Spawn(world, Allied, UnitType.AllianceDolphin, 41, 21);
        var far = Spawn(world, Allied, UnitType.AllianceDolphin, 59, 21);
        Run(simulation, 2);

        Assert.True(Vision.Sees(world, Union, near));
        Assert.False(Vision.Sees(world, Union, far));
    }

    [Fact]
    public void It_shows_what_the_enemy_units_in_its_reach_aim_at()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "reactor", new CellPos(10, 10));
        Build(world, Union, "psychic_sensor", Sensor);
        var near = Spawn(world, Allied, UnitType.AllianceRifleman, 25, 25);
        var far = Spawn(world, Allied, UnitType.AllianceRifleman, 21, 50);
        Run(simulation, 2);

        Assert.True(Vision.IntentShown(world, Union, near));
        Assert.False(Vision.IntentShown(world, Union, far));
        Assert.False(Vision.IntentShown(world, Allied, near));
    }
}
