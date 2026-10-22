using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Places a building of the player (docs/implementation/M3_base_economy.md, 4.8, 4.14): the definition at <paramref name="DefIndex"/> of
/// the rules' buildings, its north-west cell at <paramref name="Cell"/>. It must be of the player's faction, available by its
/// prerequisites and allowed by <see cref="PlacementRules"/>; it then grows. Free for now: from M3 step 8 only a building that is ready in
/// the production queue may be placed.
/// </summary>
public sealed record PlaceBuildingCommand(PlayerId Player, int ExecuteTick, uint Sequence, int DefIndex, CellPos Cell)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.PlaceBuilding;
}
