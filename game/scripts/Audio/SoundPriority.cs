namespace Rts.Client.Audio;

/// <summary>
/// How important a sound is, from 0 to 100: when every player of the pool is busy, a sound gives its player to a more important
/// one (docs/implementation/M6_menus_skirmish.md, 4.8). The levels of the plan: EVA 100 above the unit voices 80, the blasts 60,
/// the shots 50, the movement 30 and the ambience 10.
/// </summary>
public static class SoundPriority
{
    public const int Eva = 100;
    public const int Voice = 80;

    /// <summary>Not in the plan's list: the buttons sound above the blasts, below the voices.</summary>
    public const int Interface = 70;

    public const int Explosion = 60;
    public const int Shot = 50;
    public const int Movement = 30;
    public const int Ambience = 10;

    /// <summary>The levels, most important first; the definitions in <c>sounds.yaml</c> use these numbers.</summary>
    public static readonly int[] Levels = [Eva, Voice, Interface, Explosion, Shot, Movement, Ambience];
}
