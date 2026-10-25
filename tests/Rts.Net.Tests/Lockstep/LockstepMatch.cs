using Rts.Net.Lockstep;
using Rts.Net.Transport;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Tests;
using Rts.Simulation.Tests.Determinism;
using Rts.Simulation.World;

namespace Rts.Net.Tests.Lockstep;

/// <summary>
/// One computer of a match in memory: its own simulation of the reference match, its lockstep over a <see cref="SimulatedTransport"/>,
/// and a script of the commands its player gives — those of the reference scenario, each given when the computer reaches its tick.
/// </summary>
internal sealed class LockstepClient
{
    private readonly Queue<Command> _script;

    public LockstepClient(SimulatedTransport transport, PlayerId player, IReadOnlyList<PlayerId> players, LockstepSettings settings, IEnumerable<Command> script, int frameMs)
    {
        Transport = transport;
        Simulation = new GameSimulation(ReferenceScenario.Settings() with { HashInterval = 15 }, TestRules.Standard);
        Engine = new LockstepEngine(Simulation, transport, player, players, settings);
        FrameMs = frameMs;
        _script = new Queue<Command>(script.Where(command => command.Player == player));
        Engine.Stepped += _ =>
        {
            if (Simulation.LastChecksum is { } checksum && checksum.Tick == Simulation.CurrentTick)
            {
                Checksums.Add(checksum.Tick, checksum.Value);
            }
        };
    }

    public SimulatedTransport Transport { get; }

    public GameSimulation Simulation { get; }

    public LockstepEngine Engine { get; }

    /// <summary>The time between two frames of this computer.</summary>
    public int FrameMs { get; }

    /// <summary>The checksum of every 15th tick, by tick.</summary>
    public SortedDictionary<int, ulong> Checksums { get; } = new();

    /// <summary>The longest wait for bundles, in milliseconds.</summary>
    public long LongestWaitMs { get; private set; }

    /// <summary>The most ticks computed in one frame.</summary>
    public int MostTicksInFrame { get; private set; }

    /// <summary>When false, the computer is frozen: no frames, the network still runs.</summary>
    public bool Running { get; set; } = true;

    /// <summary>Pings the others and answers their pings (step 7); null without.</summary>
    public LatencyMonitor? Monitor { get; set; }

    /// <summary>The host's decision on the input delay (step 7); null on the others.</summary>
    public InputDelayAuthority? Authority { get; set; }

    /// <summary>The clock of the computer for the pings: the network's.</summary>
    public Func<long> Clock { get; set; } = () => 0;

    /// <summary>One frame: the commands due by the script, then the lockstep.</summary>
    public void Frame(long elapsedMs)
    {
        while (_script.TryPeek(out var next) && next.ExecuteTick <= Engine.CurrentTick)
        {
            Engine.Issue(_script.Dequeue());
        }

        Monitor?.Update(Clock());
        Authority?.Update(Clock());
        Engine.Update(elapsedMs);
        LongestWaitMs = Math.Max(LongestWaitMs, Engine.WaitingMs);
        MostTicksInFrame = Math.Max(MostTicksInFrame, Engine.TicksLastFrame);
    }

    /// <summary>The commands executed before <paramref name="tick"/>, in their order.</summary>
    public List<Command> ExecutedBefore(int tick) => Simulation.ExecutedCommands.Where(command => command.ExecuteTick < tick).ToList();
}

/// <summary>Computers of one match connected each to each over an in-memory network with the given conditions.</summary>
internal sealed class LockstepMatch
{
    private long _sinceStart;

    public LockstepMatch(int players, LinkConditions conditions, LockstepSettings settings, int scriptTicks, ulong networkSeed = 2026, params int[] frameMs)
    {
        Network = new SimulatedNetwork(networkSeed);
        var ids = Enumerable.Range(1, players).Select(id => new PlayerId(id)).ToList();
        var script = scriptTicks > 0 ? ReferenceScenario.Commands(scriptTicks) : [];
        Clients = ids.Select((id, i) => new LockstepClient(
            Network.AddPeer(), id, ids, settings, script, frameMs.Length == 0 ? 16 : frameMs[i % frameMs.Length])).ToList();
        for (var i = 0; i < Clients.Count; i++)
        {
            for (var j = i + 1; j < Clients.Count; j++)
            {
                Network.Connect(Clients[i].Transport, Clients[j].Transport, conditions);
            }
        }
    }

    public SimulatedNetwork Network { get; }

    public List<LockstepClient> Clients { get; }

    public LockstepClient this[int index] => Clients[index];

    public void Start()
    {
        foreach (var client in Clients)
        {
            client.Engine.Start();
        }
    }

    /// <summary>Runs millisecond by millisecond, every running computer at its own frame rate, until each has computed <paramref name="ticks"/>.</summary>
    public void RunUntil(int ticks, long limitMs)
    {
        var until = Network.NowMs + limitMs;
        while (Clients.Any(client => client.Engine.CurrentTick < ticks))
        {
            Assert.True(Network.NowMs < until, $"Ticks {string.Join(", ", Clients.Select(c => c.Engine.CurrentTick))} after {limitMs} ms.");
            Run(1);
        }
    }

    /// <summary>Runs <paramref name="milliseconds"/> of the network clock.</summary>
    public void Run(long milliseconds)
    {
        for (var ms = 0; ms < milliseconds; ms++)
        {
            Network.Advance(1);
            _sinceStart++;
            foreach (var client in Clients)
            {
                if (client.Running && _sinceStart % client.FrameMs == 0)
                {
                    client.Frame(client.FrameMs);
                }
            }
        }
    }
}
