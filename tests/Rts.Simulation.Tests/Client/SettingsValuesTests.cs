using Rts.Client.App;

namespace Rts.Simulation.Tests.Client;

/// <summary>Rules of the settings file (docs/implementation/M6_menus_skirmish.md, 4.5): defaults, unknown keys, invalid values.</summary>
public sealed class SettingsValuesTests
{
    [Fact]
    public void DefaultsAreHowTheGameStartedBeforeSettingsExisted()
    {
        var defaults = SettingsValues.Defaults;

        Assert.Equal(WindowMode.Windowed, defaults.WindowMode);
        Assert.Equal(new WindowSize(1280, 720), defaults.Resolution);
        Assert.True(defaults.VSync);
        Assert.Equal(SettingsValues.Unlimited, defaults.FpsLimit);
        Assert.Equal(100, defaults.InterfaceScale);
        Assert.Equal(100, defaults.MasterVolume);
    }

    [Fact]
    public void MissingValuesFallBackToDefaults()
    {
        var values = SettingsValues.Parse(new Dictionary<string, string>());

        Assert.Equal(SettingsValues.Defaults, values);
    }

    [Fact]
    public void UnknownKeysAreIgnored()
    {
        var raw = new Dictionary<string, string>
        {
            ["audio/music_on"] = "false",
            ["graphics/shadow_quality"] = "low",
        };

        var values = SettingsValues.Parse(raw);

        Assert.Equal(SettingsValues.Defaults, values);
        Assert.DoesNotContain("audio/music_on", values.ToRaw().Keys);
        Assert.DoesNotContain("graphics/shadow_quality", values.ToRaw().Keys);
    }

    [Fact]
    public void TheFileNamesItsFormatVersion()
    {
        Assert.Equal("1", SettingsValues.Defaults.ToRaw()["meta/format"]);
    }

    [Fact]
    public void EveryValueSurvivesASaveAndALoad()
    {
        var values = SettingsValues.Defaults with
        {
            WindowMode = WindowMode.Borderless,
            Resolution = new WindowSize(2560, 1440),
            VSync = false,
            FpsLimit = 120,
            InterfaceScale = 150,
            MasterVolume = 40,
        };

        Assert.Equal(values, SettingsValues.Parse(values.ToRaw()));
    }

    [Fact]
    public void WindowModeIsReadByNameWithoutCase()
    {
        Assert.Equal(WindowMode.Fullscreen, ParseOne("graphics/window_mode", "FULLSCREEN").WindowMode);
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("7")]
    public void UnknownWindowModeFallsBackToWindowed(string text)
    {
        Assert.Equal(WindowMode.Windowed, ParseOne("graphics/window_mode", text).WindowMode);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("")]
    public void VSyncThatIsNotABooleanFallsBackToOn(string text)
    {
        Assert.True(ParseOne("graphics/vsync", text).VSync);
    }

    [Fact]
    public void ResolutionIsKeptWhenItIsAChoice()
    {
        var raw = new Dictionary<string, string>
        {
            ["graphics/resolution_width"] = "1920",
            ["graphics/resolution_height"] = "1080",
        };

        Assert.Equal(new WindowSize(1920, 1080), SettingsValues.Parse(raw).Resolution);
    }

    [Theory]
    [InlineData("1000", "700")]
    [InlineData("abc", "720")]
    public void ResolutionOutsideTheChoicesFallsBackToDefault(string width, string height)
    {
        var raw = new Dictionary<string, string>
        {
            ["graphics/resolution_width"] = width,
            ["graphics/resolution_height"] = height,
        };

        Assert.Equal(new WindowSize(1280, 720), SettingsValues.Parse(raw).Resolution);
    }

    [Theory]
    [InlineData("45", SettingsValues.Unlimited)]
    [InlineData("144", 144)]
    public void FrameLimitMustBeAChoice(string text, int expected)
    {
        Assert.Equal(expected, ParseOne("graphics/fps_limit", text).FpsLimit);
    }

    [Theory]
    [InlineData("250", 100)]
    [InlineData("-5", 0)]
    [InlineData("40", 40)]
    public void MasterVolumeIsKeptInItsRange(string text, int expected)
    {
        Assert.Equal(expected, ParseOne("audio/master_volume", text).MasterVolume);
    }

    [Theory]
    [InlineData("10", 75)]
    [InlineData("500", 200)]
    [InlineData("137", 135)]
    [InlineData("138", 140)]
    [InlineData("abc", 100)]
    public void InterfaceScaleIsRoundedToAStepAndKeptInItsRange(string text, int expected)
    {
        Assert.Equal(expected, ParseOne("graphics/interface_scale", text).InterfaceScale);
    }

    private static SettingsValues ParseOne(string key, string text)
    {
        return SettingsValues.Parse(new Dictionary<string, string> { [key] = text });
    }
}
