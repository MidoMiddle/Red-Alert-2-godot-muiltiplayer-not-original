using System;
using System.Collections.Generic;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.World;

namespace Rts.Net.Desync;

/// <summary>
/// The checksums of a network match (docs/implementation/M9_network.md, 4.10): every time the simulation hashes its state (every
/// <c>HashInterval</c> ticks, 15 by default) the computer tells everyone its hash, and a <see cref="DesyncDetector"/> compares them
/// with the others'. On a desync it tells everyone (<see cref="DesyncReport"/>) and stops its lockstep a little after the tick:
/// <see cref="StopAfterTicks"/> later on every computer, which is past what any of them can have computed by then, so all stop on the
/// same tick and their <see cref="StateDump"/>s compare field by field.
/// </summary>
public sealed class ChecksumExchange
{
    /// <summary>
    /// How far past the desync every computer stops. One can have computed no more than D + 1 ticks past another's checksum before it
    /// arrives (the checksum leaves before the bundles of the next ticks, on the same ordered channel), D being at most 10.
    /// </summary>
    public const int StopAfterTicks = 12;

    private readonly LockstepEngine _engine;
    private readonly GameSimulation _simulation;
    private readonly ITransport _transport;
    private readonly List<Rts.Simulation.Hashing.StateChecksum> _own = new();

    public ChecksumExchange(LockstepEngine engine, GameSimulation simulation, ITransport transport, IEnumerable<PlayerId> players)
    {
        _engine = engine;
        _simulation = simulation;
        _transport = transport;
        Detector = new DesyncDetector(players);
        engine.Stepped += AfterStep;
        engine.MessageReceived += (_, message) =>
        {
            if (message is Checksum checksum)
            {
                ReceivedChecksums++;
                Found(Detector.Report(checksum.Player, checksum.Tick, checksum.Hash));
            }
        };

        // A match whose first tick was computed before the lockstep (the loading screen brings the starting units in so) has the
        // checksum of that tick already: it is everyone's too.
        AfterStep(simulation.CurrentTick - 1);
    }

    /// <summary>The desync, once; the lockstep goes on to <see cref="StopTick"/> and stops there.</summary>
    public event Action<Desync>? DesyncFound;

    public DesyncDetector Detector { get; }

    /// <summary>The tick the lockstep stops on after a desync; null while there is none.</summary>
    public int? StopTick { get; private set; }

    /// <summary>Stopped on <see cref="StopTick"/>: the state to dump is this one, the same tick as everyone's.</summary>
    public bool Stopped => StopTick is { } stop && _engine.CurrentTick >= stop;

    /// <summary>The checksums of the others that came.</summary>
    public int ReceivedChecksums { get; private set; }

    /// <summary>The checksums of this computer, in order: those of its replay.</summary>
    public IReadOnlyList<Rts.Simulation.Hashing.StateChecksum> OwnChecksums => _own;

    /// <summary>A player left the match: its checksums are no longer awaited.</summary>
    public void Drop(PlayerId player) => Found(Detector.Drop(player));

    private void AfterStep(int tick)
    {
        if (_simulation.LastChecksum is { } checksum && checksum.Tick == _simulation.CurrentTick)
        {
            _own.Add(checksum);
            _transport.SendToAll(MessageCodec.Write(new Checksum(_engine.LocalPlayer, checksum.Tick, checksum.Value)), DeliveryChannel.ReliableOrdered);
            Found(Detector.Report(_engine.LocalPlayer, checksum.Tick, checksum.Value));
        }
    }

    private void Found(Desync? desync)
    {
        if (desync is null)
        {
            return;
        }

        StopTick = desync.Tick + StopAfterTicks;
        _engine.StopAt(StopTick.Value);
        var own = desync.Hashes.TryGetValue(_engine.LocalPlayer.Value, out var hash) ? hash : 0;
        _transport.SendToAll(MessageCodec.Write(new DesyncReport(_engine.LocalPlayer, desync.Tick, own)), DeliveryChannel.ReliableOrdered);
        DesyncFound?.Invoke(desync);
    }
}
