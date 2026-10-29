using Rts.Client.Hud;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The economy and the rear (stage 2c of filling the game): the workshops repair the vehicles beside them, the heavy industry makes
/// vehicles cheaper, the ore refining annex makes the ore richer, and the atomic plant blows up when a shot brings it down. By the
/// game data; two people play, no bot.
/// </summary>
public sealed class EconomyBuildingsTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match(int credits = 50_000) =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "rear",
                9,
                new MatchOptions(StartingCredits: credits),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(10, 10)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(55, 55)),
                ],
                [new CellPos(10, 10), new CellPos(55, 55)]),
            new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static void Step(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void The_workshop_repairs_a_vehicle_standing_beside_it_for_its_share_of_the_cost()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "service_depot", 20, 20);
        var beside = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(23, 21).Center);
        var far = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(30, 30).Center);
        var soldier = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(19, 21).Center);
        world.SetHp(beside, 100, null);
        world.SetHp(far, 100, null);
        world.SetHp(soldier, 50, null);
        var credits = world.Player(Union).Credits;

        Step(simulation, 30);

        Assert.True(beside.Hp > 100, "repaired beside the workshop");
        Assert.True(world.Player(Union).Credits < credits, "for credits");
        Assert.Equal(100, far.Hp);
        Assert.Equal(50, soldier.Hp);
    }

    [Fact]
    public void The_workshop_says_it_repairs_while_a_hurt_vehicle_stands_beside_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var depot = Build(world, Allied, "alliance_service_depot", 40, 40);
        Assert.False(RepairSystem.IsRepairing(world, depot), "nothing beside it");

        var tank = world.SpawnUnit(Allied, UnitType.AllianceLightTank, new CellPos(43, 41).Center);
        Assert.False(RepairSystem.IsRepairing(world, depot), "a whole tank");

        world.SetHp(tank, 100, null);
        Assert.True(RepairSystem.IsRepairing(world, depot), "a hurt tank beside it");

        var soldier = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(39, 41).Center);
        world.SetHp(soldier, 50, null);
        world.SetHp(tank, tank.MaxHp, null);
        Assert.False(RepairSystem.IsRepairing(world, depot), "a soldier is not repaired");
    }

    [Fact]
    public void Without_the_credits_the_workshop_waits()
    {
        var simulation = Match(credits: 0);
        var world = simulation.World;
        Build(world, Union, "service_depot", 20, 20);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(23, 21).Center);
        world.SetHp(tank, 100, null);

        Step(simulation, 30);

        Assert.Equal(100, tank.Hp);
    }

    [Fact]
    public void The_ore_refining_annex_adds_its_bonus_to_the_ore_on_top_of_the_level()
    {
        var world = Match().World;
        Build(world, Allied, "alliance_ore_purifier", 40, 40);
        TechTree.Update(world);
        var player = world.Player(Allied);
        var credits = player.Credits;

        HarvesterSystem.Earn(player, 100);
        Assert.Equal(25, player.BuildingIncomeBonusPercent);
        Assert.Equal(credits + 125, player.Credits);

        player.Modifiers = new PlayerModifiers(40, 0, false);
        HarvesterSystem.Earn(player, 100);
        Assert.Equal(credits + 125 + 165, player.Credits);
    }

    [Fact]
    public void The_atomic_plant_brought_down_by_a_shot_blows_up_and_a_sold_one_does_not()
    {
        // Unarmed people beside it, of both players: only the blast can hurt them.
        foreach (var sold in new[] { false, true })
        {
            var simulation = Match();
            var world = simulation.World;
            var plant = Build(world, Union, "nuclear_reactor", 30, 30);
            var own = world.SpawnUnit(Union, UnitType.Engineer, new CellPos(35, 31).Center);
            var other = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(33, 35).Center);
            world.SetHp(plant, 1, null);
            var (ownHp, otherHp) = (own.Hp, other.Hp);

            if (sold)
            {
                simulation.Enqueue(new SellCommand(Union, simulation.CurrentTick, 0, plant.Id));
            }
            else
            {
                // One shell of a tank of the Alliance: the plant falls to a shot.
                var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
                world.Hits.Add(new Rts.Simulation.Combat.Hit(plant.Id, plant.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(plant), plant.Id));
            }

            Step(simulation, 60);

            Assert.False(plant.IsAlive, $"sold {sold}: the plant is gone");
            if (sold)
            {
                Assert.Equal((ownHp, otherHp), (own.Hp, other.Hp));
            }
            else
            {
                Assert.True(!own.IsAlive || own.Hp < ownHp, "its owner's engineer is hurt too");
                Assert.True(!other.IsAlive || other.Hp < otherHp, "the other engineer is hurt");
            }
        }
    }
    [Fact]
    public void The_atomic_plant_blows_up_with_half_the_missile()
    {
        // RA2: the nuclear reactor's DeathWeapon is NukePayload at half — 300 at the centre, the nuke's 10 cells, 2% at the edge, toxin.
        var database = GameDataFiles.Rules.Database;
        database.TryGetBuilding("nuclear_reactor", out var reactor);
        var blast = database.Weapons[database.IndexOfWeapon(reactor.DeathWeapon)];
        var missile = database.Weapons[database.IndexOfWeapon("nuclear_missile")];

        Assert.Equal(missile.Damage / 2, blast.Damage);
        Assert.Equal(missile.Warhead, blast.Warhead);
        Assert.Equal("nuke", blast.Warhead);
    }
    [Fact]
    public void An_ore_purifier_adds_a_quarter_and_works_only_with_power()
    {
        var world = Match().World;
        Build(world, Allied, "alliance_ore_purifier", 40, 40);
        PowerSystem.Update(world, null);
        TechTree.Update(world);
        Assert.True(world.Player(Allied).LowPower);
        Assert.Equal(0, world.Player(Allied).BuildingIncomeBonusPercent);

        Build(world, Allied, "alliance_power_plant", 40, 46);
        PowerSystem.Update(world, null);
        TechTree.Update(world);
        // RA2 PurifierBonus.
        Assert.Equal(25, world.Player(Allied).BuildingIncomeBonusPercent);
    }

    [Fact]
    public void A_player_may_have_one_ore_purifier_and_the_sidebar_greys_out_the_second()
    {
        // The owner (2026-10-29): one purifier, its bonus with it; the square stays, greyed, and says why.
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_construction_yard", 40, 40);
        Build(world, Allied, "alliance_power_plant", 44, 40);
        Build(world, Allied, "alliance_power_plant", 46, 40);
        Build(world, Allied, "alliance_battle_lab", 40, 44);
        var purifier = world.Rules.Database.IndexOfBuilding("alliance_ore_purifier");
        Step(simulation, 2);
        Assert.Equal(CameoState.Available, Square(world, purifier).State);

        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 0, ProductionKind.Building, purifier, 1));
        Step(simulation, 2);
        Assert.Equal(CameoState.Building, Square(world, purifier).State);

        // Cancelled, and one built: the player has one of one.
        simulation.Enqueue(new CancelProductionCommand(Allied, simulation.CurrentTick, 1, ProductionKind.Building, purifier));
        Step(simulation, 2);
        Build(world, Allied, "alliance_ore_purifier", 44, 44);
        Step(simulation, 2);
        var square = Square(world, purifier);
        Assert.Equal((CameoState.Unavailable, true, ""), (square.State, square.AtLimit, square.Missing));
        Assert.Equal(CameoActionKind.None, SidebarModel.LeftClick(square, shift: false).Kind);

        // An order sent all the same is refused.
        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 2, ProductionKind.Building, purifier, 1));
        Step(simulation, 2);
        Assert.Empty(world.Player(Allied).Queue(Rts.Simulation.Rules.ProductionCategory.Building));
        // The other buildings of the tab are free.
        Assert.Equal(CameoState.Available, Square(world, world.Rules.Database.IndexOfBuilding("alliance_power_plant")).State);
    }

    private static Cameo Square(GameWorld world, int building) =>
        SidebarModel.Cameos(world, Allied, BuildGroup.Buildings).Single(cameo => cameo.DefIndex == building);

    [Fact]
    public void A_damaged_power_plant_gives_power_by_its_hit_points()
    {
        var world = Match().World;
        var plant = Build(world, Allied, "alliance_power_plant", 40, 40);
        PowerSystem.Update(world, null);
        Assert.Equal(200, world.Player(Allied).PowerOutput);

        world.SetHp(plant, plant.MaxHp / 2, null);
        PowerSystem.Update(world, null);
        Assert.Equal(100, world.Player(Allied).PowerOutput);
    }

    [Fact]
    public void The_soviet_refinery_is_sold_for_its_soylent_and_the_allied_one_for_half_its_cost()
    {
        var simulation = Match();
        var world = simulation.World;
        var soviet = Build(world, Union, "refinery", 20, 20);
        var allied = Build(world, Allied, "alliance_ore_refinery", 40, 20);
        var (union, alliance) = (world.Player(Union).Credits, world.Player(Allied).Credits);
        simulation.Enqueue(new SellCommand(Union, simulation.CurrentTick, 0, soviet.Id));
        simulation.Enqueue(new SellCommand(Allied, simulation.CurrentTick, 0, allied.Id));
        Step(simulation, 2);

        // RA2 Soylent=300 for the Soviet refinery; the free harvester stays.
        Assert.Equal(union + 300, world.Player(Union).Credits);
        Assert.Equal(alliance + (allied.Def!.Cost / 2), world.Player(Allied).Credits);
    }
}
