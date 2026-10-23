using System.Text.RegularExpressions;
using Rts.Client.Audio;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The bus layout of the game (docs/implementation/M6_menus_skirmish.md, 4.8): Master with Music, SFX, Voice and UI under it, and EVA
/// under Voice with the radio effect. The sounds name their buses, so the layout and <see cref="SoundLibrary.Buses"/> must agree.
/// </summary>
public sealed class AudioBusLayoutTests
{
    private static readonly string Layout = File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, "game", "default_bus_layout.tres"));

    [Fact]
    public void The_layout_has_the_buses_the_sounds_can_name()
    {
        var names = Regex.Matches(Layout, @"bus/\d+/name = &""(\w+)""").Select(match => match.Groups[1].Value).ToList();

        Assert.Equal(SoundLibrary.Buses.Where(bus => bus != "Master").OrderBy(bus => bus), names.OrderBy(bus => bus));
    }

    [Fact]
    public void Eva_sends_to_voice_and_has_the_radio_effect()
    {
        var index = Regex.Match(Layout, @"bus/(\d+)/name = &""EVA""").Groups[1].Value;

        Assert.Contains($"bus/{index}/send = &\"Voice\"", Layout, StringComparison.Ordinal);
        Assert.Matches($@"bus/{index}/effect/\d+/effect = SubResource\(", Layout);
    }

    [Fact]
    public void Every_bus_except_master_sends_to_master_or_voice()
    {
        var sends = Regex.Matches(Layout, @"bus/\d+/send = &""(\w+)""").Select(match => match.Groups[1].Value).ToList();

        Assert.All(sends, send => Assert.Contains(send, new[] { "Master", "Voice" }));
    }
}
