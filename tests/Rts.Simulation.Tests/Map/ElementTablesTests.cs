using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>The tables of the element kinds: every kind has its row in its place, and the ids are unique (docs/elements_data).</summary>
public sealed class ElementTablesTests
{
    [Fact]
    public void Every_map_object_kind_has_its_row_in_its_place()
    {
        var kinds = Enum.GetValues<MapObjectKind>();

        Assert.Equal(kinds.Length, MapObjectTypes.All.Count);
        foreach (var kind in kinds)
        {
            Assert.True(MapObjectTypes.IsKnown(kind), kind.ToString());
            Assert.Equal(kind, MapObjectTypes.Get(kind).Kind);
        }
    }

    [Fact]
    public void Map_object_ids_are_unique_and_found_by_their_id()
    {
        var ids = MapObjectTypes.All.Select(type => type.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var type in MapObjectTypes.All)
        {
            Assert.True(MapObjectTypes.TryGetById(type.Id, out var found));
            Assert.Equal(type, found);
        }
    }

    [Fact]
    public void Zero_and_kinds_past_the_table_are_unknown()
    {
        Assert.False(MapObjectTypes.IsKnown(0));
        Assert.False(MapObjectTypes.IsKnown((MapObjectKind)(MapObjectTypes.All.Count + 1)));
    }

    [Fact]
    public void Every_building_kind_has_its_row_in_its_place()
    {
        var kinds = Enum.GetValues<BuildingKind>();

        Assert.Equal(kinds.Length, BuildingTypes.All.Count);
        foreach (var kind in kinds)
        {
            Assert.True(BuildingTypes.IsKnown(kind), kind.ToString());
            Assert.Equal(kind, BuildingTypes.Get(kind).Kind);
        }
    }

    [Fact]
    public void Building_ids_are_unique_and_found_by_their_id()
    {
        var ids = BuildingTypes.All.Select(type => type.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var type in BuildingTypes.All)
        {
            Assert.True(BuildingTypes.TryGetById(type.Id, out var found));
            Assert.Equal(type, found);
        }
    }
}
