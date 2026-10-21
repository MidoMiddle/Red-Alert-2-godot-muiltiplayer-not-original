using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The sandbox map of the client (game/maps/test/sandbox.txt): every element, valid starts, a clear smoke route.</summary>
public sealed class SandboxMapTests
{
    private static TestMap Sandbox() =>
        TestMapParser.Parse(File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, "game", "maps", "test", "sandbox.txt")));

    private static IEnumerable<CellPos> Cells(MapGrid grid) =>
        Enumerable.Range(0, grid.Height).SelectMany(y => Enumerable.Range(0, grid.Width).Select(x => new CellPos(x, y)));

    [Fact]
    public void Sandbox_has_every_element()
    {
        var map = Sandbox();

        Assert.Equal((64, 64), (map.Grid.Width, map.Grid.Height));
        Assert.Equal(Enum.GetValues<LandType>().Order(), Cells(map.Grid).Select(map.Grid.LandAt).Distinct().Order());
        Assert.Contains(Cells(map.Grid), map.Grid.IsBlocked);
    }

    [Fact]
    public void Sandbox_starts_with_every_unit_type_for_the_player_and_an_opponent()
    {
        var map = Sandbox();

        Assert.Equal(Enum.GetValues<UnitType>().Order(), map.Units.Where(u => u.Owner == new PlayerId(1)).Select(u => u.Type).Order());
        Assert.Contains(map.Units, u => u.Owner == new PlayerId(2));
    }

    [Fact]
    public void Smoke_route_from_the_blue_base_to_the_centre_is_passable()
    {
        // --smoke orders every blue unit straight to the centre of cell (32, 32).
        var map = Sandbox();
        var target = new CellPos(32, 32).Center;

        foreach (var unit in map.Units.Where(u => u.Owner == new PlayerId(1)))
        {
            var locomotor = UnitArchetypes.Get(unit.Type).Locomotor;
            var start = unit.Cell.Center;
            for (var step = 0; step <= 100; step++)
            {
                var point = new WorldPos(start.X + (target.X - start.X) * step / 100, start.Y + (target.Y - start.Y) * step / 100);
                Assert.True(map.Grid.CanEnter(point.ToCell(), locomotor), $"{unit.Type} is blocked at {point.ToCell()}.");
            }
        }
    }
}
