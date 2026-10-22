using System;

namespace Rts.Net.Transport;

/// <summary>
/// One direction of a connection of the in-memory network (docs/implementation/M9_network.md, 4.15): how long a message travels, how
/// much that varies, how many are lost or come twice, and when the connection breaks.
/// </summary>
public sealed record LinkConditions
{
    public static readonly LinkConditions Perfect = new();

    /// <summary>The time a message travels, in milliseconds.</summary>
    public int LatencyMs { get; init; }

    /// <summary>The travel time varies by up to this much either way, evenly: 150 ± 50 ms is 100…200 ms.</summary>
    public int JitterMs { get; init; }

    /// <summary>The share of sendings lost, 0…100. The reliable channel sends a lost message again; the unreliable one does not.</summary>
    public double LossPercent { get; init; }

    /// <summary>The share of messages of the unreliable channel that arrive twice, 0…100. The reliable channel never doubles.</summary>
    public double DuplicatePercent { get; init; }

    /// <summary>The time of the network at which the connection breaks; both ends then see it closed. Null: never.</summary>
    public long? BreakAtMs { get; init; }

    /// <summary>
    /// After how long the reliable channel sends an unanswered message again. By default as LiteNetLib does it: 25 ms + 2.1 round
    /// trips, the round trip taken as twice this direction's latency.
    /// </summary>
    public int? ResendDelayMs { get; init; }

    public int ResendMs => ResendDelayMs ?? 25 + 21 * 2 * LatencyMs / 10;

    internal void Validate()
    {
        if (LatencyMs < 0 || JitterMs < 0 || LossPercent is < 0 or > 100 || DuplicatePercent is < 0 or > 100 || ResendMs <= 0)
        {
            throw new ArgumentException($"Invalid link conditions: {this}.");
        }
    }
}
