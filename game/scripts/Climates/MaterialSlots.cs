using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Climates;

/// <summary>
/// The ground material of every cell for the ground shader (docs/implementation/M7_maps.md, 4.12), Godot-free: two bytes per
/// cell, the slot of its material in the climate (<see cref="ClimateLook.Materials"/>) and its water (0 none, 1 shallow, 2 deep,
/// by the land of the rules). Cliffs take the climate's cliff material whatever the texture says; a material the climate lacks
/// falls back to the one of the cell's land, then to the first slot.
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

        var cliff = (byte)climate.SlotOf(climate.Cliff);
        var cells = new byte[map.Width * map.Height * BytesPerCell];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var index = y * map.Width + x;
                var land = map.LandAt(cell);
                cells[index * BytesPerCell] = map.Heights.ShapeAt(cell) == CellShape.Cliff ? cliff : SlotOf(look.MaterialAt(index), land, climate);
                cells[index * BytesPerCell + 1] = land switch
                {
                    LandType.Shallow => 1,
                    LandType.Deep => 2,
                    _ => 0,
                };
            }
        }

        return cells;
    }

    private static byte SlotOf(string? material, LandType land, ClimateLook climate)
    {
        var slot = material is null ? -1 : climate.SlotOf(material);
        if (slot < 0)
        {
            slot = climate.SlotOf(Rts.Data.Maps.TestMapConverter.MaterialOf(land));
        }

        return (byte)System.Math.Max(slot, 0);
    }
}
