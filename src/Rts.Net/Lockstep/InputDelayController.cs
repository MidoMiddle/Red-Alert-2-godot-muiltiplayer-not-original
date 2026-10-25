using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;

namespace Rts.Net.Lockstep;

/// <summary>
/// The input delay a network needs (docs/implementation/M9_network.md, 4.7): a bundle sent when tick T is computed must be with every
/// other computer before it computes T + D, so D ticks must cover the longest way a bundle travels, its jitter, and one tick more —
/// for a lost packet whose copy comes with the next tick (<see cref="LockstepSettings.Redundancy"/>). Between 2 and 10 ticks. It moves
/// one tick at a time and at most every <see cref="MinIntervalMs"/>, so the game does not jerk.
/// </summary>
public sealed class InputDelayController
{
    public const int MinDelay = 2;
    public const int MaxDelay = InputDelayChange.MaxDelay;
    public const int MinIntervalMs = 2000;

    private long _lastChange = long.MinValue / 2;

    public InputDelayController(int current)
    {
        Current = Math.Clamp(current, MinDelay, MaxDelay);
    }

    public int Current { get; private set; }

    /// <summary>D = ⌈(one way + jitter) / tick⌉ + 1, within 2…10.</summary>
    public static int Required(int oneWayMs, int jitterMs, int ticksPerSecond)
    {
        var tickMs = 1000.0 / ticksPerSecond;
        return Math.Clamp((int)Math.Ceiling((oneWayMs + jitterMs) / tickMs) + 1, MinDelay, MaxDelay);
    }

    /// <summary>The next delay towards <paramref name="required"/> when it is time to move; null to keep it.</summary>
    public int? Next(int required, long nowMs)
    {
        required = Math.Clamp(required, MinDelay, MaxDelay);
        if (required == Current || nowMs - _lastChange < MinIntervalMs)
        {
            return null;
        }

        Current += Math.Sign(required - Current);
        _lastChange = nowMs;
        return Current;
    }
}

/// <summary>
/// The host decides the input delay for everyone (docs/implementation/M9_network.md, 4.7): from the round trips to its clients
/// (<see cref="LatencyMonitor"/>) it takes the longest way a bundle travels — host to client, or client to client through the host — and
/// when the delay should move, it tells everyone with <see cref="InputDelayChange"/> on a tick far enough ahead that all switch on it.
/// </summary>
public sealed class InputDelayAuthority
{
    private readonly LockstepEngine _engine;
    private readonly LatencyMonitor _monitor;
    private readonly ITransport _transport;
    private readonly int _ticksPerSecond;

    public InputDelayAuthority(LockstepEngine engine, LatencyMonitor monitor, ITransport transport, int ticksPerSecond)
    {
        _engine = engine;
        _monitor = monitor;
        _transport = transport;
        _ticksPerSecond = ticksPerSecond;
        Controller = new InputDelayController(engine.InputDelay);
    }

    public InputDelayController Controller { get; }

    /// <summary>The host decided a new delay: its clock then, the tick it takes effect on, the delay.</summary>
    public event Action<long, int, int>? Decided;

    /// <summary>The longest one-way path a bundle takes now, in milliseconds, and the worst jitter on it.</summary>
    public (int OneWayMs, int JitterMs) LongestWay()
    {
        var ways = _transport.Peers.Select(peer => _monitor.Of(peer)).OfType<PeerLatency>().ToList();
        if (ways.Count == 0)
        {
            return (0, 0);
        }

        // A client's bundle to another goes through the host: both halves.
        var halves = ways.Select(way => way.MaxRttMs / 2).OrderDescending().ToList();
        var oneWay = halves.Count > 1 ? halves[0] + halves[1] : halves[0];
        return (oneWay, ways.Max(way => way.JitterMs));
    }

    /// <summary>Moves the delay when the network asks for it; <paramref name="nowMs"/> is the host's clock.</summary>
    public void Update(long nowMs)
    {
        var (oneWay, jitter) = LongestWay();
        if (Controller.Next(InputDelayController.Required(oneWay, jitter, _ticksPerSecond), nowMs) is not { } delay)
        {
            return;
        }

        // Far enough ahead for the message to reach everyone first: no computer is more than D + 1 ticks past another.
        var tick = _engine.CurrentTick + (2 * _engine.InputDelay) + 2;
        _engine.ScheduleInputDelay(tick, delay);
        _transport.SendToAll(MessageCodec.Write(new InputDelayChange(tick, delay)), DeliveryChannel.ReliableOrdered);
        Decided?.Invoke(nowMs, tick, delay);
    }
}
