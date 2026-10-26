using Rts.Data.Maps;
using Rts.Simulation.Geometry;

namespace Rts.MapGen.Tests;

/// <summary>
/// The owner's rule (2026-10-26): on the maps from RA2 no gem drill stands by a base, there are far fewer of them than the one by
/// every start they had, and they stand in the middle ground at about the same tank way from every start (<see cref="Drills.ShareGems"/>).
/// </summary>
public sealed class Ra2GemDrillsTests
{
    public static TheoryData<string> Ra2Maps() => [.. Ra2Ids()];

    private static List<string> Ra2Ids() =>
        [.. Directory.GetFiles(Folder, "*.rtsmap").Select(file => RtsMapReader.Read(File.ReadAllText(file)))
            .Where(map => map.Origin == MapOrigin.Ra2).Select(map => map.Id).Order()];

    private static string Folder => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "game", "data", "maps");

    private static MapDefinition Load(string id) => RtsMapReader.Read(File.ReadAllText(Path.Combine(Folder, id + ".rtsmap")));

    [Fact]
    public void The_game_has_the_four_maps_from_RA2()
    {
        Assert.Equal(["facing-corners", "heaven", "stephs-high-ground", "war-pro"], Ra2Ids());
    }

    [Theory]
    [MemberData(nameof(Ra2Maps))]
    public void No_gem_drill_stands_within_the_base_radius_of_a_start(string id)
    {
        var map = Load(id);
        var gems = map.Objects.Where(o => o.Type == Drills.GemSource).ToList();
        Assert.NotEmpty(gems);
        foreach (var gem in gems)
        {
            Assert.All(map.Starts, start => Assert.True(
                Distance2(gem.Cell, start.Cell) > Drills.BaseRadius * Drills.BaseRadius,
                $"the gem drill at {gem.Cell.X},{gem.Cell.Y} is within {Drills.BaseRadius} cells of start {start.Slot}"));
        }
    }

    [Theory]
    [MemberData(nameof(Ra2Maps))]
    public void A_map_has_at_most_one_gem_drill_for_two_starts(string id)
    {
        // Before: one by every start. War Pro 8 → 1, Heaven and Steph's High Ground 4 → 1, Facing Corners 4 → 2.
        var map = Load(id);
        Assert.InRange(map.Objects.Count(o => o.Type == Drills.GemSource), 1, map.Starts.Length / 2);
        Assert.Equal(map.Starts.Length, map.Objects.Count(o => o.Type == Drills.OreSource));
    }

    [Theory]
    [MemberData(nameof(Ra2Maps))]
    public void Every_start_has_about_as_far_to_its_nearest_gem_drill(string id)
    {
        var map = Load(id);
        var ground = new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, [.. map.Objects.Where(o => o.Type != Drills.GemSource)],
            map.Bridges, map.Structures, map.Lighting, map.Playable, map.Origin).ToMapGrid();
        var gems = map.Objects.Where(o => o.Type == Drills.GemSource).Select(o => o.Cell).ToList();
        var ways = map.Starts.Select(start =>
        {
            var costs = Drills.PathCosts(ground, start.Cell);
            return gems.Min(cell => costs[cell.Y * map.Width + cell.X]);
        }).ToList();
        Assert.DoesNotContain(int.MaxValue, ways);
        Assert.True((ways.Max() - ways.Min()) * 100 <= ways.Max() * Drills.FairShare, $"tank ways {string.Join("/", ways.Select(w => w / 10))}");
    }

    [Theory]
    [MemberData(nameof(Ra2Maps))]
    public void The_file_is_what_share_gems_writes(string id)
    {
        var map = Load(id);
        Assert.Equal(RtsMapWriter.Write(map), RtsMapWriter.Write(Drills.ShareGems(map)));
    }

    private static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
