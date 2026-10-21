using System;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>Orders the listed units to move to <see cref="Target"/>.</summary>
public sealed record MoveUnitsCommand : Command
{
    /// <summary>More than a player can select at once; keeps network packets bounded (M2 decision).</summary>
    public const int MaxUnits = 256;

    public MoveUnitsCommand(PlayerId player, int executeTick, uint sequence, ImmutableArray<EntityId> units, WorldPos target)
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
        Target = target;
    }

    public ImmutableArray<EntityId> Units { get; }

    public WorldPos Target { get; }

    public override CommandType Type => CommandType.MoveUnits;

    // Records compare ImmutableArray by reference; commands must compare by content.
    public bool Equals(MoveUnitsCommand? other)
    {
        if (other is null || !base.Equals(other) || Target != other.Target || Units.Length != other.Units.Length)
        {
            return false;
        }

        for (var i = 0; i < Units.Length; i++)
        {
            if (Units[i] != other.Units[i])
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = base.GetHashCode() * 31 + Target.GetHashCode();
        foreach (var unit in Units)
        {
            hash = hash * 31 + unit.Value;
        }

        return hash;
    }
}
