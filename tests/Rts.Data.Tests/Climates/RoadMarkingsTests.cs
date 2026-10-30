using Rts.Client.Climates;
using Rts.Client.Terrain;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Climates;

/// <summary>The markings of the asphalt (the owner, 2026-10-30): a dashed line along a narrow road, edges, lanes and a strip on a band.</summary>
public sealed class RoadMarkingsTests
{
    // 16 x 12 of clear land with the cells given as road ('=') or highway ('H'), row by row.
    private static byte[] Marks(params string[] rows)
    {
        const int width = 16, height = 12;
        var land = new byte[width * height];
        Array.Fill(land, (byte)LandType.Clear);
        for (var y = 0; y < rows.Length; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
            {
                land[y * width + x] = rows[y][x] switch
                {
                    '=' => (byte)LandType.Road,
                    'H' => (byte)LandType.Highway,
                    _ => (byte)LandType.Clear,
                };
            }
        }

        var map = MapGrid.FromLayers(width, height, land, new byte[width * height]);
        return RoadMarkings.Build(MapLook.FromLand(map), map);
    }

    private static (byte Kind, byte A, byte B, byte C) At(byte[] marks, int x, int y)
    {
        var i = (y * 16 + x) * RoadMarkings.BytesPerCell;
        return (marks[i], marks[i + 1], marks[i + 2], marks[i + 3]);
    }

    [Fact]
    public void A_narrow_road_has_a_line_to_its_next_cells_straight_round_a_bend_and_across_a_corner()
    {
        var marks = Marks(
            "................",
            ".=====..........",
            ".....=..........",
            "......=.........",
            ".......=........");

        Assert.Equal((RoadMarkings.Line, (byte)0b0001_0001, (byte)0, (byte)0), At(marks, 2, 1)); // east and west
        Assert.Equal((byte)0b0001_0001, At(marks, 4, 1).A);
        // The bend: west and south; the diagonal to (6, 3) is left out of (5, 1), which reaches it through (5, 2).
        Assert.Equal((byte)0b0001_0100, At(marks, 5, 1).A);
        // Down the staircase: north and south-east, then north-west and south-east.
        Assert.Equal((byte)0b0100_0010, At(marks, 5, 2).A);
        Assert.Equal((byte)0b0010_0010, At(marks, 6, 3).A);
        Assert.Equal((byte)0b0010_0000, At(marks, 7, 4).A);
        Assert.Equal((byte)0, At(marks, 0, 0).Kind);
    }

    [Fact]
    public void A_band_knows_its_axis_its_width_and_where_each_cell_lies_across_it()
    {
        var marks = Marks(
            "................",
            "..HHHHHHHH......",
            "..HHHHHHHH......",
            "..HHHHHHHH......",
            "................",
            "..==............",
            "..==............",
            "..==............",
            "..==............");

        Assert.Equal((RoadMarkings.Band, (byte)0, (byte)0, (byte)3), At(marks, 4, 1));
        Assert.Equal((RoadMarkings.Band, (byte)0, (byte)1, (byte)3), At(marks, 4, 2));
        Assert.Equal((RoadMarkings.Band, (byte)0, (byte)2, (byte)3), At(marks, 4, 3));
        Assert.Equal((RoadMarkings.Band, (byte)1, (byte)0, (byte)2), At(marks, 2, 6));
        Assert.Equal((RoadMarkings.Band, (byte)1, (byte)1, (byte)2), At(marks, 3, 6));
    }

    [Fact]
    public void A_crossing_and_a_square_are_plain_asphalt()
    {
        var marks = Marks(
            "...=............",
            "..===...........",
            "...=............",
            "................",
            "........========",
            "........========",
            "........========",
            "........========",
            "........========",
            "........========",
            "........========",
            "........========");

        Assert.Equal(RoadMarkings.Plain, At(marks, 3, 1).Kind);
        Assert.Equal(RoadMarkings.Plain, At(marks, 12, 8).Kind);
        Assert.Equal((byte)0, At(marks, 0, 0).Kind);
    }
}
