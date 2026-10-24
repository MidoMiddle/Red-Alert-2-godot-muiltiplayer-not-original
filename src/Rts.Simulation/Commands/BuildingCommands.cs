using System;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Sells the player's ready building (docs/implementation/M3_base_economy.md, 4.8): <c>sell_refund</c> of its cost times its share of hit
/// points comes back, and the building goes.
/// </summary>
public sealed record SellCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Building)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Sell;
}

/// <summary>
/// The player's superweapon fires at <paramref name="Target"/> (stage 2d); the Phase Gate takes what stands at <paramref name="Target"/> to
/// <paramref name="Second"/>, the others leave it.
/// </summary>
public sealed record SuperweaponCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Building, CellPos Target, CellPos Second)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Superweapon;
}

/// <summary>The soldiers inside the player's building come out around it (stage 2e).</summary>
public sealed record EvacuateCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Building)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Evacuate;
}

/// <summary>Turns the repair of the player's ready building on or off.</summary>
public sealed record ToggleRepairCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Building)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.ToggleRepair;
}

/// <summary>
/// Places a line of walls (4.8): the building at <paramref name="DefIndex"/> on each of <paramref name="Cells"/>, in order. The first
/// takes the one ready in the queue; every further one is paid at once at its cost. A cell the placement rules refuse is skipped; when
/// the credits run out the line ends.
/// </summary>
public sealed record PlaceWallsCommand(PlayerId Player, int ExecuteTick, uint Sequence, int DefIndex, ImmutableArray<CellPos> Cells)
    : Command(Player, ExecuteTick, Sequence)
{
    /// <summary>The longest line one command places.</summary>
    public const int MaxCells = 64;

    public override CommandType Type => CommandType.PlaceWalls;

    public bool Equals(PlaceWallsCommand? other) =>
        other is not null && base.Equals(other) && DefIndex == other.DefIndex && Cells.AsSpan().SequenceEqual(other.Cells.AsSpan());

    public override int GetHashCode()
    {
        var hash = base.GetHashCode() * 31 + DefIndex;
        foreach (var cell in Cells)
        {
            hash = unchecked(hash * 31 + cell.X * 7 + cell.Y);
        }

        return hash;
    }
}
