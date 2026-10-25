using System;
using System.Collections.Generic;
using LiteNetLib;

namespace Rts.Net.Transport;

/// <summary>
/// The real network (docs/implementation/M9_network.md, 4.2, step 8): UDP through LiteNetLib (MIT), its reliable ordered delivery
/// for <see cref="DeliveryChannel.ReliableOrdered"/> and its plain datagrams for <see cref="DeliveryChannel.Unreliable"/>. A host
/// <see cref="Listen"/>s on a port and takes the connections that know the key of the game; a client <see cref="Connect"/>s to it.
/// LiteNetLib's thread does the sockets; its events come out of <see cref="Poll"/> on the game's main thread, as the interface asks.
/// <para>
/// A datagram cannot be larger than the path allows (about 1400 bytes); a message of the unreliable channel that would not fit is
/// not sent — only the copies of bundles go there, and their reliable original is on its way. The reliable channel splits what is
/// larger.
/// </para>
/// </summary>
public sealed class LiteNetTransport : ITransport, IDisposable
{
    /// <summary>The key a connection must bring: another program on the port is not let in.</summary>
    public const string DefaultKey = "rts-ra2-godot";

    private readonly EventBasedNetListener _listener = new();
    private readonly NetManager _manager;
    private readonly string _key;
    private readonly List<PeerId> _peers = new();
    private readonly Dictionary<int, NetPeer> _byId = new();
    private readonly Queue<TransportEvent> _events = new();
    private bool _accepting;

    /// <param name="disconnectTimeoutMs">Silence after which a connection counts as lost.</param>
    public LiteNetTransport(string key = DefaultKey, int disconnectTimeoutMs = 30_000)
    {
        _key = key;
        _manager = new NetManager(_listener)
        {
            DisconnectTimeout = disconnectTimeoutMs,
            AutoRecycle = true,
            UnsyncedEvents = false,
        };
        _listener.ConnectionRequestEvent += request =>
        {
            if (_accepting)
            {
                request.AcceptIfKey(_key);
            }
            else
            {
                request.Reject();
            }
        };
        _listener.PeerConnectedEvent += peer =>
        {
            var id = new PeerId(peer.Id);
            _byId[peer.Id] = peer;
            _peers.Add(id);
            _events.Enqueue(TransportEvent.Connected(id));
        };
        // A connection lost, closed, or one that never was: refused, or the host never answered.
        _listener.PeerDisconnectedEvent += (peer, _) =>
        {
            var id = new PeerId(peer.Id);
            _peers.Remove(id);
            _byId.Remove(peer.Id);
            _events.Enqueue(TransportEvent.Disconnected(id));
        };
        _listener.NetworkReceiveEvent += (peer, reader, _, method) =>
        {
            var channel = method == DeliveryMethod.Unreliable ? DeliveryChannel.Unreliable : DeliveryChannel.ReliableOrdered;
            _events.Enqueue(new TransportEvent(TransportEventKind.Message, new PeerId(peer.Id), channel, reader.GetRemainingBytes()));
        };
    }

    /// <summary>The port this computer listens or sends from.</summary>
    public int LocalPort => _manager.LocalPort;

    public IReadOnlyList<PeerId> Peers => _peers;

    /// <summary>The round trip LiteNetLib measures to a peer, in milliseconds; for the screen only.</summary>
    public int RoundTripMs(PeerId peer) => _byId.TryGetValue(peer.Value, out var found) ? found.RoundTripTime : 0;

    /// <summary>Opens <paramref name="port"/> (0: any free one) and takes the connections that bring the key.</summary>
    public void Listen(int port)
    {
        _accepting = true;
        if (!_manager.Start(port))
        {
            throw new InvalidOperationException($"Port {port} cannot be opened.");
        }
    }

    /// <summary>Connects to a host; <see cref="TransportEventKind.Connected"/> comes once it answers, <see cref="TransportEventKind.Disconnected"/> if it never does.</summary>
    public void Connect(string address, int port)
    {
        if (!_manager.IsRunning && !_manager.Start())
        {
            throw new InvalidOperationException("The network cannot be started.");
        }

        _manager.Connect(address, port, _key);
    }

    public void Send(PeerId peer, ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        if (_byId.TryGetValue(peer.Value, out var found))
        {
            Send(found, message, channel);
        }
    }

    public void SendToAll(ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        foreach (var peer in _byId.Values)
        {
            Send(peer, message, channel);
        }
    }

    public void Disconnect(PeerId peer)
    {
        if (_byId.TryGetValue(peer.Value, out var found))
        {
            _manager.DisconnectPeer(found);
        }
    }

    public void Poll() => _manager.PollEvents();

    public bool TryReceive(out TransportEvent transportEvent) => _events.TryDequeue(out transportEvent);

    public void Dispose() => _manager.Stop();

    private static void Send(NetPeer peer, ReadOnlySpan<byte> message, DeliveryChannel channel)
    {
        if (channel == DeliveryChannel.ReliableOrdered)
        {
            peer.Send(message, DeliveryMethod.ReliableOrdered);
        }
        else if (message.Length <= peer.GetMaxSinglePacketSize(DeliveryMethod.Unreliable))
        {
            peer.Send(message, DeliveryMethod.Unreliable);
        }
    }
}
