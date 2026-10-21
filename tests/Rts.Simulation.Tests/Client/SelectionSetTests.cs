using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>Client selection: one unit at a time, and dead units leave the selection.</summary>
public sealed class SelectionSetTests
{
    [Fact]
    public void SelectingReplacesTheSelection()
    {
        var selection = new SelectionSet();
        selection.Replace(new EntityId(1));

        selection.Replace(new EntityId(2));

        Assert.Equal(new[] { new EntityId(2) }, selection.Units);
    }

    [Fact]
    public void DeadUnitsLeaveTheSelection()
    {
        var selection = new SelectionSet();
        selection.Replace(new EntityId(3));

        selection.RemoveWhere(unit => unit.Value == 3);

        Assert.True(selection.IsEmpty);
    }

    [Fact]
    public void ClearingEmptiesTheSelection()
    {
        var selection = new SelectionSet();
        selection.Replace(new EntityId(4));

        selection.Clear();

        Assert.False(selection.Contains(new EntityId(4)));
    }
}
