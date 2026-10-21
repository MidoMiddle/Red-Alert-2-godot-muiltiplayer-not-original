using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>Control groups (docs/implementation/M2_unit_control.md, 4.14): assign, read, the dead leave, double tap.</summary>
public sealed class ControlGroupsTests
{
    private static readonly EntityId A = new(1);
    private static readonly EntityId B = new(2);
    private static readonly EntityId C = new(3);

    [Fact]
    public void AssigningReplacesTheGroupAndKeepsTheOrder()
    {
        var groups = new ControlGroups();
        groups.Assign(1, [B, A, B]);

        Assert.Equal([B, A], groups.Members(1, _ => true));

        groups.Assign(1, [C]);

        Assert.Equal([C], groups.Members(1, _ => true));
    }

    [Fact]
    public void GroupsAreSeparateAndAUnitMayBeInSeveral()
    {
        var groups = new ControlGroups();
        groups.Assign(1, [A, B]);
        groups.Assign(2, [B, C]);

        Assert.Equal([A, B], groups.Members(1, _ => true));
        Assert.Equal([B, C], groups.Members(2, _ => true));
        Assert.Empty(groups.Members(0, _ => true));
    }

    [Fact]
    public void TheDeadLeaveTheGroup()
    {
        var groups = new ControlGroups();
        groups.Assign(3, [A, B, C]);

        Assert.Equal([A, C], groups.Members(3, unit => unit != B));
        Assert.Equal([A, C], groups.Members(3, _ => true));
    }

    [Fact]
    public void ASecondTapOfTheSameDigitWithinTheWindowIsADoubleTap()
    {
        var groups = new ControlGroups();

        Assert.False(groups.Tap(1, 1000));
        Assert.True(groups.Tap(1, 1000 + ControlGroups.DoubleTapMs));
        // A third tap starts over.
        Assert.False(groups.Tap(1, 1000 + ControlGroups.DoubleTapMs + 10));
    }

    [Fact]
    public void ALateTapOrAnotherDigitIsNotADoubleTap()
    {
        var groups = new ControlGroups();

        groups.Tap(1, 1000);
        Assert.False(groups.Tap(1, 1001 + ControlGroups.DoubleTapMs));
        Assert.False(groups.Tap(2, 1002 + ControlGroups.DoubleTapMs));
        Assert.True(groups.Tap(2, 1100 + ControlGroups.DoubleTapMs));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void OnlyDigitsAreGroups(int digit)
    {
        var groups = new ControlGroups();

        Assert.Throws<ArgumentOutOfRangeException>(() => groups.Assign(digit, [A]));
        Assert.Throws<ArgumentOutOfRangeException>(() => groups.Tap(digit, 0));
    }
}
