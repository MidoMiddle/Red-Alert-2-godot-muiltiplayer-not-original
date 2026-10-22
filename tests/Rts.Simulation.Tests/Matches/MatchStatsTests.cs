using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>The statistics of a match (docs/implementation/M5_first_bot.md, 4.9, step 11): what counts as built, lost and destroyed, the credits, the series.</summary>
public sealed class MatchStatsTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Red = new(2);

    private static GameSimulation Sandbox() =>
        new(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static void Record(GameWorld world, params SimEvent[] events) => world.Stats.Record(world, [.. events]);

    [Fact]
    public void Units_out_of_a_factory_and_buildings_placed_are_built()
    {
        var world = Sandbox().World;

        Record(
            world,
            new UnitProduced(0, Blue, new EntityId(90), new EntityId(91)),
            new UnitProduced(0, Blue, new EntityId(92), new EntityId(91)),
            new BuildingPlaced(0, new EntityId(93), Red, 1, new CellPos(1, 1)));

        Assert.Equal((2, 0), (world.Stats.Of(Blue).UnitsBuilt, world.Stats.Of(Blue).BuildingsBuilt));
        Assert.Equal((0, 1), (world.Stats.Of(Red).UnitsBuilt, world.Stats.Of(Red).BuildingsBuilt));
    }

    [Fact]
    public void A_deployed_mcv_is_one_building_built_and_no_unit_lost()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard);
        var mcv = simulation.World.Entities.OfType<Unit>().Single(unit => unit.Owner == Blue && unit.Type == UnitType.Mcv);
        simulation.Enqueue(new Rts.Simulation.Commands.DeployCommand(Blue, 0, 0, mcv.Id));

        simulation.Step();

        Assert.Contains(simulation.LastTickEvents, simEvent => simEvent is Deployed);
        Assert.Equal((1, 0), (simulation.World.Stats.Of(Blue).BuildingsBuilt, simulation.World.Stats.Of(Blue).UnitsLost));
    }

    [Fact]
    public void A_death_is_lost_by_its_owner_and_destroyed_by_the_killers_player_not_by_itself_nor_by_nobody()
    {
        var world = Sandbox().World;
        var conscript = world.SpawnUnit(Red, UnitType.Conscript, new CellPos(5, 5).Center);
        var reactor = world.CreateBuilding(Red, TestRules.BuildingDef("reactor"), new CellPos(20, 20), ready: true, null);
        var own = world.SpawnUnit(Blue, UnitType.Conscript, new CellPos(8, 5).Center);
        var defeated = world.SpawnUnit(Red, UnitType.Engineer, new CellPos(9, 9).Center);
        var neutral = world.CreateBuilding(PlayerId.Neutral, TestRules.BuildingDef("reactor"), new CellPos(30, 30), ready: true, null);

        Record(
            world,
            // A shooter long gone: its player came with the shot.
            new EntityDestroyed(0, conscript.Id, new EntityId(999), DeathCause.Weapon, 1, Blue),
            new EntityDestroyed(0, reactor.Id, new EntityId(999), DeathCause.Weapon, 1, Blue),
            new EntityDestroyed(0, own.Id, new EntityId(998), DeathCause.Crushed, -1, Blue),
            new EntityDestroyed(0, defeated.Id, EntityId.None, DeathCause.Defeat),
            new EntityDestroyed(0, neutral.Id, new EntityId(999), DeathCause.Weapon, 1, Blue));

        var (blue, red) = (world.Stats.Of(Blue), world.Stats.Of(Red));
        Assert.Equal((2, 1), (red.UnitsLost, red.BuildingsLost));
        Assert.Equal((1, 1), (blue.UnitsDestroyed, blue.BuildingsDestroyed));
        // Its own conscript: lost, not destroyed; the neutral reactor counts for nobody.
        Assert.Equal((1, 0), (blue.UnitsLost, blue.BuildingsLost));
        Assert.Equal((0, 0), (red.UnitsDestroyed, red.BuildingsDestroyed));
    }

    [Fact]
    public void A_shot_and_a_crush_credit_the_player_of_the_killer()
    {
        var simulation = Sandbox();
        var tank = simulation.World.SpawnUnit(Blue, UnitType.TeslaTank, new CellPos(10, 10).Center);
        var conscript = simulation.World.SpawnUnit(Red, UnitType.Conscript, new CellPos(10, 7).Center);
        tank.Target = conscript.Id;

        simulation.Step();

        Assert.Equal(1, simulation.World.Stats.Of(Blue).UnitsDestroyed);
        Assert.Equal(1, simulation.World.Stats.Of(Red).UnitsLost);

        var rhino = simulation.World.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(20, 10).Center);
        simulation.World.SpawnUnit(Red, UnitType.Conscript, new CellPos(21, 10).Center);
        var events = new List<SimEvent>();
        Assert.True(Rts.Simulation.Systems.CrushSystem.TryCrush(simulation.World, rhino, new CellPos(21, 10), events));
        simulation.World.Stats.Record(simulation.World, events);
        Assert.Equal(2, simulation.World.Stats.Of(Blue).UnitsDestroyed);
    }

    [Fact]
    public void A_sale_is_no_loss_its_refund_counts_apart_and_a_cancel_gives_back_what_was_spent()
    {
        var world = Sandbox().World;
        var reactor = world.CreateBuilding(Blue, TestRules.BuildingDef("reactor"), new CellPos(20, 20), ready: true, null);
        world.Stats.Spent(Blue, 500);
        world.Stats.Harvested(Blue, 300);

        Record(
            world,
            new BuildingSold(0, reactor.Id, Blue, reactor.DefIndex, 250),
            new ProductionCancelled(0, Blue, ProductionKind.Unit, 0, 200));

        var blue = world.Stats.Of(Blue);
        Assert.Equal((0, 250, 300, 300), (blue.BuildingsLost, blue.CreditsFromSales, blue.CreditsSpent, blue.CreditsHarvested));
    }

    [Fact]
    public void Every_thirty_seconds_a_point_of_credits_army_and_buildings()
    {
        var world = Sandbox().World;
        world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(5, 5).Center);
        var hurt = world.SpawnUnit(Blue, UnitType.HeavyTank, new CellPos(7, 5).Center);
        hurt.Hp = hurt.MaxHp / 2;
        world.SpawnUnit(Blue, UnitType.Harvester, new CellPos(9, 5).Center);
        world.CreateBuilding(Blue, TestRules.BuildingDef("reactor"), new CellPos(20, 20), ready: true, null);
        world.CreateBuilding(Blue, TestRules.BuildingDef("wall"), new CellPos(25, 25), ready: true, null);
        var cost = TestRules.Standard.Database.Units[TestRules.Standard.Database.IndexOfUnit("heavy_tank")].Cost;

        foreach (var tick in new[] { 0, 1, 449, 450, 900 })
        {
            world.CurrentTick = tick;
            Record(world);
        }

        var samples = world.Stats.Of(Blue).Samples;
        Assert.Equal([0, 450, 900], samples.Select(sample => sample.Tick));
        Assert.Equal(new StatsSample(0, world.Player(Blue).Credits, cost + cost / 2, 1), samples[0]);
        Assert.Equal(901, world.Stats.Duration);
    }

    [Fact]
    public void The_duration_is_that_of_the_match_once_it_ended()
    {
        var world = Sandbox().World;
        world.Result = new MatchResult(300, [Blue]);
        world.CurrentTick = 500;

        Record(world);

        Assert.Equal(300, world.Stats.Duration);
    }

    [Fact]
    public void A_match_lost_counts_what_the_defeated_had_left_as_lost_and_destroyed_by_nobody()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard);
        var tally = EventTally.Attach(simulation);
        var red = simulation.World.Entities.Count(entity => entity.Owner == Red);
        simulation.Enqueue(new Rts.Simulation.Commands.SurrenderCommand(Red, 3, 0));

        for (var tick = 0; tick < 20; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(red, simulation.World.Stats.Of(Red).UnitsLost);
        Assert.Equal(0, simulation.World.Stats.Of(Blue).UnitsDestroyed);
        tally.AssertAgrees(simulation.World);
    }
}
