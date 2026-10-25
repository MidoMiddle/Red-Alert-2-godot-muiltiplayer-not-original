using System;

namespace Rts.Client.Matches;

/// <summary>
/// The game speed of RA2, 1–6 (docs/implementation/M6_menus_skirmish.md, 4.4, step 8): every step is a number of ticks a second — 8,
/// 10, 12, 15, 20, 25 — and the tick itself does not change, so the match is the same at any speed; only the clock of the client goes
/// faster or slower. Speed 4 is the rate of the simulation (15 ticks a second). The keys of the speed step through the same scale.
/// </summary>
public static class GameSpeeds
{
    public const int Slowest = 1;
    public const int Fastest = 6;
    public const int Normal = 4;

    private static readonly int[] TicksPerSecond = [8, 10, 12, 15, 20, 25];

    /// <summary>The ticks a second of <paramref name="speed"/>, 1–6; outside, the nearest end.</summary>
    public static int TicksOf(int speed) => TicksPerSecond[Math.Clamp(speed, Slowest, Fastest) - 1];

    /// <summary>How much faster than the simulation's own rate <paramref name="speed"/> runs the clock.</summary>
    public static double Multiplier(int speed, int simulationTicksPerSecond) => (double)TicksOf(speed) / simulationTicksPerSecond;

    /// <summary>One step up or down the scale, never past its ends.</summary>
    public static int Step(int speed, int direction) => Math.Clamp(speed + Math.Sign(direction), Slowest, Fastest);
}
