using System;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>A command addressed to a list of units (docs/implementation/M2_unit_control.md, 4.10).</summary>
public abstract record UnitsCommand : Command
{
    /// <summary>More than a player can select at once; keeps network packets bounded (M2 decision).</summary>
    public const int MaxUnits = 256;

    protected UnitsCommand(PlayerId player, int executeTick, uint sequence, ImmutableArray<EntityId> units)
        : base(player, executeTick, sequence)
    {
        if (units.IsDefault)
        {
            throw new ArgumentException("Units must be initialized.", nameof(units));
        }

        if (units.Length > MaxUnits)
        {
            throw new ArgumentException($"At most {MaxUnits} units per command, got {units.Length}.", nameof(units));
        }

        Units = units;
    }

    public ImmutableArray<EntityId> Units { get; }

    // Records compare ImmutableArray by reference; commands must compare by content.
    public virtual bool Equals(UnitsCommand? other) =>
        other is not null && base.Equals(other) && Units.AsSpan().SequenceEqual(other.Units.AsSpan());

    public override int GetHashCode()
    {
        var hash = base.GetHashCode();
        foreach (var unit in Units)
        {
            hash = unchecked(hash * 31 + unit.Value);
        }

        return hash;
    }
}

/// <summary>
/// Orders the units to <see cref="Target"/>. With <see cref="Queue"/> the point is added to the end of their route instead of
/// replacing it.
/// </summary>
public sealed record MoveCommand(PlayerId Player, int ExecuteTick, uint Sequence, ImmutableArray<EntityId> Units, WorldPos Target, bool Queue = false)
    : UnitsCommand(Player, ExecuteTick, Sequence, Units)
{
    public override CommandType Type => CommandType.Move;
}

/// <summary>
/// Like <see cref="MoveCommand"/>, but the units are to attack what they meet on the way. Attacking comes in M4; until then the
/// route leg only carries the mark.
/// </summary>
public sealed record AttackMoveCommand(PlayerId Player, int ExecuteTick, uint Sequence, ImmutableArray<EntityId> Units, WorldPos Target, bool Queue = false)
    : UnitsCommand(Player, ExecuteTick, Sequence, Units)
{
    public override CommandType Type => CommandType.AttackMove;
}

/// <summary>Stops the units where they are and forgets their route.</summary>
public sealed record StopCommand(PlayerId Player, int ExecuteTick, uint Sequence, ImmutableArray<EntityId> Units)
    : UnitsCommand(Player, ExecuteTick, Sequence, Units)
{
    public override CommandType Type => CommandType.Stop;
}

/// <summary>Sends every unit to a random free neighbouring cell (the simulation's random generator, units in id order).</summary>
public sealed record ScatterCommand(PlayerId Player, int ExecuteTick, uint Sequence, ImmutableArray<EntityId> Units)
    : UnitsCommand(Player, ExecuteTick, Sequence, Units)
{
    public override CommandType Type => CommandType.Scatter;
}

/// <summary>A route through <see cref="Points"/> in order; with <see cref="Loop"/> it starts over after the last point (patrol).</summary>
public sealed record WaypointsCommand : UnitsCommand
{
    public const int MaxPoints = 32;

    public WaypointsCommand(PlayerId player, int executeTick, uint sequence, ImmutableArray<EntityId> units, ImmutableArray<WorldPos> points, bool loop)
        : base(player, executeTick, sequence, units)
    {
        if (points.IsDefault || points.Length is 0 or > MaxPoints)
        {
            throw new ArgumentException($"A route has 1…{MaxPoints} points.", nameof(points));
        }

        Points = points;
        Loop = loop;
    }

    public ImmutableArray<WorldPos> Points { get; }

    public bool Loop { get; }

    public override CommandType Type => CommandType.Waypoints;

    public bool Equals(WaypointsCommand? other) =>
        other is not null && base.Equals(other) && Loop == other.Loop && Points.AsSpan().SequenceEqual(other.Points.AsSpan());

    public override int GetHashCode()
    {
        var hash = base.GetHashCode() * 2 + (Loop ? 1 : 0);
        foreach (var point in Points)
        {
            hash = unchecked(hash * 31 + point.X * 7 + point.Y);
        }

        return hash;
    }
}
