using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The weapons of the aircraft (stage 3f of filling the game): the airship's bombs and its fall, the strike jets' single volley and the
/// flight back to the air force command to rearm, the helicopter's soldiers and gun. By the game data, without the fog of war; two people
/// play, no bot.
/// </summary>
public sealed class AircraftWeaponsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "sky",
                    37,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Allied).ExploreAll();
        simulation.World.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void The_airship_bombs_what_is_under_it_and_falls_on_it_when_brought_down()
    {
        var simulation = Match();
        var world = simulation.World;
        var airship = Spawn(world, Union, UnitType.KirovAirship, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 33, 30);
        Run(simulation, 300, () => mcv.Hp < mcv.MaxHp);
        Assert.True(mcv.Hp < mcv.MaxHp, "the bombs fell");

        var below = Spawn(world, Allied, UnitType.AllianceMcv, 20, 40);
        var falling = Spawn(world, Union, UnitType.KirovAirship, 20, 40);
        world.SetHp(falling, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(falling.Id, below.Id, Allied, cannon, falling.Position, falling.Id));
        Run(simulation, 4);

        Assert.False(falling.IsAlive);
        Assert.True(below.Hp < below.MaxHp, "it fell on what was under it");
        Assert.True(airship.IsAlive);
    }

    [Fact]
    public void The_strike_jet_fires_one_volley_when_ordered_flies_back_and_rearms()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 6, 10);
        var command = Build(world, Allied, "alliance_air_force_command", 10, 10);
        var jet = Spawn(world, Allied, UnitType.AllianceHarrier, 11, 11);
        var target = Spawn(world, Union, UnitType.Mcv, 30, 30);
        var bystander = Spawn(world, Union, UnitType.Mcv, 14, 11);
        Run(simulation, 60);
        Assert.Equal(bystander.MaxHp, bystander.Hp);
        Assert.Equal(1, jet.Ammo);

        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [jet.Id], target.Id));
        var events = Run(simulation, 300, () => jet.Ammo == 0 && jet.Weapons[0].BurstLeft == 0);
        Assert.Equal(0, jet.Ammo);
        Assert.Equal(2, events.OfType<WeaponFired>().Count(fired => fired.Shooter == jet.Id));

        // The way back, then 270 ticks of loading.
        Run(simulation, 600, () => jet.Ammo == 1);
        Assert.Equal(1, jet.Ammo);
        Assert.True(System.Math.Abs(jet.Position.ToCell().X - command.Cell.X) <= 3 && System.Math.Abs(jet.Position.ToCell().Y - command.Cell.Y) <= 3);
        Assert.True(target.Hp < target.MaxHp);
    }

    [Fact]
    public void The_helicopter_takes_five_soldiers_no_vehicle_and_fires_only_when_ordered()
    {
        var simulation = Match();
        var world = simulation.World;
        var helicopter = Spawn(world, Allied, UnitType.AllianceNighthawk, 30, 30);
        var soldiers = Enumerable.Range(0, 6).Select(i => Spawn(world, Allied, UnitType.AllianceRifleman, 26 + i % 3, 33 + i / 3)).ToList();
        // A vehicle, unarmed so that only the helicopter can fire below.
        var vehicle = Spawn(world, Allied, UnitType.AllianceMcv, 20, 20);
        Assert.False(TransportSystem.CanBoard(world, vehicle, helicopter));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [.. soldiers.Select(s => s.Id)], helicopter.Id));
        Run(simulation, 300);
        Assert.Equal(5, helicopter.Passengers.Count);

        // Out of reach of the soldier left outside, in reach of the helicopter.
        var mcv = Spawn(world, Union, UnitType.Mcv, 35, 27);
        Run(simulation, 60);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [helicopter.Id], mcv.Id));
        Run(simulation, 120, () => mcv.Hp < mcv.MaxHp);
        Assert.True(mcv.Hp < mcv.MaxHp);
    }
}
