using System;
using System.Collections.Generic;
using Rts.Simulation.Random;

namespace Rts.Net.Transport;

/// <summary>The counts of one direction of a connection, for the tests.</summary>
public sealed class LinkStats
{
    /// <summary>Messages handed to the link.</summary>
    public int Sent { get; internal set; }

    /// <summary>Sendings lost on the way; on the reliable channel each of them is sent again.</summary>
    public int Lost { get; internal set; }

    /// <summary>Messages of the unreliable channel that came twice.</summary>
    public int Duplicated { get; internal set; }

    /// <summary>Messages handed to the receiver, copies included.</summary>
    public int Delivered { get; internal set; }
}

/// <summary>
/// A network in memory for the tests (docs/implementation/M9_network.md, 4.15): peers (<see cref="SimulatedTransport"/>), connections
/// between them with their own conditions each way, and a clock that moves only when told to (<see cref="Advance"/>). Everything left
/// to chance — losses, jitter, doubles — is drawn from a generator of its own seed, in the order things happen, so a test that fails
/// fails the same way every time.
/// <para>
/// The reliable channel behaves as LiteNetLib's: a lost sending is sent again after <see cref="LinkConditions.ResendMs"/>, and the
/// receiver hands the messages on in the order sent, holding back the ones that overtook a lost one. The unreliable channel loses,
/// doubles and reorders as the conditions say.
/// </para>
/// </summary>
public sealed class SimulatedNetwork
{
    private readonly DeterministicRandom _random;
    private readonly List<SimulatedTransport> _peers = new();
    private readonly List<Connection> _connections = new();

    // What is to happen, by time; things of the same millisecond happen in the order they were planned.
    private readonly PriorityQueue<Action, (long Time, long Order)> _events = new();
    private long _order;

    public SimulatedNetwork(ulong seed)
    {
        _random = new DeterministicRandom(seed, stream: 0x4E45_54);
    }

    /// <summary>The clock of the network, in milliseconds from its start.</summary>
    public long NowMs { get; private set; }

    /// <summary>A new computer on the network, not connected to anything yet.</summary>
    public SimulatedTransport AddPeer()
    {
        var peer = new SimulatedTransport(this, new PeerId(_peers.Count + 1));
        _peers.Add(peer);
        return peer;
    }

    /// <summary>
    /// Connects <paramref name="a"/> and <paramref name="b"/> now: both see the other come. The conditions of the way back are those of
    /// the way there unless given.
    /// </summary>
    public void Connect(SimulatedTransport a, SimulatedTransport b, LinkConditions? aToB = null, LinkConditions? bToA = null)
    {
        if (a.Network != this || b.Network != this || a == b)
        {
            throw new ArgumentException("Two different peers of this network connect.");
        }

        if (Find(a.Id, b.Id) is not null)
        {
            throw new InvalidOperationException($"{a.Id} and {b.Id} are connected already.");
        }

        aToB ??= LinkConditions.Perfect;
        bToA ??= aToB;
        aToB.Validate();
        bToA.Validate();
        var connection = new Connection(a, b, aToB, bToA);
        _connections.Add(connection);
        a.OnConnected(b.Id);
        b.OnConnected(a.Id);

        var breakAt = Earliest(aToB.BreakAtMs, bToA.BreakAtMs);
        if (breakAt is { } time)
        {
            Schedule(Math.Max(time, NowMs), () => Close(connection));
        }
    }

    /// <summary>New conditions for the way from <paramref name="from"/> to <paramref name="to"/>, from now on.</summary>
    public void SetConditions(SimulatedTransport from, SimulatedTransport to, LinkConditions conditions)
    {
        conditions.Validate();
        LinkOf(from, to).Conditions = conditions;
    }

    /// <summary>The counts of the way from <paramref name="from"/> to <paramref name="to"/> on their open connection.</summary>
    public LinkStats Stats(SimulatedTransport from, SimulatedTransport to) => LinkOf(from, to).Stats;

    /// <summary>Moves the clock by <paramref name="milliseconds"/>, delivering everything due by then into the inboxes of the peers.</summary>
    public void Advance(long milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "The clock only goes forward.");
        }

        var until = NowMs + milliseconds;
        while (_events.TryPeek(out _, out var key) && key.Time <= until)
        {
            var action = _events.Dequeue();
            NowMs = key.Time;
            action();
        }

        NowMs = until;
    }

    internal void Send(SimulatedTransport from, PeerId to, ReadOnlySpan<byte> data, DeliveryChannel channel)
    {
        var connection = Find(from.Id, to);
        if (connection is null)
        {
            return;
        }

        var link = connection.From(from);
        var copy = data.ToArray();
        link.Stats.Sent++;
        if (channel == DeliveryChannel.ReliableOrdered)
        {
            Attempt(link, link.NextSendSequence++, copy);
            return;
        }

        if (Roll(link.Conditions.LossPercent))
        {
            link.Stats.Lost++;
            return;
        }

        Schedule(NowMs + Delay(link), () => ArriveUnreliable(link, copy));
        if (Roll(link.Conditions.DuplicatePercent))
        {
            link.Stats.Duplicated++;
            var twin = (byte[])copy.Clone();
            Schedule(NowMs + Delay(link), () => ArriveUnreliable(link, twin));
        }
    }

    internal void Disconnect(SimulatedTransport from, PeerId peer)
    {
        if (Find(from.Id, peer) is { } connection)
        {
            Close(connection);
        }
    }

    /// <summary>One sending of a reliable message: lost and planned again, or on its way.</summary>
    private void Attempt(Link link, long sequence, byte[] data)
    {
        if (!link.Connection.Open)
        {
            return;
        }

        if (Roll(link.Conditions.LossPercent))
        {
            link.Stats.Lost++;
            Schedule(NowMs + link.Conditions.ResendMs, () => Attempt(link, sequence, data));
            return;
        }

        Schedule(NowMs + Delay(link), () => ArriveReliable(link, sequence, data));
    }

    private void ArriveReliable(Link link, long sequence, byte[] data)
    {
        if (!link.Connection.Open)
        {
            return;
        }

        link.Waiting.Add(sequence, data);
        while (link.Waiting.Remove(link.NextDeliverSequence, out var next))
        {
            link.NextDeliverSequence++;
            link.Stats.Delivered++;
            link.To.Deliver(link.Sender.Id, DeliveryChannel.ReliableOrdered, next);
        }
    }

    private void ArriveUnreliable(Link link, byte[] data)
    {
        if (!link.Connection.Open)
        {
            return;
        }

        link.Stats.Delivered++;
        link.To.Deliver(link.Sender.Id, DeliveryChannel.Unreliable, data);
    }

    private void Close(Connection connection)
    {
        if (!connection.Open)
        {
            return;
        }

        connection.Open = false;
        _connections.Remove(connection);
        connection.A.OnDisconnected(connection.B.Id);
        connection.B.OnDisconnected(connection.A.Id);
    }

    private int Delay(Link link)
    {
        var jitter = link.Conditions.JitterMs;
        var delay = link.Conditions.LatencyMs + (jitter > 0 ? _random.NextInt(-jitter, jitter + 1) : 0);
        return Math.Max(0, delay);
    }

    /// <summary>True with the chance of <paramref name="percent"/>; nothing is drawn for 0.</summary>
    private bool Roll(double percent) => percent > 0 && _random.NextInt(1_000_000) < (int)(percent * 10_000);

    private void Schedule(long time, Action action) => _events.Enqueue(action, (time, _order++));

    private Connection? Find(PeerId a, PeerId b)
    {
        foreach (var connection in _connections)
        {
            if ((connection.A.Id == a && connection.B.Id == b) || (connection.A.Id == b && connection.B.Id == a))
            {
                return connection;
            }
        }

        return null;
    }

    private Link LinkOf(SimulatedTransport from, SimulatedTransport to) =>
        Find(from.Id, to.Id)?.From(from) ?? throw new InvalidOperationException($"{from.Id} and {to.Id} are not connected.");

    private static long? Earliest(long? a, long? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private sealed class Connection
    {
        public Connection(SimulatedTransport a, SimulatedTransport b, LinkConditions aToB, LinkConditions bToA)
        {
            A = a;
            B = b;
            AToB = new Link(this, a, b, aToB);
            BToA = new Link(this, b, a, bToA);
        }

        public SimulatedTransport A { get; }

        public SimulatedTransport B { get; }

        public Link AToB { get; }

        public Link BToA { get; }

        public bool Open { get; set; } = true;

        public Link From(SimulatedTransport sender) => sender == A ? AToB : BToA;
    }

    /// <summary>One direction of a connection.</summary>
    private sealed class Link(Connection connection, SimulatedTransport sender, SimulatedTransport to, LinkConditions conditions)
    {
        public Connection Connection { get; } = connection;

        public SimulatedTransport Sender { get; } = sender;

        public SimulatedTransport To { get; } = to;

        public LinkConditions Conditions { get; set; } = conditions;

        public LinkStats Stats { get; } = new();

        public long NextSendSequence { get; set; }

        public long NextDeliverSequence { get; set; }

        /// <summary>Reliable messages that came before an earlier one, by sequence.</summary>
        public Dictionary<long, byte[]> Waiting { get; } = new();
    }
}
