using Rts.Client.Maps;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;

namespace Rts.Data.Tests.Maps;

/// <summary>Where a map comes from (the header's <c>origin</c>, <see cref="MapOrigin"/>): kept in the file, filtered and shown by the menus.</summary>
public sealed class MapOriginTests
{
    private static string MapsFolder => Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps");

    [Fact]
    public void The_origin_survives_a_write_and_a_read_and_a_map_without_it_is_the_players()
    {
        var heaven = RtsMapReader.Read(File.ReadAllText(Path.Combine(MapsFolder, "heaven.rtsmap")));
        var text = RtsMapWriter.Write(heaven);

        Assert.Equal(MapOrigin.Ra2, heaven.Origin);
        Assert.Contains("\norigin: ra2\n", text);
        Assert.True(heaven.ContentEquals(RtsMapReader.Read(text)));
        Assert.Equal(MapOrigin.User, SampleMaps.Full().Origin);
        Assert.DoesNotContain("origin", RtsMapWriter.Write(SampleMaps.Full()));
    }

    [Fact]
    public void An_unknown_origin_points_at_its_value()
    {
        var text = File.ReadAllText(Path.Combine(MapsFolder, "heaven.rtsmap")).Replace("origin: ra2", "origin: found");

        var error = Assert.Throws<RtsMapFormatException>(() => RtsMapReader.Read(text));

        Assert.Contains("origin 'found' is none of ra2, ready, generated, test, user", error.Reason);
    }

    [Fact]
    public void Every_map_of_the_game_says_where_it_comes_from()
    {
        foreach (var path in Directory.GetFiles(MapsFolder, "*.rtsmap"))
        {
            var map = RtsMapReader.Read(File.ReadAllText(path));
            var expected = map.Author == "Map generator" ? MapOrigin.Generated
                : map.Author.EndsWith("converted", StringComparison.Ordinal) ? MapOrigin.Ra2
                : map.Author == "Ready maps (M7)" ? MapOrigin.Ready
                : MapOrigin.Test;
            Assert.True(expected == map.Origin, $"{Path.GetFileName(path)}: {map.Origin}, expected {expected}");
        }
    }

    [Fact]
    public void The_catalog_filters_by_origin_and_a_map_saved_in_the_editor_is_the_players()
    {
        var files = Directory.GetFiles(MapsFolder, "*.rtsmap").Order(StringComparer.Ordinal)
            .Select(path => ($"{MapCatalog.Directory}/{Path.GetFileName(path)}", File.ReadAllText(path)));
        var (maps, _) = MapCatalog.Load(files);

        Assert.Equal(["Facing Corners", "Heaven", "Steph's High Ground", "WAR Pro"], MapCatalog.OfOrigin(maps, MapOrigin.Ra2).Select(m => m.Map.Name));
        Assert.All(MapCatalog.OfOrigin(maps, MapOrigin.Generated), m => Assert.Equal("Map generator", m.Map.Author));
        Assert.Equal(maps.Count, MapCatalog.OfOrigin(maps, null).Count());

        var mine = EditableMap.FromDefinition(maps.First(m => m.Map.Origin == MapOrigin.Generated).Map);
        mine.Origin = MapOrigin.User;
        Assert.DoesNotContain("origin", RtsMapWriter.Write(mine.ToDefinition()));
    }

    [Fact]
    public void The_editor_deletes_a_players_map_always_and_one_of_the_game_only_from_the_project()
    {
        Assert.True(MapCatalog.CanDelete($"{MapCatalog.UserDirectory}/my-hill.rtsmap", fromProject: false));
        Assert.True(MapCatalog.CanDelete($"{MapCatalog.Directory}/glacier-duel.rtsmap", fromProject: true));
        Assert.False(MapCatalog.CanDelete($"{MapCatalog.Directory}/glacier-duel.rtsmap", fromProject: false));
        Assert.False(MapCatalog.CanDelete("res://scenes/Main.tscn", fromProject: true));
    }
}
