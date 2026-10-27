using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Map;

/// <summary>
/// The kinds of the map the engine knows (M3 step 14a): every number has its id, the ids are unique and found again, and the rules
/// define every kind under its number (docs/elements_data).
/// </summary>
public sealed class ElementTablesTests
{
    [Fact]
    public void Every_map_object_kind_has_its_id_and_its_record_in_the_rules()
    {
        var kinds = Enum.GetValues<MapObjectKind>();

        Assert.Equal(kinds, MapObjectTypes.All);
        foreach (var kind in kinds)
        {
            Assert.True(MapObjectTypes.TryGetKind(MapObjectTypes.IdOf(kind), out var found));
            Assert.Equal(kind, found);
            Assert.Equal((kind, MapObjectTypes.IdOf(kind)), (TestRules.Standard.MapTypes.Get(kind).Kind, TestRules.Standard.MapTypes.Get(kind).Id));
        }

        Assert.Equal(kinds.Length, MapObjectTypes.All.Select(MapObjectTypes.IdOf).Distinct().Count());
    }

    [Fact]
    public void Zero_and_kinds_past_the_table_are_unknown()
    {
        Assert.False(MapObjectTypes.IsKnown(0));
        Assert.False(MapObjectTypes.IsKnown((MapObjectKind)(MapObjectTypes.All.Count + 1)));
        Assert.False(TestRules.Standard.MapTypes.IsKnown((MapObjectKind)(MapObjectTypes.All.Count + 1)));
        Assert.False(MapObjectTypes.TryGetKind("no_such_thing", out _));
    }

    [Fact]
    public void Every_building_kind_has_its_id_and_its_record_in_the_rules()
    {
        var kinds = Enum.GetValues<BuildingKind>();

        Assert.Equal(kinds, BuildingTypes.All);
        foreach (var kind in kinds)
        {
            Assert.True(BuildingTypes.TryGetKind(BuildingTypes.IdOf(kind), out var found));
            Assert.Equal(kind, found);
            Assert.Equal(BuildingTypes.IdOf(kind), TestRules.Standard.MapTypes.Get(kind).Id);
        }

        Assert.Equal(kinds.Length, BuildingTypes.All.Select(BuildingTypes.IdOf).Distinct().Count());
    }

    [Fact]
    public void Every_bridge_kind_has_its_id_and_its_record_in_the_rules()
    {
        foreach (var kind in Enum.GetValues<BridgeKind>())
        {
            Assert.True(BridgeTypes.TryGetKind(BridgeTypes.IdOf(kind), out var found));
            Assert.Equal(kind, found);
            Assert.Equal(kind, TestRules.Standard.MapTypes.Get(kind).Kind);
        }
    }
}
