using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Math;
using Rts.Simulation.Random;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// The crates (docs/implementation/M11_content.md, a6): where and when they come, by the seed alone; that a unit of a player on the
/// ground takes one; the weights of the kinds; and what each of the eight gives. A match of two on a clear map, without bots.
/// </summary>
public sealed class CrateSystemTests
{
    private static readonly PlayerId Person = new(1);
    private static readonly PlayerId Other = new(2);

    private static GameSimulation Match(ulong seed = 7, bool crates = true, MapGrid? map = null)
    {
        var setup = MatchSetupTests.OneOnOne(seed);
        setup = setup with { Options = setup.Options with { Crates = crates } };
        return new GameSimulation(SimulationSettings.ForMatch(setup, map ?? new MapGrid(64, 64)), TestRules.Standard);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks)
    {
        var events = new List<SimEvent>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    private static List<CellPos> Cells(GameWorld world) => world.CratesOnMap.Select(crate => crate.Cell).ToList();

    [Fact]
    public void The_map_starts_with_its_crates_where_the_seed_puts_them()
    {
        var a = Match(seed: 7);
        var b = Match(seed: 7);
        var c = Match(seed: 8);
        var events = Run(a, 1);
        Run(b, 1);
        Run(c, 1);

        Assert.Equal(CrateSystem.Most(a.World), a.World.CratesOnMap.Count);
        Assert.Equal(2, a.World.CratesOnMap.Count);
        Assert.Equal(2, events.OfType<CrateAppeared>().Count());
        Assert.Equal(Cells(a.World), Cells(b.World));
        Assert.NotEqual(Cells(a.World), Cells(c.World));
    }

    [Fact]
    public void Without_the_option_there_are_none()
    {
        var simulation = Match(crates: false);
        Run(simulation, 300);

        Assert.Empty(simulation.World.CratesOnMap);
    }

    [Fact]
    public void A_crate_lies_on_open_land_only()
    {
        // The west half is deep water, a band of ore runs across the east: crates come on the clear rest only.
        var land = new byte[64 * 64];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % 64 < 32 ? LandType.Deep : LandType.Clear);
        }

        var ore = new byte[64 * 64];
        for (var y = 20; y < 30; y++)
        {
            for (var x = 32; x < 64; x++)
            {
                ore[y * 64 + x] = new OreCell(OreKind.Ore, 5).Packed;
            }
        }

        for (ulong seed = 1; seed <= 30; seed++)
        {
            var simulation = Match(seed, map: MapGrid.FromLayers(64, 64, land, new byte[64 * 64], default, null, [], [], ore, types: TestRules.Standard.MapTypes));
            Run(simulation, 1);
            Assert.All(Cells(simulation.World), cell =>
            {
                Assert.Equal(LandType.Clear, simulation.World.Map.SurfaceAt(cell));
                Assert.True(simulation.World.Map.OreAt(cell).IsEmpty, $"ore at {cell.X}, {cell.Y}");
            });
        }
    }

    [Fact]
    public void A_crate_taken_is_replaced_after_the_regen_time()
    {
        var simulation = Match();
        var world = simulation.World;
        Run(simulation, 1);
        var crate = world.CratesOnMap[0];
        var unit = world.SpawnUnit(Person, UnitType.Conscript, crate.Cell.Center, snapToSubcell: true);

        var events = Run(simulation, 1);
        var picked = Assert.Single(events.OfType<CratePicked>());
        Assert.Equal((crate.Id, Person, unit.Id), (picked.Crate, picked.Player, picked.Unit));
        Assert.Single(world.CratesOnMap);

        Run(simulation, world.Rules.Crates.Regen - 2);
        Assert.Single(world.CratesOnMap);
        Run(simulation, 2);
        Assert.Equal(2, world.CratesOnMap.Count);
    }

    [Fact]
    public void A_neutral_unit_does_not_take_a_crate()
    {
        var simulation = Match();
        var world = simulation.World;
        Run(simulation, 1);
        world.SpawnUnit(PlayerId.Neutral, UnitType.Conscript, world.CratesOnMap[0].Cell.Center, snapToSubcell: true);

        Assert.Empty(Run(simulation, 5).OfType<CratePicked>());
        Assert.Equal(2, world.CratesOnMap.Count);
    }

    [Fact]
    public void The_kinds_fall_by_their_weights()
    {
        var crates = CratesDef.Default;
        var sum = crates.Weights.Sum();

        // Every draw maps onto a kind, each kind on as many draws as its weight.
        var counts = Enumerable.Range(0, sum).GroupBy(draw => CrateSystem.KindOf(crates, draw)).ToDictionary(g => g.Key, g => g.Count());
        for (var i = 0; i < crates.Weights.Length; i++)
        {
            Assert.Equal(crates.Weights[i], counts.GetValueOrDefault((CrateKind)(i + 1)));
        }

        // 10 000 draws of the simulation's generator come within two points of the weights (M11 §8).
        var random = new DeterministicRandom(99);
        var drawn = Enumerable.Range(0, 10_000).Select(_ => CrateSystem.KindOf(crates, random.NextInt(sum))).GroupBy(kind => kind)
            .ToDictionary(g => g.Key, g => g.Count());
        for (var i = 0; i < crates.Weights.Length; i++)
        {
            var share = drawn.GetValueOrDefault((CrateKind)(i + 1)) / 100.0;
            Assert.InRange(share, (crates.Weights[i] * 100.0 / sum) - 2, (crates.Weights[i] * 100.0 / sum) + 2);
        }
    }

    [Fact]
    public void Money_heal_and_reveal_go_to_the_player_of_the_picker()
    {
        var simulation = Match();
        var world = simulation.World;
        Run(simulation, 1);
        var crate = new Crate(99, new CellPos(30, 30));
        var picker = world.SpawnUnit(Person, UnitType.HeavyTank, crate.Cell.Center);
        var far = world.SpawnUnit(Person, UnitType.HeavyTank, new CellPos(5, 60).Center);
        var enemy = world.SpawnUnit(Other, UnitType.HeavyTank, new CellPos(31, 31).Center);
        foreach (var unit in new[] { picker, far, enemy })
        {
            world.SetHp(unit, 10, null);
        }

        var credits = world.Player(Person).Credits;
        Assert.Equal(CrateKind.Money, CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Money, null));
        Assert.Equal(credits + CratesDef.Default.Money, world.Player(Person).Credits);

        CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Heal, null);
        Assert.Equal((picker.MaxHp, far.MaxHp, 10), (picker.Hp, far.Hp, enemy.Hp));

        Assert.False(world.VisionOf(Person).IsExplored(new CellPos(60, 2)));
        CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Reveal, null);
        Assert.True(world.VisionOf(Person).IsExplored(new CellPos(60, 2)));
        Assert.False(world.VisionOf(Other).IsExplored(new CellPos(2, 60)));
    }

    [Fact]
    public void Veteran_raises_the_units_near_by_a_rank_and_without_any_to_raise_gives_money()
    {
        var simulation = Match();
        var world = simulation.World;
        var crate = new Crate(1, new CellPos(30, 30));
        var picker = world.SpawnUnit(Person, UnitType.HeavyTank, crate.Cell.Center);
        var near = world.SpawnUnit(Person, UnitType.HeavyTank, new CellPos(33, 30).Center);
        var far = world.SpawnUnit(Person, UnitType.HeavyTank, new CellPos(40, 30).Center);

        Assert.Equal(CrateKind.Veteran, CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Veteran, null));
        Assert.Equal((Rank.Veteran, Rank.Veteran, Rank.Rookie), (Veterancy.RankOf(picker), Veterancy.RankOf(near), Veterancy.RankOf(far)));
        CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Veteran, null);
        Assert.Equal(Rank.Elite, Veterancy.RankOf(picker));

        // All elite: nothing to raise, so the money.
        CrateSystem.Open(world, CratesDef.Default, crate, near, CrateKind.Veteran, null);
        var credits = world.Player(Person).Credits;
        Assert.Equal(CrateKind.Money, CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Veteran, null));
        Assert.Equal(credits + CratesDef.Default.Money, world.Player(Person).Credits);
    }

    [Fact]
    public void Armor_firepower_and_speed_stay_on_the_units_near_by_and_do_what_they_say()
    {
        var simulation = Match();
        var world = simulation.World;
        var crate = new Crate(1, new CellPos(30, 30));
        var picker = world.SpawnUnit(Person, UnitType.HeavyTank, crate.Cell.Center);
        var far = world.SpawnUnit(Person, UnitType.HeavyTank, new CellPos(45, 30).Center);
        var enemy = world.SpawnUnit(Other, UnitType.HeavyTank, new CellPos(32, 32).Center);

        foreach (var kind in new[] { CrateKind.Armor, CrateKind.Firepower, CrateKind.Speed, CrateKind.Armor })
        {
            Assert.Equal(kind, CrateSystem.Open(world, CratesDef.Default, crate, picker, kind, null));
        }

        Assert.Equal(CrateUpgrade.Armor | CrateUpgrade.Firepower | CrateUpgrade.Speed, picker.Upgrades);
        Assert.Equal((CrateUpgrade.None, CrateUpgrade.None), (far.Upgrades, enemy.Upgrades));

        // Firepower ×2 on what it deals, armor ÷1.5 on what it takes, speed ×1.2.
        Assert.Equal(180, CrateSystem.Damage(world, enemy, picker.Id, 90));
        Assert.Equal(60, CrateSystem.Damage(world, picker, enemy.Id, 90));
        Assert.Equal(90, CrateSystem.Damage(world, far, enemy.Id, 90));
        Assert.Equal(picker.Speed * 6 / 5, CrateSystem.Speed(world, picker, picker.Speed));
    }

    [Fact]
    public void Unit_gives_one_of_the_faction_s_beside_the_crate()
    {
        var simulation = Match();
        var world = simulation.World;
        var crate = new Crate(1, new CellPos(30, 30));
        var picker = world.SpawnUnit(Person, UnitType.Conscript, crate.Cell.Center, snapToSubcell: true);
        var before = world.Entities.OfType<Unit>().Count(unit => unit.Owner == Person);
        var events = new List<SimEvent>();

        Assert.Equal(CrateKind.Unit, CrateSystem.Open(world, CratesDef.Default, crate, picker, CrateKind.Unit, events));

        var spawned = Assert.Single(events.OfType<UnitSpawned>());
        Assert.Equal(Person, spawned.Owner);
        Assert.Equal(before + 1, world.Entities.OfType<Unit>().Count(unit => unit.Owner == Person));
        Assert.Equal(world.Player(Person).Faction.Id, world.Rules.Database.Units.Single(def => def.Id == UnitTypes.IdOf(spawned.Type)).Faction);
        Assert.InRange(System.Math.Abs(spawned.Position.ToCell().X - 30), 0, 2);
    }

    [Fact]
    public void A_match_with_crates_plays_back_from_its_replay_and_its_hash_follows_them()
    {
        var simulation = Match();
        var recorder = new ReplayRecorder(simulation);
        simulation.Step();
        recorder.AfterStep();
        var crate = simulation.World.CratesOnMap[0];
        // A tank by a command, which the replay keeps, onto the first crate: it takes it.
        simulation.Enqueue(new Rts.Simulation.Commands.SpawnTestUnitCommand(Person, 1, 0, UnitType.HeavyTank, crate.Cell.Center));
        for (var tick = 1; tick < 1300; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        Assert.DoesNotContain(simulation.World.CratesOnMap, c => c.Id == crate.Id);
        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);
        Assert.True(replay.Settings.Match!.Options.Crates);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);

        // The crates are state: one more changes the hash.
        var world = simulation.World;
        var hash = StateHasher.Compute(world);
        world.Crates.Add(new Crate(500, new CellPos(1, 1)));
        Assert.NotEqual(hash, StateHasher.Compute(world));
    }
}
