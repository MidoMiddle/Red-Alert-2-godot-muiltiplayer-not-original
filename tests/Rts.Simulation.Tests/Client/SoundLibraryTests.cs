using Rts.Client.Audio;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// <c>game/data/audio/sounds.yaml</c> (docs/implementation/M6_menus_skirmish.md, 4.8): the shipped definitions are valid, and a
/// mistake in a definition is reported and leaves only that sound out.
/// </summary>
public sealed class SoundLibraryTests
{
    private const string SoundsFile = "data/audio/sounds.yaml";

    [Fact]
    public void The_shipped_sounds_are_valid()
    {
        var errors = new List<string>();

        var sounds = SoundLibrary.Parse(ShippedText(), SoundsFile, errors);

        Assert.Empty(errors);
        Assert.NotEmpty(sounds);
    }

    [Fact]
    public void Every_shipped_sound_is_on_a_bus_of_the_layout_and_has_a_level_of_the_plan()
    {
        var sounds = SoundLibrary.Parse(ShippedText(), SoundsFile, []);

        foreach (var sound in sounds.Values)
        {
            Assert.Contains(sound.Bus, SoundLibrary.Buses);
            Assert.Contains(sound.Priority, SoundPriority.Levels);
            Assert.NotEmpty(sound.Files);
            Assert.True(sound.PitchMin > 0f && sound.PitchMin <= sound.PitchMax, sound.Name);
            Assert.True(sound.MaxConcurrent >= 1, sound.Name);
            Assert.True(sound.CooldownMs >= 0, sound.Name);
        }
    }

    [Fact]
    public void A_sound_gets_defaults_for_the_fields_it_leaves_out()
    {
        var sounds = SoundLibrary.Parse(
            """
            sounds:
              snd_click:
                files: [ui/click_01.wav]
                bus: UI
                priority: 70
                maxConcurrent: 2
            """,
            "test.yaml",
            []);

        var click = sounds["snd_click"];
        Assert.Equal(0f, click.VolumeDb);
        Assert.Equal(1f, click.PitchMin);
        Assert.Equal(1f, click.PitchMax);
        Assert.Equal(0, click.CooldownMs);
        Assert.False(click.Positional);
    }

    [Fact]
    public void A_sound_with_a_wrong_field_is_left_out_and_the_mistake_is_named()
    {
        var errors = new List<string>();

        var sounds = SoundLibrary.Parse(
            """
            sounds:
              snd_bad:
                files: [sfx/bad.wav]
                bus: SFX
                priority: 200
                maxConcurrent: 1
              snd_good:
                files: [sfx/good.wav]
                bus: SFX
                priority: 50
                maxConcurrent: 1
            """,
            "test.yaml",
            errors);

        Assert.DoesNotContain("snd_bad", sounds.Keys);
        Assert.Contains("snd_good", sounds.Keys);
        Assert.Contains(errors, error => error.Contains("snd_bad.priority", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("files: [sfx/bad.wav]\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1\n    volum: -3", "is not a field of a sound")]
    [InlineData("files: [sfx/bad.wav]\n    bus: Sound\n    priority: 50\n    maxConcurrent: 1", "must be one of")]
    [InlineData("files: [sfx/bad.wav]\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1\n    pitch: 1.0", "must be two numbers")]
    [InlineData("files: [sfx/bad.wav]\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1\n    pitch: [1.2, 0.8]", "must be two numbers")]
    [InlineData("files: [sfx/bad.wav]\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1\n    cooldownMs: -5", "must be a whole number")]
    [InlineData("files: [sfx/bad.wav]\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1\n    positional: maybe", "must be true or false")]
    [InlineData("files: []\n    bus: SFX\n    priority: 50\n    maxConcurrent: 1", "must list at least one file")]
    public void A_wrong_field_is_reported(string fields, string message)
    {
        var errors = new List<string>();

        SoundLibrary.Parse($"sounds:\n  snd_bad:\n    {fields}\n", "test.yaml", errors);

        Assert.Contains(errors, error => error.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_without_a_sounds_table_is_reported()
    {
        var errors = new List<string>();

        var sounds = SoundLibrary.Parse("music: {}", "test.yaml", errors);

        Assert.Empty(sounds);
        Assert.Single(errors);
    }

    private static string ShippedText() => File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, "game", "data", "audio", "sounds.yaml"));
}
