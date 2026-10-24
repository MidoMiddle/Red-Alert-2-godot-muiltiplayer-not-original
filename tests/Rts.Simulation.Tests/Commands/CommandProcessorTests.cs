using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;
using Rts.Simulation.Tests.World;

namespace Rts.Simulation.Tests.Commands;

public sealed class CommandProcessorTests
{
    private static GameWorld NewWorld() => new(seed: 1, widthCells: 64, heightCells: 64, TestRules.Standard);

    [Fact]
    public void Spawn_inside_the_map_creates_a_unit_for_the_player()
    {
        var world = NewWorld();

        var applied = CommandProcessor.Apply(world, Spawn(player: 2, tick: 0, sequence: 0, x: 300, y: 400, type: UnitType.HeavyTank));

        Assert.True(applied);
        var unit = Assert.IsType<Unit>(Assert.Single(world.Entities));
        Assert.Equal(new PlayerId(2), unit.Owner);
        Assert.Equal(new WorldPos(300, 400), unit.Position);
        Assert.Equal(UnitType.HeavyTank, unit.Type);
        Assert.Equal(TestRules.Unit(UnitType.HeavyTank).Speed, unit.Speed);
        Assert.Equal(TestRules.Unit(UnitType.HeavyTank).TurnRate, unit.TurnRate);
        Assert.Equal(BAngle.North, unit.Facing);
        Assert.Equal(BAngle.North, unit.TurretFacing);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(48)]
    [InlineData(255)]
    public void Spawn_of_an_unknown_unit_type_is_ignored(int type)
    {
        var world = NewWorld();

        Assert.False(CommandProcessor.Apply(world, Spawn(player: 1, tick: 0, sequence: 0, type: (UnitType)type)));
        Assert.Empty(world.Entities);
    }

    [Theory]
    [InlineData(1, -1, 100)]
    [InlineData(1, 64 * 256, 100)]
    [InlineData(0, 100, 100)]
    [InlineData(9, 100, 100)]
    [InlineData(-1, 100, 100)]
    public void Invalid_spawn_is_ignored(int player, int x, int y)
    {
        var world = NewWorld();

        Assert.False(CommandProcessor.Apply(world, Spawn(player, tick: 0, sequence: 0, x, y)));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Move_sets_target_only_for_own_living_units()
    {
        var world = NewWorld();
        var own = world.SpawnForTest(new PlayerId(1), new WorldPos(100, 100), speed: 10);
        var foreign = world.SpawnForTest(new PlayerId(2), new WorldPos(100, 100), speed: 10);
        var dead = world.SpawnForTest(new PlayerId(1), new WorldPos(100, 100), speed: 10);
        dead.MarkDead();

        var applied = CommandProcessor.Apply(
            world,
            Move(player: 1, tick: 0, sequence: 0, units: [own.Id.Value, foreign.Id.Value, dead.Id.Value, 999], x: 640, y: 640));

        Assert.True(applied);
        Assert.Equal(new WorldPos(640, 640), own.MoveTarget);
        Assert.Null(foreign.MoveTarget);
        Assert.Null(dead.MoveTarget);
    }

    [Fact]
    public void Move_with_target_outside_the_map_is_rejected_entirely()
    {
        var world = NewWorld();
        var unit = world.SpawnForTest(new PlayerId(1), new WorldPos(100, 100), speed: 10);

        var applied = CommandProcessor.Apply(world, Move(player: 1, tick: 0, sequence: 0, units: [unit.Id.Value], x: -5, y: 100));

        Assert.False(applied);
        Assert.Null(unit.MoveTarget);
    }

    [Fact]
    public void Move_of_only_foreign_units_changes_nothing()
    {
        var world = NewWorld();
        var foreign = world.SpawnForTest(new PlayerId(2), new WorldPos(100, 100), speed: 10);

        Assert.False(CommandProcessor.Apply(world, Move(player: 1, tick: 0, sequence: 0, units: [foreign.Id.Value])));
        Assert.Null(foreign.MoveTarget);
    }

    [Fact]
    public void Move_from_neutral_player_is_ignored()
    {
        var world = NewWorld();
        var neutralUnit = world.SpawnForTest(PlayerId.Neutral, new WorldPos(100, 100), speed: 10);

        Assert.False(CommandProcessor.Apply(world, Move(player: 0, tick: 0, sequence: 0, units: [neutralUnit.Id.Value])));
        Assert.Null(neutralUnit.MoveTarget);
    }
}
