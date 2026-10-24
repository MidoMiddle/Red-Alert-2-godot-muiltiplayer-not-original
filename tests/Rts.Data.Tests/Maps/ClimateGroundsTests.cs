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
    }

    [Fact]
    public void The_editor_takes_a_winter_map_to_the_desert_role_by_role()
    {
        var session = new MapEditSession(EditableMap.New(16, 16, 2));
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
