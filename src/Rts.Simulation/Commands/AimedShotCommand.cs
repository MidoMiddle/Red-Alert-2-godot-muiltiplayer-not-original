using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// The player inside a unit fires it along <paramref name="Yaw"/> and <paramref name="Pitch"/> (the owner, 2026-11-04: aiming as in a
/// shooter): from its eyes, the first unit, building or ground the ray meets within the reach of its weapon is hit
/// (<see cref="Systems.AimedShot"/>). Only a unit under the player's hand (<see cref="TakeControlCommand"/>) with an instant weapon
/// that is loaded fires. <paramref name="Pitch"/> is in the units of <see cref="BAngle"/> (a quarter turn is 16384), up positive.
/// </summary>
public sealed record AimedShotCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Unit, BAngle Yaw, short Pitch)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.AimedShot;
}

/// <summary>
/// The player goes into the unit (<paramref name="On"/>) or out of it: under the player's hand it chooses no targets and chases none
/// by itself (<see cref="Unit.Manual"/>), it fires only the player's aimed shots.
/// </summary>
public sealed record TakeControlCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Unit, bool On)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.TakeControl;
}
