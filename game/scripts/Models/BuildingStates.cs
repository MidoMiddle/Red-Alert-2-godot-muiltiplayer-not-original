using Rts.Simulation.Combat;

namespace Rts.Client.Models;

/// <summary>
/// Which file of a building stands for it (docs/map_elements/11_civilian_buildings/appearance.md, 5): whole, damaged, abandoned
/// (critically damaged: its soldiers came out, it is nobody's), ruins. A whole or a damaged building with soldiers inside shows them
/// at its windows (<see cref="Occupied"/>, <see cref="DamagedOccupied"/>).
/// </summary>
public enum ModelVariant
{
    Intact,
    Occupied,
    Damaged,
    DamagedOccupied,
    Abandoned,
    Ruins,
}

/// <summary>The look of a building by its state and its soldiers. Pure, without Godot, so the tests can read it.</summary>
public static class BuildingStates
{
    /// <summary>
    /// The look of a building in <paramref name="state"/>, with or without soldiers inside. A critically damaged one is abandoned
    /// whatever is inside: the simulation lets its soldiers out with the damage of the tick, and takes none again.
    /// </summary>
    public static ModelVariant Of(DamageState state, bool occupied) => state switch
    {
        DamageState.Destroyed => ModelVariant.Ruins,
        DamageState.Critical => ModelVariant.Abandoned,
        DamageState.Damaged => occupied ? ModelVariant.DamagedOccupied : ModelVariant.Damaged,
        _ => occupied ? ModelVariant.Occupied : ModelVariant.Intact,
    };
}
