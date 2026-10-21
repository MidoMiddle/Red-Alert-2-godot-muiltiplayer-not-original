using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

public sealed class SelectionSetReplaceAllTests
{
    [Fact]
    public void Replace_all_selects_exactly_the_given_units_without_repeats()
    {
        var selection = new SelectionSet();
        selection.Replace(new EntityId(9));

        selection.ReplaceAll([new EntityId(3), new EntityId(1), new EntityId(3)]);

        Assert.Equal([new EntityId(3), new EntityId(1)], selection.Units);
    }
}
