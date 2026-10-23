using System;
using System.Collections.Generic;
using System.Linq;

namespace Rts.Client.Audio;

/// <summary>What a sound asks of the pool: its priority, how loud it sounds now, and its limits from <c>sounds.yaml</c>.</summary>
public readonly record struct VoiceRequest(string Sound, int Priority, float Loudness, int MaxConcurrent, int CooldownMs);

/// <summary>The player that takes the sound, and the slot of the sound it pushed out, or -1.</summary>
public readonly record struct VoiceGrant(int Slot, int Evicted);

/// <summary>
/// The players of the sounds (docs/implementation/M6_menus_skirmish.md, 4.8): a fixed number of them, shared by every sound.
/// A sound waits out its cooldown and its place under the limit of simultaneous sounds of its kind. When every player is busy,
/// the least important sound gives way, and among equals the quietest; a sound gives way only to a more important one, or to an
/// equally important and louder one. Pure, so the rules are tested without the engine: the caller plays and stops the players.
/// </summary>
public sealed class VoicePool(int size)
{
    private readonly Voice?[] _voices = new Voice?[size];
    private readonly Dictionary<string, long> _lastStart = new();

    private sealed record Voice(string Sound, int Priority, float Loudness);

    /// <summary>Number of players.</summary>
    public int Size => _voices.Length;

    /// <summary>Number of players that are busy now.</summary>
    public int Active => _voices.Count(voice => voice is not null);

    /// <summary>
    /// Takes a player for <paramref name="request"/> at <paramref name="nowMs"/>. False when the sound is in its cooldown, is at its limit,
    /// or no player is free and none of the busy ones is less important. A refused sound does not start its cooldown.
    /// </summary>
    public bool TryAcquire(VoiceRequest request, long nowMs, out VoiceGrant grant)
    {
        grant = default;
        if (_lastStart.TryGetValue(request.Sound, out var last) && nowMs - last < request.CooldownMs)
        {
            return false;
        }

        if (ActiveOf(request.Sound) >= request.MaxConcurrent)
        {
            return false;
        }

        var slot = Array.IndexOf(_voices, null);
        var evicted = -1;
        if (slot < 0)
        {
            evicted = Quietest();
            var loser = _voices[evicted]!;
            if (loser.Priority > request.Priority || loser.Priority == request.Priority && loser.Loudness >= request.Loudness)
            {
                return false;
            }

            slot = evicted;
        }

        _voices[slot] = new Voice(request.Sound, request.Priority, request.Loudness);
        _lastStart[request.Sound] = nowMs;
        grant = new VoiceGrant(slot, evicted);
        return true;
    }

    /// <summary>Frees a player whose sound has ended.</summary>
    public void Release(int slot) => _voices[slot] = null;

    /// <summary>Number of busy players that play <paramref name="sound"/>.</summary>
    public int ActiveOf(string sound) => _voices.Count(voice => voice?.Sound == sound);

    /// <summary>The busy player that gives way first: the least important, then the quietest; the first one of those on a tie.</summary>
    private int Quietest()
    {
        var best = -1;
        for (var slot = 0; slot < _voices.Length; slot++)
        {
            if (_voices[slot] is not { } voice)
            {
                continue;
            }

            if (best < 0 || voice.Priority < _voices[best]!.Priority
                || voice.Priority == _voices[best]!.Priority && voice.Loudness < _voices[best]!.Loudness)
            {
                best = slot;
            }
        }

        return best;
    }
}
