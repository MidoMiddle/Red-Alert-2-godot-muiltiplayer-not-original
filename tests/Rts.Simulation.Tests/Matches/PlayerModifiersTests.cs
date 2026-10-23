using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>
/// The bonuses of a player (docs/implementation/M8_ai_difficulty.md, 4.5, step 2): «Brutal» earns ×1.25, builds ×1.2 at the same cost and
/// sees the whole map — in the simulation, from the level of its bot in the data, in the checksum; a person and the other levels have none.
/// </summary>
public sealed class PlayerModifiersTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerModifiers Brutal = new(25, 20, RevealMap: true);

    /// <summary>One refinery and one harvester at a field of full ore, five minutes, no credits at the start; the credits earned.</summary>
    private static PlayerState Harvest(PlayerModifiers modifiers)
    {
        var map = new MapGrid(48, 40, TestRules.Standard.Land);
        for (var y = 18; y < 26; y++)
        {
            for (var x = 18; x < 26; x++)
            {
                map.SetOre(new CellPos(x, y), new OreCell(OreKind.Ore, OreCell.MaxStage));
            }
        }

        var simulation = new GameSimulation(new SimulationSettings(1, map) { StartingCredits = 0 }, TestRules.Standard);
        simulation.World.Player(Player1).Modifiers = modifiers;
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("refinery"), new CellPos(10, 10), true, null);
        simulation.World.SpawnUnit(Player1, UnitType.Harvester, new CellPos(8, 16).Center);
        for (var tick = 0; tick < 5 * 60 * 15; tick++)
        {
            simulation.Step();
        }

        return simulation.World.Player(Player1);
    }

    /// <summary>A yard and a reactor of player 1, then a war factory ordered; the ticks until it is ready and what it cost.</summary>
    private static (int Ticks, int Spent) BuildWarFactory(PlayerModifiers modifiers)
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { MapWidthCells = 48, MapHeightCells = 32, StartingCredits = 10_000 }, TestRules.Standard);
        var player = simulation.World.Player(Player1);
        player.Modifiers = modifiers;
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("construction_yard"), new CellPos(1, 1), true, null);
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(6, 1), true, null);
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("barracks"), new CellPos(9, 1), true, null);
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("refinery"), new CellPos(1, 6), true, null);
        simulation.Step();
        var start = simulation.CurrentTick;
        var index = TestRules.Standard.Database.IndexOfBuilding("war_factory");
        simulation.Enqueue(new ProduceCommand(Player1, start, 0, ProductionKind.Building, index, 1));
        while (!simulation.LastTickEvents.OfType<ProductionReady>().Any())
        {
            simulation.Step();
        }

        return (simulation.CurrentTick - start, 10_000 - player.Credits);
    }

    /// <summary>
    /// A person (1) and a «Brutal» bot (2) on one team, a person (3) against them, on the clear 64 × 64 map of the match tests; one tick
    /// played, so that everyone sees around their start.
    /// </summary>
    private static GameSimulation TwoAgainstOne(AiDifficulty bot = AiDifficulty.Brutal)
    {
        var setup = MatchSetupTests.OneOnOne();
        setup = setup with
        {
            Players =
            [
                MatchSetupTests.Player(1, PlayerKind.Human, 10, 10) with { Team = 1 },
                MatchSetupTests.Player(2, PlayerKind.Ai, 50, 10) with { Team = 1, Difficulty = bot },
                MatchSetupTests.Player(3, PlayerKind.Human, 50, 50) with { Team = 2 },
            ],
        };
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
        simulation.Step();
        return simulation;
    }

    private static Unit FirstUnit(GameSimulation simulation, int player, UnitType type) =>
        simulation.World.Entities.OfType<Unit>().First(unit => unit.Owner == new PlayerId(player) && unit.Type == type);

    [Fact]
    public void Brutal_earns_a_quarter_more_from_the_same_ore_and_loses_no_hundredth()
    {
        var plain = Harvest(default);
        var brutal = Harvest(Brutal);

        // The bonus changes nothing but the credits: the same ore is unloaded, ×1.25 of it is paid, the rest owed in hundredths.
        Assert.InRange(plain.Credits, 4000, 7000);
        Assert.Equal(plain.Credits * 125 / 100, brutal.Credits);
        Assert.Equal(plain.Credits * 125 % 100, brutal.IncomeCarry);
        Assert.Equal(0, plain.IncomeCarry);
    }

    [Fact]
    public void A_thousand_ore_make_twelve_hundred_and_fifty_credits_unload_by_unload()
    {
        // 40 unloads of 25 — the hundredths of every one add up to whole credits.
        var player = new PlayerState(Player1, TestRules.Standard.Database.Factions[0], 0, TestRules.Standard.Database) { Modifiers = Brutal };
        for (var i = 0; i < 40; i++)
        {
            HarvesterSystem.Earn(player, 25);
        }

        Assert.Equal((1250, 0), (player.Credits, player.IncomeCarry));
        HarvesterSystem.Earn(player, 3);
        Assert.Equal((1253, 75), (player.Credits, player.IncomeCarry));
    }

    [Fact]
    public void Brutal_builds_a_fifth_faster_at_the_same_cost()
    {
        var total = ProductionRules.TotalProgress(TestRules.Standard.Database, ProductionKind.Building, TestRules.Standard.Database.IndexOfBuilding("war_factory"));
        var plain = BuildWarFactory(default);
        var brutal = BuildWarFactory(Brutal);

        // Per mille of a tick: 1000 at full power, 1200 with the bonus.
        Assert.Equal((total + 999) / 1000, plain.Ticks);
        Assert.Equal((total + 1199) / 1200, brutal.Ticks);
        Assert.True(brutal.Ticks < plain.Ticks);
        Assert.Equal(plain.Spent, brutal.Spent);
        Assert.Equal(TestRules.BuildingDef("war_factory").Cost, brutal.Spent);
    }

    [Fact]
    public void A_bot_gets_the_bonuses_of_its_level_and_a_person_none()
    {
        var brutal = TwoAgainstOne();
        var medium = TwoAgainstOne(AiDifficulty.Medium);
        var hard = TwoAgainstOne(AiDifficulty.Hard);

        Assert.Equal(Brutal, brutal.World.Player(new PlayerId(2)).Modifiers);
        Assert.Equal(default, brutal.World.Player(Player1).Modifiers);
        Assert.Equal(default, brutal.World.Player(new PlayerId(3)).Modifiers);
        Assert.Equal(default, medium.World.Player(new PlayerId(2)).Modifiers);
        // The rules of these tests have no «Hard»: a level the data does not have gives none.
        Assert.Equal(default, hard.World.Player(new PlayerId(2)).Modifiers);
    }

    [Fact]
    public void Brutal_sees_the_enemy_in_the_fog_and_its_ally_does_not()
    {
        var simulation = TwoAgainstOne();
        var world = simulation.World;
        var enemy = FirstUnit(simulation, 3, UnitType.Conscript);
        var cell = enemy.Position.ToCell();

        Assert.True(Vision.Sees(world, new PlayerId(2), enemy));
        Assert.False(Vision.Sees(world, Player1, enemy));
        // The grids stay as they are: neither the ally's screen nor the bot's own shows the cell.
        Assert.False(world.VisionOf(Player1).IsVisible(cell));
        Assert.False(world.VisionOf(new PlayerId(2)).IsVisible(cell));
    }

    [Fact]
    public void An_attack_on_a_target_in_the_fog_goes_through_for_brutal_only()
    {
        var simulation = TwoAgainstOne();
        var target = FirstUnit(simulation, 3, UnitType.Mcv);
        var bot = FirstUnit(simulation, 2, UnitType.Conscript);
        var person = FirstUnit(simulation, 1, UnitType.Conscript);
        var tick = simulation.CurrentTick;

        simulation.Enqueue(new AttackCommand(new PlayerId(2), tick, AiTurnSequence, [bot.Id], target.Id));
        simulation.Enqueue(new AttackCommand(Player1, tick, 0, [person.Id], target.Id));
        simulation.Step();

        Assert.Equal(target.Id, bot.Target);
        Assert.Null(person.Target);
    }

    [Fact]
    public void The_checksum_tells_the_bonuses_apart()
    {
        var plain = TwoAgainstOne(AiDifficulty.Medium);
        var brutal = TwoAgainstOne();

        // The same match but for the level of the bot: on the first tick only the bonuses differ.
        Assert.NotEqual(StateHasher.Compute(plain.World), StateHasher.Compute(brutal.World));
        plain.World.Player(new PlayerId(2)).Modifiers = Brutal;
        Assert.Equal(StateHasher.Compute(plain.World), StateHasher.Compute(brutal.World));
    }

    private const uint AiTurnSequence = Ai.AiTurn.FirstSequence;
}
