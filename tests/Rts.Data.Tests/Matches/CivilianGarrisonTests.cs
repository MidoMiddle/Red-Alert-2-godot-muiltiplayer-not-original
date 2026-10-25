using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Soldiers in the civilian buildings of the map (RA2 garrisons): the first one in makes an empty house his, more of his follow while
/// there is room, an enemy's house is closed; they fire from it; they come out on an order — the house is nobody's again — and, whole
/// as they went in, when it is critically damaged; then it takes none until an engineer mends it. Engineers, dogs and tech buildings
/// stay out of it. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class CivilianGarrisonTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos House = new(30, 30);
    private static readonly CellPos Derrick = new(40, 30);

    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        Array.Fill(land, (byte)LandType.Clear);
        var map = MapGrid.FromLayers(Size, Size, land, new byte[land.Length], default, null, [],
            [
                new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0),
                new BuildingState(BuildingKind.TechOilDerrick, Derrick, PlayerId.Neutral, 0),
            ]);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "garrison",
                    56,
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

    private static bool Near(CellPos cell, CellPos to) => Math.Abs(cell.X - to.X) <= 8 && Math.Abs(cell.Y - to.Y) <= 8;

    private static Unit Spawn(GameWorld world, PlayerId owner, UnitType type, int x, int y) =>
        world.SpawnUnit(owner, type, new CellPos(x, y).Center, snapToSubcell: true);

    [Fact]
    public void The_first_soldier_in_makes_the_house_his_and_his_others_follow_while_there_is_room()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        Assert.Equal(3, GarrisonSystem.Capacity(house));
        var gis = Enumerable.Range(0, 4).Select(i => Spawn(world, Allied, UnitType.AllianceRifleman, 26 + i, 34)).ToList();
        var conscript = Spawn(world, Union, UnitType.Conscript, 34, 34);
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, gis[0], house));
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, conscript, house));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gis[0].Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);
        Assert.Equal(Allied, house.Owner);
        Assert.False(gis[0].IsAlive);
        // An enemy's house is closed.
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, conscript, house));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 1, [gis[1].Id, gis[2].Id, gis[3].Id], house.Id));
        Run(simulation, 300);
        Assert.Equal(3, house.Occupants.Count);
        Assert.True(gis[3].IsAlive);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, gis[3], house));
    }

    [Fact]
    public void The_soldiers_fire_from_the_house_and_come_out_on_an_order_leaving_it_neutral()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gi.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);

        var conscript = Spawn(world, Union, UnitType.Conscript, 35, 31);
        Run(simulation, 300, () => !conscript.IsAlive);
        Assert.False(conscript.IsAlive);

        simulation.Enqueue(new EvacuateCommand(Allied, simulation.CurrentTick, 1, house.Id));
        Run(simulation, 2);
        Assert.Empty(house.Occupants);
        Assert.True(house.Owner.IsNeutral);
        Assert.Contains(world.Entities, entity => entity is Unit { IsAlive: true, Type: UnitType.AllianceRifleman } && entity.Owner == Allied);
    }

    [Fact]
    public void At_critical_damage_the_soldiers_come_out_whole_as_they_went_in_and_the_house_stands()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var hurt = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);
        var whole = Spawn(world, Allied, UnitType.AllianceRifleman, 29, 33);
        world.SetHp(hurt, 80, null);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [hurt.Id, whole.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 2);
        Assert.Equal(2, house.Occupants.Count);

        // Tanks shell the house: the soldiers inside take nothing, the house does.
        var tanks = Enumerable.Range(0, 4).Select(i => Spawn(world, Union, UnitType.HeavyTank, 36, 28 + i)).ToList();
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 1, [.. tanks.Select(t => t.Id)], house.Id));
        Run(simulation, 3000, () => house.Occupants.Count == 0);

        Assert.True(house.IsAlive);
        Assert.True(GarrisonSystem.Critical(house));
        Assert.True(house.Owner.IsNeutral);
        // Those by the house (the player's start has soldiers of its own).
        var outside = world.Entities.OfType<Unit>()
            .Where(unit => unit.IsAlive && unit.Owner == Allied && unit.Type == UnitType.AllianceRifleman && Near(unit.Position.ToCell(), House))
            .Select(unit => unit.Hp).OrderBy(hp => hp).ToList();
        Assert.Equal([80, whole.MaxHp], outside);
    }

    [Fact]
    public void A_critically_damaged_house_takes_no_soldiers_until_an_engineer_mends_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gi.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);

        world.SetHp(house, house.MaxHp / 5, null);
        Run(simulation, 1);
        Assert.Empty(house.Occupants);
        var outside = world.Entities.OfType<Unit>()
            .First(unit => unit.IsAlive && unit.Owner == Allied && unit.Type == UnitType.AllianceRifleman && Near(unit.Position.ToCell(), House));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, outside, house));

        // An engineer of any player mends it whole; then it takes soldiers again.
        var engineer = Spawn(world, Union, UnitType.Engineer, 33, 33);
        Assert.Equal(EntryAction.Mend, EntrySystem.ActionOf(world, engineer, house));
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 1, [engineer.Id], house.Id));
        Run(simulation, 300, () => !engineer.IsAlive);

        Assert.Equal(house.MaxHp, house.Hp);
        Assert.True(house.Owner.IsNeutral);
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, outside, house));
    }

    [Fact]
    public void Engineers_dogs_and_tech_buildings_stay_out_of_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var derrick = world.BuildingAt(Derrick)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);

        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, Spawn(world, Allied, UnitType.AllianceAttackDog, 27, 33), house));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, Spawn(world, Allied, UnitType.AllianceEngineer, 26, 33), house));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, gi, derrick));
        Assert.Equal(0, GarrisonSystem.Capacity(derrick));
    }
    [Fact]
    public void Only_the_gi_and_the_conscript_go_in_and_from_inside_the_house_sees()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var trooper = Spawn(world, Union, UnitType.TeslaTrooper, 28, 33);
        var ivan = Spawn(world, Union, UnitType.CrazyIvan, 29, 33);
        var conscript = Spawn(world, Union, UnitType.Conscript, 30, 33);

        // RA2 Occupier: the GI and the conscript alone.
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, trooper, house));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, ivan, house));
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, conscript, house));

        Assert.Null(VisibilitySystem.SightOf(world, house).Cell);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [conscript.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);
        // RA2: the house sees 4 cells round it while soldiers are inside.
        var (cell, radius) = VisibilitySystem.SightOf(world, house);
        Assert.NotNull(cell);
        Assert.True(radius > 0);
    }

    [Fact]
    public void A_derrick_that_falls_blows_up_round_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var derrick = world.BuildingAt(Derrick)!;
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 43, 30);
        world.SetHp(derrick, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(tank.Id, tank.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(derrick), derrick.Id));
        Run(simulation, 4);

        // RA2 OilExplosion: 600 at the centre, 4 cells round.
        Assert.False(derrick.IsAlive);
        Assert.True(!tank.IsAlive || tank.Hp < tank.MaxHp, "the blast hurt the tank beside it");
    }
    [Fact]
    public void A_house_that_falls_leaves_ruins_that_close_its_cells_for_good()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var hash = Rts.Simulation.Hashing.StateHasher.Compute(world);
        world.SetHp(house, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(house.Id, house.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(house), house.Id));
        Run(simulation, 3);

        // RA2: the ruins stay; nothing drives through them, nothing is built on them.
        Assert.False(house.IsAlive);
        Assert.Null(world.BuildingAt(House));
        Assert.True(world.Map.IsBlocked(House));
        Assert.False(world.Map.CanEnter(House, Locomotor.Track));
        Assert.False(world.Map.CanEnter(House, Locomotor.Foot));
        Assert.NotEqual(PlacementProblem.None, PlacementRules.CellProblem(world, House, world.Map.Heights.CellLevel(House)));
        Assert.NotEqual(hash, Rts.Simulation.Hashing.StateHasher.Compute(world));
        Run(simulation, 300);
        Assert.True(world.Map.IsBlocked(House));
    }
}
