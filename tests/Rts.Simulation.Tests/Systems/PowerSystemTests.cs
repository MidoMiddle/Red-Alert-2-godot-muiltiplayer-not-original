using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// The power of the players (docs/implementation/M3_base_economy.md, 4.11): the output and the drain of the ready buildings, the deficit
/// and its events, worked out only when the buildings change.
/// </summary>
public sealed class PowerSystemTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld NewWorld() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    private static Building Build(GameWorld world, string id, int x, int y, bool ready = true, PlayerId? owner = null) =>
        world.CreateBuilding(owner ?? Player1, TestRules.BuildingDef(id), new CellPos(x, y), ready, null);

    private static List<SimEvent> Update(GameWorld world)
    {
        var events = new List<SimEvent>();
        PowerSystem.Update(world, events);
        return events;
    }

    [Fact]
    public void Output_and_drain_are_the_sums_of_the_ready_buildings()
    {
        var world = NewWorld();
        Build(world, "reactor", 0, 0);
        Build(world, "barracks", 3, 0);
        Build(world, "refinery", 6, 0);

        Assert.Empty(Update(world));

        var player = world.Player(Player1);
        Assert.Equal((150, 60, false), (player.PowerOutput, player.PowerDrain, player.LowPower));
        Assert.Equal(3, player.Buildings.Count);
    }

    [Fact]
    public void Draining_more_than_the_output_is_low_power_reported_once()
    {
        var world = NewWorld();
        Build(world, "reactor", 0, 0);
        Build(world, "refinery", 3, 0);
        Update(world);
        Build(world, "war_factory", 7, 0);
        Build(world, "radar", 12, 0);

        var events = Update(world);

        Assert.Equal((150, 125, false), (world.Player(Player1).PowerOutput, world.Player(Player1).PowerDrain, world.Player(Player1).LowPower));
        Assert.Empty(events);

        Build(world, "tech_center", 14, 0);
        events = Update(world);

        Assert.True(world.Player(Player1).LowPower);
        Assert.Equal([new LowPower(0, Player1)], events);

        Build(world, "tesla_coil", 18, 0);
        Assert.Empty(Update(world));
    }

    [Fact]
    public void Losing_a_consumer_restores_the_power()
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { MapWidthCells = 40, MapHeightCells = 40 }, TestRules.Standard);
        var world = simulation.World;
        Build(world, "reactor", 0, 0);
        Build(world, "war_factory", 3, 0);
        Build(world, "radar", 8, 0);
        Build(world, "barracks", 10, 0);
        Build(world, "refinery", 13, 0);
        Build(world, "radar", 17, 0);
        simulation.Step();
        Assert.Contains(new LowPower(0, Player1), simulation.LastTickEvents);

        // The radar at (8, 0) falls: 185 - 50 = 135 against 150.
        simulation.Enqueue(new DamageMapObjectCommand(Player1, 1, 0, new CellPos(8, 0), DamageMapObjectCommand.MaxAmount));
        simulation.Step();

        Assert.False(world.Player(Player1).LowPower);
        Assert.Contains(new PowerRestored(1, Player1), simulation.LastTickEvents);
        Assert.Equal((150, 135), (world.Player(Player1).PowerOutput, world.Player(Player1).PowerDrain));
    }

    [Fact]
    public void A_growing_building_gives_and_drains_nothing_until_it_is_ready()
    {
        var world = NewWorld();
        var reactor = Build(world, "reactor", 0, 0, ready: false);
        Build(world, "barracks", 3, 0, ready: false);
        Update(world);

        Assert.Equal((0, 0), (world.Player(Player1).PowerOutput, world.Player(Player1).PowerDrain));
        Assert.Equal(2, world.Player(Player1).Buildings.Count);

        world.CompleteBuildUp(reactor);
        Update(world);

        Assert.Equal((150, 0), (world.Player(Player1).PowerOutput, world.Player(Player1).PowerDrain));
    }

    [Fact]
    public void Buildings_of_other_players_and_of_the_map_do_not_count()
    {
        var world = new GameWorld(seed: 1, TestMapParser.Parse(string.Join("\n", Enumerable.Repeat(string.Join(" ", Enumerable.Repeat(".", 20)), 10)) +
            "\nbuilding neutral_house_a 15 5 1").Grid, TestRules.Standard);
        Build(world, "reactor", 0, 0, owner: new PlayerId(2));

        Update(world);

        Assert.Equal((0, 0), (world.Player(Player1).PowerOutput, world.Player(Player1).PowerDrain));
        Assert.Empty(world.Player(Player1).Buildings);
        Assert.Equal(150, world.Player(new PlayerId(2)).PowerOutput);
    }

    [Fact]
    public void Power_is_worked_out_only_when_the_buildings_change()
    {
        var world = NewWorld();
        Build(world, "reactor", 0, 0);
        Update(world);
        world.Player(Player1).PowerOutput = 1;

        Update(world);

        Assert.Equal(1, world.Player(Player1).PowerOutput);
    }
}
