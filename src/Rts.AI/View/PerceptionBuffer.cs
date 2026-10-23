using System.Collections.Generic;

namespace Rts.AI.View;

/// <summary>
/// How late a bot learns what its player sees (docs/implementation/M8_ai_difficulty.md, 4.3, step 3): an enemy in sight becomes known to
/// the bot <see cref="AiSettings.PerceptionDelay"/> ticks after it first came into sight, and an attack on the base that many ticks after
/// it happened. What is late is the news, not the picture: a known enemy is where it is now. An enemy out of sight for less than
/// <see cref="ForgetTicks"/> is known again at once when it comes back — units at the edge of sight are not news every time; one gone
/// longer is news again.
/// </summary>
public sealed class PerceptionBuffer(int delay)
{
    /// <summary>An enemy out of sight this long, in ticks (10 s), is noticed afresh.</summary>
    public const int ForgetTicks = 150;

    // By entity id: the tick it first came into sight, and the last tick it was in sight. Sorted, so that the walk to forget is the same
    // on every machine.
    private readonly SortedDictionary<int, (int First, int Last)> _sightings = new();
    private readonly List<int> _gone = new();
    private readonly Queue<int> _attacks = new();
    private int? _lastAttack;

    /// <summary>The ticks between seeing and knowing.</summary>
    public int Delay { get; } = delay;

    /// <summary>The last attack on the base the bot knows of: the latest that happened <see cref="Delay"/> ticks ago or earlier.</summary>
    public int? KnownAttack { get; private set; }

    /// <summary>Notes that the enemy <paramref name="id"/> is in sight after <paramref name="tick"/>; true once the bot knows it.</summary>
    public bool Sight(int id, int tick)
    {
        var first = _sightings.TryGetValue(id, out var sighting) && tick - sighting.Last <= ForgetTicks ? sighting.First : tick;
        _sightings[id] = (first, tick);
        return tick - first >= Delay;
    }

    /// <summary>Whether the enemy <paramref name="id"/> is in sight but not yet known.</summary>
    public bool IsNoticing(int id, int tick) => _sightings.TryGetValue(id, out var sighting) && sighting.Last == tick && tick - sighting.First < Delay;

    /// <summary>
    /// After the sightings of <paramref name="tick"/>: forgets the enemies out of sight too long, and learns the attacks on the base —
    /// <paramref name="lastAttack"/>, the <see cref="Rts.Simulation.World.PlayerState.LastUnderAttack"/> of the player — that are old enough.
    /// </summary>
    public void EndTick(int tick, int? lastAttack)
    {
        _gone.Clear();
        foreach (var (id, sighting) in _sightings)
        {
            if (tick - sighting.Last > ForgetTicks)
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _sightings.Remove(id);
        }

        if (lastAttack is { } attack && attack != _lastAttack)
        {
            _lastAttack = attack;
            _attacks.Enqueue(attack);
        }

        while (_attacks.Count > 0 && tick - _attacks.Peek() >= Delay)
        {
            KnownAttack = _attacks.Dequeue();
        }
    }
}
