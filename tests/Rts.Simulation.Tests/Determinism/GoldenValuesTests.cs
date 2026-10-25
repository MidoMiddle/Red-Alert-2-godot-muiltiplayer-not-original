using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Random;
using Rts.Simulation.Replays;
using Rts.Simulation.Tests.Map;

namespace Rts.Simulation.Tests.Determinism;

/// <summary>
/// Golden values: numbers captured once on macOS (Apple Silicon) and frozen. Every platform in CI
/// must reproduce them bit for bit — that is the cross-platform determinism check.
///
/// Rule: a golden value changes only together with a deliberate rules change. Bump
/// <see cref="SimulationVersion.Current"/>, update the value in the same commit and explain why.
/// A golden value that changes "by itself" is a determinism bug, not a test to fix.
/// </summary>
public sealed class GoldenValuesTests
{
    private const int ScenarioTicks = 1000;

    [Fact]
    public void Random_generator_sequence()
    {
        var random = new DeterministicRandom(seed: 42);

        var actual = Enumerable.Range(0, 10).Select(_ => random.NextUInt()).ToArray();

        uint[] expected =
        [
            565663470, 3244226384, 2504567229, 903561869, 4026996297,
            2722332799, 3032858066, 272411090, 1181909318, 20290832,
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Fixed_point_operations()
    {
        static Fixed F(long value) => Fixed.FromInt(value);
        static Fixed R(long numerator, long denominator) => Fixed.FromRatio(numerator, denominator);

        long[] actual =
        [
            (F(3) / F(7)).Raw,
            (F(-3) / F(7)).Raw,
            (F(1) / F(-9)).Raw,
            (F(10) / R(3, 4)).Raw,
            (R(1, 3) * F(3)).Raw,
            (R(-5, 7) * R(3, 11)).Raw,
            (R(22, 7) * R(22, 7)).Raw,
            (R(123_456, 1000) * R(-789, 1000)).Raw,
            (R(-1, 3) * R(-1, 3)).Raw,
            (F(1_000_000) * R(1, 1_000_000)).Raw,
            R(355, 113).Raw,
            R(-1, 7).Raw,
            R(2, 3).Raw,
            R(-99_999, 100_000).Raw,
            Fixed.Sqrt(F(2)).Raw,
            Fixed.Sqrt(F(3)).Raw,
            Fixed.Sqrt(R(1, 10)).Raw,
            Fixed.Sqrt(F(1_000_000)).Raw,
            Fixed.Sqrt(R(7, 3)).Raw,
            (F(5) * 7 / 3).Raw,
        ];

        long[] expected =
        [
            28086, -28086, -7281, 873813, 65535,
            -12767, 647333, -6383540, 7281,
            0, // 1/1,000,000 is below the 1/65536 step, so it is stored as 0
            205887, -9362, 43690, -65535,
            92681, 113511, 20723, 65536000, 100107,
            764586,
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Trigonometry_tables_and_samples()
    {
        var tables = new Fnv1a64();
        foreach (var value in TrigTables.SinQuarter)
        {
            tables.Add(value);
        }

        foreach (var value in TrigTables.Atan)
        {
            tables.Add((int)value);
        }

        long[] actual =
        [
            FixedMath.Sin(BAngle.FromUnits(1)).Raw,
            FixedMath.Sin(BAngle.FromUnits(5461)).Raw,
            FixedMath.Sin(BAngle.FromUnits(12345)).Raw,
            FixedMath.Cos(BAngle.FromUnits(40000)).Raw,
            FixedMath.Sin(BAngle.FromUnits(65535)).Raw,
            FixedMath.Angle(3, -7).Value,
            FixedMath.Angle(256, 1).Value,
            FixedMath.Angle(-12_345, 6_789).Value,
            FixedMath.Angle(-1, -51_200).Value,
            FixedMath.Angle(40_000, 39_999).Value,
        ];

        long[] expected =
        [
            6, 32767, 60684, -50404, -6,
            4223, 16425, 43908,
            0, // 359.99…° rounds up to a full turn, which is north
            24576,
        ];
        Assert.Equal(140777579128644704UL, tables.Value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Pathfinding_results()
    {
        var landTypes = TestMapParserTests.Load("all_land_types.txt").Grid;
        var maze = TestMapParserTests.Load("paths/maze.txt").Grid;
        (MapGrid Map, int Sx, int Sy, int Gx, int Gy, Locomotor Locomotor)[] searches =
        [
            (landTypes, 0, 0, 23, 11, Locomotor.Wheel),
            (landTypes, 23, 11, 0, 0, Locomotor.Foot),
            (landTypes, 7, 0, 12, 6, Locomotor.Track),
            (maze, 0, 0, 14, 14, Locomotor.Foot),
            (maze, 14, 0, 0, 14, Locomotor.Wheel),
        ];

        var hash = new Fnv1a64();
        var costs = new List<int>();
        foreach (var (map, sx, sy, gx, gy, locomotor) in searches)
        {
            var path = AStar.FindPath(map, new CellPos(sx, sy), new CellPos(gx, gy), locomotor);
            costs.Add(path.Cost);
            hash.Add((byte)path.Status);
            foreach (var cell in path.Cells)
            {
                hash.Add(cell.X);
                hash.Add(cell.Y);
            }
        }

        Assert.Equal(15034282284834192112UL, hash.Value);
        Assert.Equal([2829, 2869, 817, 4000, 3200], costs);
    }

    [Fact]
    public void Smoothed_paths()
    {
        var landTypes = TestMapParserTests.Load("all_land_types.txt").Grid;
        var maze = TestMapParserTests.Load("paths/maze.txt").Grid;
        (MapGrid Map, int Sx, int Sy, int Gx, int Gy, Locomotor Locomotor)[] searches =
        [
            (landTypes, 0, 0, 23, 11, Locomotor.Wheel),
            (landTypes, 23, 11, 0, 0, Locomotor.Foot),
            (maze, 0, 0, 14, 14, Locomotor.Foot),
        ];

        var hash = new Fnv1a64();
        var counts = new List<int>();
        foreach (var (map, sx, sy, gx, gy, locomotor) in searches)
        {
            var path = PathSmoother.Smooth(map, AStar.FindPath(map, new CellPos(sx, sy), new CellPos(gx, gy), locomotor), locomotor);
            counts.Add(path.Waypoints.Length);
            foreach (var waypoint in path.Waypoints)
            {
                hash.Add(waypoint.X);
                hash.Add(waypoint.Y);
            }
        }

        Assert.Equal(4334713273682822449UL, hash.Value);
        Assert.Equal([4, 5, 15], counts);
    }

    [Fact]
    public void Reference_scenario_checksums()
    {
        var checksums = ReferenceScenario.Run(ReferenceScenario.Commands(ScenarioTicks), ScenarioTicks);

        StateChecksum[] actual = [checksums[99], checksums[499], checksums[999]];

        // Rules v10 (M2 step 12): routes, Stop, AttackMove, Waypoints, Scatter; the scenario now issues all of them.
        // Rules v11 (M2 step 15): the same states, hashed with the map and paths as digests and only the taken places.
        // Rules v12 (M2, after closing): units of a group swap places; the first difference comes after tick 500.
        // Rules v13 (M7 step 2): the map digest covers the corner heights; the states are the same.
        // Rules v16 (M7 step 5): the map digest covers the map objects too (none here); the states are the same.
        // Rules v17 (M7 step 6): the map digest covers the bridges too (none here); the states are the same.
        // Rules v18 (M7 step 7): the map digest covers the buildings and their cells too (none here); the states are the same.
        // Rules v19 (M3 step 5): the hash covers the credits of the players; the states are the same.
        // Rules v21 (M3 step 8): the hash covers the production queues (empty here); the states are the same.
        // Rules v22 (M3 step 9): the map digest covers the ore (none here); the states are the same.
        // Rules v23 (M3 step 10): the hash covers the work of the harvesters, which follow the orders of the scenario.
        // Rules v26 (M4 step 2): the hash covers the hit points of the units; the states are the same.
        // Rules v27 (M4 step 3): the hash covers the targets and weapons (none fire here); the states are the same.
        // Rules v30 (M4 step 6): the units of both players find each other by themselves and fight where they meet.
        // Rules v31 (M4 step 7): the scenario issues Attack, ForceAttack and SetStance too; units follow what they see and stances are hashed.
        // Rules v32 (M4 step 8): vehicles crush the soldiers of the other player they drive over; the first crush comes after tick 100.
        // Rules v35 (M4 step 11): the fog of war: the grids of what every player sees are hashed, and units fight only what they see.
        // Rules v36 (M4 step 13): the hash covers the tick of the last attack notice of every player (none here); the states are the same.
        // Rules v37 (M4 step 14): tanks drive over the enemy soldiers within four cells by themselves; the first difference after tick 100.
        // Rules v38 (M5 step 10): the hash covers the tick every player lost the match on (none here, no match); the states are the same.
        // Rules v39 (M8 step 2): the hash covers the bonuses of every player and the hundredths of income owed (none here); the states are the same.
        // Rules v44 (stage 2a, the shipyard): the hash covers a fifth production queue, the naval one (empty here); the states are the same.
        // Rules v45 (stage 2b, aircraft): and a sixth, the aircraft one (empty); the states are the same.
        // Rules v48 (stage 2e, the garrison): the hash covers the building a soldier goes into and the soldiers inside every building
        // (none here); the states are the same.
        // Rules v49 (stage 3a): the hash covers the temporal beam that holds every entity (none here); the states are the same.
        // Rules v50 (stage 3b): and the bombs, the toxin on the ground, deploying, jumps and blackouts (none); the states are the same.
        // Rules v51 (stage 3c): the version only; no shot flies here.
        // Rules v52 (stage 3d): the passengers and the parasite of every unit in the hash (none); the states are the same.
        // Rules v54 (stage 3f): the ammo of every unit in the hash (0 for all here); the states are the same.
        // Rules v55: a carrier and its Hornets in the hash (none); the states are the same.
        // Rules v56 (stage 2d): the charges, the cover and the storms in the hash (none); the states are the same.
        // Rules v57 (loose ends, stage 1): the load of a paradrop plane and the income of a derrick in the hash (none); the states are the same.
        // Rules v60 (loose ends, stage 3): the experience of every unit and the veterans of a spy in the hash (none); the states are the same.
        StateChecksum[] expected =
        [
            new(100, 0xf972d5216d8ab4d5UL),
            new(500, 0x80f7cc1b0404861aUL),
            new(1000, 0x3b2267761c666311UL),
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Battle_checksums()
    {
        var checksums = BattleDeterminismTests.Once.Value;

        StateChecksum[] actual = [.. checksums.Where(c => c.Tick is 1000 or 2000 or 3000)];

        // Rules v37 (M4 step 15): the general battle, 150 against 150 with their bases on the load map — projectiles, damage,
        // crushing, defenses, power, the fog and the attack notices all in one fight.
        // Rules v38 (M5 step 10): the tick of defeat of every player in the hash (none, no match); the battle is the same.
        // Rules v39 (M8 step 2): the bonuses of every player and the hundredths of income owed in the hash (none); the battle is the same.
        // Rules v44 (stage 2a): the naval production queue in the hash (empty); the battle is the same.
        // Rules v45 (stage 2b): the aircraft queue in the hash (empty); the battle is the same — no flier, no anti-air weapon fires.
        // Rules v48 (stage 2e): the soldiers inside buildings and the buildings soldiers go into in the hash (none); the battle is the
        // same — no bunker, no lens tower.
        // Rules v49 (stage 3a): the temporal beam of every entity in the hash (none); the battle is the same — no soldier of 3a fights.
        // Rules v50 (stage 3b): the bombs, the toxin, deploying, jumps and blackouts in the hash (none); a weapon fires only at what it
        // can hurt, and every weapon of the battle at the ground hurts every armor (the anti-air guns fire at the air only).
        // Rules v51 (stage 3c): the hit points of every shot in flight in the hash (0 for all here): tick 1000 changes, with shots in the
        // air; ticks 2000 and 3000, with none, are as they were.
        // Rules v52 (stage 3d): the passengers and the parasite of every unit in the hash (none); the battle is the same.
        // Rules v54 (stage 3f): the ammo of every unit in the hash (0 for all); the battle is the same.
        // Rules v55: a carrier and its Hornets in the hash (none); the battle is the same.
        // Rules v56 (stage 2d): the charges, the cover and the storms in the hash; the battle is the same.
        // Rules v57 (loose ends, stage 1): the load of a paradrop plane and the income of a derrick in the hash; the battle is the same.
        // Rules v60 (loose ends, stage 3): experience in the hash; the killers gain it, the veterans fight harder.
        StateChecksum[] expected =
        [
            new(1000, 0xd1bccb27290223eeUL),
            new(2000, 0x6f2f22b17ad18279UL),
            new(3000, 0xd629a75f933a181dUL),
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reference_scenario_replay_bytes()
    {
        var live = new GameSimulation(ReferenceScenario.Settings(), TestRules.Standard);
        var recorder = new ReplayRecorder(live);
        foreach (var command in ReferenceScenario.Commands(ScenarioTicks))
        {
            live.Enqueue(command);
        }

        for (var i = 0; i < ScenarioTicks; i++)
        {
            live.Step();
            recorder.AfterStep();
        }

        var bytes = ReplayFormat.Write(recorder.Build());
        var hash = new Fnv1a64();
        hash.Add(bytes);

        // Replay and command format v2 (M2 step 2): the spawn command carries the unit type.
        // Rules v3 (M2 step 3): same bytes except the recorded checksums, which now include the map.
        // Replay format v3, rules v4 (M2 step 4): one more byte, the "no map" flag; checksums unchanged on clear land.
        // Replay format v4, command format v3 (M2 step 12): new commands in the scenario, the queue byte in moves.
        // Rules v11 (M2 step 15): the version and the recorded checksums change, the commands do not.
        // Rules v12 (M2, after closing): place swaps change the checksums from tick 500 on.
        // Replay format v5, rules v13 (M7 step 2): the format version, the rules version and the checksums change; the
        // scenario has no map of its own, so no height layer is written.
        // Rules v14 (M7 step 3): slopes and cliffs in passability; the scenario is on flat ground, so only the version changes.
        // Rules v15 (M7 step 4): what follows a map change; the map never changes here, so only the version changes.
        // Replay format v6, command format v4, rules v16 (M7 step 5): versions and checksums change; no map, so no object list.
        // Replay format v7, rules v17 (M7 step 6): versions and checksums change; no map, so no bridge list.
        // Replay format v8, rules v18 (M7 step 7): versions and checksums change; no map, so no building list.
        // Replay format v9 (M3 step 3): the format version and 8 bytes of data hash (no data, zero); the checksums do not change.
        // Replay format v10, rules v19 (M3 step 5): 4 bytes of starting credits; versions and checksums change.
        // Replay format v11, command format v5, rules v20 (M3 step 6): only the versions change; the scenario has no buildings.
        // Replay format v12, command format v6, rules v21 (M3 step 8): versions and checksums change.
        // Replay format v13, rules v22 (M3 step 9): versions and checksums change; no map, so no ore layer.
        // Replay format v14, command format v7, rules v23 (M3 step 10): versions and checksums change.
        // Replay format v15, command format v8, rules v24 (M3 step 11): only the versions change; the scenario has no buildings.
        // Rules v25 (M3 step 12): the construction yard is never built; only the version changes.
        // Rules v26 (M4 step 2): versions and checksums change; the hit points of the units are hashed.
        // Rules v27 (M4 step 3): versions and checksums change; the targets and weapons of the units are hashed.
        // Rules v28 (M4 step 4): only the version changes; nobody fires, so there are no projectiles.
        // Rules v29 (M4 step 5): only the version changes; nobody is hit.
        // Rules v30 (M4 step 6): the version and the checksums change; the units of both players fight where they meet.
        // Rules v31 (M4 step 7): other commands (Attack, ForceAttack, SetStance among them), so other bytes and length.
        // Rules v32 (M4 step 8): the version and the checksums change; the commands are the same.
        // Rules v33 (M4 step 9): only the version changes; no building falls in the scenario.
        // Rules v34 (M4 step 10): only the version changes; the scenario has no defenses.
        // Rules v35 (M4 step 11): the version and the checksums change: what the players see is hashed.
        // Rules v36 (M4 step 13): the version and the checksums change: a new field of every player is hashed.
        // Rules v37 (M4 step 14): the version and the checksums change: tanks crush the soldiers near them by themselves.
        // Replay format v17 (M5 step 2): one byte more, the "no match setup" flag after the map; the version changes, the commands and
        // checksums do not.
        // Replay format v18 (M5 step 3): the starts of the map in the match setup; there is no setup here, so only the version changes.
        // Replay format v19, command format v10, rules v38 (M5 step 10): the versions and the checksums change (the tick of defeat in the
        // hash); no surrender here, so the commands are the same.
        // Rules v39 (M8 step 2): the version and the checksums change (the bonuses of the players in the hash); the commands are the same.
        // Rules v40 (M8 step 11): only the version changes — the allies come from the teams of a match setup, and there is none here.
        // Replay format v20 (M8 step 13): the personality of every player in the match setup; there is none here, so only the version
        // changes.
        // Replay format v21, rules v41 (the fog of war, an option of the match): no match setup here, so only the versions change; the
        // checksums do not.
        // Rules v42 (the Alliance: its unit types, the starting infantry of a faction): only the version changes — the scenario keeps to
        // the eight unit types of M2.
        // Rules v43 (every unit of both factions; ships and aircraft available later): only the version changes.
        // Rules v44 (stage 2a): the version and the checksums change — the naval queue is hashed; the commands are the same.
        // Rules v45 (stage 2b): the same again with the aircraft queue.
        // Rules v46 (stage 2c, the workshops, the discount, the bonus of the ore, the blast): only the version changes.
        // Rules v47 (stage 2d, the uplink and the veil): only the version changes — no veil here, and the veils are hashed only when one is.
        // Replay format v22, rules v48 (stage 2e, the garrison and the lens chains): the versions and the checksums change — the soldiers
        // inside and the building a soldier goes into are hashed; the commands are the same.
        // Rules v49 (stage 3a): the version and the checksums change — the temporal beam of every entity is hashed.
        // Rules v50 (stage 3b): again, with the bombs, the toxin, deploying, jumps and blackouts.
        // Rules v51 (stage 3c): the version and so the bytes.
        // Rules v52 (stage 3d): the version and the checksums: passengers and parasites are hashed.
        // Rules v53 (stage 3e): only the version; nothing new is hashed.
        // Rules v54 (stage 3f): the version and the checksums: the ammo of every unit is hashed.
        // Rules v55: again, with a carrier's Hornets.
        // Replay format v23, rules v56 (stage 2d): the versions and the checksums; the commands are the same.
        // Rules v57 (loose ends, stage 1): the version and the checksums. Rules v58 (stage 2): the version. Rules v59 (wide bridges): the version.
        // Rules v60 (stage 3: veterancy): the version and the checksums. Rules v61 (stage 4: the Osprey): the version.
        // Replay format v24, rules v62 (stage 5: the countries): the versions. Rules v63 (the yard packs up, the Rifleman digs in): the version.
        // Replay format v25, rules v71 (the repair mode mends a taken tech building): the versions.
        // Replay format v26, rules v72 (M9 step 10: PlayerLeft): the versions; nothing new is hashed.
        // Replay format v27 (M9 step 11): one byte more, the network flag of a match on one computer.
        // Replay format v28 (M6 step 8: Unit Count in the match setup, which this scenario has not): the version only.
        Assert.Equal((22270, 18435161094427401548UL), (bytes.Length, hash.Value));
    }
}
