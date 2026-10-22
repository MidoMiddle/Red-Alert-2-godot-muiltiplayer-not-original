using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.Matches;

/// <summary>
/// The eight colours of the players (docs/implementation/M5_first_bot.md, 4.11; M6, 4.3 — no two players alike): a player plays the
/// colour of its match setup, and without one — the sandbox, a test map — the colour of its slot, so player 1 is blue and 2 red as
/// before. Pure, tested in Rts.Simulation.Tests; <see cref="PlayerColors"/> gives the Godot colours.
/// </summary>
public static class PlayerPalette
{
    /// <summary>Blue, red, green, yellow, orange, purple, teal, pink.</summary>
    public static readonly (float R, float G, float B)[] Colors =
    [
        (0.2f, 0.45f, 0.95f), (0.9f, 0.25f, 0.2f), (0.25f, 0.75f, 0.3f), (0.95f, 0.85f, 0.2f),
        (0.95f, 0.55f, 0.15f), (0.6f, 0.3f, 0.85f), (0.2f, 0.75f, 0.75f), (0.95f, 0.45f, 0.7f),
    ];

    /// <summary>The grey of the neutral player.</summary>
    public static readonly (float R, float G, float B) Neutral = (0.6f, 0.6f, 0.6f);

    /// <summary>The colour of <paramref name="owner"/> in the match of <paramref name="match"/> (or none).</summary>
    public static (float R, float G, float B) Of(PlayerId owner, MatchSetup? match)
    {
        if (owner.IsNeutral)
        {
            return Neutral;
        }

        var index = match?.PlayerOf(owner)?.Color ?? owner.Value - 1;
        return Colors[(index % Colors.Length + Colors.Length) % Colors.Length];
    }
}
