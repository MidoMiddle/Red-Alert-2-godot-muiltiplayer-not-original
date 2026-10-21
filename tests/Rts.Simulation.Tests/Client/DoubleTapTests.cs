using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>The double press shared by the control groups and the type select key (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class DoubleTapTests
{
    private const int TypeKey = 1;

    [Fact]
    public void ASecondPressWithinTheWindowIsADoubleTap()
    {
        var taps = new DoubleTap();

        Assert.False(taps.Press(TypeKey, 1000));
        Assert.True(taps.Press(TypeKey, 1000 + DoubleTap.WindowMs));
    }

    [Fact]
    public void ASecondPressAfterTheWindowIsASingleTap()
    {
        var taps = new DoubleTap();

        Assert.False(taps.Press(TypeKey, 1000));
        Assert.False(taps.Press(TypeKey, 1000 + DoubleTap.WindowMs + 1));
    }

    [Fact]
    public void AThirdPressStartsOverInsteadOfCountingAsAnotherDoubleTap()
    {
        var taps = new DoubleTap();
        taps.Press(TypeKey, 1000);
        Assert.True(taps.Press(TypeKey, 1100));

        Assert.False(taps.Press(TypeKey, 1200));
        Assert.True(taps.Press(TypeKey, 1300));
    }

    [Fact]
    public void PressesOfDifferentKeysDoNotPair()
    {
        var taps = new DoubleTap();
        taps.Press(TypeKey, 1000);

        Assert.False(taps.Press(2, 1100));
        Assert.False(taps.Press(TypeKey, 1200));
    }
}
