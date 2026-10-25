using System.Collections.Generic;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Simulation.World;

namespace Rts.Net.Session;

/// <summary>
/// What the host decided in a running match, as every computer sees it (docs/implementation/M9_network.md, 4.11, 4.12): who paused and
/// for how long, how many pauses each has taken, who left and why. Fed with the host's <see cref="Pause"/>, <see cref="Resume"/> and
/// <see cref="PlayerLeft"/> — on a client as they come, on the host from its <see cref="MatchReferee.Announced"/>. For the screen only.
/// </summary>
public sealed class MatchStatus
{
    private readonly Dictionary<PlayerId, int> _pausesTaken = new();
    private readonly Dictionary<PlayerId, LeaveReason> _left = new();

    public MatchStatus(RefereeSettings? settings = null)
    {
        Settings = settings ?? new RefereeSettings();
    }

    public RefereeSettings Settings { get; }

    /// <summary>The player whose pause stands; null while the game goes.</summary>
    public PlayerId? PausedBy { get; private set; }

    /// <summary>How long the pause has stood, in milliseconds.</summary>
    public long PauseElapsedMs { get; private set; }

    /// <summary>How long the pause may still stand, in milliseconds.</summary>
    public long PauseLeftMs => PausedBy is null ? 0 : System.Math.Max(0, Settings.PauseMs - PauseElapsedMs);

    /// <summary>The players out of the match, and why.</summary>
    public IReadOnlyDictionary<PlayerId, LeaveReason> Left => _left;

    /// <summary>The pauses <paramref name="player"/> may still take.</summary>
    public int PausesLeft(PlayerId player) => Settings.PausesPerPlayer - _pausesTaken.GetValueOrDefault(player);

    public void Apply(NetMessage message)
    {
        switch (message)
        {
            case Pause { Reason: PauseReason.Player } pause:
                PausedBy = pause.Player;
                PauseElapsedMs = 0;
                _pausesTaken[pause.Player] = _pausesTaken.GetValueOrDefault(pause.Player) + 1;
                break;
            case Resume:
                PausedBy = null;
                break;
            case PlayerLeft left:
                _left[left.Player] = left.Reason;
                break;
        }
    }

    /// <summary>Counts the time of the pause.</summary>
    public void Update(long elapsedMs)
    {
        if (PausedBy is not null)
        {
            PauseElapsedMs += elapsedMs;
        }
    }
}
