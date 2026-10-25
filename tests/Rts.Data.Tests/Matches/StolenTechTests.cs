using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The stolen technology of RA2 (docs/ra2_units/allies/chrono_commando, psi_commando; soviets/chrono_ivan, yuri_prime): a spy in a
/// battle lab gives its player the technology of the lab's faction, and the units that need it open at the player's barracks — the
/// Allies' lab the Chrono Commando and the Chrono Ivan, the Soviets' the Psi Commando and Yuri Prime. Then what each of them does. By the
/// game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class StolenTechTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "stolen",
                    61,
                    new MatchOptions(StartingCredits: 20_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    private static bool CanBuild(GameWorld world, PlayerId player, string id)
    {
        TechTree.Update(world);
        return world.Player(player).CanBuildUnit(world.Rules.Database.IndexOfUnit(id));
    }

    /// <summary>The player's spy goes into the lab of the other: true once it is in.</summary>
    private static void Infiltrate(GameSimulation simulation, PlayerId spyOwner, Building lab)
    {
        var spy = simulation.World.SpawnUnit(spyOwner, UnitType.AllianceSpy, new CellPos(lab.Cell.X, lab.FarCell.Y + 2).Center, snapToSubcell: true);
        simulation.Enqueue(new EnterCommand(spyOwner, simulation.CurrentTick, 0, [spy.Id], lab.Id));
        Run(simulation, 300, () => !spy.IsAlive);
        Assert.False(spy.IsAlive);
    }

    [Fact]
    public void A_spy_in_the_soviet_lab_opens_the_psi_commando_and_in_the_allied_lab_the_chrono_ivan()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_barracks", 50, 50);
        Build(world, Union, "barracks", 10, 10);
        var sovietLab = Build(world, Union, "tech_center", 20, 10);
        var alliedLab = Build(world, Allied, "alliance_battle_lab", 40, 40);
        Assert.False(CanBuild(world, Allied, "alliance_psi_commando"));
        Assert.False(CanBuild(world, Union, "chrono_ivan"));

        Infiltrate(simulation, Allied, sovietLab);
        Assert.Contains("union", world.Player(Allied).StolenTech);
        Assert.True(CanBuild(world, Allied, "alliance_psi_commando"));
        Assert.False(CanBuild(world, Allied, "alliance_chrono_commando"));

        // RA2: only the Allies train spies; one the Soviets hold (by mind control) steals all the same.
        Infiltrate(simulation, Union, alliedLab);
        Assert.True(CanBuild(world, Union, "chrono_ivan"));
        Assert.False(CanBuild(world, Union, "yuri_prime"));
    }

    [Fact]
    public void The_chrono_commando_brings_a_building_down_at_once_and_jumps()
    {
        var simulation = Match();
        var world = simulation.World;
        var commando = world.SpawnUnit(Allied, UnitType.AllianceChronoCommando, new CellPos(20, 24).Center, snapToSubcell: true);
        var reactor = Build(world, Union, "reactor", 20, 20);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [commando.Id], reactor.Id));
        Run(simulation, 200, () => !reactor.IsAlive);

        Assert.False(reactor.IsAlive);
        Assert.True(commando.Archetype.Has(UnitTalents.Teleports));
        Assert.True(world.Rules.Unit(UnitType.ChronoIvan).Has(UnitTalents.Teleports));
    }

    [Fact]
    public void Yuri_prime_is_one_per_player_takes_what_he_is_told_from_afar_and_nothing_by_himself()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Player(Union).StolenTech.Add("union");
        Build(world, Union, "reactor", 4, 4);
        Build(world, Union, "barracks", 10, 4);
        var prime = world.SpawnUnit(Union, UnitType.YuriPrime, new CellPos(10, 30).Center, snapToSubcell: true);
        Assert.True(CanBuild(world, Union, "yuri_prime"));

        var order = new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, world.Rules.Database.IndexOfUnit("yuri_prime"), 1);
        simulation.Enqueue(order);
        Run(simulation, 2);
        Assert.Contains(order, simulation.RefusedCommands);

        // 20 cells off: by himself he takes nothing (RA2 CanPassiveAquire=no); ordered, he does.
        var mcv = world.SpawnUnit(Allied, UnitType.AllianceMcv, new CellPos(30, 30).Center);
        Run(simulation, 100);
        Assert.Equal(Allied, mcv.Owner);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 1, [prime.Id], mcv.Id));
        Run(simulation, 300, () => mcv.Owner == Union);
        Assert.Equal(Union, mcv.Owner);
    }

    [Fact]
    public void The_psi_commando_takes_a_vehicle()
    {
        var simulation = Match();
        var world = simulation.World;
        var commando = world.SpawnUnit(Allied, UnitType.AlliancePsiCommando, new CellPos(20, 20).Center, snapToSubcell: true);
        var mcv = world.SpawnUnit(Union, UnitType.Mcv, new CellPos(24, 20).Center);
        Run(simulation, 200, () => mcv.Owner == Allied);

        Assert.Equal(Allied, mcv.Owner);
        Assert.Equal(commando.Id, mcv.ControlledBy);
    }
}
