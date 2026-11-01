using System.Collections.Immutable;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The civilians of the map (docs/map_elements/13_civilians): with the option they come out of the houses and by the benches of a town at
/// the start, nobody fires at them unordered, a tank crushes them, a vehicle near frightens them into a house — where they take no seat of
/// the garrison — or away into cover; they come out after 20 s of quiet, run out when soldiers move in and die when the house falls. By
/// the game data; two people play, no bot.
/// </summary>
public sealed class CivilianTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // Two houses of 2 x 2 and two benches, far from the starts at (6, 6) and (58, 58).
    private static readonly CellPos House = new(30, 30);
    private static readonly CellPos OtherHouse = new(30, 40);
    private static readonly CellPos Bench = new(34, 30);
    private static readonly CellPos OtherBench = new(36, 30);
    private const int Road = 28;

    private static GameSimulation Match(bool civilians = true, string climate = "urban", ulong seed = 13) =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "town",
                seed,
                new MatchOptions(StartingCredits: 50_000, FogOfWar: false, Civilians: civilians),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(6, 6)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(58, 58)),
                ],
                [new CellPos(6, 6), new CellPos(58, 58)])
            {
                Climate = climate,
            },
            Map()),
            GameDataFiles.Rules);

    private static MapGrid Map()
    {
        var types = GameDataFiles.Rules.MapTypes;
        types.TryGetObject("bench", out var bench);
        var land = new byte[64 * 64];
        System.Array.Fill(land, (byte)LandType.Clear);
        // A road along the north of the first house: the cars park beside it.
        for (var x = 20; x < 44; x++)
        {
            land[(Road * 64) + x] = (byte)LandType.Road;
        }

        return MapGrid.FromLayers(64, 64, land, new byte[land.Length], default,
            [MapObjectState.New(bench, Bench, 0), MapObjectState.New(bench, OtherBench, 0)], [],
            [new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0), new BuildingState(BuildingKind.NeutralHouseA, OtherHouse, PlayerId.Neutral, 0)],
            types: types);
    }

    private static List<Unit> Civilians(GameWorld world) =>
        [.. world.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Civilian is not null)];

    private static Building HouseAt(GameWorld world, CellPos cell) => world.Entities.OfType<Building>().Single(building => building.Cell == cell);

    private static void Step(GameSimulation simulation, int ticks, System.Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    // Only the civilian at the cell, the others taken off: one civilian alone by a house.
    private static Unit OnlyCivilianAt(GameWorld world, CellPos cell)
    {
        foreach (var other in Civilians(world))
        {
            other.Civilian = null;
            other.MarkDead();
        }

        world.RemoveDead();
        var civilian = CivilianSystem.Spawn(world, UnitType.CivMan, cell);
        civilian.Civilian!.Timer = 10_000;
        return civilian;
    }

    [Fact]
    public void With_the_option_civilians_of_the_climate_come_out_of_the_houses_and_by_the_benches()
    {
        var world = Match().World;
        var civilians = Civilians(world);

        // Two out of each house and three by each bench.
        Assert.Equal(2 * CivilianStart.PerHouse + 2 * CivilianStart.PerPoint, civilians.Count);
        Assert.All(civilians, civilian =>
        {
            Assert.True(civilian.Owner.IsNeutral);
            Assert.Contains(civilian.Type, new[] { UnitType.CivMan, UnitType.CivWoman });
            Assert.True(System.Math.Max(System.Math.Abs(civilian.Position.ToCell().X - 6), System.Math.Abs(civilian.Position.ToCell().Y - 6)) >= CivilianStart.StartCells);
            Assert.Equal(CivilianState.Rest, civilian.Civilian!.State);
        });

        // Without the option, or on a map of a climate none of them lives in, there are none.
        Assert.Empty(Civilians(Match(civilians: false).World));
        Assert.Empty(Civilians(Match(climate: "").World));
        Assert.All(Civilians(Match(climate: "winter").World), civilian => Assert.Contains(civilian.Type, new[] { UnitType.CivWinterMan, UnitType.CivWinterWoman }));
    }

    [Fact]
    public void Nobody_fires_at_a_civilian_unordered_a_forced_shot_kills_it_and_a_tank_crushes_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var civilian = OnlyCivilianAt(world, new CellPos(20, 20));
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(24, 20).Center);

        Step(simulation, 60);
        Assert.True(civilian.IsAlive);
        Assert.Null(tank.Target);

        // AP hurts a person a quarter (RA2): a few shells. Crouched in cover, it keeps its head down under fire and stays.
        civilian.Civilian!.State = CivilianState.Cover;
        simulation.Enqueue(new ForceAttackCommand(Union, simulation.CurrentTick, 0, [tank.Id], civilian.Id, civilian.Position));
        Step(simulation, 600, () => !civilian.IsAlive);
        Assert.False(civilian.IsAlive);

        var another = CivilianSystem.Spawn(world, UnitType.CivWoman, new CellPos(16, 24));
        another.Civilian!.Timer = 10_000;
        another.Civilian.State = CivilianState.Cover;
        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 1, [tank.Id], new CellPos(16, 24).Center));
        Step(simulation, 400, () => !another.IsAlive);
        Assert.False(another.IsAlive);
    }

    [Fact]
    public void A_vehicle_near_sends_a_civilian_running_into_a_house_where_it_takes_no_seat_of_the_soldiers()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = HouseAt(world, House);
        var civilian = OnlyCivilianAt(world, new CellPos(34, 34));
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(38, 34).Center);

        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 0, [tank.Id], new CellPos(36, 34).Center));
        Step(simulation, 30, () => civilian.Civilian!.State == CivilianState.Flee);
        Assert.Equal(CivilianState.Flee, civilian.Civilian!.State);
        Assert.Equal(civilian.Archetype.Speed * 3 / 2, civilian.Speed);
        Assert.Equal(house.Id, civilian.Civilian.Shelter);

        Step(simulation, 300, () => !civilian.IsAlive);
        Assert.False(civilian.IsAlive);
        var hidden = Assert.Single(house.Sheltered);
        Assert.Equal(UnitType.CivMan, hidden.Type);
        Assert.True(house.Owner.IsNeutral);
        Assert.Empty(house.Occupants);

        // Three soldiers still go in, all its seats; the civilian runs out.
        var riflemen = Enumerable.Range(0, 3).Select(i => world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(26 + i, 34).Center, snapToSubcell: true)).ToList();
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [.. riflemen.Select(unit => unit.Id)], house.Id));
        Step(simulation, 300, () => house.Occupants.Count == 3);
        Assert.Equal(3, house.Occupants.Count);
        Assert.Empty(house.Sheltered);
        var outside = Assert.Single(Civilians(world));
        Step(simulation, 2);
        Assert.Equal(CivilianState.Flee, outside.Civilian!.State);
    }

    [Fact]
    public void After_twenty_seconds_of_quiet_they_come_out_one_by_one_and_a_falling_house_kills_those_inside()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = HouseAt(world, House);
        OnlyCivilianAt(world, new CellPos(28, 28));
        // Two inside, as if they had hidden there.
        house.Sheltered.Add(new Occupant(world.Rules.Unit(UnitType.CivMan), 50));
        house.Sheltered.Add(new Occupant(world.Rules.Unit(UnitType.CivWoman), 40));
        var before = Civilians(world).Count;

        Step(simulation, CivilianSystem.QuietTicks - 1);
        Assert.Equal(2, house.Sheltered.Count);
        Step(simulation, 1);
        Assert.Single(house.Sheltered);
        Step(simulation, CivilianSystem.ExitTicks);
        Assert.Empty(house.Sheltered);
        Assert.Equal(before + 2, Civilians(world).Count);
        Assert.Contains(Civilians(world), civilian => civilian.Type == UnitType.CivWoman && civilian.Hp == 40);

        // Hidden again, they fall with the house.
        var other = HouseAt(world, OtherHouse);
        other.Sheltered.Add(new Occupant(world.Rules.Unit(UnitType.CivMan), 50));
        var count = Civilians(world).Count;
        var tanks = Enumerable.Range(0, 4).Select(i => world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(26 + i, 46).Center)).ToList();
        simulation.Enqueue(new ForceAttackCommand(Union, simulation.CurrentTick, 0, [.. tanks.Select(tank => tank.Id)], other.Id, Rts.Simulation.Combat.Aim.CentreOf(other)));
        Step(simulation, 3000, () => !other.IsAlive);
        Assert.False(other.IsAlive);
        Assert.Empty(other.Sheltered);
        Assert.True(Civilians(world).Count <= count);
    }

    [Fact]
    public void With_no_house_near_a_frightened_civilian_runs_away_crouches_and_strolls_again()
    {
        var simulation = Match();
        var world = simulation.World;
        var civilian = OnlyCivilianAt(world, new CellPos(48, 14));
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(52, 14).Center);

        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 0, [tank.Id], new CellPos(50, 14).Center));
        Step(simulation, 30, () => civilian.Civilian!.State == CivilianState.Flee);
        Assert.Null(civilian.Civilian!.Shelter);
        Step(simulation, 300, () => civilian.Civilian!.State == CivilianState.Cover);
        Assert.Equal(CivilianState.Cover, civilian.Civilian!.State);
        Assert.Equal(civilian.Archetype.Speed, civilian.Speed);
        Assert.True(civilian.Position.X < new CellPos(48, 14).Center.X, "it ran away from the tank, west");

        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 1, [tank.Id], new CellPos(58, 30).Center));
        Step(simulation, CivilianSystem.CoverTicks + 60, () => civilian.Civilian!.State != CivilianState.Cover);
        Assert.NotEqual(CivilianState.Cover, civilian.Civilian!.State);
    }

    [Fact]
    public void Cars_park_beside_the_road_by_their_points_never_drive_and_burn_when_shot()
    {
        var simulation = Match();
        var world = simulation.World;
        var cars = world.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Archetype.Category == UnitCategory.Vehicle && unit.Owner.IsNeutral).ToList();

        // One by each house whose footprint has a road within its reach: the first house, by the road.
        var car = Assert.Single(cars);
        Assert.Equal(UnitType.CivCar, car.Type);
        Assert.Null(car.Civilian);
        var cell = car.Position.ToCell();
        Assert.NotEqual(LandType.Road, world.Map.LandAt(cell));
        Assert.Equal(Road + 1, cell.Y);

        var at = car.Position;
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(24, 33).Center);
        Step(simulation, 300);
        Assert.Equal(at, car.Position);
        Assert.Null(tank.Target);

        simulation.Enqueue(new ForceAttackCommand(Union, simulation.CurrentTick, 0, [tank.Id], car.Id, car.Position));
        Step(simulation, 600, () => !car.IsAlive);
        Assert.False(car.IsAlive);
    }

    [Fact]
    public void A_frightened_animal_runs_twice_as_fast_and_hides_in_no_house()
    {
        var simulation = Match();
        var world = simulation.World;
        OnlyCivilianAt(world, new CellPos(10, 50));
        var cow = CivilianSystem.Spawn(world, UnitType.CivCow, new CellPos(34, 34));
        cow.Civilian!.Timer = 10_000;
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(38, 34).Center);

        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 0, [tank.Id], new CellPos(36, 34).Center));
        Step(simulation, 30, () => cow.Civilian.State == CivilianState.Flee);
        Assert.Equal(CivilianState.Flee, cow.Civilian.State);
        Assert.Equal(cow.Archetype.Speed * 2, cow.Speed);
        // The house is 3 cells off, but a cow does not go in.
        Assert.Null(cow.Civilian.Shelter);
        Step(simulation, 300, () => cow.Civilian.State == CivilianState.Cover);
        Assert.True(cow.IsAlive);
        Assert.Empty(HouseAt(world, House).Sheltered);
    }

    [Fact]
    public void Two_runs_of_one_seed_are_the_same_and_strolls_keep_by_the_gathering_points()
    {
        ulong Run()
        {
            var simulation = Match(seed: 99);
            var tank = simulation.World.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(20, 30).Center);
            simulation.Enqueue(new MoveCommand(Union, 0, 0, [tank.Id], new CellPos(40, 30).Center));
            Step(simulation, 1500);
            Assert.All(Civilians(simulation.World), civilian =>
                Assert.True(System.Math.Max(System.Math.Abs(civilian.Position.ToCell().X - civilian.Civilian!.Anchor.X),
                    System.Math.Abs(civilian.Position.ToCell().Y - civilian.Civilian.Anchor.Y)) <= CivilianSystem.WanderCells + CivilianSystem.RunCells + 2));
            return Rts.Simulation.Hashing.StateHasher.Compute(simulation.World);
        }

        Assert.Equal(Run(), Run());
    }
}
