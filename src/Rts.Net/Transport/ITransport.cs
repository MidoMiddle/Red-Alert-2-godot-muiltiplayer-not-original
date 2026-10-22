using System;
using System.Collections.Generic;

namespace Rts.Net.Transport;

/// <summary>A computer at the other end of a connection, as the transport numbers it. Not a player: a relay is a peer too.</summary>
public readonly record struct PeerId(int Value)
{
    public override string ToString() => "peer " + Value;
}

/// <summary>The channels of a connection (docs/implementation/M9_network.md, 4.2).</summary>
public enum DeliveryChannel : byte
{
    /// <summary>Channel 0: every message arrives, once, in the order sent. The handshake, the lobby, the ticks, the checksums.</summary>
    ReliableOrdered = 0,

    /// <summary>Channel 1: a message may be lost, come twice or overtake another. Pings only.</summary>
    Unreliable = 1,
}

public enum TransportEventKind : byte
{
    Connected = 1,
    Disconnected = 2,
    Message = 3,
}

/// <summary>What happened on the network: a peer came or went, or a message from it arrived (<see cref="Data"/>, a copy of its own).</summary>
public readonly record struct TransportEvent(TransportEventKind Kind, PeerId Peer, DeliveryChannel Channel, byte[] Data)
{
    public static TransportEvent Connected(PeerId peer) => new(TransportEventKind.Connected, peer, DeliveryChannel.ReliableOrdered, Array.Empty<byte>());

    public static TransportEvent Disconnected(PeerId peer) => new(TransportEventKind.Disconnected, peer, DeliveryChannel.ReliableOrdered, Array.Empty<byte>());
}

/// <summary>
/// The network as the lockstep sees it (docs/implementation/M9_network.md, 3, 4.2): connections to peers and messages over two
/// channels. Everything above works through this, so LiteNetLib (step 8) and the in-memory network of the tests are interchangeable.
/// Single-threaded: the game calls <see cref="Poll"/> on its main thread, then takes the events out one by one.
/// </summary>
public interface ITransport
{
    /// <summary>The peers connected now, in the order they came.</summary>
    IReadOnlyList<PeerId> Peers { get; }

    /// <summary>Sends a copy of <paramref name="message"/> to <paramref name="peer"/>; to a peer that is not connected, nothing.</summary>
    void Send(PeerId peer, ReadOnlySpan<byte> message, DeliveryChannel channel);

    /// <summary>Sends a copy of <paramref name="message"/> to every connected peer.</summary>
    void SendToAll(ReadOnlySpan<byte> message, DeliveryChannel channel);

    /// <summary>Closes the connection to <paramref name="peer"/>; both ends get <see cref="TransportEventKind.Disconnected"/>.</summary>
    void Disconnect(PeerId peer);

    /// <summary>Takes what has arrived from the network; it then comes out of <see cref="TryReceive"/>.</summary>
    void Poll();

    /// <summary>The next event in the order it happened, or false when none is waiting.</summary>
    bool TryReceive(out TransportEvent transportEvent);
}
