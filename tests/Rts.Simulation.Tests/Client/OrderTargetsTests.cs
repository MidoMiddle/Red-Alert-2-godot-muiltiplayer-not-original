using Rts.Client.Controls;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>Which selected units an order reaches: the living own units, in selection order, at most one command's worth.</summary>
public sealed class OrderTargetsTests
{
    [Fact]
    public void DeadAndForeignUnitsAreDroppedAndTheSelectionOrderIsKept()
    {
        var own = new HashSet<EntityId> { new(3), new(1) };

        var picked = OrderTargets.Pick([new EntityId(3), new EntityId(2), new EntityId(1)], own.Contains);

        Assert.Equal([new EntityId(3), new EntityId(1)], picked);
    }

    [Fact]
    public void OnlyTheFirstUnitsOfOneCommandAreKept()
    {
        var selected = Enumerable.Range(1, 300).Select(i => new EntityId(i)).ToList();

        var picked = OrderTargets.Pick(selected, _ => true);

        Assert.Equal(UnitsCommand.MaxUnits, picked.Count);
        Assert.Equal(selected.Take(UnitsCommand.MaxUnits), picked);
    }

    [Fact]
    public void TheLimitCanBeLower()
    {
        Assert.Equal([new EntityId(1), new EntityId(2)], OrderTargets.Pick([new EntityId(1), new EntityId(2), new EntityId(3)], _ => true, 2));
    }
}
