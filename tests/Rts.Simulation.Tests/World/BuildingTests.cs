using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>The minimal building of M7 (docs/implementation/M7_maps.md, 4.8, step 7): from the map, blocking, damage, destruction.</summary>
public sealed class BuildingTests
{
    private const int Width = 16;
    private const int Height = 10;
    private static readonly PlayerId Blue = new(1);
    private static readonly CellPos House = new(7, 4);

    private static byte[] ClearLand() => Enumerable.Repeat((byte)LandType.Clear, Width * Height).ToArray();

    /// <summary>16 x 10 of clear land with a neutral house at (7, 4)…(8, 5) and whatever else is given.</summary>
    private static MapGrid Village(params BuildingState[] more) =>
        MapGrid.FromLayers(Width, Height, ClearLand(), new byte[Width * Height], default, null, null,
            [new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 2), .. more]);

    private static void Order(GameWorld world, Unit unit, CellPos target) =>
        CommandProcessor.Apply(world, new MoveCommand(unit.Owner, world.CurrentTick, 0, [unit.Id], target.Center));

    private static HashSet<CellPos> RunToStop(GameWorld world, Unit unit)
    {
        var events = new List<SimEvent>();
        var visited = new HashSet<CellPos>();
        for (var ticks = 0; unit.MoveTarget is not null || unit.PathPending || unit.HoldTicks > 0; ticks++)
        {
            Assert.True(ticks < 3000, "The unit never stopped.");
            world.PathRequests.Process(world, events);
            MovementSystem.Update(world, events);
            world.RemoveDead();
            world.CurrentTick++;
            ReservationTests.AssertOccupancy(world);
            visited.Add(unit.Position.ToCell());
        }

        return visited;
    }

    private static IEnumerable<CellPos> Footprint(CellPos corner, int width, int height) =>
        from y in Enumerable.Range(corner.Y, height) from x in Enumerable.Range(corner.X, width) select new CellPos(x, y);

    [Fact]
    public void The_buildings_of_the_map_are_the_first_entities_of_the_world()
    {
        var world = new GameWorld(seed: 1, Village(new BuildingState(BuildingKind.TechOilDerrick, new CellPos(1, 1), new PlayerId(2), 0)), TestRules.Standard);

        var house = Assert.IsType<Building>(world.Entities[0]);
        var derrick = Assert.IsType<Building>(world.Entities[1]);
        Assert.Equal((new EntityId(1), PlayerId.Neutral, BuildingKind.NeutralHouseA, House, 2, 800), (house.Id, house.Owner, house.Kind, house.Cell, house.Facing, house.Hp));
        Assert.Equal((new EntityId(2), new PlayerId(2), 1000), (derrick.Id, derrick.Owner, derrick.Hp));
        Assert.True(derrick.Type.Capturable);
        Assert.Equal(3, house.Type.Garrison);
        Assert.Equal(new EntityId(3), world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(12, 8).Center).Id);
        Assert.Same(house, world.BuildingAt(new CellPos(8, 5)));
        Assert.Null(world.BuildingAt(new CellPos(9, 5)));
    }

    [Fact]
    public void A_building_closes_its_footprint_to_everybody_and_takes_no_orders()
    {
        var world = new GameWorld(seed: 1, Village(), TestRules.Standard);
        Assert.All(Footprint(House, 2, 2), cell =>
        {
            Assert.True(world.Map.HasBuilding(cell));
            Assert.All(Enum.GetValues<Locomotor>(), l => Assert.False(world.Map.CanEnter(cell, l)));
        });
        Assert.True(world.Map.CanEnter(new CellPos(9, 4), Locomotor.Foot));

        Assert.False(CommandProcessor.Apply(world, new SpawnTestUnitCommand(Blue, 0, 0, UnitType.Conscript, new CellPos(8, 4).Center)));
        Assert.False(CommandProcessor.Apply(world, new MoveCommand(PlayerId.Neutral, 0, 0, [new EntityId(1)], new CellPos(2, 2).Center)));

        var tank = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(2, 4).Center);
        Order(world, tank, new CellPos(13, 5));
        var visited = RunToStop(world, tank);

        Assert.Equal(new CellPos(13, 5), tank.Position.ToCell());
        Assert.DoesNotContain(visited, world.Map.HasBuilding);
    }

    [Fact]
    public void Blows_take_hit_points_until_the_building_falls_and_its_cells_open()
    {
        var land = ClearLand();
        var world = new GameWorld(seed: 1, MapGrid.FromLayers(Width, Height, land, new byte[land.Length], default,
            [MapObjectState.New(MapObjectKind.PineTree, new CellPos(6, 4), 0)], null,
            [new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0)]), TestRules.Standard);
        var house = (Building)world.Entities[0];
        var events = new List<SimEvent>();

        // The tree next door takes its own blow: the building is chosen by the cell, nothing else.
        world.DamageAtCell(new CellPos(6, 4), 100, events);
        Assert.Equal((800, 300), (house.Hp, world.Map.Objects[0].Hp));

        var before = StateHasher.Compute(world);
        for (var blow = 0; blow < 3; blow++)
        {
            Assert.True(CommandProcessor.Apply(world, new DamageMapObjectCommand(Blue, 0, (uint)blow, new CellPos(8, 5), 200), events));
        }

        Assert.Equal(new[] { 600, 400, 200 }, events.OfType<BuildingDamaged>().Select(e => e.Hp));
        Assert.NotEqual(before, StateHasher.Compute(world));
        Assert.True(world.Map.HasBuilding(House));

        events.Clear();
        Assert.True(world.DamageAtCell(House, 200, events));

        Assert.False(house.IsAlive);
        Assert.Equal(0, house.Hp);
        Assert.Contains(new BuildingDestroyed(0, house.Id, BuildingKind.NeutralHouseA, House), events);

        // M4 step 9: the cells stay closed on the tick it fell and open at its end.
        Assert.True(world.Map.HasBuilding(House));
        Assert.DoesNotContain(events, e => e is MapChanged);
        DeathSystem.Update(world, events);
        Assert.Contains(new MapChanged(0, House, new CellPos(8, 5)), events);
        Assert.All(Footprint(House, 2, 2), cell => Assert.True(world.Map.CanEnter(cell, Locomotor.Wheel)));
        Assert.Null(world.BuildingAt(House));
        Assert.False(world.DamageAtCell(House, 200, events));

        world.RemoveDead();
        Assert.DoesNotContain(world.Entities, e => e.Id == house.Id);
        // Along row 5: the tree still stands on row 4.
        var jeep = world.SpawnUnit(Blue, UnitType.SupportVehicle, new CellPos(2, 5).Center);
        Order(world, jeep, new CellPos(13, 5));
        Assert.Contains(new CellPos(8, 5), RunToStop(world, jeep));
    }

    [Theory]
    [InlineData(BuildingKind.NeutralHouseA, 15, 4, 0, 0, "neutral_house_a at 15,4 reaches outside the 16x10 map")]
    [InlineData(BuildingKind.NeutralHouseA, 2, 2, 0, 0, "cell 3,3 is Shallow")]
    [InlineData(BuildingKind.NeutralShed, 12, 1, 0, 0, "cell 12,1 is Rock")]
    [InlineData(BuildingKind.TechHospital, 10, 6, 0, 0, "cell 12,8 is not flat ground")]
    [InlineData(BuildingKind.NeutralShed, 8, 5, 0, 0, "cell 8,5 is taken")]
    [InlineData(BuildingKind.NeutralShed, 0, 9, 0, 0, "cell 0,9 is taken")]
    [InlineData(BuildingKind.NeutralShed, 0, 0, 9, 0, "owner 9 must be 0…8")]
    [InlineData(BuildingKind.NeutralShed, 0, 0, 0, 8, "facing 8 0…7")]
    [InlineData((BuildingKind)0, 0, 0, 0, 0, "unknown building kind 0")]
    public void A_building_needs_flat_free_dry_ground_on_the_map(BuildingKind kind, int x, int y, int owner, int facing, string problem)
    {
        var land = ClearLand();
        land[3 * Width + 3] = (byte)LandType.Shallow;
        land[1 * Width + 12] = (byte)LandType.Rock;
        var heights = new byte[(Width + 1) * (Height + 1)];
        heights[9 * (Width + 1) + 13] = 1;

        var error = Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Width, Height, land, new byte[land.Length], heights,
            [MapObjectState.New(MapObjectKind.LampPost, new CellPos(0, 9), 0)], null,
            [new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0), new BuildingState(kind, new CellPos(x, y), new PlayerId(owner), facing)]));

        Assert.Contains(problem, error.Message);
    }

    [Fact]
    public void A_footprint_lies_along_x_and_does_not_turn_with_the_facing()
    {
        var turned = new BuildingState(BuildingKind.TechAirfield, new CellPos(10, 1), PlayerId.Neutral, 2);
        var world = new GameWorld(seed: 1, Village(turned), TestRules.Standard);
        var airfield = (Building)world.Entities[1];

        Assert.All(Footprint(new CellPos(10, 1), 4, 3), cell => Assert.True(airfield.Covers(cell) && world.Map.HasBuilding(cell)));
        Assert.Equal(12, Enumerable.Range(0, Width * Height).Count(i => airfield.Covers(new CellPos(i % Width, i / Width))));
        Assert.False(world.Map.HasBuilding(new CellPos(10, 4)));
    }

    [Theory]
    [InlineData("building neutral_house_a 1 0 0 9", "line 3, column 26: owner 0 must be 0…8 and facing 9 0…7")]
    [InlineData("building neutral_castle 1 0", "line 3, column 10: unknown building 'neutral_castle'")]
    [InlineData("building neutral_shed 2 0 1 2 3", "line 3, column 1: expected 'building <type> <x> <y> [owner] [facing]'")]
    [InlineData("building neutral_house_a 1 1", "line 3, column 26: neutral_house_a at 1,1 reaches outside the 6x2 map")]
    public void A_bad_building_line_names_its_line(string line, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => TestMapParser.Parse($". . . . . .\n. . . . . .\n{line}\n"));

        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void Building_lines_take_an_owner_and_a_facing()
    {
        var map = TestMapParser.Parse(". . . . . .\n. . . . . .\nbuilding neutral_house_a 1 0 2 5\nbuilding neutral_shed 4 1\n").Grid;

        Assert.Equal(
            new[] { new BuildingState(BuildingKind.NeutralHouseA, new CellPos(1, 0), new PlayerId(2), 5), new BuildingState(BuildingKind.NeutralShed, new CellPos(4, 1), PlayerId.Neutral, 0) },
            map.StartBuildings);
    }

    [Fact]
    public void A_copy_of_the_map_keeps_a_fallen_building_down()
    {
        var world = new GameWorld(seed: 1, Village(), TestRules.Standard);
        var pristine = world.Map.Clone();

        world.DamageAtCell(House, 800, null);
        DeathSystem.Update(world, null);
        var copy = world.Map.Clone();

        Assert.True(copy.ContentEquals(world.Map));
        Assert.False(copy.ContentEquals(pristine));
        Assert.False(copy.HasBuilding(House));
        Assert.True(copy.CanEnter(House, Locomotor.Foot));
    }

    [Fact]
    public void A_match_that_knocks_down_a_building_survives_the_replay()
    {
        var start = Village(new BuildingState(BuildingKind.TechHospital, new CellPos(11, 6), new PlayerId(1), 4));
        var simulation = new GameSimulation(new SimulationSettings(seed: 5, start), TestRules.Standard);
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 0, UnitType.HeavyTank, new CellPos(2, 4).Center));
        simulation.Enqueue(new MoveCommand(Blue, 1, 1, [new EntityId(3)], new CellPos(14, 4).Center));
        for (var tick = 20; tick < 24; tick++)
        {
            simulation.Enqueue(new DamageMapObjectCommand(Blue, tick, (uint)tick, new CellPos(8, 4), 200));
        }

        var destroyed = false;
        for (var tick = 0; tick < 300; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
            destroyed |= simulation.LastTickEvents.OfType<BuildingDestroyed>().Any();
        }

        Assert.True(destroyed);
        Assert.Equal(2, simulation.World.Entities.Count);
        var bytes = ReplayFormat.Write(recorder.Build());
        var read = ReplayFormat.Read(bytes);

        Assert.True(read.Settings.Map!.ContentEquals(start));
        Assert.Equal(start.StartBuildings, read.Settings.Map.StartBuildings);
        Assert.Equal(bytes, ReplayFormat.Write(read));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(read, TestRules.Standard).Status);
    }

    [Theory]
    [InlineData(BuildingKind.NeutralFuelTank, 1, 1, 800, 0)]
    [InlineData(BuildingKind.NeutralSmokestack, 1, 1, 1500, 0)]
    [InlineData(BuildingKind.NeutralCoolingTower, 2, 2, 1500, 0)]
    [InlineData(BuildingKind.NeutralFactoryHall, 4, 3, 800, 6)]
    [InlineData(BuildingKind.NeutralBoilerHouse, 2, 2, 800, 2)]
    public void An_industrial_building_has_its_footprint_and_closes_it_to_every_ground_mover(
        BuildingKind kind, int width, int height, int hp, int garrison)
    {
        var type = BuildingTypes.Get(kind);
        Assert.Equal((width, height, hp, garrison, false), (type.Width, type.Height, type.MaxHp, type.Garrison, type.Capturable));

        var map = MapGrid.FromLayers(Width, Height, ClearLand(), new byte[Width * Height], default, null, null,
            [new BuildingState(kind, new CellPos(4, 4), PlayerId.Neutral, 0)]);
        for (var y = 4; y < 4 + height; y++)
        {
            for (var x = 4; x < 4 + width; x++)
            {
                var cell = new CellPos(x, y);
                Assert.True(map.HasBuilding(cell));
                Assert.False(map.CanEnter(cell, Locomotor.Foot));
                Assert.False(map.CanEnter(cell, Locomotor.Track));
                Assert.False(map.CanEnter(cell, Locomotor.Wheel));
            }
        }
    }
}
