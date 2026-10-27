namespace Rts.Simulation.Rules;

/// <summary>
/// A kind of bridge (docs/implementation/M3_base_economy.md, step 14a; M7, 4.6): the hit points of a segment, its width in cells and
/// whether it is a high bridge of RA2 on piers. The engine knows each kind by its id (<see cref="Map.BridgeTypes"/>).
/// </summary>
public sealed record BridgeDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int SegmentHp { get; init; }

    public required int Width { get; init; }

    public required bool High { get; init; }
}
