using Rts.Client.Hud;
using Rts.Client.Terrain;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The shapes of buildings, the crystals of ore and the notices of the base (docs/implementation/M3_base_economy.md, 4.16, 4.17).
/// </summary>
public sealed class BaseVisualsTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    [Fact]
    public void Walls_are_low_defenses_a_little_taller_and_other_buildings_grow_with_their_footprint()
    {
        Assert.Equal(BuildingShape.WallHeight, BuildingShape.Of(TestRules.BuildingDef("wall")).Height);
        Assert.Equal(BuildingShape.DefenseHeight, BuildingShape.Of(TestRules.BuildingDef("tesla_coil")).Height);
        Assert.True(BuildingShape.Of(TestRules.BuildingDef("construction_yard")).Height > BuildingShape.Of(TestRules.BuildingDef("reactor")).Height);
        Assert.True(BuildingShape.Of(TestRules.BuildingDef("reactor")).Height > BuildingShape.DefenseHeight);
    }

    [Fact]
    public void The_marks_stand_for_rules()
    {
        Assert.Equal((true, false), (BuildingShape.Of(TestRules.BuildingDef("reactor")).Stack, BuildingShape.Of(TestRules.BuildingDef("reactor")).Dish));
        Assert.True(BuildingShape.Of(TestRules.BuildingDef("radar")).Dish);
        Assert.Equal(new CellPos(1, 3), BuildingShape.Of(TestRules.BuildingDef("refinery")).Dock);
        Assert.Equal(new CellPos(1, 2), BuildingShape.Of(TestRules.BuildingDef("barracks")).Exit);
        Assert.Equal(new CellPos(1, 4), BuildingShape.Of(TestRules.BuildingDef("war_factory")).Exit);

        var yard = BuildingShape.Of(TestRules.BuildingDef("construction_yard"));
        Assert.Equal((false, false, (CellPos?)null, (CellPos?)null), (yard.Stack, yard.Dish, yard.Dock, yard.Exit));
    }

    [Theory]
    [InlineData(23, 23, 0f, 0f)]
    [InlineData(23, 23, 0.5f, 0.5f / 23)]
    [InlineData(23, 1, 0.5f, 22.5f / 23)]
    [InlineData(23, 0, 0.3f, 1f)]
    [InlineData(0, 0, 0f, 1f)]
    public void A_building_grows_smoothly_between_ticks(int buildUp, int left, float alpha, float growth)
    {
        Assert.Equal(growth, BuildingShape.Growth(buildUp, left, alpha), 5);
    }

    [Fact]
    public void Ore_shows_one_to_four_crystals_more_and_larger_with_the_stage()
    {
        Assert.Empty(OreCrystals.Of(3, 4, OreCell.Empty));
        Assert.Equal(1, OreCrystals.CountOf(0));
        Assert.Equal(OreCrystals.MaxCrystals, OreCrystals.CountOf(OreCell.MaxStage));

        var previousCount = 0;
        var previousSize = 0f;
        for (var stage = 0; stage <= OreCell.MaxStage; stage++)
        {
            var crystals = OreCrystals.Of(3, 4, new OreCell(OreKind.Ore, stage));
            Assert.True(crystals.Count >= previousCount);
            Assert.True(crystals[0].Size > previousSize);
            Assert.All(crystals, crystal => Assert.True(crystal.X is > 0.1f and < 0.9f && crystal.Z is > 0.1f and < 0.9f));
            (previousCount, previousSize) = (crystals.Count, crystals[0].Size);
        }

        Assert.Equal(OreCrystals.Of(7, 9, new OreCell(OreKind.Ore, 5)), OreCrystals.Of(7, 9, new OreCell(OreKind.Ore, 5)));
        Assert.True(OreCrystals.Of(7, 9, new OreCell(OreKind.Gems, 5))[0].Size > OreCrystals.Of(7, 9, new OreCell(OreKind.Ore, 5))[0].Size);
    }

    [Fact]
    public void The_notices_speak_of_the_player_only()
    {
        Assert.Equal("UI_EVA_CONSTRUCTION_COMPLETE", EvaMessages.KeyOf(new ProductionReady(5, Player1, 2), Player1));
        Assert.Equal("UI_EVA_UNIT_READY", EvaMessages.KeyOf(new UnitProduced(5, Player1, new EntityId(9), new EntityId(3)), Player1));
        Assert.Equal("UI_EVA_LOW_POWER", EvaMessages.KeyOf(new LowPower(5, Player1), Player1));
        Assert.Equal("UI_EVA_BUILDING_SOLD", EvaMessages.KeyOf(new BuildingSold(5, new EntityId(3), Player1, 1, 300), Player1));
        Assert.Equal("UI_EVA_CANNOT_PLACE", EvaMessages.KeyOf(new CannotPlace(5, Player1, 1, new CellPos(1, 1), PlacementProblem.TooFar), Player1));
        Assert.Null(EvaMessages.KeyOf(new LowPower(5, Player2), Player1));
        Assert.Null(EvaMessages.KeyOf(new ProductionReady(5, Player2, 2), Player1));
        Assert.Null(EvaMessages.KeyOf(new BuildUpComplete(5, new EntityId(3)), Player1));
        Assert.Equal("UI_EVA_UNDER_ATTACK", EvaMessages.KeyOf(new UnderAttack(5, Player1, new CellPos(4, 4).Center), Player1));
        Assert.Null(EvaMessages.KeyOf(new UnderAttack(5, Player2, new CellPos(4, 4).Center), Player1));
    }

    [Fact]
    public void The_end_of_a_match_speaks_to_the_loser_the_others_and_the_winners()
    {
        Assert.Equal("UI_EVA_YOU_ARE_DEFEATED", EvaMessages.KeyOf(new PlayerDefeated(5, Player1), Player1));
        Assert.Equal("UI_EVA_PLAYER_DEFEATED", EvaMessages.KeyOf(new PlayerDefeated(5, Player2), Player1));
        Assert.Equal("UI_EVA_VICTORY", EvaMessages.KeyOf(new MatchEnded(15, [Player1]), Player1));
        Assert.Equal("UI_EVA_DEFEAT", EvaMessages.KeyOf(new MatchEnded(15, [Player1]), Player2));
        Assert.Equal("UI_EVA_DEFEAT", EvaMessages.KeyOf(new MatchEnded(15, []), Player1));
    }

    [Fact]
    public void The_same_notice_waits_before_it_comes_again_and_funds_wait_longer()
    {
        var messages = new EvaMessages();
        var low = new LowPower(5, Player1);
        var funds = new InsufficientFunds(5, Player1, ProductionCategory.Building);

        Assert.NotNull(messages.Next(low, Player1, 0));
        Assert.Null(messages.Next(low, Player1, 2.9));
        Assert.NotNull(messages.Next(low, Player1, 3.1));

        Assert.NotNull(messages.Next(funds, Player1, 0));
        Assert.Null(messages.Next(funds, Player1, 9));
        Assert.NotNull(messages.Next(funds, Player1, 10));
        Assert.NotNull(messages.Next(new PowerRestored(5, Player1), Player1, 10));
    }
}
