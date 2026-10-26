using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.Map;

/// <summary>
/// The two levels of a high bridge (2026-10-26; docs/map_elements/05_bridges): its deck at the level of the plateaus it joins,
/// and the ground under it — water for ships, a valley for anyone. One step rule decides the level a unit is on:
/// <list type="bullet">
/// <item>from a shore, a straight step along the bridge onto its end cell gets a ground mover onto the deck, while the segment stands;</item>
/// <item>on the deck, a step to any neighbouring cell of the same deck stays on it (a diagonal needs both cells beside it deck too);</item>
/// <item>from an end cell, the straight step along the bridge onto its shore gets back to the ground;</item>
/// <item>no other step leaves the deck; on the ground every step is as it always was.</item>
/// </list>
/// So the levels of a path follow from the level it starts on and its cells. Ships never get on a deck. Every rule here reads only
/// the map: the level a unit is on is <see cref="World.Unit.OnDeck"/>, kept in step with the place it holds (<see cref="Movement.Reservations"/>).
/// </summary>
public static class Decks
{
    /// <summary>
    /// The step rule of the path search (with <see cref="AStar.CanStep"/> on the ground: no corner cutting). True when a unit with
    /// <paramref name="locomotor"/> on <paramref name="fromDeck"/> may step from <paramref name="from"/> by (dx, dy), and the level it
    /// lands on.
    /// </summary>
    public static bool TryStep(MapGrid map, CellPos from, bool fromDeck, int dx, int dy, Locomotor locomotor, out bool toDeck)
    {
        var to = new CellPos(from.X + dx, from.Y + dy);
        if (!map.Contains(to))
        {
            toDeck = false;
            return false;
        }

        if (fromDeck)
        {
            return StepOffDeck(map, from, to, dx, dy, locomotor, cornerRule: true, out toDeck);
        }

        if (map.DeckCount > 0 && EntersDeck(map, from, to, locomotor))
        {
            toDeck = true;
            return true;
        }

        toDeck = false;
        return AStar.CanStep(map, from, dx, dy, locomotor);
    }

    /// <summary>
    /// The step rule of movement (with <see cref="MapGrid.CanEnter"/> on the ground, as a unit moving cell to cell has always gone):
    /// true when a unit on <paramref name="fromDeck"/> in <paramref name="from"/> may come into <paramref name="to"/>, a neighbouring cell,
    /// and the level it is on there.
    /// </summary>
    public static bool TryEnter(MapGrid map, CellPos from, bool fromDeck, CellPos to, Locomotor locomotor, out bool toDeck)
    {
        if (!map.Contains(to))
        {
            toDeck = false;
            return false;
        }

        var (dx, dy) = (to.X - from.X, to.Y - from.Y);
        if (fromDeck)
        {
            return StepOffDeck(map, from, to, dx, dy, locomotor, cornerRule: false, out toDeck);
        }

        if (map.DeckCount > 0 && EntersDeck(map, from, to, locomotor))
        {
            toDeck = true;
            return true;
        }

        toDeck = false;
        return map.CanEnter(to, locomotor);
    }

    /// <summary>Speed in percent on the level: the deck's road, or the cell's own (<see cref="MapGrid.SpeedPercent"/>).</summary>
    public static int SpeedPercent(MapGrid map, CellPos cell, bool onDeck, Locomotor locomotor) =>
        onDeck ? (map.DeckSlot(cell) is var slot and >= 0 ? map.DeckSpeedPercent(slot, locomotor) : 0) : map.SpeedPercent(cell, locomotor);

    /// <summary>True when a unit can stand in the cell on the level.</summary>
    public static bool CanStand(MapGrid map, CellPos cell, bool onDeck, Locomotor locomotor) => SpeedPercent(map, cell, onDeck, locomotor) > 0;

    /// <summary>
    /// The level of every cell of a way that starts on <paramref name="startOnDeck"/>, by the step rule of movement; a step the rule
    /// refuses keeps the level it came with (the way is planned again when a unit gets there).
    /// </summary>
    public static bool[] Levels(MapGrid map, IReadOnlyList<CellPos> cells, bool startOnDeck, Locomotor locomotor)
    {
        var levels = new bool[cells.Count];
        var onDeck = startOnDeck;
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0 && cells[i] != cells[i - 1] && TryEnter(map, cells[i - 1], onDeck, cells[i], locomotor, out var next))
            {
                onDeck = next;
            }

            levels[i] = onDeck && map.IsDeck(cells[i]);
            onDeck = levels[i];
        }

        return levels;
    }

    // Onto a deck from its shore: the cell is an end cell of deck this shore leads onto, the segment stands, the mover is not a ship.
    private static bool EntersDeck(MapGrid map, CellPos from, CellPos to, Locomotor locomotor)
    {
        if (locomotor == Locomotor.Naval)
        {
            return false;
        }

        var slot = map.DeckSlot(to);
        if (slot < 0)
        {
            return false;
        }

        var entries = map.DeckEntries(from.Y * map.Width + from.X);
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] == slot)
            {
                return map.DeckSpeedPercent(slot, locomotor) > 0;
            }
        }

        return false;
    }

    // From the deck: along the same deck, or off an end cell onto its shore.
    private static bool StepOffDeck(MapGrid map, CellPos from, CellPos to, int dx, int dy, Locomotor locomotor, bool cornerRule, out bool toDeck)
    {
        var slot = map.DeckSlot(from);
        var bridge = slot >= 0 ? map.DeckSegment(slot).Bridge : null;
        var next = map.DeckSlot(to);
        if (bridge is not null && next >= 0 && map.DeckSegment(next).Bridge == bridge && dx is >= -1 and <= 1 && dy is >= -1 and <= 1)
        {
            toDeck = true;
            if (map.DeckSpeedPercent(next, locomotor) == 0)
            {
                return false;
            }

            // A diagonal along the deck needs both cells beside the corner on the same deck, standing.
            return !cornerRule || dx == 0 || dy == 0
                || (OnSameDeck(map, new CellPos(from.X + dx, from.Y), bridge, locomotor) && OnSameDeck(map, new CellPos(from.X, from.Y + dy), bridge, locomotor));
        }

        toDeck = false;
        return slot >= 0 && map.DeckExit(slot) == to.Y * map.Width + to.X && map.CanEnter(to, locomotor);
    }

    private static bool OnSameDeck(MapGrid map, CellPos cell, Bridge bridge, Locomotor locomotor)
    {
        var slot = map.DeckSlot(cell);
        return slot >= 0 && map.DeckSegment(slot).Bridge == bridge && map.DeckSpeedPercent(slot, locomotor) > 0;
    }
}
