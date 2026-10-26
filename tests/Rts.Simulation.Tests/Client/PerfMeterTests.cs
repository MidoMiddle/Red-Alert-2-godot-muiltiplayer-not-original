using Rts.Client.Debug;

namespace Rts.Simulation.Tests.Client;

/// <summary>The sums and the lines of the performance log (the owner, 2026-10-26; <see cref="PerfMeter"/>).</summary>
public sealed class PerfMeterTests
{
    [Fact]
    public void A_second_adds_up_its_frames_ticks_phases_and_bots_and_starts_again()
    {
        var meter = new PerfMeter();
        for (var tick = 0; tick < 2; tick++)
        {
            meter.Phase("vision before", 2);
            meter.Phase("movement", 3 + tick);
            meter.Phase("bot 2", 4);
            meter.EndTick(100);
        }

        meter.Frame(0.010);
        meter.Frame(0.030);

        var header = meter.Header().Split(',');
        var line = meter.Line(new PerfSample(1, 30, 5, 12, 3, 4, 2, 50, 20, 900, 64, 32, 7, 1, 0)).Split(',');
        string Column(string name) => line[System.Array.IndexOf(header, name)];

        Assert.Equal(header.Length, line.Length);
        Assert.Equal("2", Column("fps"));
        Assert.Equal("20.00", Column("frame_ms_avg"));
        Assert.Equal("30.00", Column("frame_ms_max"));
        Assert.Equal("2", Column("ticks"));
        Assert.Equal("9.50", Column("tick_ms_avg"));
        Assert.Equal("10.00", Column("tick_ms_max"));
        Assert.Equal("19.00", Column("sim_ms"));
        Assert.Equal("8.00", Column("bots_ms"));
        Assert.Equal("200", Column("path_expanded"));
        Assert.Equal("7.00", Column("ms_movement"));
        Assert.Equal("8.00", Column("ms_bot_2"));
        Assert.Equal("12", Column("units"));

        // The next second starts from nothing.
        var next = meter.Line(new PerfSample(2, 45, 5, 12, 3, 4, 2, 50, 20, 900, 64, 32, 7, 1, 0)).Split(',');
        Assert.Equal("0", next[System.Array.IndexOf(header, "ticks")]);
        Assert.Equal("0.00", next[System.Array.IndexOf(header, "ms_movement")]);
    }

    [Fact]
    public void A_phase_new_after_the_header_counts_in_the_tick_without_a_column()
    {
        var meter = new PerfMeter();
        meter.Phase("movement", 1);
        meter.EndTick(0);
        var header = meter.Header().Split(',');

        meter.Phase("movement", 1);
        meter.Phase("bot 3", 5);
        meter.EndTick(0);
        var line = meter.Line(new PerfSample(1, 15, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)).Split(',');

        // Both ticks of the second: 1, then 1 and the 5 of the bot without a column.
        Assert.Equal(header.Length, line.Length);
        Assert.Equal("7.00", line[System.Array.IndexOf(header, "sim_ms")]);
    }
}
