using System;
using System.Collections.Generic;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;

namespace Rts.Net.Transport;

/// <summary>
/// A room of the relay as a network (docs/implementation/M9_network.md, 4.3, 4.14, step 12). The only real connection is to the relay
/// (<c>Rts.Server</c>); the members of the room are the peers, numbered as the relay numbers them (<see cref="PeerId"/> = member). It is
/// the star of the direct mode with the relay in the middle: the one who opened the room hosts the lobby and is member 1, its peers are
/// the clients; a client's only peer is the host. So the sessions, the lockstep and the host's say above work over it unchanged.
/// <para>
/// Every message goes inside a <see cref="Relayed"/> frame, on the channel it was sent on; the relay passes it on to whom it is for and
/// says whom it is from. The comings and goings of members come as <see cref="RoomMember"/> and become connections and disconnections.
/// The relay closing on us, or refusing the room, disconnects every peer.
/// </para>
/// </summary>
public sealed class RelayTransport : ITransport
{
    /// <summary>The member that opened the room and hosts it.</summary>
    public static readonly PeerId HostMember = new(1);

    private readonly ITransport _relay;
    private readonly string _requested;
    private readonly List<PeerId> _peers = new();
    private readonly Queue<TransportEvent> _events = new();
    private PeerId? _server;

    /// <param name="relay">The connection to the relay; it may still be connecting.</param>
    /// <param name="roomCode">The room to join; empty to open a new one and host it.</param>
    public RelayTransport(ITransport relay, string roomCode = "")
    {
        _relay = relay;
        _requested = roomCode;
    }

    /// <summary>The code of the room once in it: the one to give the others; null before.</summary>
    public string? RoomCode { get; private set; }

    /// <summary>Our number in the room; 0 before we are in.</summary>
    public int Member { get; private set; }

    public bool IsHost => Member == HostMember.Value;

    /// <summary>Why the relay did not take us in; null when it did or has not answered yet.</summary>
    public Reject? Rejection { get; private set; }

    /// <summary>The relay closed the connection, or it never opened.</summary>
    public bool Closed { get; private set; }

    public IReadOnlyList<PeerId> Peers => _peers;

    public void Send(PeerId peer, ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        if (_server is { } server && _peers.Contains(peer))
        {
            _relay.Send(server, Relayed.Frame(peer.Value, message), channel);
        }
    }

    public void SendToAll(ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        if (_server is { } server && _peers.Count > 0)
        {
            _relay.Send(server, Relayed.Frame(0, message), channel);
        }
    }

    /// <summary>The host puts a member out of the room; a client leaves the room — its connection to the relay closes.</summary>
    public void Disconnect(PeerId peer)
    {
        if (_server is not { } server || !_peers.Contains(peer))
        {
            return;
        }

        if (IsHost)
        {
            _relay.Send(server, MessageCodec.Write(new RoomMember(peer.Value, false)), DeliveryChannel.ReliableOrdered);
        }
        else
        {
            _relay.Disconnect(server);
        }
    }

    public void Poll()
    {
        _relay.Poll();
        while (_relay.TryReceive(out var received))
        {
            switch (received.Kind)
            {
                case TransportEventKind.Connected when _server is null && !Closed:
                    _server = received.Peer;
                    _relay.Send(received.Peer, MessageCodec.Write(new RoomRequest(_requested)), DeliveryChannel.ReliableOrdered);
                    break;
                case TransportEventKind.Disconnected when _server is null || received.Peer == _server:
                    Close();
                    break;
                case TransportEventKind.Message when received.Peer == _server:
                    Handle(received);
                    break;
            }
        }
    }

    public bool TryReceive(out TransportEvent transportEvent) => _events.TryDequeue(out transportEvent);

    private void Handle(TransportEvent received)
    {
        NetMessage message;
        try
        {
            message = MessageCodec.Read(received.Data);
        }
        catch (MessageFormatException error) when (error.Error == MessageError.WrongProtocolVersion && Member == 0)
        {
            Refused(new Reject(RejectReason.ProtocolVersion, $"The relay speaks the network protocol {error.ProtocolVersion}, we {NetProtocol.Version}."));
            return;
        }
        catch (MessageFormatException)
        {
            return;
        }

        switch (message)
        {
            case RoomJoined joined when Member == 0:
                RoomCode = joined.Code;
                Member = joined.Member;
                if (!IsHost)
                {
                    Come(HostMember);
                }

                break;
            case Reject reject when Member == 0:
                Refused(reject);
                break;
            case RoomMember { Here: true } member when Member != 0:
                Come(new PeerId(member.Member));
                break;
            case RoomMember { Here: false } member when Member != 0:
                Go(new PeerId(member.Member));
                break;
            case Relayed relayed when _peers.Contains(new PeerId(relayed.Member)):
                _events.Enqueue(new TransportEvent(TransportEventKind.Message, new PeerId(relayed.Member), received.Channel, relayed.Payload));
                break;
        }
    }

    // A refusal comes to a client's session as the host's: it connects, refuses, closes — as a host of the direct mode would. One who
    // wanted to open a room has no session waiting for a host: Rejection says it.
    private void Refused(Reject reject)
    {
        Rejection = reject;
        Closed = true;
        if (_requested != "")
        {
            _events.Enqueue(TransportEvent.Connected(HostMember));
            _events.Enqueue(new TransportEvent(TransportEventKind.Message, HostMember, DeliveryChannel.ReliableOrdered, MessageCodec.Write(reject)));
            _events.Enqueue(TransportEvent.Disconnected(HostMember));
        }

        if (_server is { } server)
        {
            _relay.Disconnect(server);
        }
    }

    private void Come(PeerId peer)
    {
        if (!_peers.Contains(peer))
        {
            _peers.Add(peer);
            _events.Enqueue(TransportEvent.Connected(peer));
        }
    }

    private void Go(PeerId peer)
    {
        if (_peers.Remove(peer))
        {
            _events.Enqueue(TransportEvent.Disconnected(peer));
        }
    }

    private void Close()
    {
        Closed = true;
        foreach (var peer in _peers.ToArray())
        {
            Go(peer);
        }
    }
}
