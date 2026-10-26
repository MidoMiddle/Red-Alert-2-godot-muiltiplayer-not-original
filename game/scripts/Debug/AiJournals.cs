using System;
using System.Collections.Generic;
using System.IO;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>
/// What the bots of a match built, counted from the events of the simulation (<see cref="UnitProduced"/>, <see cref="UnitCloned"/>,
/// <see cref="BuildingPlaced"/>), and written at the end into the journal of each: <c>totals: units — …; buildings — …</c>.
/// </summary>
public sealed class AiJournals(SortedDictionary<int, string> files)
{
    private readonly SortedDictionary<int, (SortedDictionary<string, int> Units, SortedDictionary<string, int> Buildings)> _built = new();
    private bool _finished;
    private int _lastTick;

    /// <summary>Counts what <paramref name="simEvent"/> says a bot built.</summary>
    public void Record(SimEvent simEvent, GameWorld world)
    {
        _lastTick = simEvent.Tick;
        switch (simEvent)
        {
            case UnitProduced { Player: var player, Unit: var id } when files.ContainsKey(player.Value):
                Count(player, world, id, units: true);
                break;
            case UnitCloned { Player: var player, Unit: var id } when files.ContainsKey(player.Value):
                Count(player, world, id, units: true);
                break;
            case BuildingPlaced { Player: var player, DefIndex: var index } when files.ContainsKey(player.Value) && index >= 0:
                Add(player, world.Rules.Database.Buildings[index].Id, units: false);
                break;
        }
    }

    /// <summary>Writes the totals into every journal, once: at the end of the match (<paramref name="why"/>), or as the game leaves it.</summary>
    public void Finish(string why)
    {
        var tick = _lastTick;
        if (_finished)
        {
            return;
        }

        _finished = true;
        var time = $"{tick / 900}:{tick % 900 / 15:00}";
        foreach (var (player, path) in files)
        {
            var (units, buildings) = _built.GetValueOrDefault(player, (new(StringComparer.Ordinal), new(StringComparer.Ordinal)));
            File.AppendAllText(path,
                $"{time} totals ({why}): units — {List(units)}; buildings — {List(buildings)}\n");
        }
    }

    private void Count(PlayerId player, GameWorld world, EntityId id, bool units)
    {
        if (world.TryGet(id, out var entity) && entity is Unit unit)
        {
            Add(player, unit.Archetype.Id, units);
        }
    }

    private void Add(PlayerId player, string id, bool units)
    {
        if (!_built.TryGetValue(player.Value, out var built))
        {
            built = (new SortedDictionary<string, int>(StringComparer.Ordinal), new SortedDictionary<string, int>(StringComparer.Ordinal));
            _built[player.Value] = built;
        }

        var into = units ? built.Units : built.Buildings;
        into[id] = into.GetValueOrDefault(id) + 1;
    }

    private static string List(SortedDictionary<string, int> counts)
    {
        var total = 0;
        foreach (var count in counts.Values)
        {
            total += count;
        }

        return counts.Count == 0 ? "none" : $"{total}: " + string.Join(", ", System.Linq.Enumerable.Select(counts, pair => $"{pair.Key} {pair.Value}"));
    }
}
