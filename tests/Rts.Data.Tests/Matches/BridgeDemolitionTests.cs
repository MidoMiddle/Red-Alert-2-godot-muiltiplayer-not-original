using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Movement;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The commando's C4 on a bridge (RA2): ordered at a bridge segment she walks up and plants it, the segment falls, and an engineer in the
/// repair hut mends the bridge; with «Destroyable Bridges» off the bridge stands. By the game data, without the fog of war; two people
/// play, no bot.
/// </summary>
public sealed class BridgeDemolitionTests
{
    private const int Size = 64;
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos Hut = new(28, 22);
    private static readonly CellPos Middle = new(31, 20);

    private static GameSimulation Match(bool destroyableBridges = true)
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
                    "c4",
                    54,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, DestroyableBridges: destroyableBridges),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
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

    private static BridgeSegment SegmentAt(GameWorld world, CellPos cell) => world.Map.Bridges[0].Segments.First(segment => segment.Cells.Contains(cell));

    [Fact]
    public void The_commando_blows_a_bridge_segment_and_an_engineer_mends_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var tanya = world.SpawnUnit(Allied, UnitType.AllianceTanya, new CellPos(24, 23).Center, snapToSubcell: true);
        Assert.True(BridgeDemolition.CanBlow(world, tanya, Middle));
        Assert.False(BridgeDemolition.CanBlow(world, tanya, new CellPos(24, 20)));
        var segment = SegmentAt(world, Middle);

        simulation.Enqueue(new ForceAttackCommand(Allied, simulation.CurrentTick, 0, [tanya.Id], null, Middle.Center));
        Run(simulation, 600, () => !segment.IsIntact);

        Assert.False(segment.IsIntact);
        Assert.False(world.Map.CanEnter(Middle, Locomotor.Track));
        Assert.Equal(world.Map.Bridges[0].Segments.Count - 1, world.Map.Bridges[0].Segments.Count(standing => standing.IsIntact));
        Assert.True(tanya.IsAlive);
        Assert.Null(tanya.TargetPoint);
        Assert.Empty(world.BridgeBombs);
        Assert.False(BridgeDemolition.CanBlow(world, tanya, Middle));

        var hut = world.BuildingAt(Hut)!;
        var engineer = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(26, 25).Center, snapToSubcell: true);
        Assert.Equal(EntryAction.RepairBridge, EntrySystem.ActionOf(world, engineer, hut));
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [engineer.Id], hut.Id));
        Run(simulation, 300, () => !engineer.IsAlive);

        Assert.False(engineer.IsAlive);
        Assert.All(world.Map.Bridges[0].Segments, mended => Assert.True(mended.IsIntact));
        Assert.True(world.Map.CanEnter(Middle, Locomotor.Track));
    }

    [Fact]
    public void The_commando_plants_her_c4_from_its_range_not_her_pistols()
    {
        var simulation = Match();
        var world = simulation.World;
        var tanya = world.SpawnUnit(Allied, UnitType.AllianceTanya, new CellPos(22, 20).Center, snapToSubcell: true);
        simulation.Enqueue(new ForceAttackCommand(Allied, simulation.CurrentTick, 0, [tanya.Id], null, Middle.Center));
        Run(simulation, 600, () => world.BridgeBombs.Count > 0);

        Assert.Single(world.BridgeBombs);
        // Her pistols reach six cells; the C4 one and a half, so she stood by the bridge.
        Assert.True(Math.Abs(tanya.Position.ToCell().X - Middle.X) <= 2);
        Assert.True(SegmentAt(world, Middle).IsIntact);
    }

    [Fact]
    public void With_destroyable_bridges_off_the_bridge_stands()
    {
        var simulation = Match(destroyableBridges: false);
        var world = simulation.World;
        var tanya = world.SpawnUnit(Allied, UnitType.AllianceTanya, new CellPos(24, 23).Center, snapToSubcell: true);
        Assert.False(BridgeDemolition.CanBlow(world, tanya, Middle));

        simulation.Enqueue(new ForceAttackCommand(Allied, simulation.CurrentTick, 0, [tanya.Id], null, Middle.Center));
        Run(simulation, 600);

        Assert.All(world.Map.Bridges[0].Segments, standing => Assert.True(standing.IsIntact));
        Assert.Empty(world.BridgeBombs);
    }

    [Fact]
    public void A_soldier_without_c4_does_not_hurt_the_bridge()
    {
        var simulation = Match();
        var world = simulation.World;
        var gi = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(26, 20).Center, snapToSubcell: true);
        Assert.False(BridgeDemolition.CanBlow(world, gi, Middle));

        simulation.Enqueue(new ForceAttackCommand(Allied, simulation.CurrentTick, 0, [gi.Id], null, Middle.Center));
        Run(simulation, 300);

        Assert.All(world.Map.Bridges[0].Segments, standing => Assert.Equal(standing.Bridge.Type.SegmentHp, standing.Hp));
    }
}
