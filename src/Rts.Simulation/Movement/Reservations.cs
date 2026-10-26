using System;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>
/// Who stands where (docs/implementation/M2_unit_control.md, 4.7 and 4.8). A vehicle holds a whole cell; infantry holds one of
/// the five spots of a cell (subcells: centre and four corners), so up to five soldiers of any players share a cell. Vehicles
/// and infantry never share a cell: a crushing vehicle first kills the enemy infantry of a cell it drives into (M4 step 8,
/// <see cref="Systems.CrushSystem"/>). Moving into another cell needs room there; the move takes the new
/// place and frees the old one at once. Units move in <see cref="EntityId"/> order, so nobody takes a place twice in one tick.
/// Part of the simulation state.
/// </summary>
public sealed class Reservations
{
    public const int SubcellsPerCell = 5;

    private static readonly WorldPos[] Offsets = [new(0, 0), new(-64, -64), new(64, -64), new(-64, 64), new(64, 64)];

    private readonly int[] _vehicle;
    private readonly int[] _infantry;
    private readonly Map.MapGrid? _map;

    internal Reservations(int width, int height)
    {
        Width = width;
        Height = height;
        _vehicle = new int[width * height];
        _infantry = new int[width * height * SubcellsPerCell];
    }

    /// <summary>The places of a map: its cells, then the cells of deck of its high bridges.</summary>
    internal Reservations(Map.MapGrid map)
    {
        Width = map.Width;
        Height = map.Height;
        _map = map;
        _vehicle = new int[map.NodeCount];
        _infantry = new int[map.NodeCount * SubcellsPerCell];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Point of a spot in a cell: 0 centre, 1 north-west, 2 north-east, 3 south-west, 4 south-east (±64 leptons).</summary>
    public static WorldPos SubcellPoint(CellPos cell, int subcell) => cell.Center + Offsets[subcell];

    public EntityId VehicleAt(CellPos cell, bool onDeck = false) => new(_vehicle[IndexOf(cell, onDeck)]);

    public EntityId InfantryAt(CellPos cell, int subcell, bool onDeck = false) => new(_infantry[IndexOf(cell, onDeck) * SubcellsPerCell + subcell]);

    public int InfantryCount(CellPos cell, bool onDeck = false)
    {
        var start = IndexOf(cell, onDeck) * SubcellsPerCell;
        var count = 0;
        for (var i = 0; i < SubcellsPerCell; i++)
        {
            count += _infantry[start + i] != 0 ? 1 : 0;
        }

        return count;
    }

    /// <summary>Nobody stands in the cell.</summary>
    public bool IsFree(CellPos cell) => _vehicle[IndexOf(cell)] == 0 && InfantryCount(cell) == 0;

    /// <summary>
    /// The unit to deal with when the cell has no room: its vehicle, or the soldier on the lowest spot.
    /// <see cref="EntityId.None"/> when nobody stands there.
    /// </summary>
    public EntityId Blocker(CellPos cell, bool onDeck = false)
    {
        var index = IndexOf(cell, onDeck);
        if (_vehicle[index] != 0)
        {
            return new EntityId(_vehicle[index]);
        }

        for (var i = 0; i < SubcellsPerCell; i++)
        {
            if (_infantry[index * SubcellsPerCell + i] != 0)
            {
                return new EntityId(_infantry[index * SubcellsPerCell + i]);
            }
        }

        return EntityId.None;
    }

    /// <summary>
    /// True when <paramref name="unit"/> could stand in the cell (or already does), on <paramref name="onDeck"/> — by default the level it
    /// is on now.
    /// </summary>
    public bool HasRoomFor(CellPos cell, Unit unit, bool? onDeck = null) => HasRoomFor(cell, unit.Archetype.Placement, unit.Id, onDeck ?? unit.OnDeck);

    /// <summary>
    /// Room for a unit with <paramref name="placement"/>: a vehicle needs the cell empty of others, a soldier needs no vehicle
    /// and a free spot (or one it holds already).
    /// </summary>
    public bool HasRoomFor(CellPos cell, UnitPlacement placement, EntityId self, bool onDeck = false)
    {
        var index = IndexOf(cell, onDeck);
        var vehicle = _vehicle[index];
        if (vehicle != 0 && vehicle != self.Value)
        {
            return false;
        }

        for (var i = 0; i < SubcellsPerCell; i++)
        {
            var soldier = _infantry[index * SubcellsPerCell + i];
            if (placement == UnitPlacement.Cell && soldier != 0 && soldier != self.Value)
            {
                // A vehicle does not share a cell with infantry.
                return false;
            }

            if (placement == UnitPlacement.Subcell && (soldier == 0 || soldier == self.Value))
            {
                return true;
            }
        }

        // A vehicle met no soldier; a soldier found every spot taken by others.
        return placement == UnitPlacement.Cell;
    }

    /// <summary>Owner of every place in index order (vehicles, then soldier spots), for the state hash.</summary>
    internal ReadOnlySpan<int> VehicleCells => _vehicle;

    internal ReadOnlySpan<int> InfantrySpots => _infantry;

    /// <summary>
    /// Takes room for <paramref name="unit"/> in the cell. A soldier gets the free spot nearest to <paramref name="preferred"/>
    /// (ties to the lower spot number), or keeps the spot it holds there. Returns the spot (0 for a vehicle), or -1 when there
    /// is no room.
    /// </summary>
    internal int TryEnter(CellPos cell, Unit unit, WorldPos preferred, bool? onDeck = null)
    {
        var deck = onDeck ?? unit.OnDeck;
        if (!HasRoomFor(cell, unit, deck))
        {
            return -1;
        }

        var index = IndexOf(cell, deck);
        if (unit.Archetype.Placement == UnitPlacement.Cell)
        {
            _vehicle[index] = unit.Id.Value;
            return 0;
        }

        var best = -1;
        var bestDistance = long.MaxValue;
        for (var i = 0; i < SubcellsPerCell; i++)
        {
            var soldier = _infantry[index * SubcellsPerCell + i];
            if (soldier == unit.Id.Value)
            {
                return i;
            }

            if (soldier != 0)
            {
                continue;
            }

            var distance = (SubcellPoint(cell, i) - preferred).LengthSquared;
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        _infantry[index * SubcellsPerCell + best] = unit.Id.Value;
        return best;
    }

    /// <summary>Frees whatever place <paramref name="unit"/> holds in the cell, on the ground and on a deck over it.</summary>
    internal void Leave(CellPos cell, Unit unit)
    {
        LeaveAt(IndexOf(cell), unit);
        if (_map is { DeckCount: > 0 } map && map.DeckSlot(cell) is var slot and >= 0)
        {
            LeaveAt(Width * Height + slot, unit);
        }
    }

    /// <summary>Puts a marker owner into a vehicle place; for tests of the state hash.</summary>
    internal void SetVehicle(CellPos cell, EntityId owner) => _vehicle[IndexOf(cell)] = owner.Value;

    private void LeaveAt(int index, Unit unit)
    {
        if (_vehicle[index] == unit.Id.Value)
        {
            _vehicle[index] = 0;
        }

        for (var i = 0; i < SubcellsPerCell; i++)
        {
            if (_infantry[index * SubcellsPerCell + i] == unit.Id.Value)
            {
                _infantry[index * SubcellsPerCell + i] = 0;
            }
        }
    }

    // The place of a cell on a level: the cell's own, or after the cells the one of the deck over it.
    private int IndexOf(CellPos cell, bool onDeck = false)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {Width}x{Height} map.");
        }

        // On a level a cell has no deck over: the ground (a unit on a deck asked about the shore it drives off to).
        if (onDeck && _map is { DeckCount: > 0 } map && map.DeckSlot(cell) is var slot and >= 0)
        {
            return Width * Height + slot;
        }

        return cell.Y * Width + cell.X;
    }
}
