using System.Collections.Immutable;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Aircraft (stage 2b of filling the game): built by the air command and, the airship, by the war factory; they fly straight over
/// land and water, take no cell, and are hit only by weapons that fire at the air — the flak cannon fires at them by itself, a tank
/// cannot. By the game data; two people play, no bot.
/// </summary>
public sealed class AircraftTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "air",
                    7,
                    new MatchOptions(StartingCredits: 50_000),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(10, 10)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(55, 55)),
                    ],
                    [new CellPos(10, 10), new CellPos(55, 55)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);

        // A band of deep water across the middle of the map.
        for (var y = 28; y <= 34; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                simulation.World.Map.SetLand(new CellPos(x, y), LandType.Deep);
            }
        }

        return simulation;
    }

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
    public void The_air_command_builds_a_strike_jet_that_takes_no_cell()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_construction_yard", 6, 6);
        Build(world, Allied, "alliance_power_plant", 10, 6);
        Build(world, Allied, "alliance_ore_refinery", 6, 10);
        Build(world, Allied, "alliance_air_force_command", 10, 10);
        TechTree.Update(world);
        var jet = world.Rules.Database.IndexOfUnit("alliance_harrier");
        Assert.Equal(ProductionCategory.Aircraft, ProductionRules.CategoryOf(world.Rules.Database, ProductionKind.Unit, jet));
        Assert.True(world.Player(Allied).CanBuildUnit(jet));

        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 0, ProductionKind.Unit, jet, 1));
        Unit? flier = null;
        for (var tick = 0; tick < 1500 && flier is null; tick++)
        {
            simulation.Step();
            flier = world.Entities.OfType<Unit>().FirstOrDefault(unit => unit.Type == UnitType.AllianceHarrier);
        }

        Assert.NotNull(flier);
        Assert.True(flier.Archetype.Flies);
        Step(simulation, 60);
        var cell = flier.Position.ToCell();
        Assert.True(world.Reservations.HasRoomFor(cell, UnitPlacement.Cell, EntityId.None), "a flier leaves its cell to the ground");
    }

    [Fact]
    public void A_flier_flies_straight_over_the_water_to_its_point()
    {
        var simulation = Match();
        var world = simulation.World;
        var jet = world.SpawnUnit(Allied, UnitType.AllianceHarrier, new CellPos(20, 20).Center);
        var target = new CellPos(20, 44).Center;

        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [jet.Id], target));
        var overWater = false;
        for (var tick = 0; tick < 300 && (tick == 0 || jet.MoveTarget is not null); tick++)
        {
            simulation.Step();
            overWater |= world.Map.LandAt(jet.Position.ToCell()) == LandType.Deep;
            Assert.False(jet.PathPending);
        }

        Assert.True(overWater);
        Assert.Equal(target, jet.Position);
        Assert.Equal(20 * WorldPos.LeptonsPerCell + WorldPos.LeptonsPerCell / 2, jet.Position.X);
    }

    [Fact]
    public void The_flak_cannon_fires_at_a_flier_by_itself_and_a_tank_cannot_attack_one()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "construction_yard", 46, 46);
        Build(world, Union, "reactor", 50, 46);
        Build(world, Union, "flak_cannon", 46, 50);
        var jet = world.SpawnUnit(Allied, UnitType.AllianceHarrier, new CellPos(48, 53).Center);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(44, 52).Center);
        var full = jet.Hp;

        var attack = new AttackCommand(Union, simulation.CurrentTick, 0, [tank.Id], jet.Id);
        simulation.Enqueue(attack);
        Step(simulation, 150);

        Assert.True(jet.Hp < full, "the flak cannon hit it");
        Assert.Contains(attack, simulation.RefusedCommands);
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.HeavyTank, jet));
    }

    [Fact]
    public void An_anti_air_weapon_leaves_the_ground_alone()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "construction_yard", 46, 46);
        Build(world, Union, "reactor", 50, 46);
        Build(world, Union, "flak_cannon", 46, 50);
        var rifleman = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(47, 53).Center);
        var full = rifleman.Hp;

        Step(simulation, 150);

        Assert.Equal(full, rifleman.Hp);
    }
}
