using Rts.Client.Debug;
using Rts.Simulation.Geometry;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.Tests.Client;

public sealed class PathTextTests
{
    [Fact]
    public void Describes_each_state_of_the_preview()
    {
        var path = new CellPath(PathStatus.Unreachable, [new CellPos(0, 0), new CellPos(1, 1)], 141, 12);

        Assert.Equal("path off (F6)", PathText.Describe(enabled: false, selectedUnits: 1, path));
        Assert.Equal("path: select a unit", PathText.Describe(enabled: true, selectedUnits: 0, path));
        Assert.Equal("paths: no selected unit is driving", PathText.Describe(enabled: true, selectedUnits: 2, path));
        Assert.Equal("paths of 5 units", PathText.Describe(enabled: true, selectedUnits: 7, path: null, drivingUnits: 5));
        Assert.Equal("path: point at the map", PathText.Describe(enabled: true, selectedUnits: 1, path: null));
        Assert.Equal("path Unreachable · 2 cells · 2 waypoints · cost 141 · 12 nodes", PathText.Describe(enabled: true, selectedUnits: 1, path));
        Assert.Equal("unit path Unreachable · 2 cells · 2 waypoints · cost 141 · 12 nodes", PathText.Describe(enabled: true, selectedUnits: 1, path, following: true));
    }

    [Fact]
    public void Describes_the_path_queue()
    {
        Assert.Equal("paths queue 2 · nodes 4120/6000 this tick", PathText.DescribeQueue(2, 4120, 6000));
    }
}
