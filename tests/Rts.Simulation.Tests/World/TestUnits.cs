using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>Units with test-chosen speed for movement tests that do not care about the unit type.</summary>
internal static class TestUnits
{
    /// <summary>
    /// A unit with the given speed. The default conscript turns instantly, so a test checks straight-line movement only.
    /// </summary>
    public static Unit SpawnForTest(this GameWorld world, PlayerId owner, WorldPos position, int speed, UnitType type = UnitType.Conscript)
    {
        var unit = world.SpawnUnit(owner, type, position);
        unit.Speed = speed;
        return unit;
    }
}
