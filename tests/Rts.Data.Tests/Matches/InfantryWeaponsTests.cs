using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The weapons of the infantry (stage 3a of filling the game): each soldier does what its RA2 counterpart does — the marksman picks off
/// soldiers from afar, the commando and the dogs kill one at a shot or a bite and leave vehicles alone, the shock and toxin troopers
/// hurt what their warheads hurt, the jetpack trooper flies and only weapons at the air reach it, and the time trooper erases its target
/// from time unless its beam is broken. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class InfantryWeaponsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "infantry",
                    17,
                    new MatchOptions(StartingCredits: 50_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);

        // Both sides know the whole field: who sees what is not the question here.
        simulation.World.VisionOf(Allied).ExploreAll();
        simulation.World.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static void Step(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void Every_soldier_of_the_stage_carries_its_weapon()
    {
        var rules = Match().World.Rules;
        UnitType[] soldiers =
        [
            UnitType.AttackDog, UnitType.TeslaTrooper, UnitType.Desolator, UnitType.AllianceAttackDog, UnitType.AllianceRocketeer,
            UnitType.AllianceChronoLegionnaire, UnitType.AllianceTanya, UnitType.AllianceSniper,
        ];

        Assert.All(soldiers, type => Assert.True(rules.Unit(type).Armed, type.ToString()));
        Assert.All(soldiers, type => Assert.False(rules.Database.Units[rules.Database.IndexOfUnit(rules.Unit(type).Id)].AbilityLater, type.ToString()));
    }

    [Fact]
    public void The_marksman_kills_a_soldier_twelve_cells_off_and_leaves_a_tank_alone()
    {
        // Its 1% against vehicles rounds to no share at all: it does not pick a tank, and cannot be ordered at one.
        var simulation = Match();
        var world = simulation.World;
        var sniper = Spawn(world, Allied, UnitType.AllianceSniper, 10, 30);
        var soldier = Spawn(world, Union, UnitType.Engineer, 22, 30);
        var tank = Spawn(world, Union, UnitType.HeavyTank, 10, 42);

        Step(simulation, 120);
        Assert.False(soldier.IsAlive);

        Step(simulation, 300);
        Assert.Null(sniper.Target);
        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.AllianceSniper, tank));
    }

    [Fact]
    public void The_commando_kills_a_soldier_with_one_shot()
    {
        var simulation = Match();
        var world = simulation.World;
        var commando = Spawn(world, Allied, UnitType.AllianceTanya, 20, 20);
        var conscript = Spawn(world, Union, UnitType.Conscript, 25, 20);
        var shots = 0;
        for (var tick = 0; tick < 120 && conscript.IsAlive; tick++)
        {
            simulation.Step();
            shots += simulation.LastTickEvents.OfType<WeaponFired>().Count(fired => fired.Shooter == commando.Id);
        }

        Assert.False(conscript.IsAlive);
        Assert.Equal(1, shots);
    }

    [Theory]
    [InlineData(UnitType.AttackDog, UnitType.AllianceRifleman, true)]
    [InlineData(UnitType.AllianceAttackDog, UnitType.Conscript, false)]
    public void A_dog_kills_a_soldier_with_one_bite_and_leaves_a_tank_alone(UnitType dog, UnitType soldier, bool union)
    {
        var simulation = Match();
        var world = simulation.World;
        var (mine, theirs) = union ? (Union, Allied) : (Allied, Union);
        var hound = Spawn(world, mine, dog, 20, 20);
        // An unarmed vehicle: a tank would shoot the dog.
        var tank = Spawn(world, theirs, union ? UnitType.AllianceMcv : UnitType.Mcv, 22, 22);
        var attack = new AttackCommand(mine, simulation.CurrentTick, 0, [hound.Id], tank.Id);
        simulation.Enqueue(attack);
        Step(simulation, 60);

        Assert.Contains(attack, simulation.RefusedCommands);
        Assert.Null(hound.Target);
        Assert.Equal(tank.MaxHp, tank.Hp);

        var victim = Spawn(world, theirs, soldier, 26, 20);
        for (var tick = 0; tick < 200 && victim.IsAlive; tick++)
        {
            simulation.Step();
        }

        Assert.False(victim.IsAlive);
        Assert.Contains(world.Rules.Database.Weapons, weapon => weapon.Damage >= victim.MaxHp && weapon.Range <= Rts.Simulation.Math.Fixed.FromInt(2));
    }

    [Fact]
    public void The_shock_trooper_hurts_a_tank_and_the_toxin_trooper_cannot_touch_a_building()
    {
        var simulation = Match();
        var world = simulation.World;
        Spawn(world, Union, UnitType.TeslaTrooper, 20, 20);
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 22, 20);
        Step(simulation, 30);
        Assert.True(tank.Hp < tank.MaxHp);

        var toxin = Spawn(world, Union, UnitType.Desolator, 40, 40);
        world.Rules.Database.TryGetBuilding("alliance_power_plant", out var plant);
        var target = world.CreateBuilding(Allied, plant, new CellPos(43, 40), true, null);
        var attack = new AttackCommand(Union, simulation.CurrentTick, 0, [toxin.Id], target.Id);
        simulation.Enqueue(attack);
        Step(simulation, 2);
        Assert.Contains(attack, simulation.RefusedCommands);
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.Desolator, target));
    }

    [Fact]
    public void The_jetpack_trooper_flies_and_only_weapons_at_the_air_reach_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var flier = Spawn(world, Allied, UnitType.AllianceRocketeer, 20, 20);
        var tank = Spawn(world, Union, UnitType.HeavyTank, 22, 20);

        Assert.True(flier.Archetype.Flies);
        Assert.False(GarrisonSystem.CanEnter(flier));
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.HeavyTank, flier));
        Assert.True(TargetingSystem.CanHurt(world.Rules, UnitType.SupportVehicle, flier));
        Assert.Equal(Rts.Simulation.Rules.ProductionCategory.Infantry,
            Rts.Simulation.World.ProductionRules.CategoryOf(world.Rules.Database, ProductionKind.Unit, world.Rules.Database.IndexOfUnit("alliance_rocketeer")));

        Step(simulation, 60);
        Assert.Equal(flier.MaxHp, flier.Hp);
        Assert.True(tank.Hp < tank.MaxHp, "its guns hurt the tank");
    }

    [Fact]
    public void The_time_trooper_erases_a_soldier_after_ten_times_its_hit_points_and_nothing_else_hurts_it_meanwhile()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Allied, UnitType.AllianceChronoLegionnaire, 20, 20);
        var engineer = Spawn(world, Union, UnitType.Engineer, 24, 20);
        var heldAt = -1;
        for (var tick = 0; tick < 600 && engineer.IsAlive; tick++)
        {
            simulation.Step();
            if (heldAt < 0 && engineer.ErasedBy == trooper.Id)
            {
                heldAt = simulation.CurrentTick;
                // A shell meanwhile does nothing.
                var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
                world.Hits.Add(new Rts.Simulation.Combat.Hit(engineer.Id, trooper.Id, Allied, cannon, engineer.Position, engineer.Id));
            }

            if (engineer.IsAlive)
            {
                Assert.Equal(engineer.MaxHp, engineer.Hp);
            }
        }

        Assert.False(engineer.IsAlive);
        // 10 × 75 hit points at 8 a tick: 94 ticks of the beam, counted from the tick of the first shot.
        Assert.Equal(94, simulation.CurrentTick - heldAt + 1);
    }

    [Fact]
    public void A_broken_beam_lets_go_and_the_target_comes_back_whole()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Allied, UnitType.AllianceChronoLegionnaire, 20, 20);
        var engineer = Spawn(world, Union, UnitType.Engineer, 24, 20);
        for (var tick = 0; tick < 200 && engineer.ErasePoints < 200; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(trooper.Id, engineer.ErasedBy);
        var at = engineer.Position;
        simulation.Enqueue(new StopCommand(Allied, simulation.CurrentTick, 0, [trooper.Id]));
        Step(simulation, 2);

        Assert.True(engineer.IsAlive);
        Assert.Null(engineer.ErasedBy);
        Assert.Equal(0, engineer.ErasePoints);
        Assert.Equal(engineer.MaxHp, engineer.Hp);
        Assert.Equal(at, engineer.Position);
    }
}
