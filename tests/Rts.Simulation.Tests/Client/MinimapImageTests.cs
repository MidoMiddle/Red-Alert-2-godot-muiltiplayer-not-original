using Rts.Client.Hud;
using Rts.Client.Terrain;
using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Client;

/// <summary>The ground of the minimap (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class MinimapImageTests
{
    [Fact]
    public void EveryLandTypeKeepsItsColourDarkenedAndTreesAreDarkGreen()
    {
        foreach (var land in Enum.GetValues<LandType>().Where(LocomotorRules.IsKnown))
        {
            var (r, g, b) = LandColors.Of(land);
            Assert.Equal((r * MinimapImage.Shade, g * MinimapImage.Shade, b * MinimapImage.Shade), MinimapImage.ColorOf(land, blocked: false));
        }

        var tree = MinimapImage.TreeColor;
        Assert.Equal((tree.R * MinimapImage.Shade, tree.G * MinimapImage.Shade, tree.B * MinimapImage.Shade), MinimapImage.ColorOf(LandType.Clear, blocked: true));
    }

    [Fact]
    public void PixelsGoRowByRowFromTheNorthWest()
    {
        // 3x2: deep water at (2, 0), a tree at (0, 1).
        byte[] land = [(byte)LandType.Clear, (byte)LandType.Clear, (byte)LandType.Deep, (byte)LandType.Clear, (byte)LandType.Road, (byte)LandType.Clear];
        byte[] blocked = [0, 0, 0, 1, 0, 0];
        var map = MapGrid.FromLayers(3, 2, land, blocked);

        var pixels = MinimapImage.Pixels(map);

        Assert.Equal(3 * 2 * 3, pixels.Length);
        Assert.Equal(Bytes(MinimapImage.ColorOf(LandType.Deep, false)), pixels[6..9]);
        Assert.Equal(Bytes(MinimapImage.ColorOf(LandType.Clear, true)), pixels[9..12]);
        Assert.Equal(Bytes(MinimapImage.ColorOf(LandType.Road, false)), pixels[12..15]);
    }

    [Fact]
    public void ComponentsRoundAndClampToBytes()
    {
        Assert.Equal(0, MinimapImage.ToByte(-0.5f));
        Assert.Equal(128, MinimapImage.ToByte(0.5f));
        Assert.Equal(255, MinimapImage.ToByte(1.2f));
    }

    private static byte[] Bytes((float R, float G, float B) color) =>
        [MinimapImage.ToByte(color.R), MinimapImage.ToByte(color.G), MinimapImage.ToByte(color.B)];
}
