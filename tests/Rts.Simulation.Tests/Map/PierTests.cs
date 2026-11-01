using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>Piers (docs/beach_elements/14_pier): a low bridge from a shore out into the water, a road for the ground, closed to ships.</summary>
public sealed class PierTests
{
    private const int Width = 16;
    private const int Height = 9;
    private static readonly PlayerId Blue = new(1);

    /// <summary>16 x 9: land on columns 0…5, the sea beyond, and a pier from 6,4 out to 9,4 unless <paramref name="pier"/> is false.</summary>
    private static MapGrid Shore(bool pier = true)
    {
        var land = new byte[Width * Height];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % Width >= 6 ? LandType.Deep : LandType.Clear);
        }

        List<BridgeState> bridges = pier ? [Pier(new CellPos(6, 4), new CellPos(9, 4))] : [];
        return MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null, bridges, types: TestRules.Standard.MapTypes);
    }

    private static BridgeState Pier(CellPos from, CellPos to) => BridgeState.New(TestRules.Standard.MapTypes.Get(BridgeKind.PierWood), from, to);

    private static void Step(GameWorld world, List<SimEvent> events)
    {
        world.PathRequests.Process(world, events);
        MovementSystem.Update(world, events);
        world.RemoveDead();
        world.CurrentTick++;
        ReservationTests.AssertOccupancy(world);
    }

    [Fact]
    public void A_pier_is_a_road_a_cell_wide_on_the_water_closed_to_ships_which_go_round_its_end()
    {
        var map = Shore();

        Assert.True(map.Types.Get(BridgeKind.PierWood).Pier);
        Assert.Single(map.Bridges[0].Segments[0].Cells);
        Assert.Equal((LandType.Deep, LandType.Road), (map.LandAt(new CellPos(9, 4)), map.SurfaceAt(new CellPos(9, 4))));
        Assert.False(map.CanEnter(new CellPos(8, 4), Locomotor.Naval));
        Assert.True(map.CanEnter(new CellPos(8, 3), Locomotor.Naval));
        Assert.False(map.CanEnter(new CellPos(8, 3), Locomotor.Foot));

        var path = AStar.FindPath(map, new CellPos(7, 1), new CellPos(7, 7), Locomotor.Naval);
        Assert.Equal(PathStatus.Found, path.Status);
        Assert.DoesNotContain(path.Cells, cell => cell.Y == 4 && cell.X <= 9);
    }

    [Fact]
    public void A_conscript_walks_out_to_the_end_of_the_pier()
    {
        var world = new GameWorld(seed: 1, Shore(), TestRules.Standard);
        var unit = world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(2, 4).Center);
        CommandProcessor.Apply(world, new MoveCommand(Blue, 0, 0, [unit.Id], new CellPos(9, 4).Center));

        for (var ticks = 0; unit.MoveTarget is not null || unit.PathPending; ticks++)
        {
            Assert.True(ticks < 3000, "The unit never stopped.");
            Step(world, []);
        }

        Assert.Equal(new CellPos(9, 4), unit.Position.ToCell());
    }

    [Fact]
    public void A_fallen_segment_is_water_again_and_drowns_whoever_stands_on_it()
    {
        var world = new GameWorld(seed: 1, Shore(), TestRules.Standard);
        var cell = new CellPos(8, 4);
        var conscript = world.SpawnUnit(Blue, UnitType.Conscript, cell.Center, snapToSubcell: true);
        var beyond = world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(9, 4).Center, snapToSubcell: true);
        var events = new List<SimEvent>();

        Assert.True(world.DamageAtCell(cell, 400, events));

        Assert.False(conscript.IsAlive);
        Assert.True(beyond.IsAlive);
        Assert.True(world.Map.CanEnter(cell, Locomotor.Naval));
        Assert.Contains(new MapChanged(0, cell, cell), events);
        Assert.Equal(new[] { 400, 400, 0, 400 }, world.Map.Bridges[0].Segments.Select(s => s.Hp));
        Assert.True(world.Map.ContentEquals(world.Map.Clone()));
    }

    [Theory]
    [InlineData(6, 4, 6, 4, "a pier runs two cells or more")]
    [InlineData(7, 4, 9, 4, "no shore at 6,4, a pier runs from a shore into the water")]
    [InlineData(9, 4, 6, 4, "no shore at 10,4")]
    [InlineData(5, 4, 8, 4, "cell 5,4 is not water")]
    public void A_pier_runs_from_a_shore_out_into_the_water(int x1, int y1, int x2, int y2, string problem)
    {
        var land = Shore(pier: false).LandBytes();

        var error = Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null,
            [Pier(new CellPos(x1, y1), new CellPos(x2, y2))], types: TestRules.Standard.MapTypes));

        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void A_pier_ends_in_the_water()
    {
        var land = Shore(pier: false).LandBytes();
        land[4 * Width + 10] = (byte)LandType.Clear;

        var error = Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default, null,
            [Pier(new CellPos(6, 4), new CellPos(9, 4))], types: TestRules.Standard.MapTypes));

        Assert.Contains("no water at 10,4, a pier ends in the water", error.Message);
    }

    [Fact]
    public void No_repair_hut_belongs_to_a_pier()
    {
        Assert.Null(BridgeRepair.BridgeOf(Shore(), new CellPos(5, 3)));
    }
}
