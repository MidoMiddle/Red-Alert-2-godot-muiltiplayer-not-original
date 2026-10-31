using Rts.Client.Audio;

namespace Rts.Simulation.Tests.Client;

/// <summary>The sounds of the menus (<see cref="InterfaceSounds"/>): which a pressed button plays, and that each is defined.</summary>
public sealed class InterfaceSoundsTests
{
    // The game in English: a key is turned into the text of the buttons that carry it.
    private static string English(string key) => key switch
    {
        "UI_BUTTON_BACK" => "Back",
        "UI_BUTTON_CANCEL" => "Cancel",
        "UI_MAP_BACK_TO_LIST" => "Back to the list",
        "UI_MATCH_RESUME" => "Resume",
        _ => key,
    };

    [Theory]
    [InlineData("StartGame", "Start game", InterfaceSounds.Start)]
    [InlineData("PlayAgain", "Play again", InterfaceSounds.Start)]
    [InlineData("RestartButton", "Restart", InterfaceSounds.Start)]
    [InlineData("Back", "Back", InterfaceSounds.Back)]
    [InlineData("@Button@42", "Back", InterfaceSounds.Back)]
    [InlineData("@Button@43", "Back to the list", InterfaceSounds.Back)]
    [InlineData("@Button@44", "Resume", InterfaceSounds.Back)]
    [InlineData("SettingsButton", "Settings", InterfaceSounds.Click)]
    [InlineData("@Button@45", "Generate", InterfaceSounds.Click)]
    public void A_pressed_button_plays_the_sound_of_what_it_does(string name, string text, string sound) =>
        Assert.Equal(sound, InterfaceSounds.OfPress(name, text, English));

    [Fact]
    public void Every_sound_of_the_menus_is_defined()
    {
        var sounds = SoundLibrary.Parse(
            File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, "game", "data", "audio", "sounds.yaml")), "data/audio/sounds.yaml", []);

        Assert.All(InterfaceSounds.All, sound => Assert.Equal("UI", sounds[sound].Bus));
    }
}
