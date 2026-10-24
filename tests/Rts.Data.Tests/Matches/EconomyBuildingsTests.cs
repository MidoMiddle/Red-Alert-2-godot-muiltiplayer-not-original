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
    public void The_heavy_industry_makes_vehicles_cheaper_and_two_do_not_add_up()
    {
        var simulation = Match();
        var world = simulation.World;
        var rules = world.Rules.Database;
        var tank = rules.IndexOfUnit("heavy_tank");
        var conscript = rules.IndexOfUnit("conscript");
        Build(world, Union, "industrial_plant", 30, 30);
        Build(world, Union, "industrial_plant", 34, 30);
        TechTree.Update(world);
        var player = world.Player(Union);

        Assert.Equal(25, player.VehicleDiscountPercent);
        Assert.Equal(675, ProductionRules.CostOf(rules, player, ProductionKind.Unit, tank));
        Assert.Equal(100, ProductionRules.CostOf(rules, player, ProductionKind.Unit, conscript));

        // A whole tank from the war factory costs the discounted price.
        Build(world, Union, "construction_yard", 6, 6);
        Build(world, Union, "nuclear_reactor", 40, 6);
        Build(world, Union, "refinery", 6, 10);
        Build(world, Union, "war_factory", 12, 10);
        TechTree.Update(world);
        var before = player.Credits;
        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, tank, 1));
        var built = false;
        for (var tick = 0; tick < 1500 && !built; tick++)
        {
            simulation.Step();
            built = world.Entities.OfType<Unit>().Any(unit => unit.Owner == Union && unit.Type == UnitType.HeavyTank);
        }

        Assert.True(built);
        Assert.Equal(675, before - player.Credits);
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
}
