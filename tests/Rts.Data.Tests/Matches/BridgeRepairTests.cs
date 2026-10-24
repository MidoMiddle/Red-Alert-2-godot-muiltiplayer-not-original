using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Movement;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The bridge repair huts (loose ends, stage 2): an engineer going into the hut mends its bridge; a whole bridge takes none; the hut
/// cannot be hurt; a fallen segment with a ship on its water stays down. By the game data, without the fog of war; two people play, no
/// bot.
/// </summary>
public sealed class BridgeRepairTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos Hut = new(28, 21);

    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % Size is >= 30 and <= 33 ? LandType.Deep : LandType.Clear);
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[land.Length], default, null,
            [BridgeState.New(BridgeKind.WoodLow, new CellPos(30, 20), new CellPos(33, 20))],
            [new BuildingState(BuildingKind.TechBridgeRepairHut, Hut, PlayerId.Neutral, 0)]);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "bridge",
                    53,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
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

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    private static void Fell(GameWorld world, int segment) => world.ChangeMap([new DestroyBridgeSegmentEdit(1, segment)], null);

    [Fact]
    public void An_engineer_in_the_hut_mends_its_bridge()
    {
        var simulation = Match();
        var world = simulation.World;
        var hut = world.BuildingAt(Hut)!;
        var engineer = world.SpawnUnit(Union, UnitType.Engineer, new CellPos(26, 24).Center, snapToSubcell: true);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, hut));

        Fell(world, 1);
        Fell(world, 2);
        world.Map.SetSegmentHp(world.Map.Bridges[0].Segments[0], 100);
        Assert.False(world.Map.CanEnter(new CellPos(31, 20), Locomotor.Track));
        Assert.Equal(EntryAction.RepairBridge, EntrySystem.ActionOf(world, engineer, hut));

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [engineer.Id], hut.Id));
        Run(simulation, 300, () => !engineer.IsAlive);
        Assert.False(engineer.IsAlive);
        Assert.All(world.Map.Bridges[0].Segments, segment => Assert.Equal(segment.Bridge.Type.SegmentHp, segment.Hp));
        Assert.True(world.Map.CanEnter(new CellPos(31, 20), Locomotor.Track));
        Assert.Equal(PlayerId.Neutral, hut.Owner);
    }

    [Fact]
    public void The_hut_cannot_be_hurt()
    {
        var simulation = Match();
        var world = simulation.World;
        var hut = world.BuildingAt(Hut)!;
        Assert.True(world.DamageAtCell(Hut, 5000, null));
        Assert.Equal(hut.MaxHp, hut.Hp);
        Assert.True(hut.IsAlive);
    }

    [Fact]
    public void A_fallen_segment_with_a_ship_on_its_water_stays_down()
    {
        var simulation = Match();
        var world = simulation.World;
        var hut = world.BuildingAt(Hut)!;
        Fell(world, 1);
        Fell(world, 2);
        world.SpawnUnit(Allied, UnitType.AllianceDestroyer, new CellPos(31, 20).Center, snapToSubcell: true);
        var engineer = world.SpawnUnit(Union, UnitType.Engineer, new CellPos(26, 24).Center, snapToSubcell: true);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [engineer.Id], hut.Id));
        Run(simulation, 300, () => !engineer.IsAlive);

        Assert.False(world.Map.Bridges[0].Segments[1].IsIntact);
        Assert.True(world.Map.Bridges[0].Segments[2].IsIntact);
    }
}
