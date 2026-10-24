namespace Rts.MapGen.Stages;

/// <summary>Noise and hash channels of the stages: each use draws its own independent noise from the seed.</summary>
internal static class Channels
{
    public const int Height = 1;
    public const int Ramps = 2;
    public const int River = 3;
    public const int Lakes = 4;
    public const int Forest = 5;
    public const int Rough = 6;
    public const int Starts = 7;
    public const int Variant = 8;
    public const int Grass = 9;
    public const int Dirt = 10;

    // The houses of a town (the urban climate).
    public const int Town = 11;
}
