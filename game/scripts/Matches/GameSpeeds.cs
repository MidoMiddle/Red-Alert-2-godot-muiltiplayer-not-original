using System;

namespace Rts.Client.Matches;

/// <summary>
/// The game speed of RA2, 1–6 (docs/implementation/M6_menus_skirmish.md, 4.4): every step is a number of ticks a second — the frame
/// rates of RA2's six fastest speeds, 12, 15, 20, 30, 45, 60 (a frame of RA2 is a tick; RA2 starts at 45, its rules' GameSpeed=1; the
/// owner, 2026-10-26: «at the top it all moves slowly») — and the tick itself does not change, so the match is the same at any speed;
/// only the clock of the client goes faster or slower. Speed 2 is the simulation's own rate (15 ticks a second), 4 the default. The keys
/// of the speed step through the same scale.
/// </summary>
public static class GameSpeeds
{
    public const int Slowest = 1;
    public const int Fastest = 6;
    public const int Normal = 4;

    private static readonly int[] TicksPerSecond = [12, 15, 20, 30, 45, 60];

    /// <summary>The ticks a second of <paramref name="speed"/>, 1–6; outside, the nearest end.</summary>
    public static int TicksOf(int speed) => TicksPerSecond[Math.Clamp(speed, Slowest, Fastest) - 1];

    /// <summary>How much faster than the simulation's own rate <paramref name="speed"/> runs the clock.</summary>
    public static double Multiplier(int speed, int simulationTicksPerSecond) => (double)TicksOf(speed) / simulationTicksPerSecond;

    /// <summary>One step up or down the scale, never past its ends.</summary>
    public static int Step(int speed, int direction) => Math.Clamp(speed + Math.Sign(direction), Slowest, Fastest);
}
