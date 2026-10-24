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

        // The Union's eight of M2: the units of the Alliance are not on the test maps.
        Assert.Equal(Enum.GetValues<UnitType>().Where(t => t <= UnitType.TeslaTank).Order(), map.Units.Where(u => u.Owner == new PlayerId(1)).Select(u => u.Type).Order());
        Assert.Contains(map.Units, u => u.Owner == new PlayerId(2));
    }

    [Fact]
    public void Sandbox_has_ore_and_gems_near_the_blue_base()
    {
        // The yard of the blue MCV (20, 22) deploys on 19…21, 21…23; RA2 maps put a field within a short drive of the base.
        var map = Sandbox();
        var mcv = map.Units.Single(u => u.Owner == new PlayerId(1) && u.Type == UnitType.Mcv).Cell;
        var near = Cells(map.Grid).Where(c => System.Math.Max(System.Math.Abs(c.X - mcv.X), System.Math.Abs(c.Y - mcv.Y)) <= 16).ToList();

        Assert.True(near.Count(c => map.Grid.OreAt(c).Kind == OreKind.Ore) >= 40, "too little ore near the blue base");
        Assert.Contains(near, c => map.Grid.OreAt(c).Kind == OreKind.Gems);
    }

    [Fact]
    public void Smoke_route_from_the_blue_base_to_the_centre_is_passable()
    {
        // --smoke orders every blue unit straight to the centre of cell (32, 32).
        var map = Sandbox();
        var target = new CellPos(32, 32).Center;

        foreach (var unit in map.Units.Where(u => u.Owner == new PlayerId(1)))
        {
            var locomotor = TestRules.Unit(unit.Type).Locomotor;
            var start = unit.Cell.Center;
            for (var step = 0; step <= 100; step++)
            {
                var point = new WorldPos(start.X + (target.X - start.X) * step / 100, start.Y + (target.Y - start.Y) * step / 100);
                Assert.True(map.Grid.CanEnter(point.ToCell(), locomotor), $"{unit.Type} is blocked at {point.ToCell()}.");
            }
        }
    }
}
