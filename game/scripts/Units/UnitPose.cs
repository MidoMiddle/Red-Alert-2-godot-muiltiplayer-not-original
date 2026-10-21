using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Client.Units;

/// <summary>What the renderer interpolates between two ticks: position, hull and turret direction.</summary>
public readonly record struct UnitPose(WorldPos Position, BAngle Facing, BAngle TurretFacing);
