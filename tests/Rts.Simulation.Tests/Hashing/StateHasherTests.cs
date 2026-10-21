using System.Reflection;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Math;
using Rts.Simulation.Random;
using Rts.Simulation.World;
using Rts.Simulation.Tests.World;

namespace Rts.Simulation.Tests.Hashing;

public sealed class StateHasherTests
{
    private static readonly PlayerId Player1 = new(1);

    /// <summary>Two units, one with a move target: covers every Unit field.</summary>
    private static (GameWorld World, Unit Moving, Unit Idle) BuildWorld(int idleOwner = 2, UnitType idleType = UnitType.Conscript)
    {
        var world = new GameWorld(seed: 7, widthCells: 64, heightCells: 64);
        var moving = world.SpawnForTest(Player1, new WorldPos(100, 200), speed: 32);
        moving.MoveTarget = new WorldPos(900, 800);
        var idle = world.SpawnForTest(new PlayerId(idleOwner), new WorldPos(300, 400), speed: 16, idleType);
        return (world, moving, idle);
    }

    [Fact]
    public void Same_state_gives_the_same_hash()
    {
        Assert.Equal(StateHasher.Compute(BuildWorld().World), StateHasher.Compute(BuildWorld().World));
    }

    public static TheoryData<string> Mutations => new()
    {
        "tick", "random", "next id", "owner", "alive", "position x", "position y",
        "target removed", "target added", "target x", "target y", "speed", "entity count",
        "type", "turn rate", "facing", "turret facing", "land", "obstacle", "height",
        "path pending", "path", "path index", "path request", "path search progress", "path budget",
        "wait ticks", "replans", "hold ticks", "resume target", "occupied cell", "path request avoid", "returning",
        "soldier spot", "subcell", "route", "route point", "attack-move", "order index", "loop",
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void Any_change_gives_a_different_hash(string mutation)
    {
        var baseline = StateHasher.Compute(BuildWorld().World);
        // Owner and type are immutable: those mutations are worlds built with a different second unit.
        var (world, moving, idle) = BuildWorld(
            idleOwner: mutation == "owner" ? 3 : 2,
            idleType: mutation == "type" ? UnitType.Engineer : UnitType.Conscript);

        switch (mutation)
        {
            case "tick": world.CurrentTick++; break;
            case "random": world.Random.NextUInt(); break;
            case "next id": idle.MarkDead(); world.RemoveDead(); world.SpawnForTest(new PlayerId(2), new WorldPos(300, 400), speed: 16); break;
            case "owner": break;
            case "alive": idle.MarkDead(); break;
            case "position x": moving.Position += new WorldPos(1, 0); break;
            case "position y": moving.Position += new WorldPos(0, 1); break;
            case "target removed": moving.MoveTarget = null; break;
            case "target added": idle.MoveTarget = new WorldPos(0, 0); break;
            case "target x": moving.MoveTarget = new WorldPos(901, 800); break;
            case "target y": moving.MoveTarget = new WorldPos(900, 801); break;
            case "speed": moving.Speed++; break;
            case "entity count": world.SpawnForTest(Player1, new WorldPos(0, 0), speed: 1); break;
            case "type": break;
            case "turn rate": moving.TurnRate++; break;
            case "facing": moving.Facing = BAngle.FromUnits(1); break;
            case "turret facing": moving.TurretFacing = BAngle.FromUnits(1); break;
            case "land": world.Map.SetLand(new CellPos(63, 0), LandType.Ice); break;
            case "obstacle": world.Map.SetBlocked(new CellPos(0, 63), true); break;
            case "height": world.Map.SetCornerLevel(64, 64, 1); break;
            case "path pending": moving.PathPending = true; break;
            case "path": moving.Path = AStar.FindPath(world.Map, new CellPos(0, 0), new CellPos(2, 1), Locomotor.Foot); break;
            case "path index": moving.PathIndex = 3; break;
            case "path request": world.PathRequests.Request(idle, 0, new CellPos(5, 5)); break;
            case "path search progress":
                // Same request in both worlds would be a different test; here progress alone must change the hash.
                world.PathRequests.Request(idle, 0, new CellPos(40, 40));
                var baselineWithRequest = StateHasher.Compute(world);
                world.PathRequests.Requests[0].Search.Advance(3);
                Assert.NotEqual(baselineWithRequest, StateHasher.Compute(world));
                break;
            case "path budget": world.PathRequests.Budget = 1; break;
            case "wait ticks": moving.WaitTicks = 4; break;
            case "replans": moving.Replans = 1; break;
            case "returning": idle.Returning = true; break;
            case "hold ticks": idle.HoldTicks = 5; break;
            case "resume target": idle.ResumeTarget = new WorldPos(10, 10); break;
            case "occupied cell": world.Reservations.SetVehicle(new CellPos(60, 60), new EntityId(99)); break;
            case "soldier spot":
                var soldier = world.SpawnForTest(Player1, new CellPos(50, 50).Center, speed: 1);
                var withSoldier = StateHasher.Compute(world);
                world.Reservations.Leave(new CellPos(50, 50), soldier);
                world.Reservations.TryEnter(new CellPos(50, 50), soldier, new CellPos(50, 50).Center + new WorldPos(64, 64));
                Assert.NotEqual(withSoldier, StateHasher.Compute(world));
                break;
            case "subcell": idle.Subcell = (idle.Subcell + 1) % 5; break;
            case "route": idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)]; break;
            case "route point":
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)];
                var withRoute = StateHasher.Compute(world);
                idle.Orders = [new UnitOrder(new WorldPos(5, 6), false)];
                Assert.NotEqual(withRoute, StateHasher.Compute(world));
                break;
            case "attack-move":
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)];
                var plain = StateHasher.Compute(world);
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), true)];
                Assert.NotEqual(plain, StateHasher.Compute(world));
                break;
            case "order index": idle.OrderIndex = 1; break;
            case "loop": idle.LoopOrders = true; break;
            case "path request avoid":
                world.PathRequests.Request(idle, 0, new CellPos(5, 5));
                var withoutAvoid = StateHasher.Compute(world);
                world.PathRequests.Request(idle, 0, new CellPos(5, 5), [new CellPos(4, 4)]);
                Assert.NotEqual(withoutAvoid, StateHasher.Compute(world));
                break;
            default: throw new ArgumentException(mutation);
        }

        Assert.NotEqual(baseline, StateHasher.Compute(world));
    }

    /// <summary>
    /// Every instance field of the simulation state, as declared by each type. If this test fails,
    /// a field was added or removed: update StateHasher first, then this list.
    /// </summary>
    [Theory]
    [InlineData(typeof(Entity), new[] { "<Id>k__BackingField", "<Owner>k__BackingField", "<IsAlive>k__BackingField" })]
    [InlineData(typeof(Unit), new[]
    {
        "<Archetype>k__BackingField", "<Position>k__BackingField", "<MoveTarget>k__BackingField", "<Speed>k__BackingField",
        "<TurnRate>k__BackingField", "<Facing>k__BackingField", "<TurretFacing>k__BackingField",
        "<PathPending>k__BackingField", "<Path>k__BackingField", "<PathIndex>k__BackingField",
        "<WaitTicks>k__BackingField", "<Replans>k__BackingField", "<ResumeTarget>k__BackingField", "<HoldTicks>k__BackingField",
        "<Returning>k__BackingField", "<Subcell>k__BackingField",
        "<Orders>k__BackingField", "<OrderIndex>k__BackingField", "<LoopOrders>k__BackingField",
    })]
    [InlineData(typeof(GameWorld), new[]
    {
        "_entities", "_nextEntityId", "<CurrentTick>k__BackingField", "<Random>k__BackingField", "<Map>k__BackingField",
        "<PathRequests>k__BackingField", "<Reservations>k__BackingField",
        // Derived from unit positions and rebuilt from them; not hashed (docs/implementation/M2_unit_control.md, 4.9).
        "<Spatial>k__BackingField",
    })]
    [InlineData(typeof(Rts.Simulation.Movement.Reservations), new[] { "_vehicle", "_infantry", "<Width>k__BackingField", "<Height>k__BackingField" })]
    [InlineData(typeof(PathRequestQueue), new[] { "_pathfinder", "_requests", "<Budget>k__BackingField", "<ExpandedLastTick>k__BackingField" })]
    [InlineData(typeof(PathRequest), new[]
    {
        "<Unit>k__BackingField", "<RequestTick>k__BackingField", "<Start>k__BackingField", "<Goal>k__BackingField",
        "<Locomotor>k__BackingField", "<Search>k__BackingField", "<Avoid>k__BackingField",
    })]
    [InlineData(typeof(MapGrid), new[]
    {
        "_land", "_blocked", "_heights", "_objects", "<Width>k__BackingField", "<Height>k__BackingField",
        // An index of the objects by cell, built from the list above.
        "_objectsByCell",
        // Caches of the layers above: their digest for the hash, a stamp for A* step costs (M2 step 15), and the speed of
        // every locomotor on every cell (M7 step 3).
        "_digest", "<Version>k__BackingField", "_passability",
    })]
    [InlineData(typeof(CornerHeightGrid), new[] { "_levels", "<Width>k__BackingField", "<Height>k__BackingField" })]
    [InlineData(typeof(MapObject), new[]
    {
        "<Id>k__BackingField", "<Kind>k__BackingField", "<Cell>k__BackingField", "<Variant>k__BackingField", "<Hp>k__BackingField",
        "<Destroyed>k__BackingField",
    })]
    [InlineData(typeof(DeterministicRandom), new[] { "_state", "_increment" })]
    public void Every_state_field_is_known_to_the_hasher(Type type, string[] hashedFields)
    {
        var actualFields = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(field => field.Name)
            .Order()
            .ToList();

        Assert.Equal(hashedFields.Order().ToList(), actualFields);
    }

    [Fact]
    public void A_path_digest_follows_its_content_through_with()
    {
        var path = new CellPath(PathStatus.Found, [new CellPos(0, 0), new CellPos(1, 1), new CellPos(2, 2)], 282, 3);
        var digest = path.Digest;
        var smoothed = path with { Waypoints = [new CellPos(0, 0), new CellPos(2, 2)] };
        var fresh = new CellPath(PathStatus.Found, path.Cells, 282, 3) { Waypoints = [new CellPos(0, 0), new CellPos(2, 2)] };

        Assert.Equal(digest, path.Digest);
        Assert.NotEqual(digest, smoothed.Digest);
        Assert.Equal(fresh.Digest, smoothed.Digest);
        Assert.NotEqual(digest, (path with { Cost = 283 }).Digest);
    }

    [Fact]
    public void The_map_digest_changes_with_any_cell_and_comes_back_with_it()
    {
        var map = new MapGrid(8, 8);
        var clear = map.Digest;

        map.SetLand(new CellPos(3, 4), LandType.Rough);
        var rough = map.Digest;
        map.SetLand(new CellPos(3, 4), LandType.Clear);
        map.SetBlocked(new CellPos(7, 7), true);

        Assert.NotEqual(clear, rough);
        Assert.NotEqual(clear, map.Digest);
        map.SetBlocked(new CellPos(7, 7), false);
        Assert.Equal(clear, map.Digest);
    }
}
