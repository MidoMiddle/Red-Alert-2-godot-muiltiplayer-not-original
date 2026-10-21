using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Data.Maps;

/// <summary>
/// Debug squads for playing a <c>.rtsmap</c> map in the sandbox before skirmish starts exist (M6): slot 1 gets one unit of
/// every type in a 4 x 2 block from its start, the other slots four units (heavy tank, support vehicle, conscript,
/// rocketeer) in a 2 x 2 block, like the red group of the sandbox. Cells off the map or closed to a unit are skipped.
/// </summary>
public static class StartSquads
{
    private static readonly UnitType[] Others = [UnitType.HeavyTank, UnitType.SupportVehicle, UnitType.Conscript, UnitType.Rocketeer];

    public static IReadOnlyList<TestMapUnit> Place(MapDefinition map, MapGrid grid)
    {
        var units = new List<TestMapUnit>();
        foreach (var start in map.Starts)
        {
            var player = new PlayerId(start.Slot);
            var types = new List<UnitType>();
            if (start.Slot == 1)
            {
                foreach (var archetype in UnitArchetypes.All)
                {
                    types.Add(archetype.Type);
                }
            }
            else
            {
                types.AddRange(Others);
            }

            var columns = start.Slot == 1 ? 4 : 2;
            for (var i = 0; i < types.Count; i++)
            {
                var cell = new CellPos(start.Cell.X + i % columns, start.Cell.Y + i / columns);
                if (grid.CanEnter(cell, UnitArchetypes.Get(types[i]).Locomotor))
                {
                    units.Add(new TestMapUnit(types[i], cell, player));
                }
            }
        }

        return units;
    }
}
