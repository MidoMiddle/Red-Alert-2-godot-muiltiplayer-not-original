using System.Collections.Generic;
using System.Linq;
using Rts.Simulation.World;

namespace Rts.Net.Session;

/// <summary>
/// The synchronous start (docs/implementation/M9_network.md, 4.9): the host waits until every person of the match has loaded, then
/// tick 0 begins for all at once. One who has not loaded within <see cref="TimeoutMs"/> is dropped before the start, so the others are
/// not held. Bots load with every computer and are not waited for.
/// </summary>
public sealed class LoadingBarrier
{
    private readonly Dictionary<PlayerId, int> _progress;
    private long _elapsedMs;

    public LoadingBarrier(IEnumerable<PlayerId> people, int timeoutMs)
    {
        _progress = people.Distinct().ToDictionary(player => player, _ => 0);
        TimeoutMs = timeoutMs;
    }

    public int TimeoutMs { get; }

    /// <summary>How far each of the people still awaited has loaded, in percent.</summary>
    public IReadOnlyDictionary<PlayerId, int> Progress => _progress;

    /// <summary>Everyone awaited has loaded.</summary>
    public bool Done => _progress.Values.All(percent => percent >= 100);

    /// <summary>The time is up and someone has not loaded.</summary>
    public bool TimedOut => !Done && _elapsedMs >= TimeoutMs;

    /// <summary>The people who have not loaded yet.</summary>
    public IReadOnlyList<PlayerId> Waiting => _progress.Where(pair => pair.Value < 100).Select(pair => pair.Key).OrderBy(player => player.Value).ToList();

    /// <summary>How far <paramref name="player"/> has loaded; a report of another player, or going back, is ignored. Returns whether it was taken.</summary>
    public bool Report(PlayerId player, int percent)
    {
        if (!_progress.TryGetValue(player, out var before) || percent <= before)
        {
            return false;
        }

        _progress[player] = percent;
        return true;
    }

    /// <summary>A person left before the start: no longer awaited.</summary>
    public void Drop(PlayerId player) => _progress.Remove(player);

    public void Advance(long elapsedMs) => _elapsedMs += elapsedMs;
}
