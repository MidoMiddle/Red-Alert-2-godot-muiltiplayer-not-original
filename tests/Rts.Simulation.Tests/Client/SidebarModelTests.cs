using Rts.Client.Hud;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The sidebar read from the simulation (docs/implementation/M3_base_economy.md, 4.17): open tabs, the squares and their states, and
/// what a click does, carried through the simulation the way the sidebar sends it.
/// </summary>
public sealed class SidebarModelTests
{
    private static readonly PlayerId Player1 = new(1);

    private uint _sequence;

    private static GameSimulation Match(int credits = 10_000) =>
        new(new SimulationSettings(1, new MapGrid(40, 30, TestRules.Standard.Land)) { StartingCredits = credits }, TestRules.Standard);

    private static Building Build(GameSimulation simulation, string id, int x, int y) =>
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef(id), new CellPos(x, y), true, null);

    /// <summary>A ready construction yard, its technologies worked out.</summary>
    private static GameSimulation WithYard(int credits = 10_000)
    {
        var simulation = Match(credits);
        Build(simulation, "construction_yard", 2, 2);
        simulation.Step();
        return simulation;
    }

    private static IReadOnlyList<Cameo> Tab(GameSimulation simulation, BuildGroup group) => SidebarModel.Cameos(simulation.World, Player1, group);

    private static Cameo Square(GameSimulation simulation, BuildGroup group, string name) => Tab(simulation, group).Single(cameo => cameo.Name == name);

    /// <summary>What the sidebar does with an action: the matching command, sent and played for a tick.</summary>
    private void Do(GameSimulation simulation, CameoAction action)
    {
        Command? command = action.Kind switch
        {
            CameoActionKind.Produce => new ProduceCommand(Player1, simulation.CurrentTick, _sequence++, action.Item, action.DefIndex, action.Count),
            CameoActionKind.Pause => new PauseProductionCommand(Player1, simulation.CurrentTick, _sequence++, action.Item, action.DefIndex),
            CameoActionKind.Cancel => new CancelProductionCommand(Player1, simulation.CurrentTick, _sequence++, action.Item, action.DefIndex),
            _ => null,
        };
        if (command is not null)
        {
            simulation.Enqueue(command);
        }

        simulation.Step();
    }

    private void Left(GameSimulation simulation, BuildGroup group, string name, bool shift = false) =>
        Do(simulation, SidebarModel.LeftClick(Square(simulation, group, name), shift));

    private void Right(GameSimulation simulation, BuildGroup group, string name) =>
        Do(simulation, SidebarModel.RightClick(Square(simulation, group, name)));

    private static void Run(GameSimulation simulation, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void The_tabs_open_with_their_factories_and_close_without_them()
    {
        var simulation = Match();
        Assert.All(Enum.GetValues<BuildGroup>(), group => Assert.False(SidebarModel.TabOpen(simulation.World, Player1, group)));

        Build(simulation, "construction_yard", 2, 2);
        var barracks = Build(simulation, "barracks", 8, 2);
        Assert.True(SidebarModel.TabOpen(simulation.World, Player1, BuildGroup.Buildings));
        Assert.True(SidebarModel.TabOpen(simulation.World, Player1, BuildGroup.Defense));
        Assert.True(SidebarModel.TabOpen(simulation.World, Player1, BuildGroup.Infantry));
        Assert.False(SidebarModel.TabOpen(simulation.World, Player1, BuildGroup.Vehicles));

        simulation.Enqueue(new SellCommand(Player1, simulation.CurrentTick, _sequence++, barracks.Id));
        simulation.Step();
        Assert.False(SidebarModel.TabOpen(simulation.World, Player1, BuildGroup.Infantry));
    }

    [Fact]
    public void A_tab_lists_the_items_of_its_category_in_the_order_of_ra2_without_the_construction_yard()
    {
        var simulation = WithYard();

        Assert.Equal(["reactor", "refinery", "barracks", "war_factory", "radar", "tech_center"], Tab(simulation, BuildGroup.Buildings).Select(c => c.Name));
        Assert.Equal(["wall", "sentry_bunker", "flak_cannon", "tesla_coil"], Tab(simulation, BuildGroup.Defense).Select(c => c.Name));
        Assert.Equal(["conscript", "engineer", "rocketeer"], Tab(simulation, BuildGroup.Infantry).Select(c => c.Name));
        Assert.All(Tab(simulation, BuildGroup.Infantry), cameo => Assert.Equal(ProductionKind.Unit, cameo.Kind));
    }

    [Fact]
    public void An_unavailable_square_names_what_is_missing()
    {
        var simulation = WithYard();

        Assert.Equal(CameoState.Available, Square(simulation, BuildGroup.Buildings, "reactor").State);
        Assert.Equal(new Cameo(ProductionKind.Building, TestRules.Standard.Database.IndexOfBuilding("barracks"), "barracks", 100,
            CameoState.Unavailable, 0f, 0, "reactor") { Role = CameoRole.Production, Code = "BAR" }, Square(simulation, BuildGroup.Buildings, "barracks"));
        Assert.Equal("war_factory, radar", Square(simulation, BuildGroup.Buildings, "tech_center").Missing);
        Assert.Equal(CameoAction.None, SidebarModel.LeftClick(Square(simulation, BuildGroup.Buildings, "barracks"), false));
    }

    [Fact]
    public void A_building_goes_from_the_clock_to_on_hold_and_back_to_ready()
    {
        var simulation = WithYard();
        Build(simulation, "reactor", 6, 2);
        simulation.Step();
        Left(simulation, BuildGroup.Buildings, "barracks");
        Run(simulation, 4);

        var building = Square(simulation, BuildGroup.Buildings, "barracks");
        Assert.Equal(CameoState.Building, building.State);
        Assert.InRange(building.Progress, 0.2f, 0.5f);
        Assert.Equal(1, building.Count);
        // The queue of buildings takes one: the others wait, a second click on it does nothing.
        Assert.Equal(CameoState.Busy, Square(simulation, BuildGroup.Buildings, "reactor").State);
        Assert.Equal(CameoState.Available, Square(simulation, BuildGroup.Defense, "wall").State);
        Assert.Equal(CameoAction.None, SidebarModel.LeftClick(building, false));

        Right(simulation, BuildGroup.Buildings, "barracks");
        var held = Square(simulation, BuildGroup.Buildings, "barracks");
        Assert.Equal(CameoState.OnHold, held.State);
        Run(simulation, 5);
        Assert.Equal(held.Progress, Square(simulation, BuildGroup.Buildings, "barracks").Progress);

        Left(simulation, BuildGroup.Buildings, "barracks");
        Run(simulation, 20);
        var ready = Square(simulation, BuildGroup.Buildings, "barracks");
        Assert.Equal(CameoState.Ready, ready.State);
        Assert.Equal(1f, ready.Progress);
        Assert.Equal(new CameoAction(CameoActionKind.Place, ProductionKind.Building, ready.DefIndex), SidebarModel.LeftClick(ready, false));
    }

    [Fact]
    public void A_right_click_on_hold_or_on_ready_cancels_with_the_credits_back()
    {
        var simulation = WithYard();
        Left(simulation, BuildGroup.Buildings, "reactor");
        Run(simulation, 4);
        Right(simulation, BuildGroup.Buildings, "reactor");
        Right(simulation, BuildGroup.Buildings, "reactor");

        Assert.Equal(CameoState.Available, Square(simulation, BuildGroup.Buildings, "reactor").State);
        Assert.Equal(10_000, simulation.World.Player(Player1).Credits);

        Left(simulation, BuildGroup.Buildings, "reactor");
        Run(simulation, 20);
        Right(simulation, BuildGroup.Buildings, "reactor");
        Assert.Equal(CameoState.Available, Square(simulation, BuildGroup.Buildings, "reactor").State);
        Assert.Equal(10_000, simulation.World.Player(Player1).Credits);
    }

    [Fact]
    public void Without_credits_the_square_waits_for_them()
    {
        var simulation = WithYard(credits: 0);
        Left(simulation, BuildGroup.Buildings, "reactor");
        simulation.Step();

        var waiting = Square(simulation, BuildGroup.Buildings, "reactor");
        Assert.Equal(CameoState.WaitingForFunds, waiting.State);
        Assert.Equal(CameoActionKind.Pause, SidebarModel.RightClick(waiting).Kind);
    }

    [Fact]
    public void Units_queue_one_or_five_with_shift_and_count_on_their_square()
    {
        var simulation = WithYard();
        Build(simulation, "reactor", 6, 2);
        Build(simulation, "barracks", 9, 2);
        simulation.Step();

        Left(simulation, BuildGroup.Infantry, "conscript", shift: true);
        Assert.Equal(5, Square(simulation, BuildGroup.Infantry, "conscript").Count);
        Left(simulation, BuildGroup.Infantry, "engineer");
        Left(simulation, BuildGroup.Infantry, "engineer");

        var engineer = Square(simulation, BuildGroup.Infantry, "engineer");
        Assert.Equal((CameoState.Building, 2, 0f), (engineer.State, engineer.Count, engineer.Progress));
        Assert.Equal(CameoState.Available, Square(simulation, BuildGroup.Infantry, "rocketeer").State);

        // Shift orders five units, one building.
        Assert.Equal(ProductionRules.MaxOrder, SidebarModel.LeftClick(Square(simulation, BuildGroup.Infantry, "rocketeer"), true).Count);
        Assert.Equal(1, SidebarModel.LeftClick(Square(simulation, BuildGroup.Defense, "wall"), true).Count);
    }

    [Fact]
    public void A_full_queue_of_units_greys_out_the_others()
    {
        var simulation = WithYard();
        Build(simulation, "reactor", 6, 2);
        Build(simulation, "barracks", 9, 2);
        simulation.Step();
        for (var i = 0; i < 3; i++)
        {
            Left(simulation, BuildGroup.Infantry, "conscript", shift: true);
        }

        Assert.Equal(ProductionRules.MaxUnitQueue, Square(simulation, BuildGroup.Infantry, "conscript").Count);
        Assert.Equal(CameoState.Busy, Square(simulation, BuildGroup.Infantry, "engineer").State);
    }

    [Fact]
    public void The_sidebar_cannot_order_a_construction_yard()
    {
        var simulation = WithYard();
        var yard = TestRules.Standard.Database.IndexOfBuilding("construction_yard");

        Do(simulation, new CameoAction(CameoActionKind.Produce, ProductionKind.Building, yard));

        Assert.Empty(simulation.World.Player(Player1).Queue(ProductionCategory.Building));
    }
}
