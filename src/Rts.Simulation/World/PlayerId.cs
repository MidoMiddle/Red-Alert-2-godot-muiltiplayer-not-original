namespace Rts.Simulation.World;

/// <summary>
/// Player identifier. 0 is the neutral player (owns neutral buildings and map objects, takes no part
/// in victory conditions); 1–8 are human or bot players (docs/implementation/conventions.md, section 4).
/// </summary>
public readonly record struct PlayerId(int Value)
{
    public const int MaxPlayers = 8;

    public static readonly PlayerId Neutral = new(0);

    public bool IsNeutral => Value == 0;

    public override string ToString() => IsNeutral ? "Neutral" : "P" + Value;
}
