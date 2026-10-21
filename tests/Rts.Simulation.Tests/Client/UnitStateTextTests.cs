using Rts.Client.Debug;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

public sealed class UnitStateTextTests
{
    [Fact]
    public void Describes_each_movement_state()
    {
        var world = new GameWorld(seed: 1, widthCells: 8, heightCells: 8);
        var unit = world.SpawnUnit(new PlayerId(1), UnitType.HeavyTank, new CellPos(1, 1).Center);

        Assert.Equal("unit -", UnitStateText.Describe(null));
        Assert.Equal("unit idle", UnitStateText.Describe(unit));

        unit.MoveTarget = new CellPos(5, 5).Center;
        unit.WaitTicks = 3;
        unit.Replans = 1;
        Assert.Equal("unit moving · wait 3 · replans 1", UnitStateText.Describe(unit));

        unit.Orders = [new UnitOrder(new CellPos(2, 2).Center, false), new UnitOrder(new CellPos(3, 3).Center, true), new UnitOrder(new CellPos(4, 4).Center, false)];
        unit.OrderIndex = 1;
        unit.LoopOrders = true;
        Assert.Equal("unit moving · wait 3 · replans 1 · order 2/3 · patrol · attack-move", UnitStateText.Describe(unit));
        unit.Orders = [];
        unit.OrderIndex = 0;
        unit.LoopOrders = false;

        unit.PathPending = true;
        Assert.Equal("unit waiting for path", UnitStateText.Describe(unit));

        unit.PathPending = false;
        unit.ResumeTarget = new CellPos(7, 7).Center;
        Assert.Equal("unit giving way", UnitStateText.Describe(unit));

        unit.MoveTarget = null;
        unit.HoldTicks = 12;
        Assert.Equal("unit giving way · hold 12", UnitStateText.Describe(unit));
    }
}
