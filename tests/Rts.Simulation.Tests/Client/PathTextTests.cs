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

        Assert.Equal("path off (F3)", PathText.Describe(enabled: false, selectedUnits: 1, path));
        Assert.Equal("path: select one unit", PathText.Describe(enabled: true, selectedUnits: 0, path));
        Assert.Equal("path: select one unit", PathText.Describe(enabled: true, selectedUnits: 2, path));
        Assert.Equal("path: point at the map", PathText.Describe(enabled: true, selectedUnits: 1, path: null));
        Assert.Equal("path Unreachable · 2 cells · cost 141 · 12 nodes", PathText.Describe(enabled: true, selectedUnits: 1, path));
    }
}
