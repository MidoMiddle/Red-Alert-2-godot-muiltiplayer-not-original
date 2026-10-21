using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Debug command (docs/implementation/M7_maps.md, step 5): takes <paramref name="Amount"/> hit points (1…10 000) off the first
/// destructible standing map object on <paramref name="Cell"/>. Until the weapons of M4, which will go the same way.
/// </summary>
public sealed record DamageMapObjectCommand(PlayerId Player, int ExecuteTick, uint Sequence, CellPos Cell, int Amount)
    : Command(Player, ExecuteTick, Sequence)
{
    public const int MaxAmount = 10_000;

    public override CommandType Type => CommandType.DamageMapObject;
}
