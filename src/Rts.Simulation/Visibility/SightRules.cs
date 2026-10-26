using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Visibility;

/// <summary>
/// How far a unit sees (docs/implementation/M7_maps.md, 4.2, step 8): the base sight of its type plus one cell for every two
/// height levels of the cell it stands on, at most four. The level of a cell is its lowest corner, so a slope counts by its
/// foot. The relief never hides anything, as in RA2. A cell is in sight when <c>dx² + dy² ≤ r²</c>. Integers only. The fog of
/// war of M4 calls this whenever a unit enters another cell; until then nothing in the match does, so the rule changes no
/// state.
/// </summary>
public static class SightRules
{
    /// <summary>Height levels for one more cell of sight.</summary>
    public const int LevelsPerBonusCell = 2;

    /// <summary>The most cells height can add.</summary>
    public const int MaxHeightBonus = 4;

    /// <summary>Cells of sight a cell of <paramref name="level"/> adds: 0 for levels 0–1, +1 for 2–3, … +4 from level 8.</summary>
    public static int HeightBonus(int level) => Math.IntMath.Min(Math.IntMath.Max(level, 0) / LevelsPerBonusCell, MaxHeightBonus);

    /// <summary>The sight radius in cells of a unit with <paramref name="baseSight"/> standing on <paramref name="cell"/>.</summary>
    public static int SightAt(MapGrid map, CellPos cell, int baseSight) => baseSight + HeightBonus(map.Heights.CellLevel(cell));

    /// <summary>The sight radius of <paramref name="unit"/> where it stands now.</summary>
    /// <remarks>On a high bridge's deck (<see cref="Decks"/>) the unit sees from the deck's level.</remarks>
    public static int SightOf(Unit unit, MapGrid map) =>
        unit.OnDeck && map.DeckSlot(unit.Position.ToCell()) is var slot and >= 0
            ? unit.Archetype.Sight + HeightBonus(map.DeckLevel(slot))
            : SightAt(map, unit.Position.ToCell(), unit.Archetype.Sight);

    /// <summary>True when a cell <paramref name="dx"/>, <paramref name="dy"/> cells away lies within <paramref name="radius"/>.</summary>
    public static bool InSight(int dx, int dy, int radius) => (long)dx * dx + (long)dy * dy <= (long)radius * radius;
}
