using System;
using Rts.Client.App;
using Rts.Simulation.Combat;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The health bar of RA2 as small squares — pips (docs/implementation/M4_combat.md, 4.20, step 13): 4 for a soldier, 8 for a vehicle,
/// 2 per cell of width for a building up to 12; as many filled as the share of hit points, rounded up, in the colour of the
/// <see cref="DamageState"/>. Shown for everybody, for the selected and the damaged, or — the default, as in RA2 (the owner, 2026-10-25) —
/// for the selected only; "selected" takes in what is under the cursor. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public static class HealthPips
{
    public const int Infantry = 4;
    public const int Vehicle = 8;
    public const int MaxBuilding = 12;

    /// <summary>The number of pips of a unit or a building.</summary>
    public static int CountOf(Entity entity) => entity switch
    {
        Unit { Archetype.Category: UnitCategory.Infantry } => Infantry,
        Unit => Vehicle,
        Building building => Math.Min(MaxBuilding, 2 * building.Width),
        _ => 0,
    };

    /// <summary>How many of <paramref name="pips"/> are filled at <paramref name="hp"/> of <paramref name="max"/>.</summary>
    public static int Filled(int pips, int hp, int max) =>
        hp <= 0 || max <= 0 ? 0 : (int)Math.Min(pips, ((long)pips * hp + max - 1) / max);

    /// <summary>
    /// Whether a bar shows: always, for the selected and the damaged, or for the selected only; <paramref name="selected"/> is true for
    /// what is selected or under the cursor.
    /// </summary>
    public static bool Shows(HealthBarMode mode, bool selected, int hp, int max) => mode switch
    {
        HealthBarMode.Always => true,
        HealthBarMode.Selected => selected,
        _ => selected || hp < max,
    };

    /// <summary>The colour of the filled pips, RGB 0…1: green whole, yellow damaged, red critical.</summary>
    public static (float R, float G, float B) ColorOf(int hp, int max) => DamageStates.Of(hp, max) switch
    {
        DamageState.Healthy => (0.2f, 0.85f, 0.25f),
        DamageState.Damaged => (0.95f, 0.8f, 0.15f),
        _ => (0.9f, 0.2f, 0.15f),
    };
}
