using System;
using System.Collections.Generic;

namespace Rts.Net.Transport;

/// <summary>
/// One computer on a <see cref="SimulatedNetwork"/> (docs/implementation/M9_network.md, 4.15): the transport of the tests, with no
/// sockets. What the network delivers waits in the inbox until taken; <see cref="Poll"/> has nothing to do, since the network delivers
/// when its clock moves.
/// </summary>
public sealed class SimulatedTransport : ITransport
{
    private readonly List<PeerId> _peers = new();
    private readonly Queue<TransportEvent> _inbox = new();

    internal SimulatedTransport(SimulatedNetwork network, PeerId id)
    {
        Network = network;
        Id = id;
    }

    /// <summary>How the others see this computer.</summary>
    public PeerId Id { get; }

    public IReadOnlyList<PeerId> Peers => _peers;

    internal SimulatedNetwork Network { get; }

    public void Send(PeerId peer, ReadOnlySpan<byte> message, DeliveryChannel channel) => Network.Send(this, peer, message, channel);

    public void SendToAll(ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        foreach (var peer in _peers)
        {
            Network.Send(this, peer, message, channel);
        }
    }

    public void Disconnect(PeerId peer) => Network.Disconnect(this, peer);

    public void Poll()
    {
    }

    public bool TryReceive(out TransportEvent transportEvent) => _inbox.TryDequeue(out transportEvent);

    internal void OnConnected(PeerId peer)
    {
        _peers.Add(peer);
        _inbox.Enqueue(TransportEvent.Connected(peer));
    }

    internal void OnDisconnected(PeerId peer)
    {
        _peers.Remove(peer);
        _inbox.Enqueue(TransportEvent.Disconnected(peer));
    }

    internal void Deliver(PeerId from, DeliveryChannel channel, byte[] data) =>
        _inbox.Enqueue(new TransportEvent(TransportEventKind.Message, from, channel, data));

    public override string ToString() => Id.ToString();
}
