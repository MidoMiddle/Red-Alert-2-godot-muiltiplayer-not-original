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

    private static GameSimulation Match() =>
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
                new MapGrid(64, 64)),
            GameDataFiles.Rules);

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
        new(Allied, simulation.CurrentTick, _sequence++, unit.Id, on);

    // A shot east, down to the height (leptons over the ground) of a point the distance away.
    private static AimedShotCommand Shot(GameSimulation simulation, Unit unit, int height, int distance, int turn = 0) =>
        new(Allied, simulation.CurrentTick, _sequence++, unit.Id, new BAngle((ushort)(BAngle.East.Value + turn)),
            (short)System.Math.Round(System.Math.Atan2(height - AimedShot.EyeHeight, distance) * BAngle.FullTurn / (2 * System.Math.PI)));

    private static (GameSimulation Simulation, Unit Shooter) Shooter()
    {
        var simulation = Match();
        var shooter = simulation.World.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(30, 30).Center);
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
}
