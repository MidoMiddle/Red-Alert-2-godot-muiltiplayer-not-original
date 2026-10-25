using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Net.Protocol;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Net.Lockstep;

/// <summary>
/// The core of a network match (docs/implementation/M9_network.md, 4.1, 4.5, 4.6): every computer runs the whole simulation and only
/// the commands travel. Tick T is computed once the bundles of every player for T are here — empty ones too. A command given while
/// tick T is next runs on T + D, D being the input delay: the bundle for T + D leaves when T is computed, so the bundles of D ticks
/// ahead are always on their way, and that is the margin against the latency and its jitter.
/// <para>
/// The pace is that of the local clock, as in a single match (15 ticks a second); a computer that has fallen behind the others —
/// their bundles for ticks past its own are here — computes up to <see cref="LockstepSettings.MaxTicksPerFrame"/> a frame to catch up.
/// Time spent waiting for bundles is not owed afterwards: everyone waited.
/// </para>
/// Single-threaded: the game calls <see cref="Update"/> every frame on its main thread.
/// </summary>
public sealed class LockstepEngine
{
    // The clock counts in milliseconds × ticks a second, so that a tick is a whole number of units at any rate.
    private const long UnitsPerTick = 1000;

    private readonly GameSimulation _simulation;
    private readonly ITransport _transport;

    // By player id: the bundles not executed yet, by tick; and the first tick from the current one whose bundle has not come.
    private readonly Dictionary<int, TickBundle>?[] _bundles = new Dictionary<int, TickBundle>?[PlayerId.MaxPlayers + 1];
    private readonly int[] _received = new int[PlayerId.MaxPlayers + 1];

    private readonly List<Command> _pending = new();

    // Our last bundles as sent, for their copies over the unreliable channel; the input delay changes to come, by tick.
    private readonly Queue<byte[]> _recent = new();
    private readonly SortedDictionary<int, int> _delayChanges = new();
    private int _nextBundleTick;
    private uint _nextSequence;
    private long _clock;
    private bool _started;

    /// <param name="players">Every player of the match whose bundles are awaited, the local one among them.</param>
    public LockstepEngine(
        GameSimulation simulation, ITransport transport, PlayerId localPlayer, IEnumerable<PlayerId> players, LockstepSettings? settings = null)
    {
        Settings = settings ?? new LockstepSettings();
        Settings.Validate();
        Players = players.Distinct().OrderBy(player => player.Value).ToImmutableArray();
        if (!Players.Contains(localPlayer) || Players.Any(player => player.Value < 1 || player.Value > PlayerId.MaxPlayers))
        {
            throw new ArgumentException($"The players are 1…{PlayerId.MaxPlayers}, {localPlayer} among them.", nameof(players));
        }

        _simulation = simulation;
        _transport = transport;
        LocalPlayer = localPlayer;
        foreach (var player in Players)
        {
            _bundles[player.Value] = new Dictionary<int, TickBundle>();
            _received[player.Value] = simulation.CurrentTick;
        }

        _nextBundleTick = simulation.CurrentTick;
        InputDelay = Settings.InputDelay;
    }

    /// <summary>The input delay now: a command given while tick T is next runs on T + D.</summary>
    public int InputDelay { get; private set; }

    /// <summary>The input delay changed, with the tick it changed on — the same on every computer.</summary>
    public event Action<int, int>? InputDelayChanged;

    /// <summary>
    /// From <paramref name="tick"/> on the input delay is <paramref name="delay"/> (step 7, 4.7): the host decides it and every computer
    /// switches on the same tick. One that hears of it late switches at once. Does not touch the match: the commands carry their ticks.
    /// </summary>
    public void ScheduleInputDelay(int tick, int delay)
    {
        if (delay < 1 || delay > Protocol.Messages.InputDelayChange.MaxDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "1…10.");
        }

        _delayChanges[tick] = delay;
    }

    /// <summary>Before each computed tick, with its number: the game keeps the poses of the units to draw them between ticks.</summary>
    public event Action<int>? Stepping;

    /// <summary>After each computed tick, with its number; the replay is recorded here (<c>ReplayRecorder.AfterStep</c>).</summary>
    public event Action<int>? Stepped;

    /// <summary>How far into the next tick the local clock is, 0…1: the game draws the units that far between two ticks.</summary>
    public float TickProgress => Math.Clamp((float)_clock / UnitsPerTick, 0f, 1f);

    /// <summary>A message other than a bundle, for the session above (the lobby, the checksums, the pings).</summary>
    public event Action<PeerId, NetMessage>? MessageReceived;

    public LockstepSettings Settings { get; }

    public PlayerId LocalPlayer { get; }

    public ImmutableArray<PlayerId> Players { get; }

    /// <summary>The tick computed next.</summary>
    public int CurrentTick => _simulation.CurrentTick;

    /// <summary>How long a tick has been due without the bundles to compute it; 0 while the game goes.</summary>
    public long WaitingMs { get; private set; }

    /// <summary>Long enough without bundles to tell the player (4.11).</summary>
    public bool IsWaitingForPlayers => WaitingMs >= Settings.WaitNoticeMs;

    /// <summary>The tick the lockstep does not compute: it stops there (a desync, 4.10); null to run on.</summary>
    public int? StopTick { get; private set; }

    /// <summary>The ticks computed by the last <see cref="Update"/>.</summary>
    public int TicksLastFrame { get; private set; }

    /// <summary>Bundles that came again, or for a tick already computed; ignored.</summary>
    public int DuplicateBundles { get; private set; }

    /// <summary>Messages that broke the protocol — unreadable, from a stranger, in our name, far ahead; ignored, for the session to judge.</summary>
    public int RejectedMessages { get; private set; }

    /// <summary>The players whose bundle for <see cref="CurrentTick"/> has not come.</summary>
    public IReadOnlyList<PlayerId> MissingPlayers() => Players.Where(player => _received[player.Value] <= CurrentTick).ToList();

    /// <summary>Computes no tick from <paramref name="tick"/> on; the network is still read.</summary>
    public void StopAt(int tick) => StopTick = StopTick is { } stop ? Math.Min(stop, tick) : tick;

    /// <summary>Sends the empty bundles of the first D ticks: nobody can have given a command for them.</summary>
    public void Start()
    {
        if (_started)
        {
            throw new InvalidOperationException("The lockstep has started already.");
        }

        _started = true;
        SendUntil(CurrentTick + InputDelay - 1);
    }

    /// <summary>
    /// A command of the local player, given now. It is numbered and put on the tick of the next bundle to leave — D ticks after the next
    /// computed one — and returned so; whatever tick and sequence it came with are replaced.
    /// </summary>
    public Command Issue(Command command)
    {
        if (command.Player != LocalPlayer)
        {
            throw new ArgumentException($"{LocalPlayer} cannot give a command of {command.Player}.", nameof(command));
        }

        var scheduled = command with { ExecuteTick = _nextBundleTick, Sequence = _nextSequence++ };
        _pending.Add(scheduled);
        return scheduled;
    }

    /// <summary>
    /// One frame of <paramref name="elapsedMs"/>: takes what came from the network, then computes the ticks due by the clock — or more,
    /// to catch up — as long as their bundles are here.
    /// </summary>
    public void Update(long elapsedMs)
    {
        if (!_started)
        {
            throw new InvalidOperationException("Start the lockstep first.");
        }

        ReceiveAll();
        _clock += elapsedMs * _simulation.Settings.TicksPerSecond;
        var ticks = 0;
        while (ticks < Settings.MaxTicksPerFrame && (_clock >= UnitsPerTick || IsBehind()) && IsReady(CurrentTick)
               && (StopTick is not { } stop || CurrentTick < stop))
        {
            Step();
            ticks++;
            _clock = Math.Max(0, _clock - UnitsPerTick);
        }

        var stalled = ticks < Settings.MaxTicksPerFrame && _clock >= UnitsPerTick && !IsReady(CurrentTick) && StopTick != CurrentTick;
        WaitingMs = stalled ? WaitingMs + elapsedMs : 0;
        // Never more than one tick owed: waiting is not paid back, and a frame that ran out of ticks drops the rest, as the single game
        // does; a computer left behind catches up by the bundles of the others.
        _clock = Math.Min(_clock, UnitsPerTick);
        TicksLastFrame = ticks;
    }

    /// <summary>
    /// A bundle from another player. Ignored when it came already or is for a computed tick; refused when it is of a stranger, in our
    /// name, or too far ahead. Returns whether it was taken.
    /// </summary>
    public bool Receive(TickBundle bundle)
    {
        var player = bundle.Player;
        if (player == LocalPlayer || _bundles[player.Value] is not { } bundles || bundle.Tick > _nextBundleTick + Settings.MaxTicksAhead)
        {
            RejectedMessages++;
            return false;
        }

        if (bundle.Tick < _received[player.Value] || bundles.ContainsKey(bundle.Tick))
        {
            DuplicateBundles++;
            return false;
        }

        Accept(bundle);
        return true;
    }

    private void ReceiveAll()
    {
        _transport.Poll();
        while (_transport.TryReceive(out var received))
        {
            if (received.Kind != TransportEventKind.Message)
            {
                continue;
            }

            NetMessage message;
            try
            {
                message = MessageCodec.Read(received.Data);
            }
            catch (MessageFormatException)
            {
                RejectedMessages++;
                continue;
            }

            if (message is TickBundle bundle)
            {
                Receive(bundle);
            }
            else if (message is Protocol.Messages.InputDelayChange change)
            {
                ScheduleInputDelay(change.Tick, change.Delay);
                MessageReceived?.Invoke(received.Peer, message);
            }
            else
            {
                MessageReceived?.Invoke(received.Peer, message);
            }
        }
    }

    private void Accept(TickBundle bundle)
    {
        var player = bundle.Player.Value;
        _bundles[player]!.Add(bundle.Tick, bundle);
        while (_bundles[player]!.ContainsKey(_received[player]))
        {
            _received[player]++;
        }
    }

    private bool IsReady(int tick)
    {
        foreach (var player in Players)
        {
            if (_received[player.Value] <= tick)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The others are past us: each has sent the bundle of a tick further than D ahead of ours, so each has computed ticks we have not.
    /// </summary>
    private bool IsBehind()
    {
        var any = false;
        foreach (var player in Players)
        {
            if (player == LocalPlayer)
            {
                continue;
            }

            if (_received[player.Value] <= CurrentTick + InputDelay)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    private void Step()
    {
        var tick = CurrentTick;
        while (_delayChanges.Count > 0 && _delayChanges.First() is var (from, delay) && from <= tick)
        {
            _delayChanges.Remove(from);
            if (delay != InputDelay)
            {
                InputDelay = delay;
                InputDelayChanged?.Invoke(tick, delay);
            }
        }

        SendUntil(tick + InputDelay);
        foreach (var player in Players)
        {
            var bundles = _bundles[player.Value]!;
            foreach (var command in bundles[tick].Commands)
            {
                _simulation.Enqueue(command);
            }

            bundles.Remove(tick);
        }

        Stepping?.Invoke(tick);
        _simulation.Step();
        Stepped?.Invoke(tick);
    }

    /// <summary>Sends our bundles up to <paramref name="lastTick"/>; the commands given so far go into the first of them.</summary>
    private void SendUntil(int lastTick)
    {
        while (_nextBundleTick <= lastTick)
        {
            var bundle = new TickBundle(LocalPlayer, _nextBundleTick, _pending.ToImmutableArray());
            _pending.Clear();
            var bytes = MessageCodec.Write(bundle);
            _transport.SendToAll(bytes, DeliveryChannel.ReliableOrdered);
            if (Settings.Redundancy > 0)
            {
                // The copies: this bundle and the ones before it, each its own datagram; whichever comes first is taken.
                _recent.Enqueue(bytes);
                if (_recent.Count > Settings.Redundancy + 1)
                {
                    _recent.Dequeue();
                }

                foreach (var copy in _recent)
                {
                    _transport.SendToAll(copy, DeliveryChannel.Unreliable);
                }
            }

            Accept(bundle);
            _nextBundleTick++;
        }
    }
}
