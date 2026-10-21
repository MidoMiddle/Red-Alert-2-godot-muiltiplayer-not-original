using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests.Commands;

public sealed class CommandSerializerTests
{
    [Fact]
    public void Spawn_has_the_exact_v1_byte_layout()
    {
        var bytes = Serialize(Spawn(player: 1, tick: 5, sequence: 7, x: 256, y: -1));

        Assert.Equal(
            "01" + "01000000" + "05000000" + "07000000" + "00010000" + "FFFFFFFF",
            Convert.ToHexString(bytes));
    }

    [Fact]
    public void Move_has_the_exact_v1_byte_layout()
    {
        var bytes = Serialize(Move(player: 2, tick: 10, sequence: 3, units: [1, 300], x: 128, y: 640));

        Assert.Equal(
            "02" + "02000000" + "0A000000" + "03000000" + "0200" + "01000000" + "2C010000" + "80000000" + "80020000",
            Convert.ToHexString(bytes));
    }

    public static TheoryData<Command> RoundTripCases => new()
    {
        Spawn(player: 1, tick: 0, sequence: 0),
        Spawn(player: 8, tick: int.MaxValue, sequence: uint.MaxValue, x: -5, y: int.MinValue),
        Move(player: 3, tick: 42, sequence: 9, units: []),
        Move(player: 3, tick: 42, sequence: 9, units: [7]),
        Move(player: 1, tick: 1, sequence: 1, units: Enumerable.Range(1, MoveUnitsCommand.MaxUnits).ToArray(), x: -100, y: 99999),
    };

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Round_trip_preserves_the_command(Command command)
    {
        var bytes = Serialize(command);

        var read = CommandSerializer.Read(bytes, out var bytesRead);

        Assert.Equal(command, read);
        Assert.Equal(bytes.Length, bytesRead);
        Assert.Equal(CommandSerializer.GetSize(command), bytesRead);
    }

    [Fact]
    public void Batch_of_random_commands_round_trips()
    {
        var random = new TestRandom(7);
        var commands = new List<Command>();
        for (var i = 0; i < 100; i++)
        {
            var player = 1 + random.NextInt(8);
            var tick = random.NextInt(1000);
            if (random.NextInt(2) == 0)
            {
                commands.Add(Spawn(player, tick, (uint)i, random.NextInt(20000), random.NextInt(20000)));
            }
            else
            {
                var units = Enumerable.Range(0, random.NextInt(20)).Select(_ => 1 + random.NextInt(5000)).ToArray();
                commands.Add(Move(player, tick, (uint)i, units, random.NextInt(20000), random.NextInt(20000)));
            }
        }

        var read = CommandSerializer.ReadAll(CommandSerializer.WriteAll(commands));

        Assert.Equal(commands, read);
    }

    [Fact]
    public void Empty_batch_round_trips()
    {
        Assert.Empty(CommandSerializer.ReadAll(CommandSerializer.WriteAll([])));
    }

    [Fact]
    public void Unknown_type_is_rejected()
    {
        var bytes = Serialize(Spawn(player: 1, tick: 0, sequence: 0));
        bytes[0] = 99;

        Assert.Throws<InvalidDataException>(() => CommandSerializer.Read(bytes, out _));
    }

    [Fact]
    public void Truncated_data_is_rejected()
    {
        var bytes = Serialize(Move(player: 1, tick: 0, sequence: 0, units: [1, 2, 3]));

        for (var length = 0; length < bytes.Length; length++)
        {
            var truncated = bytes[..length];
            Assert.Throws<InvalidDataException>(() => CommandSerializer.Read(truncated, out _));
        }
    }

    [Fact]
    public void Too_many_units_are_rejected_when_reading()
    {
        var bytes = Serialize(Move(player: 1, tick: 0, sequence: 0, units: []));
        bytes[13] = 0x01; // count = 257 (little-endian 0x0101)
        bytes[14] = 0x01;

        Assert.Throws<InvalidDataException>(() => CommandSerializer.Read(bytes, out _));
    }

    [Fact]
    public void Too_many_units_are_rejected_when_creating()
    {
        Assert.Throws<ArgumentException>(() => Move(player: 1, tick: 0, sequence: 0, units: Enumerable.Range(1, 257).ToArray()));
    }

    [Fact]
    public void Trailing_bytes_in_a_batch_are_rejected()
    {
        var bytes = CommandSerializer.WriteAll([Spawn(player: 1, tick: 0, sequence: 0)]).Append((byte)0).ToArray();

        Assert.Throws<InvalidDataException>(() => CommandSerializer.ReadAll(bytes));
    }

    [Fact]
    public void Shuffled_serialized_commands_give_identical_worlds()
    {
        var commands = new List<Command>
        {
            Spawn(player: 1, tick: 0, sequence: 0, x: 100, y: 100),
            Spawn(player: 2, tick: 0, sequence: 0, x: 900, y: 900),
            Spawn(player: 1, tick: 0, sequence: 1, x: 200, y: 100),
            Move(player: 1, tick: 1, sequence: 2, units: [1, 2, 3], x: 5000, y: 5000),
            Move(player: 2, tick: 1, sequence: 1, units: [2], x: 300, y: 300),
            Spawn(player: 2, tick: 2, sequence: 2, x: 400, y: 400),
        };
        var bytes = CommandSerializer.WriteAll(commands);

        var inOrder = RunTicks(CommandSerializer.ReadAll(bytes), ticks: 3);
        var shuffled = RunTicks(CommandSerializer.ReadAll(bytes).Reverse().ToList(), ticks: 3);

        Assert.Equal(Describe(inOrder), Describe(shuffled));
        Assert.Equal(4, inOrder.Entities.Count);
    }

    private static byte[] Serialize(Command command)
    {
        var bytes = new byte[CommandSerializer.GetSize(command)];
        CommandSerializer.Write(command, bytes);
        return bytes;
    }

    private static GameWorld RunTicks(IReadOnlyList<Command> commands, int ticks)
    {
        var world = new GameWorld(seed: 1, widthCells: 64, heightCells: 64);
        var queue = new CommandQueue();
        foreach (var command in commands)
        {
            queue.Enqueue(command);
        }

        for (var tick = 0; tick < ticks; tick++)
        {
            foreach (var command in queue.TakeForTick(tick))
            {
                CommandProcessor.Apply(world, command);
            }
        }

        return world;
    }

    private static List<string> Describe(GameWorld world) => world.Entities
        .Cast<TestUnit>()
        .Select(u => $"{u.Id} {u.Owner} {u.Position} {u.MoveTarget}")
        .ToList();
}
