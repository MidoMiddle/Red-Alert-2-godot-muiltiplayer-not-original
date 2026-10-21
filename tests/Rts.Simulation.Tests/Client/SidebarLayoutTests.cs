using Rts.Client.Hud;

namespace Rts.Simulation.Tests.Client;

/// <summary>The sidebar sizes and tabs of docs/implementation/M6_menus_skirmish.md, 4.7.</summary>
public sealed class SidebarLayoutTests
{
    [Fact]
    public void TheFieldIsTheViewMinusTheSidebar()
    {
        Assert.Equal(1690f, SidebarLayout.FieldWidth(1920f));
    }

    [Fact]
    public void TheItemGridHoldsTwoColumnsOfTenRows()
    {
        Assert.Equal(20, SidebarLayout.Slots);
    }

    [Fact]
    public void TheBuildGroupsAreTheFourTabsOfTheDocs()
    {
        var keys = Enum.GetValues<BuildGroup>().Select(BuildGroups.LocalizationKey).ToList();

        Assert.Equal(new[] { "UI_TAB_BUILDINGS", "UI_TAB_DEFENSE", "UI_TAB_INFANTRY", "UI_TAB_VEHICLES" }, keys);
    }
}
