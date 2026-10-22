using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Data.Tests.Maps;
using Rts.MapTool;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Economy;

/// <summary>
/// The economy in the default sandbox (game/maps/test/sandbox.txt): the ore field by the blue base feeds a refinery's harvester, so
/// the run of step 14 from the blue MCV's cell gets its base and ten tanks there as on a ready map (first unload at 2:15, the tenth
/// tank at 9:56).
/// </summary>
public sealed class SandboxEconomyTests
{
    private static readonly string SandboxPath = Path.Combine(SampleMaps.RepoRoot, "game", "maps", "test", "sandbox.txt");

    [Fact]
    public void A_refinery_harvester_brings_credits_in_the_sandbox()
    {
        var map = SandboxWithStart();
        var sandbox = TestMapParser.Parse(File.ReadAllText(SandboxPath), GameDataFiles.Rules).Grid;
        var grid = map.ToMapGrid();
        // The same ground and ore; only the order of the map objects differs (the map file sorts them by type).
        Assert.Equal(sandbox.OreBytes(), grid.OreBytes());
        Assert.Equal(sandbox.LandBytes(), grid.LandBytes());
        Assert.Equal(sandbox.BlockedBytes(), grid.BlockedBytes());
        Assert.Equal(
            sandbox.Objects.Select(o => (o.Kind, o.Cell.X, o.Cell.Y, o.Variant)).Order(),
            grid.Objects.Select(o => (o.Kind, o.Cell.X, o.Cell.Y, o.Variant)).Order());

        var report = EconomyRun.Play(map, GameDataFiles.Rules);

        Assert.True(report.Unloads > 0, "no harvester unloaded");
        // The field lies by the refinery and the gems pay double, so the harvester brings in more than on frozen-crossing (about 1400).
        Assert.True(report.IncomePerHarvesterMinute >= 850, $"income {report.IncomePerHarvesterMinute} per harvester-minute");
        Assert.Equal(10, report.TankTicks.Count);
        Assert.True(report.LowestCredits >= 0);
    }

    /// <summary>
    /// The sandbox as a map with a start at the blue MCV (20, 22) and its ore, which the converter leaves out; the run plays maps with
    /// starts. The stages of the sandbox (ore 8, gems 11) are densities of the map file, so the ground and the ore of the match are the same.
    /// </summary>
    private static MapDefinition SandboxWithStart()
    {
        var test = TestMapParser.Parse(File.ReadAllText(SandboxPath), GameDataFiles.Rules);
        var map = TestMapConverter.Convert(test, "sandbox", "Sandbox");
        var ore = new byte[map.Width * map.Height];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = test.Grid.OreAt(new CellPos(x, y));
                ore[y * map.Width + x] = cell.IsEmpty
                    ? (byte)0
                    : Enumerable.Range(1, MapDefinition.MaxOre).Select(d => (byte)d).Single(d => MapDefinition.OreOf(d) == cell);
            }
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players,
            [new MapStart(1, new CellPos(20, 22))],
            map.TextureLegend, map.Heights, map.Land, map.Texture, ImmutableArray.Create(ore), map.Objects, map.Bridges, map.Structures);
    }
}
