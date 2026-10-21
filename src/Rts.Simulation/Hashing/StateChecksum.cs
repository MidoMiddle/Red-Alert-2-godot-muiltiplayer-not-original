namespace Rts.Simulation.Hashing;

/// <summary>Checksum of the world state at the start of <see cref="Tick"/>.</summary>
public readonly record struct StateChecksum(int Tick, ulong Value)
{
    public override string ToString() => $"{Tick}:{Value:x16}";
}
