using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>D on a mixed group of soldiers that dig in (RA2): those standing dig in, those dug in stay; then all stand up.</summary>
public sealed class DeployGroupTests
{
    [Fact]
    public void While_one_stands_only_the_standing_dig_in()
    {
        Assert.True(DeployGroup.Sends(digsIn: true, dugIn: false, anyStanding: true));
        Assert.False(DeployGroup.Sends(digsIn: true, dugIn: true, anyStanding: true));
    }

    [Fact]
    public void All_dug_in_all_stand_up()
    {
        Assert.True(DeployGroup.Sends(digsIn: true, dugIn: true, anyStanding: false));
    }

    [Fact]
    public void Other_units_deploy_as_before()
    {
        // An MCV or a transport in the group: its deploy goes whatever the soldiers do.
        Assert.True(DeployGroup.Sends(digsIn: false, dugIn: false, anyStanding: true));
        Assert.True(DeployGroup.Sends(digsIn: false, dugIn: false, anyStanding: false));
    }
}
