using System;
using System.Linq;

namespace Rts.Client.Audio;

/// <summary>
/// The sounds of the menus (<c>sounds.yaml</c>, <c>snd_ui_*</c>), and which a pressed button plays: the start of a match rises, back
/// and cancel fall, every other button clicks. Pure, so the tests check it without Godot.
/// </summary>
public static class InterfaceSounds
{
    public const string Hover = "snd_ui_hover";
    public const string Click = "snd_ui_click";
    public const string Back = "snd_ui_back";
    public const string Start = "snd_ui_start";
    public const string ToggleOn = "snd_ui_toggle_on";
    public const string ToggleOff = "snd_ui_toggle_off";
    public const string Select = "snd_ui_select";
    public const string Tab = "snd_ui_tab";
    public const string Tick = "snd_ui_tick";

    /// <summary>Every sound of the menus, for the test that each is defined.</summary>
    public static readonly string[] All = [Hover, Click, Back, Start, ToggleOn, ToggleOff, Select, Tab, Tick];

    /// <summary>The names of the buttons that start a match: the skirmish's start, play again, restart from the pause.</summary>
    private static readonly string[] StartNames = ["StartGame", "PlayAgain", "RestartButton"];

    /// <summary>The names of buttons that go back.</summary>
    private static readonly string[] BackNames = ["Back", "Cancel"];

    /// <summary>The texts of buttons that go back, as translation keys: a back button has no name of its own on every screen.</summary>
    private static readonly string[] BackKeys = ["UI_BUTTON_BACK", "UI_BUTTON_CANCEL", "UI_MAP_BACK_TO_LIST", "UI_MATCH_RESUME"];

    /// <summary>
    /// The sound of a button named <paramref name="name"/> with the text <paramref name="text"/> when it is pressed; <paramref name="translate"/>
    /// turns a key into its text in the language of the game.
    /// </summary>
    public static string OfPress(string name, string text, Func<string, string> translate)
    {
        if (StartNames.Contains(name))
        {
            return Start;
        }

        return BackNames.Contains(name) || BackKeys.Any(key => text == translate(key)) ? Back : Click;
    }
}
