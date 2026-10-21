using System.Reflection;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Random;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Hashing;

public sealed class StateHasherTests
{
    private static readonly PlayerId Player1 = new(1);

    /// <summary>Two units, one with a move target: covers every TestUnit field.</summary>
    private static (GameWorld World, TestUnit Moving, TestUnit Idle) BuildWorld(int idleOwner = 2)
    {
        var world = new GameWorld(seed: 7, widthCells: 64, heightCells: 64);
        var moving = world.SpawnTestUnit(Player1, new WorldPos(100, 200), speed: 32);
        moving.MoveTarget = new WorldPos(900, 800);
        var idle = world.SpawnTestUnit(new PlayerId(idleOwner), new WorldPos(300, 400), speed: 16);
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
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void Any_change_gives_a_different_hash(string mutation)
    {
        var baseline = StateHasher.Compute(BuildWorld().World);
        // Owner is immutable: that mutation is a world built with a different owner of the second unit.
        var (world, moving, idle) = BuildWorld(idleOwner: mutation == "owner" ? 3 : 2);

        switch (mutation)
        {
            case "tick": world.CurrentTick++; break;
            case "random": world.Random.NextUInt(); break;
            case "next id": idle.MarkDead(); world.RemoveDead(); world.SpawnTestUnit(new PlayerId(2), new WorldPos(300, 400), speed: 16); break;
            case "owner": break;
            case "alive": idle.MarkDead(); break;
            case "position x": moving.Position += new WorldPos(1, 0); break;
            case "position y": moving.Position += new WorldPos(0, 1); break;
            case "target removed": moving.MoveTarget = null; break;
            case "target added": idle.MoveTarget = new WorldPos(0, 0); break;
            case "target x": moving.MoveTarget = new WorldPos(901, 800); break;
            case "target y": moving.MoveTarget = new WorldPos(900, 801); break;
            case "speed": moving.Speed++; break;
            case "entity count": world.SpawnTestUnit(Player1, new WorldPos(0, 0), speed: 1); break;
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
    [InlineData(typeof(TestUnit), new[] { "<Position>k__BackingField", "<MoveTarget>k__BackingField", "<Speed>k__BackingField" })]
    [InlineData(typeof(GameWorld), new[]
    {
        "_entities", "_nextEntityId", "<CurrentTick>k__BackingField", "<Random>k__BackingField",
        "<WidthCells>k__BackingField", "<HeightCells>k__BackingField",
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
}
