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
        "type", "turn rate", "facing", "turret facing", "land", "obstacle",
        "path pending", "path", "path index", "path request", "path search progress", "path budget",
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
    })]
    [InlineData(typeof(GameWorld), new[]
    {
        "_entities", "_nextEntityId", "<CurrentTick>k__BackingField", "<Random>k__BackingField", "<Map>k__BackingField",
        "<PathRequests>k__BackingField",
    })]
    [InlineData(typeof(PathRequestQueue), new[] { "_pathfinder", "_requests", "<Budget>k__BackingField", "<ExpandedLastTick>k__BackingField" })]
    [InlineData(typeof(PathRequest), new[]
    {
        "<Unit>k__BackingField", "<RequestTick>k__BackingField", "<Start>k__BackingField", "<Goal>k__BackingField",
        "<Locomotor>k__BackingField", "<Search>k__BackingField",
    })]
    [InlineData(typeof(MapGrid), new[] { "_land", "_blocked", "<Width>k__BackingField", "<Height>k__BackingField" })]
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
}
