using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Team;

/// <summary>An attack a bot of the team is about to make (M8, 4.8): where, with what strength, announced on which tick.</summary>
public sealed record AttackIntent(PlayerId Player, CellPos Target, int Strength, int Tick);

/// <summary>A call for help of a bot of the team whose base is attacked by more than it holds (M8, 4.8).</summary>
public sealed record DefenseRequest(PlayerId Player, CellPos Cell, int Strength, int Tick);

/// <summary>
/// The board of the bots of one team (docs/implementation/M8_ai_difficulty.md, 4.8, step 11): the attacks they announce, the answers of
/// the others — they join or not, with what strength — and their calls for help. State of the bots, not of the match: not hashed, and the
/// same on every machine because the bots think one after another in the order of the players. Every bot of a team holds the same board;
/// a bot alone holds none. What is older than <see cref="StaleTicks"/> is dropped.
/// </summary>
public sealed class TeamBlackboard(ImmutableArray<PlayerId> bots)
{
    /// <summary>Older than this an entry is forgotten: 60 s.</summary>
    public const int StaleTicks = 900;

    private readonly SortedDictionary<int, AttackIntent> _intents = new();
    private readonly SortedDictionary<int, DefenseRequest> _requests = new();

    // By the player who announced: the allies who answered, whether they join, with what strength.
    private readonly SortedDictionary<int, SortedDictionary<int, (bool Joins, int Strength)>> _answers = new();
    private readonly List<int> _stale = new();

    /// <summary>The bots of the team, in the order of the players.</summary>
    public ImmutableArray<PlayerId> Bots { get; } = bots;

    public IReadOnlyCollection<AttackIntent> Intents => _intents.Values;

    public IReadOnlyCollection<DefenseRequest> Requests => _requests.Values;

    /// <summary>Announces the attack of <paramref name="intent"/>'s player, in place of an earlier one of it; its answers start anew.</summary>
    public void Announce(AttackIntent intent)
    {
        _intents[intent.Player.Value] = intent;
        _answers[intent.Player.Value] = new SortedDictionary<int, (bool, int)>();
    }

    /// <summary>The attack of <paramref name="player"/> is over or called off.</summary>
    public void Withdraw(PlayerId player)
    {
        _intents.Remove(player.Value);
        _answers.Remove(player.Value);
    }

    /// <summary>The answer of <paramref name="ally"/> to the attack of <paramref name="player"/>.</summary>
    public void Answer(PlayerId player, PlayerId ally, bool joins, int strength)
    {
        if (_answers.TryGetValue(player.Value, out var answers))
        {
            answers[ally.Value] = (joins, strength);
        }
    }

    /// <summary>Whether <paramref name="ally"/> has answered the attack of <paramref name="player"/>.</summary>
    public bool Answered(PlayerId player, PlayerId ally) => _answers.TryGetValue(player.Value, out var answers) && answers.ContainsKey(ally.Value);

    /// <summary>Whether every other bot of the team has answered the attack of <paramref name="player"/>.</summary>
    public bool AllAnswered(PlayerId player)
    {
        foreach (var bot in Bots)
        {
            if (bot != player && !Answered(player, bot))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The attack of the team (4.8): of the attacks announced, the one with the greatest strength — its own and that of the allies who
    /// join it; of two as strong, that of the lower player. Null with none.
    /// </summary>
    public AttackIntent? TeamAttack()
    {
        AttackIntent? best = null;
        var bestStrength = -1;
        foreach (var (player, intent) in _intents)
        {
            var strength = intent.Strength;
            foreach (var (_, answer) in _answers[player])
            {
                strength += answer.Joins ? answer.Strength : 0;
            }

            if (strength > bestStrength)
            {
                (best, bestStrength) = (intent, strength);
            }
        }

        return best;
    }

    /// <summary>Calls for help from the base of <paramref name="request"/>'s player.</summary>
    public void Request(DefenseRequest request) => _requests[request.Player.Value] = request;

    /// <summary>The base of <paramref name="player"/> is safe again.</summary>
    public void Clear(PlayerId player) => _requests.Remove(player.Value);

    /// <summary>Drops what is older than <see cref="StaleTicks"/> at <paramref name="tick"/>.</summary>
    public void Forget(int tick)
    {
        _stale.Clear();
        foreach (var (player, intent) in _intents)
        {
            if (tick - intent.Tick > StaleTicks)
            {
                _stale.Add(player);
            }
        }

        foreach (var player in _stale)
        {
            Withdraw(new PlayerId(player));
        }

        _stale.Clear();
        foreach (var (player, request) in _requests)
        {
            if (tick - request.Tick > StaleTicks)
            {
                _stale.Add(player);
            }
        }

        foreach (var player in _stale)
        {
            _requests.Remove(player);
        }
    }
}
