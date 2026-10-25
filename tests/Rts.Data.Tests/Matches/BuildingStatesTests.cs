using Rts.Client.Models;
using Rts.Simulation.Combat;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The look of a building by its state and its soldiers (docs/map_elements/11_civilian_buildings/appearance.md, 5): whole and damaged
/// ones take soldiers and show them; a critically damaged one is abandoned, and its ruins are drawn once it falls.
/// </summary>
public sealed class BuildingStatesTests
{
    [Theory]
    [InlineData(DamageState.Healthy, false, ModelVariant.Intact)]
    [InlineData(DamageState.Healthy, true, ModelVariant.Occupied)]
    [InlineData(DamageState.Damaged, false, ModelVariant.Damaged)]
    [InlineData(DamageState.Damaged, true, ModelVariant.DamagedOccupied)]
    [InlineData(DamageState.Critical, false, ModelVariant.Abandoned)]
    [InlineData(DamageState.Critical, true, ModelVariant.Abandoned)]
    [InlineData(DamageState.Destroyed, false, ModelVariant.Ruins)]
    public void A_building_takes_the_look_of_its_state_and_its_soldiers(DamageState state, bool occupied, ModelVariant expected) =>
        Assert.Equal(expected, BuildingStates.Of(state, occupied));
}
