using Rts.Client.App;
using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>Rules of the settings file (docs/implementation/M6_menus_skirmish.md, 4.5 and 4.6): defaults, unknown keys, invalid values.</summary>
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
        Assert.Equal(ControlScheme.Classic, defaults.Scheme);
        Assert.Equal(5, defaults.ScrollSpeed);
        Assert.True(defaults.EdgeScroll);
        Assert.True(defaults.CameraDrag);
        Assert.Equal(HealthBarMode.Damaged, defaults.HealthBars);
    }

    [Fact]
    public void ThePlaytestSurveyIsOffWithoutTheKeyAndComesBackFromTheFile()
    {
        // M8 step 14: a file of before has no key — the survey is off, the format is the same.
        Assert.False(SettingsValues.Defaults.PlaytestSurvey);
        Assert.False(SettingsValues.Parse(new Dictionary<string, string>()).PlaytestSurvey);
        var on = SettingsValues.Defaults with { PlaytestSurvey = true };
        Assert.Equal("true", on.ToRaw()["interface/playtest_survey"]);
        Assert.True(SettingsValues.Parse(on.ToRaw()).PlaytestSurvey);
    }

    [Fact]
    public void HealthBarsReadTheirModeAndFallBackOnAnUnknownOne()
    {
        Assert.Equal(HealthBarMode.Always, SettingsValues.Parse(new Dictionary<string, string> { ["interface/health_bars"] = "always" }).HealthBars);
        Assert.Equal(HealthBarMode.Damaged, SettingsValues.Parse(new Dictionary<string, string> { ["interface/health_bars"] = "never" }).HealthBars);
        Assert.Equal("always", (SettingsValues.Defaults with { HealthBars = HealthBarMode.Always }).ToRaw()["interface/health_bars"]);
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
            Scheme = ControlScheme.Modern,
            ScrollSpeed = 8,
            EdgeScroll = false,
            CameraDrag = false,
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

    [Theory]
    [InlineData("modern", ControlScheme.Modern)]
    [InlineData("CLASSIC", ControlScheme.Classic)]
    [InlineData("left-handed", ControlScheme.Classic)]
    public void SchemeIsReadByNameOrFallsBackToClassic(string text, ControlScheme expected)
    {
        Assert.Equal(expected, ParseOne("controls/scheme", text).Scheme);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("11", 10)]
    [InlineData("7", 7)]
    [InlineData("fast", 5)]
    public void ScrollSpeedIsKeptInItsRange(string text, int expected)
    {
        Assert.Equal(expected, ParseOne("controls/scroll_speed", text).ScrollSpeed);
    }

    private static SettingsValues ParseOne(string key, string text)
    {
        return SettingsValues.Parse(new Dictionary<string, string> { [key] = text });
    }
}
