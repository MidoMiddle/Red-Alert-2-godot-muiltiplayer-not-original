using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Orders <paramref name="Count"/> (1…5) items of a unit or building of the player's faction (docs/implementation/M3_base_economy.md,
/// 4.10, 4.14). It must be available by its prerequisites, the player needs a ready factory of its category and room in the queue: a
/// building or defense takes the queue alone, infantry and vehicles queue up to 15. When the first item of the definition is paused, the
/// command resumes it instead.
/// </summary>
public sealed record ProduceCommand(PlayerId Player, int ExecuteTick, uint Sequence, ProductionKind Kind, int DefIndex, int Count)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Produce;
}

/// <summary>Pauses the first unfinished item of the definition: it neither goes on nor costs anything until it is ordered again.</summary>
public sealed record PauseProductionCommand(PlayerId Player, int ExecuteTick, uint Sequence, ProductionKind Kind, int DefIndex)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.PauseProduction;
}

/// <summary>Takes the last item of the definition out of its queue and gives back everything paid for it.</summary>
public sealed record CancelProductionCommand(PlayerId Player, int ExecuteTick, uint Sequence, ProductionKind Kind, int DefIndex)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.CancelProduction;
}

/// <summary>Where the units of the player's factory <paramref name="Building"/> drive once they are out.</summary>
public sealed record SetRallyPointCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Building, WorldPos Point)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.SetRallyPoint;
}
