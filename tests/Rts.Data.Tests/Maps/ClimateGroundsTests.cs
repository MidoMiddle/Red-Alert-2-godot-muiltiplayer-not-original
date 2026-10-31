using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>
/// The ground of the climates of RA2 by role: what a cell is made of, the editor carrying a map over to another climate (the
/// generator's: Rts.MapGen.Tests).
/// </summary>
public sealed class ClimateGroundsTests
{
    [Fact]
    public void A_role_has_a_material_in_every_climate()
    {
        Assert.Equal("snow", ClimateGrounds.MaterialOf("winter", LandType.Clear));
        Assert.Equal("grass", ClimateGrounds.MaterialOf("temperate", LandType.Clear));
        Assert.Equal("sand", ClimateGrounds.MaterialOf("desert", LandType.Clear));
        Assert.Equal("water", ClimateGrounds.MaterialOf("desert", LandType.Ice));
        Assert.Equal("clay", ClimateGrounds.Translate("dirt", "winter", "desert"));
        Assert.Equal("pavers", ClimateGrounds.Translate("grass", "winter", "urban"));
        Assert.Equal("grass", ClimateGrounds.Translate("pavers", "urban", "winter"));
        Assert.Equal("sand", ClimateGrounds.Translate("unknown", "winter", "desert"));
        Assert.Equal("grass", ClimateGrounds.MaterialOf("coast", LandType.Clear));
        Assert.Equal("shore", ClimateGrounds.MaterialOf("coast", LandType.Beach));
        Assert.Equal("sand", ClimateGrounds.Translate("dirt", "winter", "coast"));
        Assert.Equal("sand", ClimateGrounds.Translate("clay", "desert", "coast"));
    }

    [Fact]
    public void The_shallows_of_the_coast_lie_over_a_sandy_shoal_and_elsewhere_are_water()
    {
        Assert.Equal("shoal", ClimateGrounds.MaterialOf("coast", LandType.Shallow));
        Assert.Equal("water", ClimateGrounds.MaterialOf("coast", LandType.Deep));
        Assert.Equal("water", ClimateGrounds.MaterialOf("winter", LandType.Shallow));
        Assert.Equal("water", ClimateGrounds.Translate("shoal", "coast", "temperate"));

        // The editor takes a winter lake to the coast by its depth: the shallow over the shoal, the deep dark; and back to water.
        var session = new MapEditSession(EditableMap.New(16, 16, 2), GameDataFiles.Rules.MapTypes);
        var (shallow, deep) = (new Rts.Simulation.Geometry.CellPos(5, 5), new Rts.Simulation.Geometry.CellPos(9, 9));
        var dot = new Brush(BrushShape.Square, 1);
        session.BeginStroke(EditTool.Water, dot, new EditOptions { Water = LandType.Shallow });
        session.Paint(shallow.X, shallow.Y, shallow);
        session.EndStroke();
        session.BeginStroke(EditTool.Water, dot, new EditOptions { Water = LandType.Deep });
        session.Paint(deep.X, deep.Y, deep);
        session.EndStroke();
        Assert.Equal('w', session.Map.TextureAt(shallow));

        session.SetClimate("coast");
        Assert.Equal('q', session.Map.TextureAt(shallow));
        Assert.Equal('w', session.Map.TextureAt(deep));
        session.SetClimate("winter");
        Assert.Equal('w', session.Map.TextureAt(shallow));
    }

    [Fact]
    public void The_editor_takes_a_winter_map_to_the_desert_role_by_role()
    {
        var session = new MapEditSession(EditableMap.New(16, 16, 2), GameDataFiles.Rules.MapTypes);
        var snow = session.Map.TextureAt(new Rts.Simulation.Geometry.CellPos(3, 3));
        Assert.Equal('s', snow);

        session.SetClimate("desert");
        Assert.Equal("desert", session.Map.Climate);
        Assert.Equal('n', session.Map.TextureAt(new Rts.Simulation.Geometry.CellPos(3, 3)));
        var map = session.Map.ToDefinition();
        Assert.Equal("desert", map.Climate);
        Assert.Contains(map.TextureLegend, entry => entry is { Code: 'n', Material: "sand" });
    }
}
