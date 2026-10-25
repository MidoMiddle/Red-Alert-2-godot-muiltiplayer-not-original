using System.Collections.Generic;
using System.Linq;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;

namespace Rts.Net.Lockstep;

/// <summary>The round trip to a peer over the last <see cref="LatencyMonitor.WindowMs"/>: its mean, its worst and its spread, in milliseconds.</summary>
public readonly record struct PeerLatency(int RttMs, int MaxRttMs, int JitterMs);

/// <summary>
/// How long the way to every peer is (docs/implementation/M9_network.md, 4.7): a <see cref="Ping"/> with our clock to each, four times
/// a second over the unreliable channel; the peer sends it back as a <see cref="Pong"/> at once, and the time it took is a sample.
/// The samples of the last five seconds give the mean, the worst and the jitter (worst minus best). Our clock never leaves this
/// computer: a pong carries our own timestamp back. Nothing of it enters the simulation.
/// </summary>
public sealed class LatencyMonitor
{
    public const int IntervalMs = 250;
    public const int WindowMs = 5000;

    private readonly ITransport _transport;
    private readonly Dictionary<PeerId, Queue<(long At, int Rtt)>> _samples = new();
    private long _now;
    private long _nextPing;

    public LatencyMonitor(ITransport transport)
    {
        _transport = transport;
    }

    /// <summary>Answers the pings and takes the pongs that come to the lockstep.</summary>
    public void Attach(LockstepEngine engine) => engine.MessageReceived += Handle;

    /// <summary>Pings every peer when it is time; <paramref name="nowMs"/> is this computer's clock.</summary>
    public void Update(long nowMs)
    {
        _now = nowMs;
        if (nowMs < _nextPing)
        {
            return;
        }

        _nextPing = nowMs + IntervalMs;
        _transport.SendToAll(MessageCodec.Write(new Ping(nowMs)), DeliveryChannel.Unreliable);
    }

    public void Handle(PeerId peer, NetMessage message)
    {
        switch (message)
        {
            case Ping ping:
                _transport.Send(peer, MessageCodec.Write(new Pong(ping.Timestamp)), DeliveryChannel.Unreliable);
                break;
            case Pong pong when pong.Timestamp <= _now:
                if (!_samples.TryGetValue(peer, out var samples))
                {
                    samples = new Queue<(long, int)>();
                    _samples[peer] = samples;
                }

                samples.Enqueue((_now, (int)(_now - pong.Timestamp)));
                while (samples.Peek().At < _now - WindowMs)
                {
                    samples.Dequeue();
                }

                break;
        }
    }

    /// <summary>The way to <paramref name="peer"/>; null before its first pong.</summary>
    public PeerLatency? Of(PeerId peer) =>
        _samples.TryGetValue(peer, out var samples) && samples.Count > 0
            ? new PeerLatency((int)samples.Average(sample => sample.Rtt), samples.Max(sample => sample.Rtt), samples.Max(sample => sample.Rtt) - samples.Min(sample => sample.Rtt))
            : null;

    /// <summary>The peers measured so far.</summary>
    public IEnumerable<PeerId> Peers => _samples.Keys;
}
