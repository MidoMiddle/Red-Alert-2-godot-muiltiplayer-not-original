using Godot;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.Matches;

/// <summary>
/// The colours of the players in the session on screen (<see cref="PlayerPalette"/>): the host sets the match setup when a session starts,
/// the views of units, buildings and the minimap read it.
/// </summary>
public static class PlayerColors
{
    /// <summary>The match setup of the session on screen; null in the sandbox.</summary>
    public static MatchSetup? Match { get; set; }

    public static Color Of(PlayerId owner)
    {
        var (r, g, b) = PlayerPalette.Of(owner, Match);
        return new Color(r, g, b);
    }
}
