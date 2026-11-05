using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The aimed shot of the player inside a unit (the owner, 2026-11-04): a ray from the unit's eyes, the first unit, building or ground it
/// meets takes it, a soldier's head kills. By the game data on flat ground; two people play, no bot. The Rifleman shoots a cell away,
/// where its spread of a degree and a half strays a few leptons only.
/// </summary>
public sealed class AimedShotTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);
    private static uint _sequence;

    private static GameSimulation Match(MapGrid? map = null) =>
        new(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "aim",
                    71,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map ?? new MapGrid(64, 64)),
            GameDataFiles.Rules);

    // Flat clear ground with the map objects of the data at their cells.
    private static MapGrid WithObjects(params (string Id, CellPos Cell)[] objects)
    {
        var types = GameDataFiles.Rules.MapTypes;
        var land = new byte[64 * 64];
        System.Array.Fill(land, (byte)LandType.Clear);
        var states = objects.Select(o =>
        {
            Assert.True(types.TryGetObject(o.Id, out var type));
            return MapObjectState.New(type, o.Cell, 0);
        }).ToList();
        return MapGrid.FromLayers(64, 64, land, new byte[land.Length], default, states, [], [], types: types);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    private static void Give(GameSimulation simulation, Command command) => simulation.Enqueue(command);

    private static TakeControlCommand Control(GameSimulation simulation, Unit unit, bool on) =>
        new(unit.Owner, simulation.CurrentTick, _sequence++, unit.Id, on);

    // A shot east, down (or up) to the height (leptons over the ground) of a point the distance away, from eyes as high over the
    // ground as the unit flies.
    private static AimedShotCommand Shot(GameSimulation simulation, Unit unit, int height, int distance, int turn = 0) =>
        new(unit.Owner, simulation.CurrentTick, _sequence++, unit.Id, new BAngle((ushort)(BAngle.East.Value + turn)),
            (short)System.Math.Round(System.Math.Atan2(height - AimedShot.EyeHeight - Altitude.Target(unit), distance) * BAngle.FullTurn / (2 * System.Math.PI)));

    private static (GameSimulation Simulation, Unit Shooter) Shooter(MapGrid? map = null, UnitType type = UnitType.AllianceRifleman, PlayerId? owner = null)
    {
        var simulation = Match(map);
        var shooter = simulation.World.SpawnUnit(owner ?? Allied, type, new CellPos(30, 30).Center);
        Give(simulation, Control(simulation, shooter, true));
        Run(simulation, 2);
        Assert.True(shooter.Manual);
        return (simulation, shooter);
    }

    [Fact]
    public void A_shot_in_the_body_hurts_one_in_the_head_kills()
    {
        var (simulation, shooter) = Shooter();
        var world = simulation.World;
        var body = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(31, 30).Center);

        Give(simulation, Shot(simulation, shooter, 60, 256));
        var events = Run(simulation, 2);
        Assert.True(body.IsAlive);
        Assert.InRange(body.Hp, 1, body.MaxHp - 1);
        Assert.Contains(events, e => e is AimedHit { Headshot: false } hit && hit.Target == body.Id);

        // Loaded again, in the head.
        Run(simulation, 40);
        Give(simulation, Shot(simulation, shooter, AimedShot.SoldierHeight - (AimedShot.HeadHeight / 2), 256));
        events = Run(simulation, 2);
        Assert.False(body.IsAlive);
        Assert.Contains(events, e => e is AimedHit { Headshot: true } hit && hit.Target == body.Id);
    }

    [Fact]
    public void A_shot_beside_the_target_hurts_nobody_and_lands_on_the_ground()
    {
        var (simulation, shooter) = Shooter();
        var aside = simulation.World.SpawnUnit(Union, UnitType.Conscript, new CellPos(31, 30).Center);

        // A twelfth of a turn off to the side, down at the ground two cells away.
        Give(simulation, Shot(simulation, shooter, 0, 512, BAngle.FullTurn / 12));
        var events = Run(simulation, 2);
        Assert.Equal(aside.MaxHp, aside.Hp);
        Assert.Contains(events, e => e is AimedHit { Target: null });
        Assert.Contains(events, e => e is ProjectileImpact impact && impact.Shooter == shooter.Id);
    }

    [Fact]
    public void The_first_in_the_line_of_fire_takes_the_shot_be_it_the_shooters_own()
    {
        var (simulation, shooter) = Shooter();
        var world = simulation.World;
        var own = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(31, 30).Center);
        var enemy = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(32, 30).Center);

        // Neither fires by itself: only the aimed shot is measured.
        own.Weapons[0].ReloadLeft = 10_000;
        enemy.Weapons[0].ReloadLeft = 10_000;
        Give(simulation, Shot(simulation, shooter, 60, 512));
        var events = Run(simulation, 2);
        Assert.True(own.Hp < own.MaxHp);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.Contains(events, e => e is AimedHit hit && hit.Target == own.Id);
    }

    [Fact]
    public void A_building_in_the_way_takes_the_shot()
    {
        var (simulation, shooter) = Shooter();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("alliance_power_plant", out var plant);
        var wall = world.CreateBuilding(Union, plant, new CellPos(31, 29), ready: true, null);
        var behind = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(34, 30).Center);

        Give(simulation, Shot(simulation, shooter, 60, 1024));
        var events = Run(simulation, 2);
        Assert.Equal(behind.MaxHp, behind.Hp);
        Assert.Contains(events, e => e is AimedHit hit && hit.Target == wall.Id);
    }

    [Fact]
    public void A_second_shot_waits_for_the_reload()
    {
        var (simulation, shooter) = Shooter();
        Give(simulation, Shot(simulation, shooter, 0, 512));
        Give(simulation, Shot(simulation, shooter, 0, 512));
        var events = Run(simulation, 1);
        Give(simulation, Shot(simulation, shooter, 0, 512));
        events.AddRange(Run(simulation, 1));
        Assert.Single(events, e => e is AimedHit);
    }

    [Fact]
    public void Only_a_unit_under_the_hand_fires_aimed_shots_and_it_fires_no_others()
    {
        var simulation = Match();
        var world = simulation.World;
        var shooter = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(30, 30).Center);
        var enemy = world.SpawnUnit(Union, UnitType.Conscript, new CellPos(32, 30).Center);
        enemy.Stance = Stance.HoldPosition;
        enemy.Weapons[0].ReloadLeft = 10_000;

        // Not under the hand: the aimed shot is refused.
        Give(simulation, Shot(simulation, shooter, 60, 512));
        var events = Run(simulation, 1);
        Assert.DoesNotContain(events, e => e is AimedHit);

        // Under the hand: it does not fire at the enemy in its reach by itself.
        Give(simulation, Control(simulation, shooter, true));
        Run(simulation, 1);
        var hp = enemy.Hp;
        events = Run(simulation, 60);
        Assert.DoesNotContain(events, e => e is WeaponFired fired && fired.Shooter == shooter.Id);
        Assert.Equal(hp, enemy.Hp);

        // Let go: it does again.
        Give(simulation, Control(simulation, shooter, false));
        events = Run(simulation, 60);
        Assert.Contains(events, e => e is WeaponFired fired && fired.Shooter == shooter.Id);
    }

    [Fact]
    public void The_same_shots_on_the_same_seed_end_the_same()
    {
        static (WorldPos Point, int Hp) Play()
        {
            var (simulation, shooter) = Shooter();
            var target = simulation.World.SpawnUnit(Union, UnitType.Conscript, new CellPos(33, 30).Center);
            var points = new List<WorldPos>();
            for (var i = 0; i < 3; i++)
            {
                Give(simulation, Shot(simulation, shooter, 60, 768));
                points.AddRange(Run(simulation, 30).OfType<AimedHit>().Select(hit => hit.Point));
            }

            return (points[^1], target.Hp);
        }

        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void A_stone_wall_in_the_way_takes_the_shot_and_loses_hit_points()
    {
        var (simulation, shooter) = Shooter(WithObjects(("wall_stone", new CellPos(31, 30))));
        var enemy = simulation.World.SpawnUnit(Union, UnitType.Conscript, new CellPos(33, 30).Center);
        enemy.Weapons[0].ReloadLeft = 10_000;
        var wall = simulation.World.Map.ObjectsAt(new CellPos(31, 30))[0];

        Give(simulation, Shot(simulation, shooter, 60, 768));
        var events = Run(simulation, 2);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.True(wall.Hp < wall.Type.MaxHp);
        Assert.Contains(events, e => e is AimedHit { Cover: true, Target: null });
    }

    [Fact]
    public void A_low_fence_stops_a_shot_at_the_legs_and_not_one_at_the_head()
    {
        var fence = new CellPos(31, 30);
        var (simulation, shooter) = Shooter(WithObjects(("fence_wood", fence)));
        var enemy = simulation.World.SpawnUnit(Union, UnitType.Conscript, new CellPos(32, 30).Center);
        enemy.Weapons[0].ReloadLeft = 10_000;

        Give(simulation, Shot(simulation, shooter, 10, 512));
        var events = Run(simulation, 2);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
        Assert.Contains(events, e => e is AimedHit { Cover: true });
        Assert.True(simulation.World.Map.ObjectsAt(fence)[0].Hp < simulation.World.Map.ObjectsAt(fence)[0].Type.MaxHp);

        Run(simulation, 40);
        Give(simulation, Shot(simulation, shooter, AimedShot.SoldierHeight - (AimedShot.HeadHeight / 2), 512));
        events = Run(simulation, 2);
        Assert.False(enemy.IsAlive);
        Assert.Contains(events, e => e is AimedHit { Headshot: true });
    }

    [Fact]
    public void The_sandbags_of_one_dug_in_stop_a_shot_low_and_its_head_kneels_lower()
    {
        var (simulation, shooter) = Shooter();
        var dug = simulation.World.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(31, 30).Center);
        Give(simulation, new DeployCommand(Allied, simulation.CurrentTick, _sequence++, dug.Id));
        Run(simulation, 2);
        Assert.True(dug.Deployed);

        Give(simulation, Shot(simulation, shooter, 20, 256));
        var events = Run(simulation, 2);
        Assert.Equal(dug.MaxHp, dug.Hp);
        Assert.Contains(events, e => e is AimedHit { Cover: true, Target: null });

        Run(simulation, 40);
        Give(simulation, Shot(simulation, shooter, AimedShot.KneelingHeight - (AimedShot.HeadHeight / 2), 256));
        events = Run(simulation, 2);
        Assert.False(dug.IsAlive);
        Assert.Contains(events, e => e is AimedHit { Headshot: true });
    }

    [Fact]
    public void A_bullet_for_the_ground_goes_through_a_plane_one_for_the_air_hits_it()
    {
        var (simulation, shooter) = Shooter();
        var plane = simulation.World.SpawnUnit(Union, UnitType.AllianceHarrier, new CellPos(31, 30).Center);
        Assert.Equal(Altitude.Aircraft, Altitude.Target(plane));

        Give(simulation, Shot(simulation, shooter, Altitude.Aircraft, 256));
        var events = Run(simulation, 1);
        Assert.Equal(plane.MaxHp, plane.Hp);
        Assert.Contains(events, e => e is AimedHit { Target: null });

        // The Flak Track's gun for the air (its second weapon) hits it; its cannon is no instant one.
        var (flak, track) = Shooter(type: UnitType.SupportVehicle, owner: Allied);
        var high = flak.World.SpawnUnit(Union, UnitType.AllianceHarrier, new CellPos(31, 30).Center);
        Give(flak, Shot(flak, track, Altitude.Aircraft, 256));
        events = Run(flak, 2);
        Assert.Contains(events, e => e is AimedHit hit && hit.Target == high.Id);
        Assert.True(high.Hp < high.MaxHp);
    }

    [Fact]
    public void One_with_a_jet_pack_fires_down_from_its_height()
    {
        var (simulation, flier) = Shooter(type: UnitType.AllianceRocketeer);
        Assert.Equal(Altitude.Jumpjet, Altitude.Target(flier));
        var below = simulation.World.SpawnUnit(Union, UnitType.Conscript, new CellPos(31, 30).Center);
        below.Weapons[0].ReloadLeft = 10_000;

        Give(simulation, Shot(simulation, flier, 60, 256));
        var events = Run(simulation, 2);
        Assert.Contains(events, e => e is AimedHit hit && hit.Target == below.Id);
        Assert.True(below.Hp < below.MaxHp);
    }

    [Fact]
    public void The_heights_are_those_the_client_drew_before()
    {
        var rules = GameDataFiles.Rules;
        Assert.Equal(1.5f, Altitude.Of(rules.Unit(UnitType.AllianceHarrier)) / 256f);
        Assert.Equal(2f, Altitude.Of(rules.Unit(UnitType.KirovAirship)) / 256f);
        Assert.Equal(1f, Altitude.Of(rules.Unit(UnitType.AllianceRocketeer)) / 256f);
        Assert.Equal(0, Altitude.Of(rules.Unit(UnitType.Conscript)));
    }
}
