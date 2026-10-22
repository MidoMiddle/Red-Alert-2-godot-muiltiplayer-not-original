using Rts.Client.Debug;

namespace Rts.Simulation.Tests.Client;

/// <summary>Colours and modes of the cell overlay F7 (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class OverlayColorsTests
{
    [Fact]
    public void PassabilityIsRedOrangeNothingOrGreen()
    {
        Assert.Equal(OverlayColors.Impassable, OverlayColors.Passability(0));
        Assert.Equal(OverlayColor.None, OverlayColors.Passability(100));
        Assert.Equal((255, 150, 20), Rgb(OverlayColors.Passability(50)));
        Assert.True(OverlayColors.Passability(50).A > OverlayColors.Passability(99).A, "slower is denser");
        Assert.True(OverlayColors.Passability(99).A > 0);
        Assert.Equal((40, 220, 60), Rgb(OverlayColors.Passability(120)));
    }

    [Fact]
    public void OccupancyIsBlueForAVehicleAndYellowDenserWithSoldiers()
    {
        Assert.Equal(OverlayColor.None, OverlayColors.Occupancy(vehicle: false, soldiers: 0));
        Assert.Equal(OverlayColors.Vehicle, OverlayColors.Occupancy(vehicle: true, soldiers: 0));
        var one = OverlayColors.Occupancy(vehicle: false, soldiers: 1);
        var five = OverlayColors.Occupancy(vehicle: false, soldiers: 5);
        Assert.Equal((255, 220, 30), Rgb(one));
        Assert.True(five.A > one.A);
    }

    [Fact]
    public void HeatGrowsUntilItSaturates()
    {
        Assert.Equal(OverlayColor.None, OverlayColors.Heat(0));
        Assert.True(OverlayColors.Heat(1).A > 0);
        Assert.True(OverlayColors.Heat(6).A > OverlayColors.Heat(1).A);
        Assert.Equal(OverlayColors.Heat(OverlayColors.HotUnits), OverlayColors.Heat(100));
    }

    [Fact]
    public void ModesGoRoundAndDescribeThemselves()
    {
        var mode = CellOverlayMode.Off;
        var seen = new List<CellOverlayMode>();
        for (var i = 0; i < 5; i++)
        {
            mode = CellOverlayModes.Next(mode);
            seen.Add(mode);
        }

        Assert.Equal([CellOverlayMode.Passability, CellOverlayMode.Reservations, CellOverlayMode.Buckets, CellOverlayMode.Sight, CellOverlayMode.Off], seen);
        Assert.Equal("cells: sight of the selected (F7)", CellOverlayModes.Describe(CellOverlayMode.Sight, "track"));
        Assert.Equal("cells: passability wheel (F7)", CellOverlayModes.Describe(CellOverlayMode.Passability, "wheel"));
        Assert.Equal("cells off (F7)", CellOverlayModes.Describe(CellOverlayMode.Off, "track"));
    }

    private static (int, int, int) Rgb(OverlayColor color) => (color.R, color.G, color.B);
}
