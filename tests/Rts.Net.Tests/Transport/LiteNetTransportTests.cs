using System.Collections.Immutable;
using System.Diagnostics;
using Rts.Net.Desync;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Session;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Transport;

/// <summary>
/// Step 8 of M9 (docs/implementation/M9_network.md, 4.2): LiteNetLib over UDP on this computer — connections, both channels, the
/// order and the size of the reliable one, the key — and a whole match through it: the handshake, the start and the lockstep of a host
/// and two clients.
/// </summary>
[Collection("UDP")]
public sealed class LiteNetTransportTests
{
    private static readonly NetIdentity Identity = new("0.9.0+abc", 71, 42);

    [Fact]
    public void A_client_connects_with_the_key_and_both_channels_deliver_the_reliable_one_in_order_and_whole()
    {
        using var host = new LiteNetTransport();
        using var client = new LiteNetTransport();
        host.Listen(0);
        client.Connect("127.0.0.1", host.LocalPort);
        Pump(() => host.Peers.Count == 1 && client.Peers.Count == 1, host, client);
        var hostEvents = Drain(host);
        Assert.Equal(TransportEventKind.Connected, Assert.Single(hostEvents).Kind);

        // 1000 small ones in order, one larger than a datagram, and one over the unreliable channel.
        for (var i = 0; i < 1000; i++)
        {
            client.SendToAll(BitConverter.GetBytes(i), DeliveryChannel.ReliableOrdered);
        }

        var large = Enumerable.Range(0, NetProtocol.MaxMessageSize).Select(i => (byte)i).ToArray();
        client.SendToAll(large, DeliveryChannel.ReliableOrdered);
        client.SendToAll([7, 7, 7], DeliveryChannel.Unreliable);
        var received = new List<TransportEvent>();
        Pump(() =>
        {
            received.AddRange(Drain(host));
            return received.Count(e => e.Channel == DeliveryChannel.ReliableOrdered) == 1001 && received.Any(e => e.Channel == DeliveryChannel.Unreliable);
        }, host, client);

        var reliable = received.Where(e => e.Channel == DeliveryChannel.ReliableOrdered).ToList();
        Assert.Equal(Enumerable.Range(0, 1000), reliable.Take(1000).Select(e => BitConverter.ToInt32(e.Data)));
        Assert.Equal(large, reliable[1000].Data);
        Assert.Equal(new byte[] { 7, 7, 7 }, received.Single(e => e.Channel == DeliveryChannel.Unreliable).Data);

        // Either end closes; both hear it.
        host.Disconnect(host.Peers[0]);
        Pump(() => client.Peers.Count == 0 && host.Peers.Count == 0, host, client);
        Assert.Contains(Drain(client), e => e.Kind == TransportEventKind.Disconnected);
    }

    [Fact]
    public void A_program_without_the_key_is_not_let_in()
    {
        using var host = new LiteNetTransport();
        using var stranger = new LiteNetTransport(key: "something else", disconnectTimeoutMs: 2000);
        host.Listen(0);
        stranger.Connect("127.0.0.1", host.LocalPort);
        Pump(() => Drain(stranger).Any(e => e.Kind == TransportEventKind.Disconnected) || stranger.Peers.Count > 0, host, stranger, timeoutMs: 8000);

        Assert.Empty(host.Peers);
        Assert.Empty(stranger.Peers);
    }

    [Fact]
    public void A_host_and_two_clients_meet_start_together_and_play_the_same_match_over_udp()
    {
        using var hostTransport = new LiteNetTransport();
        hostTransport.Listen(0);
        var lobby = new LobbyState("udp_map", 9, new MatchOptions(FogOfWar: false),
            [new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, "Host", "union", "", 0, 1, LobbySlot.RandomStart, true)]);
        var host = new HostSession(hostTransport, Identity, lobby, new PlayerId(1));
        var clientTransports = new[] { new LiteNetTransport(), new LiteNetTransport() };
        var clients = clientTransports.Select((transport, i) => new ClientSession(transport, Identity, $"Client {i}", (_, hash) => hash == 9)).ToList();
        try
        {
            foreach (var transport in clientTransports)
            {
                transport.Connect("127.0.0.1", hostTransport.LocalPort);
            }

            void Sessions()
            {
                host.Update(1);
                clients.ForEach(client => client.Update());
            }

            Wait(() => clients.All(client => client.Phase == SessionPhase.Lobby) && host.Lobby.Slots.Length == 3, Sessions);
            var starts = ImmutableArray.Create(new CellPos(10, 10), new CellPos(50, 50), new CellPos(10, 50));
            var setup = new MatchSetup("udp_map", 5, new MatchOptions(FogOfWar: false),
                host.Lobby.Slots.Select(slot => new MatchPlayer(slot.Slot, PlayerKind.Human, AiDifficulty.Medium, "union", slot.Color, slot.Slot, slot.Slot - 1, starts[slot.Slot - 1])).ToImmutableArray(),
                starts);
            host.Start(new StartGame(setup, 15, 15, 9, 3));
            Wait(() => clients.All(client => client.Phase == SessionPhase.Loading), Sessions);
            host.ReportLoaded(100);
            clients.ForEach(client => client.ReportLoaded(100));
            Wait(() => host.Phase == SessionPhase.Running && clients.All(client => client.Phase == SessionPhase.Running), Sessions);

            var computers = new List<(GameSimulation Simulation, LockstepEngine Engine, ChecksumExchange Exchange)>();
            void Add(ITransport transport, PlayerId player, IReadOnlyList<PlayerId> players)
            {
                var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
                var engine = new LockstepEngine(simulation, transport, player, players, new LockstepSettings { InputDelay = 3 });
                computers.Add((simulation, engine, new ChecksumExchange(engine, simulation, transport, players)));
                engine.Start();
            }

            Add(new HostRelay(hostTransport), new PlayerId(1), host.ActivePlayers);
            for (var i = 0; i < clients.Count; i++)
            {
                Add(clientTransports[i], clients[i].Player!.Value, clients[i].ActivePlayers);
            }

            var ordered = new bool[computers.Count];
            Wait(() => computers.All(c => c.Engine.CurrentTick >= 300), () =>
            {
                for (var i = 0; i < computers.Count; i++)
                {
                    var (simulation, engine, _) = computers[i];
                    if (!ordered[i] && engine.CurrentTick >= 20)
                    {
                        var units = simulation.World.Entities.Where(e => e is Unit && e.Owner == engine.LocalPlayer).Select(e => e.Id).ToImmutableArray();
                        engine.Issue(new MoveCommand(engine.LocalPlayer, 0, 0, units, new CellPos(32, 32).Center, false));
                        ordered[i] = true;
                    }

                    engine.Update(16);
                }
            }, timeoutMs: 60_000);

            Assert.All(computers, c => Assert.Null(c.Exchange.Detector.Found));
            Assert.All(computers, c => Assert.True(c.Exchange.Detector.LastAgreedTick >= 285, $"agreed up to {c.Exchange.Detector.LastAgreedTick}"));
            Assert.All(computers, c => Assert.Equal(3, c.Simulation.ExecutedCommands.Select(command => command.Player).Distinct().Count()));
        }
        finally
        {
            foreach (var transport in clientTransports)
            {
                transport.Dispose();
            }
        }
    }

    private static List<TransportEvent> Drain(ITransport transport)
    {
        transport.Poll();
        var events = new List<TransportEvent>();
        while (transport.TryReceive(out var transportEvent))
        {
            events.Add(transportEvent);
        }

        return events;
    }

    // Polls until the condition holds; the sockets work on their own thread.
    private static void Pump(Func<bool> done, ITransport a, ITransport b, int timeoutMs = 10_000)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(watch.ElapsedMilliseconds < timeoutMs, "Timed out.");
            a.Poll();
            b.Poll();
            Thread.Sleep(1);
        }
    }

    private static void Wait(Func<bool> done, Action step, int timeoutMs = 15_000)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Assert.True(watch.ElapsedMilliseconds < timeoutMs, "Timed out.");
            step();
            Thread.Sleep(1);
        }
    }
}
