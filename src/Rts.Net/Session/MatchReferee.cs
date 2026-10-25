using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Net.Transport;
using Rts.Simulation.World;

namespace Rts.Net.Session;

/// <summary>The rules of leaving and pausing a network match (docs/implementation/M9_network.md, 4.11, 4.12).</summary>
public sealed record RefereeSettings
{
    /// <summary>A player whose bundles keep the game waiting this long is out (<see cref="LeaveReason.Timeout"/>).</summary>
    public int DropAfterMs { get; init; } = 30_000;

    /// <summary>After this long waiting for a player the others may vote to drop it.</summary>
    public int KickVoteAfterMs { get; init; } = 10_000;

    /// <summary>Pauses each player may take in a match.</summary>
    public int PausesPerPlayer { get; init; } = 3;

    /// <summary>The longest a pause lasts; then the game goes on by itself.</summary>
    public int PauseMs { get; init; } = 60_000;

    /// <summary>How long the connection of a player put out stays open, for the news to reach it.</summary>
    public int LeaverLingerMs { get; init; } = 1000;
}

/// <summary>
/// The host's say in a running network match (docs/implementation/M9_network.md, 4.11, 4.12, step 10). Who is out and from which tick,
/// and when the game pauses, must be the same on every computer, so one decides it and tells the others: the host.
/// <list type="bullet">
/// <item>A player leaves when its connection is lost, when the game has waited <see cref="RefereeSettings.DropAfterMs"/> for its
/// bundles, or when most of the others vote to drop it (<see cref="KickVote"/>, after <see cref="RefereeSettings.KickVoteAfterMs"/> of
/// waiting). It leaves on the first tick whose bundle of it the host has not got (<see cref="LockstepEngine.ReceivedUntil"/>): every
/// client gets the player's bundles through the host, so nobody can have computed that tick. The host tells everyone
/// (<see cref="PlayerLeft"/>), takes nothing more from its connection and closes it.</item>
/// <item>A player asks for a pause (<see cref="PauseRequest"/>), at most <see cref="RefereeSettings.PausesPerPlayer"/> times; the game
/// stands from the tick of the host's next bundle on (<see cref="Pause"/>). Only that player takes it off, or the time runs out
/// (<see cref="RefereeSettings.PauseMs"/>); then <see cref="Resume"/>.</item>
/// </list>
/// What it says to the others it raises as <see cref="Announced"/> too, for the host's own screen (<see cref="MatchStatus"/>).
/// </summary>
public sealed class MatchReferee
{
    private readonly LockstepEngine _engine;
    private readonly ITransport _transport;
    private readonly Dictionary<PeerId, PlayerId> _players;
    private readonly Dictionary<PlayerId, int> _pausesTaken = new();
    private readonly Dictionary<PlayerId, HashSet<PlayerId>> _votes = new();
    private readonly List<(PeerId Peer, long At)> _closing = new();
    private long _now;
    private long _pausedAt;

    /// <param name="players">The player of every client's connection.</param>
    public MatchReferee(LockstepEngine engine, ITransport transport, IReadOnlyDictionary<PeerId, PlayerId> players, RefereeSettings? settings = null)
    {
        _engine = engine;
        _transport = transport;
        _players = players.ToDictionary(pair => pair.Key, pair => pair.Value);
        Settings = settings ?? new RefereeSettings();
        engine.PeerDisconnected += peer =>
        {
            if (_players.TryGetValue(peer, out var player))
            {
                Leave(player, LeaveReason.Disconnected);
            }
        };
        engine.MessageReceived += (peer, message) =>
        {
            // A client speaks for its own player only.
            switch (message)
            {
                case PauseRequest request when IsFrom(peer, request.Player):
                    RequestPause(request.Player, request.Pause);
                    break;
                case KickVote vote when IsFrom(peer, vote.Voter):
                    Vote(vote.Voter, vote.Target);
                    break;
            }
        };
    }

    public RefereeSettings Settings { get; }

    /// <summary>What the host told everyone, as it was sent.</summary>
    public event Action<NetMessage>? Announced;

    /// <summary>The player whose pause stands now; null while the game goes.</summary>
    public PlayerId? PausedBy { get; private set; }

    /// <summary>The pauses <paramref name="player"/> may still take.</summary>
    public int PausesLeft(PlayerId player) => Settings.PausesPerPlayer - _pausesTaken.GetValueOrDefault(player);

    /// <summary>
    /// Whether the others may vote to drop <paramref name="target"/> now: the game has waited for its bundles
    /// <see cref="RefereeSettings.KickVoteAfterMs"/>.
    /// </summary>
    public bool CanVoteOut(PlayerId target) =>
        target != _engine.LocalPlayer && _engine.WaitingMs >= Settings.KickVoteAfterMs && _engine.MissingPlayers().Contains(target);

    /// <summary>Counts the time: ends a pause that ran out, drops a player waited for too long, closes the connections of those put out.</summary>
    public void Update(long elapsedMs)
    {
        _now += elapsedMs;
        if (PausedBy is { } pauser && _now - _pausedAt >= Settings.PauseMs)
        {
            EndPause(pauser);
        }

        if (_engine.WaitingMs >= Settings.DropAfterMs)
        {
            foreach (var player in _engine.MissingPlayers())
            {
                Leave(player, LeaveReason.Timeout);
            }
        }
        else if (_engine.WaitingMs == 0)
        {
            // The game goes again: the votes were for a wait that is over.
            _votes.Clear();
        }

        foreach (var (peer, at) in _closing.Where(closing => closing.At <= _now).ToList())
        {
            _closing.Remove((peer, at));
            _transport.Disconnect(peer);
        }
    }

    /// <summary>
    /// <paramref name="player"/> asks to pause (<paramref name="pause"/>) or to go on. Returns whether it was done: a pause while none
    /// stands, by a player still in the match with pauses left; going on, by the player who paused.
    /// </summary>
    public bool RequestPause(PlayerId player, bool pause)
    {
        if (!pause)
        {
            if (PausedBy != player)
            {
                return false;
            }

            EndPause(player);
            return true;
        }

        if (PausedBy is not null || !_engine.RemainingPlayers().Contains(player) || PausesLeft(player) <= 0)
        {
            return false;
        }

        _pausesTaken[player] = _pausesTaken.GetValueOrDefault(player) + 1;
        PausedBy = player;
        _pausedAt = _now;
        var tick = _engine.NextBundleTick;
        _engine.PauseAt(tick);
        Announce(new Pause(player, PauseReason.Player, tick));
        return true;
    }

    /// <summary>
    /// <paramref name="voter"/> votes to drop <paramref name="target"/>; once more than half of the players still in the match but the
    /// target agree, it is out (<see cref="LeaveReason.Kicked"/>). Returns whether the vote counted.
    /// </summary>
    public bool Vote(PlayerId voter, PlayerId target)
    {
        var remaining = _engine.RemainingPlayers();
        if (voter == target || !remaining.Contains(voter) || !remaining.Contains(target) || !CanVoteOut(target))
        {
            return false;
        }

        if (!_votes.TryGetValue(target, out var voters))
        {
            voters = new HashSet<PlayerId>();
            _votes[target] = voters;
        }

        voters.Add(voter);
        if (voters.Count * 2 > remaining.Count - 1)
        {
            Leave(target, LeaveReason.Kicked);
        }

        return true;
    }

    /// <summary>
    /// <paramref name="player"/> is out of the match: from the first tick whose bundle of it has not come, on every computer. Once; never
    /// the host's own player — the host leaving ends the match.
    /// </summary>
    public void Leave(PlayerId player, LeaveReason reason)
    {
        if (player == _engine.LocalPlayer || !_engine.RemainingPlayers().Contains(player))
        {
            return;
        }

        var tick = _engine.ReceivedUntil(player);
        Announce(new PlayerLeft(player, reason, tick));
        _engine.RemovePlayer(player, tick);
        foreach (var (peer, _) in _players.Where(pair => pair.Value == player).ToList())
        {
            (_transport as HostRelay)?.Block(peer);
            _players.Remove(peer);
            _closing.Add((peer, _now + Settings.LeaverLingerMs));
        }

        _votes.Remove(player);
        foreach (var voters in _votes.Values)
        {
            voters.Remove(player);
        }

        if (PausedBy == player)
        {
            EndPause(player);
        }
    }

    private bool IsFrom(PeerId peer, PlayerId player) => _players.TryGetValue(peer, out var seat) && seat == player;

    private void EndPause(PlayerId player)
    {
        PausedBy = null;
        _engine.Resume();
        Announce(new Resume(player, _engine.CurrentTick));
    }

    private void Announce(NetMessage message)
    {
        _transport.SendToAll(MessageCodec.Write(message), DeliveryChannel.ReliableOrdered);
        Announced?.Invoke(message);
    }
}
