using System;

namespace Rts.Net.Lockstep;

/// <summary>The rules of the pace of a network match (docs/implementation/M9_network.md, 4.6, 4.7, 4.11).</summary>
public sealed record LockstepSettings
{
    /// <summary>A command given while tick T is next runs on tick T + D. Adaptive from step 7; 2…10.</summary>
    public int InputDelay { get; init; } = 2;

    /// <summary>The most ticks computed in one frame, when catching up with the others (4.6).</summary>
    public int MaxTicksPerFrame { get; init; } = 10;

    /// <summary>After this long without the bundles of a tick the game shows «waiting for player» (4.11).</summary>
    public int WaitNoticeMs { get; init; } = 3000;

    /// <summary>A bundle further than this ahead of our own is refused: no honest peer can be there, everyone needs our bundles.</summary>
    public int MaxTicksAhead { get; init; } = 256;

    internal void Validate()
    {
        if (InputDelay < 1 || MaxTicksPerFrame < 1 || WaitNoticeMs < 0 || MaxTicksAhead < InputDelay)
        {
            throw new ArgumentException($"Invalid lockstep settings: {this}.");
        }
    }
}
