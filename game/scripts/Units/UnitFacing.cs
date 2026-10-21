using System;
using Rts.Simulation.Math;

namespace Rts.Client.Units;

/// <summary>
/// Simulation angles in Godot space. A <see cref="BAngle"/> grows clockwise from north (−Y on the map, −Z in Godot),
/// while a positive Godot rotation around +Y turns counterclockwise, so the yaw is the negated angle.
/// Pure math without Godot types, tested in Rts.Simulation.Tests.
/// </summary>
public static class UnitFacing
{
    private const float RadiansPerUnit = 2f * MathF.PI / BAngle.FullTurn;

    /// <summary>Rotation around Godot +Y, in radians, that turns a model looking along −Z toward <paramref name="angle"/>.</summary>
    public static float ToYaw(BAngle angle) => -angle.Value * RadiansPerUnit;

    /// <summary>
    /// Yaw between two ticks: <paramref name="alpha"/> 0 is <paramref name="from"/>, 1 is <paramref name="to"/>,
    /// always along the shorter arc, so a turn through north does not spin the model the long way round.
    /// </summary>
    public static float InterpolatedYaw(BAngle from, BAngle to, float alpha) =>
        -(from.Value + BAngle.Difference(from, to) * alpha) * RadiansPerUnit;
}
