using System.Buffers.Binary;
using Rts.Net.Transport;

namespace Rts.Net.Tests.Transport;

/// <summary>
/// The in-memory network (docs/implementation/M9_network.md, 4.15, step 2): messages arrive after the set latency and within the
/// jitter; the reliable channel neither loses nor reorders, whatever the losses; the unreliable one loses and doubles as told; the
/// connection breaks on time; and the same seed gives the same network.
/// </summary>
public sealed class SimulatedTransportTests
{
    private static readonly LinkConditions Internet = new() { LatencyMs = 150, JitterMs = 50, LossPercent = 2 };

    [Fact]
    public void Connected_peers_see_each_other_come()
    {
        var network = new SimulatedNetwork(1);
        var a = network.AddPeer();
        var b = network.AddPeer();

        network.Connect(a, b);

        Assert.Equal([b.Id], a.Peers);
        Assert.Equal([a.Id], b.Peers);
        Assert.Equal([TransportEvent.Connected(b.Id)], Drain(a).Select(Strip));
        Assert.Equal([TransportEvent.Connected(a.Id)], Drain(b).Select(Strip));
    }

    [Theory]
    [InlineData(DeliveryChannel.ReliableOrdered)]
    [InlineData(DeliveryChannel.Unreliable)]
    public void A_message_arrives_after_the_latency_and_not_before(DeliveryChannel channel)
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 100 });

        a.Send(b.Id, [1, 2, 3], channel);
        network.Advance(99);
        Assert.Empty(Drain(b));

        network.Advance(1);
        var received = Assert.Single(Drain(b));
        Assert.Equal(TransportEventKind.Message, received.Kind);
        Assert.Equal(a.Id, received.Peer);
        Assert.Equal(channel, received.Channel);
        Assert.Equal([1, 2, 3], received.Data);
    }

    [Fact]
    public void A_message_without_latency_arrives_on_the_next_step_of_the_clock()
    {
        var (network, a, b) = Pair(LinkConditions.Perfect);

        a.Send(b.Id, [7], DeliveryChannel.ReliableOrdered);
        Assert.Empty(Drain(b));

        network.Advance(0);
        Assert.Equal([7], Assert.Single(Drain(b)).Data);
    }

    [Fact]
    public void Each_way_has_its_own_latency()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 50 }, new LinkConditions { LatencyMs = 200 });

        a.Send(b.Id, [1], DeliveryChannel.ReliableOrdered);
        b.Send(a.Id, [2], DeliveryChannel.ReliableOrdered);
        network.Advance(50);
        Assert.Single(Drain(b));
        Assert.Empty(Drain(a));

        network.Advance(150);
        Assert.Single(Drain(a));
    }

    [Fact]
    public void The_jitter_spreads_the_arrivals_evenly_around_the_latency()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 150, JitterMs = 50 });
        const int Count = 2000;
        for (var i = 0; i < Count; i++)
        {
            a.Send(b.Id, Payload(i), DeliveryChannel.Unreliable);
        }

        var arrivals = new List<long>();
        for (var ms = 0; ms <= 250; ms++)
        {
            network.Advance(ms == 0 ? 0 : 1);
            arrivals.AddRange(Drain(b).Select(_ => network.NowMs));
        }

        Assert.Equal(Count, arrivals.Count);
        Assert.Equal(100, arrivals.Min());
        Assert.Equal(200, arrivals.Max());
        Assert.InRange(arrivals.Average(), 147, 153);
        // Even: each fifth of the range takes about a fifth of the messages.
        foreach (var fifth in Enumerable.Range(0, 5))
        {
            Assert.InRange(arrivals.Count(time => time >= 100 + 20 * fifth && time < 120 + 20 * fifth), 340, 460);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(30)]
    public void The_reliable_channel_loses_nothing_and_keeps_the_order(double lossPercent)
    {
        var (network, a, b) = Pair(Internet with { LossPercent = lossPercent });
        const int Count = 1000;
        var received = new List<int>();
        for (var i = 0; i < Count; i++)
        {
            a.Send(b.Id, Payload(i), DeliveryChannel.ReliableOrdered);
            network.Advance(10);
            received.AddRange(Drain(b).Select(Index));
        }

        network.Advance(30_000);
        received.AddRange(Drain(b).Select(Index));

        Assert.Equal(Enumerable.Range(0, Count), received);
        var stats = network.Stats(a, b);
        Assert.Equal(Count, stats.Sent);
        Assert.Equal(Count, stats.Delivered);
        Assert.InRange(stats.Lost, Count * lossPercent / 200, Count * lossPercent / 50);
    }

    [Fact]
    public void A_reliable_message_lost_holds_back_the_ones_after_it_until_it_is_sent_again()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 100, LossPercent = 100, ResendDelayMs = 300 });
        a.Send(b.Id, Payload(0), DeliveryChannel.ReliableOrdered);
        network.Advance(10);
        network.SetConditions(a, b, new LinkConditions { LatencyMs = 100, ResendDelayMs = 300 });
        a.Send(b.Id, Payload(1), DeliveryChannel.ReliableOrdered);

        // The second is there at 110 ms but waits for the first, sent again at 300 ms and there at 400 ms.
        network.Advance(380);
        Assert.Empty(Drain(b));
        network.Advance(10);
        Assert.Equal([0, 1], Drain(b).Select(Index));
    }

    [Fact]
    public void A_silent_link_delivers_everything_in_order_once_it_comes_back()
    {
        var (network, a, b) = Pair(Internet);
        network.SetConditions(a, b, Internet with { LossPercent = 100 });
        for (var i = 0; i < 75; i++)
        {
            a.Send(b.Id, Payload(i), DeliveryChannel.ReliableOrdered);
            network.Advance(66);
        }

        Assert.Empty(Drain(b));
        network.SetConditions(a, b, Internet);
        network.Advance(5_000);

        Assert.Equal(Enumerable.Range(0, 75), Drain(b).Select(Index));
    }

    [Fact]
    public void The_unreliable_channel_loses_doubles_and_reorders()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 150, JitterMs = 50, LossPercent = 2, DuplicatePercent = 5 });
        const int Count = 10_000;
        var received = new List<int>();
        for (var i = 0; i < Count; i++)
        {
            a.Send(b.Id, Payload(i), DeliveryChannel.Unreliable);
            network.Advance(5);
            received.AddRange(Drain(b).Select(Index));
        }

        network.Advance(1_000);
        received.AddRange(Drain(b).Select(Index));

        var stats = network.Stats(a, b);
        Assert.InRange(stats.Lost, 100, 300);
        Assert.InRange(stats.Duplicated, 350, 650);
        Assert.Equal(Count - stats.Lost + stats.Duplicated, received.Count);
        Assert.Equal(Count - stats.Lost, received.Distinct().Count());
        Assert.NotEqual(received.Order(), received);
    }

    [Fact]
    public void The_same_seed_gives_the_same_network_and_another_seed_another()
    {
        static List<(long Time, int Index)> Run(ulong seed)
        {
            var network = new SimulatedNetwork(seed);
            var a = network.AddPeer();
            var b = network.AddPeer();
            network.Connect(a, b, Internet with { DuplicatePercent = 5 });
            Drain(b);
            var arrivals = new List<(long, int)>();
            for (var i = 0; i < 500; i++)
            {
                a.Send(b.Id, Payload(i), i % 2 == 0 ? DeliveryChannel.ReliableOrdered : DeliveryChannel.Unreliable);
                network.Advance(7);
                arrivals.AddRange(Drain(b).Select(e => (network.NowMs, Index(e))));
            }

            return arrivals;
        }

        Assert.Equal(Run(5), Run(5));
        Assert.NotEqual(Run(5), Run(6));
    }

    [Fact]
    public void The_connection_breaks_on_time_and_what_was_on_the_way_is_lost()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 100, BreakAtMs = 500 });
        a.Send(b.Id, Payload(1), DeliveryChannel.ReliableOrdered);
        network.Advance(450);
        a.Send(b.Id, Payload(2), DeliveryChannel.ReliableOrdered);

        network.Advance(49);
        Assert.Equal([1], Drain(b).Select(Index));
        Assert.Equal([b.Id], a.Peers);

        network.Advance(1);
        Assert.Equal([TransportEvent.Disconnected(b.Id)], Drain(a).Select(Strip));
        Assert.Equal([TransportEvent.Disconnected(a.Id)], Drain(b).Select(Strip));
        Assert.Empty(a.Peers);
        Assert.Empty(b.Peers);

        a.Send(b.Id, Payload(3), DeliveryChannel.ReliableOrdered);
        network.Advance(1_000);
        Assert.Empty(Drain(b));
    }

    [Fact]
    public void Either_end_can_close_the_connection()
    {
        var (network, a, b) = Pair(new LinkConditions { LatencyMs = 100 });
        a.Send(b.Id, Payload(1), DeliveryChannel.ReliableOrdered);

        b.Disconnect(a.Id);
        network.Advance(1_000);

        Assert.Equal([TransportEvent.Disconnected(b.Id)], Drain(a).Select(Strip));
        Assert.Equal([TransportEvent.Disconnected(a.Id)], Drain(b).Select(Strip));
    }

    [Fact]
    public void A_message_to_everyone_reaches_every_connected_peer_and_only_them()
    {
        var network = new SimulatedNetwork(1);
        var host = network.AddPeer();
        var first = network.AddPeer();
        var second = network.AddPeer();
        var stranger = network.AddPeer();
        network.Connect(host, first, new LinkConditions { LatencyMs = 30 });
        network.Connect(host, second, new LinkConditions { LatencyMs = 60 });
        Drain(host, first, second);

        host.SendToAll(Payload(9), DeliveryChannel.ReliableOrdered);
        network.Advance(60);

        Assert.Equal([9], Drain(first).Select(Index));
        Assert.Equal([9], Drain(second).Select(Index));
        Assert.Empty(Drain(stranger));
    }

    private static (SimulatedNetwork Network, SimulatedTransport A, SimulatedTransport B) Pair(LinkConditions aToB, LinkConditions? bToA = null)
    {
        var network = new SimulatedNetwork(2026);
        var a = network.AddPeer();
        var b = network.AddPeer();
        network.Connect(a, b, aToB, bToA);
        Drain(a, b);
        return (network, a, b);
    }

    private static byte[] Payload(int index)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, index);
        return bytes;
    }

    private static int Index(TransportEvent message) => BinaryPrimitives.ReadInt32LittleEndian(message.Data);

    /// <summary>The event without its data array, which compares by reference.</summary>
    private static TransportEvent Strip(TransportEvent e) => e with { Data = [] };

    private static List<TransportEvent> Drain(SimulatedTransport peer)
    {
        var events = new List<TransportEvent>();
        peer.Poll();
        while (peer.TryReceive(out var e))
        {
            events.Add(e);
        }

        return events;
    }

    private static void Drain(params SimulatedTransport[] peers)
    {
        foreach (var peer in peers)
        {
            Drain(peer);
        }
    }
}
