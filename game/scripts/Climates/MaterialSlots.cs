using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Climates;

/// <summary>
/// The ground material of every cell for the ground shader (docs/implementation/M7_maps.md, 4.12), Godot-free: two bytes per
/// cell, the slot of its material in the climate (<see cref="ClimateLook.Materials"/>) and its water (0 none, 1 shallow, 2 deep,
/// by the land of the rules). A cliff cell keeps its own material too: it shows above and below the wall, and the shader puts the
/// climate's cliff material on the wall itself (<see cref="ClimateLook.Cliff"/>). A material the climate lacks falls back to the
/// one of the cell's land in that climate (<see cref="Rts.Data.Maps.ClimateGrounds"/>), then to the first slot. A dirt road or a path
/// is no strip of its own material but the ground of its neighbours, the most common of them (else the climate's clear ground):
/// the shader wears ruts into it (<c>RoadMarkings.Ruts</c>; the owner, 2026-10-30).
/// </summary>
public static class MaterialSlots
{
    public const int BytesPerCell = 2;

    public static byte[] Build(MapLook look, ClimateLook climate, MapGrid map)
    {
        if (look.Width != map.Width || look.Height != map.Height)
        {
            look = MapLook.FromLand(map);
        }

        var cells = new byte[map.Width * map.Height * BytesPerCell];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var index = y * map.Width + x;
                var land = map.LandAt(cell);
                cells[index * BytesPerCell] = SlotOf(look.MaterialAt(index), land, climate);
                cells[index * BytesPerCell + 1] = land switch
                {
                    LandType.Shallow => 1,
                    LandType.Deep => 2,
                    _ => 0,
                };
            }
        }

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.LandAt(new CellPos(x, y)) is LandType.Track or LandType.Path)
                {
                    cells[(y * map.Width + x) * BytesPerCell] = GroundAround(map, cells, x, y, climate);
                }
            }
        }

        return cells;
    }

    // The most common material of the open ground round the cell (the first of a tie, east first), else the climate's clear ground.
    private static byte GroundAround(MapGrid map, byte[] cells, int x, int y, ClimateLook climate)
    {
        System.Span<int> counts = stackalloc int[256];
        var best = -1;
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var near = new CellPos(x + dx, y + dy);
                if (!map.Contains(near) || map.LandAt(near) is not (LandType.Clear or LandType.Rough or LandType.Beach or LandType.Ice))
                {
                    continue;
                }

                var slot = cells[(near.Y * map.Width + near.X) * BytesPerCell];
                if (++counts[slot] > (best < 0 ? 0 : counts[best]))
                {
                    best = slot;
                }
            }
        }

        return best >= 0 ? (byte)best : SlotOf(null, LandType.Clear, climate);
    }

    private static byte SlotOf(string? material, LandType land, ClimateLook climate)
    {
        var slot = material is null ? -1 : climate.SlotOf(material);
        if (slot < 0)
        {
            slot = climate.SlotOf(Rts.Data.Maps.ClimateGrounds.MaterialOf(climate.Id, land));
        }

        return (byte)System.Math.Max(slot, 0);
    }
}
