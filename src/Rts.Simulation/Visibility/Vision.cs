using Rts.Simulation.World;

namespace Rts.Simulation.Visibility;

/// <summary>
/// What a player sees (docs/implementation/M4_combat.md, 4.12, 4.14): its own units and buildings always; another's unit while the cell
/// it stands on is visible to the player or one of its allies; another's building while one cell of its footprint is. Only what is in
/// sight now counts, not what was seen before (owner's decision, M4 step 11). An order to attack what its player does not see is
/// dropped, so orders cannot be a map hack; the choice of targets takes only what its owner sees. A player with
/// <see cref="Matches.PlayerModifiers.RevealMap"/> — «Brutal», named in the menu — sees everything (M8 step 2); its allies do not.
/// In a match without the fog of war (<see cref="Matches.MatchOptions.FogOfWar"/> off, the skirmish screen's choice) whatever the team
/// has explored stays in sight, as in classic RA2: only the shroud over the unexplored is left.
/// </summary>
public static class Vision
{
    /// <summary>Whether the match of <paramref name="world"/> is played without the fog of war: what was explored stays seen.</summary>
    public static bool NoFog(GameWorld world) => world.Match is { Options.FogOfWar: false };

    public static bool Sees(GameWorld world, PlayerId player, Entity entity)
    {
        if (entity.Owner == player || (!player.IsNeutral && world.Player(player).Modifiers.RevealMap))
        {
            return true;
        }

        var explored = NoFog(world);
        var allies = world.AlliesOf(player);
        for (var i = 0; i < PlayerId.MaxPlayers; i++)
        {
            if ((allies & (1 << i)) != 0 && SeenBy(world.VisionOf(new PlayerId(i + 1)), entity, explored))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the player or one of its allies sees <paramref name="cell"/> now (M6, A6): the fog on screen is the team's. Unlike
    /// <see cref="Sees"/>, <see cref="Matches.PlayerModifiers.RevealMap"/> opens no cell: the screen shows a person, not a bot. Without
    /// the fog of war every cell the team has explored is in sight.
    /// </summary>
    public static bool IsVisible(GameWorld world, PlayerId player, Geometry.CellPos cell)
    {
        if (NoFog(world))
        {
            return IsExplored(world, player, cell);
        }

        var allies = world.AlliesOf(player);
        for (var i = 0; i < PlayerId.MaxPlayers; i++)
        {
            if ((allies & (1 << i)) != 0 && world.VisionOf(new PlayerId(i + 1)).IsVisible(cell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the player or one of its allies has ever seen <paramref name="cell"/> (M6, A6).</summary>
    public static bool IsExplored(GameWorld world, PlayerId player, Geometry.CellPos cell)
    {
        var allies = world.AlliesOf(player);
        for (var i = 0; i < PlayerId.MaxPlayers; i++)
        {
            if ((allies & (1 << i)) != 0 && world.VisionOf(new PlayerId(i + 1)).IsExplored(cell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What the team of the player knows of <paramref name="cell"/> (M6, A6); a player alone, its own grid's state.</summary>
    public static CellVisibility StateAt(GameWorld world, PlayerId player, Geometry.CellPos cell) =>
        IsVisible(world, player, cell) ? CellVisibility.Visible : IsExplored(world, player, cell) ? CellVisibility.Fog : CellVisibility.Shroud;

    // In sight of the grid now — or, without the fog of war, ever explored by it.
    private static bool SeenBy(VisibilityGrid grid, Entity entity, bool explored)
    {
        bool Seen(Geometry.CellPos cell) => explored ? grid.IsExplored(cell) : grid.IsVisible(cell);

        switch (entity)
        {
            case Unit unit:
                return Seen(unit.Position.ToCell());
            case Building building:
                for (var y = building.Cell.Y; y <= building.FarCell.Y; y++)
                {
                    for (var x = building.Cell.X; x <= building.FarCell.X; x++)
                    {
                        if (Seen(new Geometry.CellPos(x, y)))
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
