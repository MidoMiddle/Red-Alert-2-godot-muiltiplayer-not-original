using Rts.Simulation.World;

namespace Rts.Simulation.Visibility;

/// <summary>
/// What a player sees (docs/implementation/M4_combat.md, 4.12, 4.14): its own units and buildings always; another's unit while the cell
/// it stands on is visible to the player or one of its allies; another's building while one cell of its footprint is. Only what is in
/// sight now counts, not what was seen before (owner's decision, M4 step 11). An order to attack what its player does not see is
/// dropped, so orders cannot be a map hack; the choice of targets takes only what its owner sees.
/// </summary>
public static class Vision
{
    public static bool Sees(GameWorld world, PlayerId player, Entity entity)
    {
        if (entity.Owner == player)
        {
            return true;
        }

        var allies = world.AlliesOf(player);
        for (var i = 0; i < PlayerId.MaxPlayers; i++)
        {
            if ((allies & (1 << i)) != 0 && SeenBy(world.VisionOf(new PlayerId(i + 1)), entity))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SeenBy(VisibilityGrid grid, Entity entity)
    {
        switch (entity)
        {
            case Unit unit:
                return grid.IsVisible(unit.Position.ToCell());
            case Building building:
                for (var y = building.Cell.Y; y <= building.FarCell.Y; y++)
                {
                    for (var x = building.Cell.X; x <= building.FarCell.X; x++)
                    {
                        if (grid.IsVisible(new Geometry.CellPos(x, y)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            default:
                return false;
        }
    }
}
