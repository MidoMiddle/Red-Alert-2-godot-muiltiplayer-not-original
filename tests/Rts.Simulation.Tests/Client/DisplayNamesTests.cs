using Rts.Client.Hud;

namespace Rts.Simulation.Tests.Client;

/// <summary>The names on screen: RA2's while the setting is on and the key is known, ours otherwise.</summary>
public sealed class DisplayNamesTests
{
    private static string Translate(string key) => key == "RA2_NAME_ALLIANCE_RIFLEMAN" ? "GI" : key;

    [Fact]
    public void The_screen_shows_the_name_of_ra2_while_the_setting_is_on()
    {
        Assert.Equal("RA2_NAME_ALLIANCE_RIFLEMAN", DisplayNames.KeyOf("alliance_rifleman"));
        Assert.Equal("GI", DisplayNames.Of("alliance_rifleman", "Rifleman", ra2: true, Translate));
        Assert.Equal("Rifleman", DisplayNames.Of("alliance_rifleman", "Rifleman", ra2: false, Translate));
        Assert.Equal("Light Tank", DisplayNames.Of("alliance_light_tank", "Light Tank", ra2: true, Translate));
    }
}
