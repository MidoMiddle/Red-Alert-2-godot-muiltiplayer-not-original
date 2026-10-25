using Rts.Simulation;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Yuri of RA2 (docs/ra2_units/soviets/yuri): his mind control takes one enemy unit at a time for his owner, lets the first go when he
/// takes a second, and leaves none taken when he dies; dogs, harvesters, fliers and buildings are not taken. Deployed, his psychic wave
/// kills the soldiers round him, his owner's too, but not his owner's Yuris nor vehicles, and he stands up again. By the game data,
/// without the fog of war; two people play, no bot.
/// </summary>
public sealed class YuriTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match(bool explored = true)
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "yuri",
                    51,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        if (explored)
        {
            simulation.World.VisionOf(Union).ExploreAll();
            simulation.World.VisionOf(Allied).ExploreAll();
        }

        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

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
    public void He_takes_an_enemy_vehicle_for_his_owner()
    {
        var simulation = Match();
        var world = simulation.World;
        var yuri = Spawn(world, Union, UnitType.Yuri, 20, 20);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 25, 20);

        var events = Run(simulation, 120, () => mcv.Owner == Union);

        Assert.Equal(Union, mcv.Owner);
        Assert.Equal(yuri.Id, mcv.ControlledBy);
        Assert.Equal(Allied, mcv.ControlledFrom);
        Assert.Equal(mcv.Id, yuri.Controlling);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
        Assert.Contains(events, evt => evt is UnitControlled { Unit: var unit } && unit == mcv.Id);
    }

    [Fact]
    public void Ordered_onto_a_second_unit_he_lets_the_first_go()
    {
        var simulation = Match();
        var world = simulation.World;
        var yuri = Spawn(world, Union, UnitType.Yuri, 20, 20);
        var first = Spawn(world, Allied, UnitType.AllianceMcv, 25, 20);
        var second = Spawn(world, Allied, UnitType.AllianceMcv, 20, 25);
        Run(simulation, 120, () => yuri.Controlling is not null);
        var taken = yuri.Controlling == first.Id ? first : second;
        var other = taken == first ? second : first;

        // Holding one, he looks for no other by himself.
        Run(simulation, 300);
        Assert.Equal(Allied, other.Owner);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [yuri.Id], other.Id));
        Run(simulation, 300, () => other.Owner == Union);

        Assert.Equal(Union, other.Owner);
        Assert.Equal(Allied, taken.Owner);
        Assert.Null(taken.ControlledBy);
        Assert.Equal(other.Id, yuri.Controlling);
    }

    [Fact]
    public void When_he_dies_the_unit_goes_back()
    {
        var simulation = Match();
        var world = simulation.World;
        var yuri = Spawn(world, Union, UnitType.Yuri, 20, 20);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 25, 20);
        Run(simulation, 120, () => mcv.Owner == Union);
        Assert.Equal(Union, mcv.Owner);

        world.SetHp(yuri, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Hit(yuri.Id, yuri.Id, Allied, cannon, yuri.Position, yuri.Id));
        var events = Run(simulation, 2);

        Assert.False(yuri.IsAlive);
        Assert.Equal(Allied, mcv.Owner);
        Assert.Null(mcv.ControlledBy);
        Assert.Contains(events, evt => evt is UnitReleased { Unit: var unit } && unit == mcv.Id);
    }

    [Fact]
    public void Dogs_harvesters_fliers_buildings_and_held_units_are_not_taken()
    {
        var simulation = Match();
        var world = simulation.World;
        var yuri = Spawn(world, Union, UnitType.Yuri, 20, 20);
        var other = Spawn(world, Union, UnitType.Yuri, 10, 10);
        var dog = Spawn(world, Allied, UnitType.AllianceAttackDog, 50, 50);
        var harvester = Spawn(world, Allied, UnitType.AllianceHarvester, 24, 20);
        var helicopter = Spawn(world, Allied, UnitType.AllianceNighthawk, 20, 24);
        world.Rules.Database.TryGetBuilding("alliance_power_plant", out var plant);
        var building = world.CreateBuilding(Allied, plant, new CellPos(15, 15), true, null);

        Assert.False(MindControlSystem.CanTake(world, yuri, dog, byItself: false));
        Assert.False(MindControlSystem.CanTake(world, yuri, harvester, byItself: false));
        Assert.False(MindControlSystem.CanTake(world, yuri, helicopter, byItself: false));
        Assert.False(MindControlSystem.CanTake(world, yuri, building, byItself: false));
        Assert.False(MindControlSystem.CanTake(world, yuri, other, byItself: false));

        Run(simulation, 300);
        Assert.Equal(Allied, harvester.Owner);
        Assert.Equal(Allied, helicopter.Owner);

        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 25, 20);
        Run(simulation, 300, () => mcv.Owner == Union);
        Assert.False(MindControlSystem.CanTake(world, other, mcv, byItself: false));
    }

    [Fact]
    public void His_wave_kills_the_soldiers_round_him_but_not_his_owner_s_yuris_nor_vehicles_and_he_stands_up_again()
    {
        var simulation = Match();
        var world = simulation.World;
        var yuri = Spawn(world, Union, UnitType.Yuri, 20, 20);
        var friend = Spawn(world, Union, UnitType.Yuri, 21, 21);
        var conscript = Spawn(world, Union, UnitType.Conscript, 19, 21);
        var rifleman = Spawn(world, Allied, UnitType.AllianceRifleman, 22, 20);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 20, 18);
        var far = Spawn(world, Allied, UnitType.AllianceRifleman, 20, 28);

        simulation.Enqueue(new DeployCommand(Union, simulation.CurrentTick, 0, yuri.Id));
        Run(simulation, 3);

        Assert.True(yuri.Deployed);
        Assert.False(conscript.IsAlive);
        Assert.False(rifleman.IsAlive);
        Assert.True(friend.IsAlive);
        Assert.Equal(friend.MaxHp, friend.Hp);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
        Assert.True(far.IsAlive);

        Run(simulation, RadiationSystem.UndeployDelay + 2);
        Assert.False(yuri.Deployed);
    }

    [Fact]
    public void A_match_with_a_mind_control_plays_back_from_its_replay()
    {
        // Nothing outside the commands: the map is not opened to the players.
        var simulation = Match(explored: false);
        var recorder = new ReplayRecorder(simulation);
        simulation.Step();
        recorder.AfterStep();
        simulation.Enqueue(new SpawnTestUnitCommand(Union, 1, 0, UnitType.Yuri, new CellPos(20, 20).Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Allied, 1, 0, UnitType.AllianceMcv, new CellPos(25, 20).Center));
        var taken = false;
        for (var tick = 1; tick < 400; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
            taken |= simulation.LastTickEvents.OfType<UnitControlled>().Any();
        }

        Assert.True(taken);
        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()));
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, GameDataFiles.Rules).Status);
    }
}
