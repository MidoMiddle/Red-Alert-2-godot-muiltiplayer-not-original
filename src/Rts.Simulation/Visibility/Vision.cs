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
/// <para>
/// Stage 2d: a cell a veil generator of a player outside the team veils (<see cref="IsVeiled"/>) counts as unexplored for the team —
/// shroud, and no building or unit seen there from memory — unless one of the team sees it now: who comes near enough sees in.
/// </para>
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
            if ((allies & (1 << i)) != 0 && SeenBy(world, player, world.VisionOf(new PlayerId(i + 1)), entity, explored))
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
    public static bool IsVisible(GameWorld world, PlayerId player, Geometry.CellPos cell) =>
        NoFog(world) ? IsExplored(world, player, cell) : InSightOfTeam(world, player, cell);

    /// <summary>
    /// Whether the player or one of its allies has ever seen <paramref name="cell"/> (M6, A6) — and, if a veil from outside the team
    /// covers it, sees it now (stage 2d).
    /// </summary>
    public static bool IsExplored(GameWorld world, PlayerId player, Geometry.CellPos cell)
    {
        var allies = world.AlliesOf(player);
        var explored = false;
        for (var i = 0; i < PlayerId.MaxPlayers && !explored; i++)
        {
            explored = (allies & (1 << i)) != 0 && world.VisionOf(new PlayerId(i + 1)).IsExplored(cell);
        }

        return explored && (!IsVeiled(world, player, cell) || InSightOfTeam(world, player, cell));
    }

    /// <summary>
    /// Whether a veil generator of a player outside the team of <paramref name="player"/> veils <paramref name="cell"/> from it (stage 2d).
    /// </summary>
    public static bool IsVeiled(GameWorld world, PlayerId player, Geometry.CellPos cell)
    {
        var allies = world.AlliesOf(player);
        for (var i = 0; i < PlayerId.MaxPlayers; i++)
        {
            if ((allies & (1 << i)) == 0 && world.VisionOf(new PlayerId(i + 1)).IsVeiled(cell))
            {
                return true;
            }
        }

        return false;
    }

    // Whether the player or one of its allies has the cell in sight now.
    private static bool InSightOfTeam(GameWorld world, PlayerId player, Geometry.CellPos cell)
    {
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

    /// <summary>What the team of the player knows of <paramref name="cell"/> (M6, A6); a player alone, its own grid's state.</summary>
    public static CellVisibility StateAt(GameWorld world, PlayerId player, Geometry.CellPos cell) =>
        IsVisible(world, player, cell) ? CellVisibility.Visible : IsExplored(world, player, cell) ? CellVisibility.Fog : CellVisibility.Shroud;

    // In sight of the grid now — or, without the fog of war, ever explored by it and not veiled from the player (stage 2d).
    private static bool SeenBy(GameWorld world, PlayerId player, VisibilityGrid grid, Entity entity, bool explored)
    {
        bool Seen(Geometry.CellPos cell) =>
            grid.IsVisible(cell) || (explored && grid.IsExplored(cell) && !IsVeiled(world, player, cell));

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
