using Rts.Data.Tests.Maps;

namespace Rts.Data.Tests.Rules;

/// <summary>
/// The names of RA2 the interface shows for now (the owner's wish, 2026-10-24): every unit and building of the data has its key
/// <c>RA2_NAME_&lt;ID&gt;</c> in en.csv, and every such key names one.
/// </summary>
public sealed class Ra2NamesTests
{
    [Fact]
    public void Every_unit_and_building_has_its_name_of_ra2()
    {
        var lines = File.ReadAllLines(Path.Combine(SampleMaps.RepoRoot, "game", "localization", "en.csv"));
        var names = lines
            .Where(line => line.StartsWith("RA2_NAME_", StringComparison.Ordinal))
            .Select(line => line.Split(',', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
        var database = GameDataFiles.Current.Database;
        // The buildings of the map too: the names over the field show them.
        var ids = database.Units.Select(unit => unit.Id).Concat(database.Buildings.Select(building => building.Id))
            .Concat(Rts.Simulation.World.BuildingTypes.All.Select(type => type.Id)).ToList();

        Assert.All(ids, id => Assert.True(names.TryGetValue("RA2_NAME_" + id.ToUpperInvariant(), out var name) && name.Length > 0, id));
        Assert.Equal(ids.Count, names.Count);
        Assert.Equal("GI", names["RA2_NAME_ALLIANCE_RIFLEMAN"]);
        Assert.Equal("Iron Curtain", names["RA2_NAME_IRON_CURTAIN"]);
    }
}
