using System;
using System.Collections.Generic;
using Rts.Net.Protocol;

namespace Rts.Net.Transport;

/// <summary>
/// The host of the direct mode is the relay of its match (docs/implementation/M9_network.md, 4.3): every client is connected to it
/// only, so what a client says to everyone — its bundles, checksums, load progress, pause requests, surrender, desync reports — the host
/// passes on to the other clients, byte for byte, and takes it itself too. Wraps the host's transport; the lockstep of the host reads
/// through it.
/// </summary>
public sealed class HostRelay : ITransport
{
    private static readonly HashSet<MessageType> Forwarded =
    [
        MessageType.TickBundle, MessageType.Checksum, MessageType.LoadProgress, MessageType.PauseRequest, MessageType.Surrender,
        MessageType.DesyncReport,
    ];

    private readonly ITransport _inner;

    public HostRelay(ITransport inner)
    {
        _inner = inner;
    }

    /// <summary>Messages passed on to the other clients.</summary>
    public int ForwardedMessages { get; private set; }

    public IReadOnlyList<PeerId> Peers => _inner.Peers;

    public void Send(PeerId peer, ReadOnlySpan<byte> message, DeliveryChannel channel) => _inner.Send(peer, message, channel);

    public void SendToAll(ReadOnlySpan<byte> message, DeliveryChannel channel) => _inner.SendToAll(message, channel);

    public void Disconnect(PeerId peer) => _inner.Disconnect(peer);

    public void Poll() => _inner.Poll();

    public bool TryReceive(out TransportEvent transportEvent)
    {
        if (!_inner.TryReceive(out transportEvent))
        {
            return false;
        }

        if (transportEvent is { Kind: TransportEventKind.Message, Data.Length: > 0 } && Forwarded.Contains((MessageType)transportEvent.Data[0]))
        {
            foreach (var peer in _inner.Peers)
            {
                if (peer != transportEvent.Peer)
                {
                    _inner.Send(peer, transportEvent.Data, transportEvent.Channel);
                    ForwardedMessages++;
                }
            }
        }

        return true;
    }
}
