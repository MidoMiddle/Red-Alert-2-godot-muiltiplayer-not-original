using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

public sealed class BoxSelectionTests
{
    private static readonly (EntityId, ScreenPoint)[] Units =
    [
        (new EntityId(1), new ScreenPoint(10, 10)),
        (new EntityId(2), new ScreenPoint(50, 50)),
        (new EntityId(3), new ScreenPoint(100, 20)),
        (new EntityId(4), new ScreenPoint(200, 200)),
    ];

    [Fact]
    public void Units_inside_the_box_are_picked_edges_included()
    {
        Assert.Equal([new EntityId(1), new EntityId(2), new EntityId(3)], BoxSelection.Pick(Units, new ScreenPoint(10, 10), new ScreenPoint(100, 50)));
    }

    [Fact]
    public void Corners_may_come_in_any_order()
    {
        var expected = BoxSelection.Pick(Units, new ScreenPoint(0, 0), new ScreenPoint(60, 60));

        Assert.Equal(expected, BoxSelection.Pick(Units, new ScreenPoint(60, 60), new ScreenPoint(0, 0)));
        Assert.Equal(expected, BoxSelection.Pick(Units, new ScreenPoint(60, 0), new ScreenPoint(0, 60)));
        Assert.Equal([new EntityId(1), new EntityId(2)], expected);
    }

    [Fact]
    public void Nothing_outside_is_picked()
    {
        Assert.Empty(BoxSelection.Pick(Units, new ScreenPoint(300, 300), new ScreenPoint(400, 400)));
    }

    [Fact]
    public void Toggle_and_add_all_change_the_selection()
    {
        var selection = new SelectionSet();
        selection.Replace(new EntityId(1));

        selection.Toggle(new EntityId(2));
        Assert.Equal([new EntityId(1), new EntityId(2)], selection.Units);
        selection.Toggle(new EntityId(1));
        Assert.Equal([new EntityId(2)], selection.Units);

        selection.AddAll([new EntityId(2), new EntityId(5), new EntityId(3)]);
        Assert.Equal([new EntityId(2), new EntityId(5), new EntityId(3)], selection.Units);
    }

    [Fact]
    public void A_type_select_on_screen_picks_units_of_the_type_inside_the_field_only()
    {
        (EntityId, UnitType, ScreenPoint)[] units =
        [
            (new EntityId(1), UnitType.HeavyTank, new ScreenPoint(10, 10)),
            (new EntityId(2), UnitType.Conscript, new ScreenPoint(20, 20)),
            (new EntityId(3), UnitType.HeavyTank, new ScreenPoint(800, 600)),
            (new EntityId(4), UnitType.HeavyTank, new ScreenPoint(801, 10)),
            (new EntityId(5), UnitType.HeavyTank, new ScreenPoint(-1, 10)),
        ];

        Assert.Equal(
            [new EntityId(1), new EntityId(3)],
            BoxSelection.SameTypeInField(units, UnitType.HeavyTank, new ScreenRect(0, 0, 800, 600)));
    }

    [Fact]
    public void A_whole_map_type_select_keeps_the_first_units_of_the_type_up_to_the_limit()
    {
        (EntityId, UnitType)[] units =
        [
            (new EntityId(1), UnitType.HeavyTank),
            (new EntityId(2), UnitType.Conscript),
            (new EntityId(3), UnitType.HeavyTank),
            (new EntityId(4), UnitType.HeavyTank),
        ];

        Assert.Equal([new EntityId(1), new EntityId(3)], BoxSelection.SameTypeOnMap(units, UnitType.HeavyTank, 2));
        Assert.Equal([new EntityId(1), new EntityId(3), new EntityId(4)], BoxSelection.SameTypeOnMap(units, UnitType.HeavyTank, 256));
    }

    [Fact]
    public void A_box_dragged_over_the_sidebar_is_clipped_to_the_field_and_misses_what_is_hidden()
    {
        var field = new ScreenRect(0, 0, 1690, 1008);
        var (from, to) = BoxSelection.ClipToField(new ScreenPoint(1000, 100), new ScreenPoint(1800, 300), field);
        (EntityId, ScreenPoint)[] behindSidebar =
        [
            (new EntityId(8), new ScreenPoint(1500, 200)),
            (new EntityId(9), new ScreenPoint(1750, 200)),
        ];

        Assert.Equal(new ScreenPoint(1690, 300), to);
        Assert.Equal([new EntityId(8)], BoxSelection.Pick(behindSidebar, from, to));
    }
}
