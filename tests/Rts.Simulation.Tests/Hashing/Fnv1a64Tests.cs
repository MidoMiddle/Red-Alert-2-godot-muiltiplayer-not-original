using System.Text;
using Rts.Simulation.Hashing;

namespace Rts.Simulation.Tests.Hashing;

public sealed class Fnv1a64Tests
{
    [Theory]
    [InlineData("", 0xcbf29ce484222325UL)]
    [InlineData("a", 0xaf63dc4c8601ec8cUL)]
    [InlineData("foobar", 0x85944171f73967e8UL)]
    public void Matches_reference_vectors(string input, ulong expected)
    {
        var hash = new Fnv1a64();

        hash.Add(Encoding.ASCII.GetBytes(input));

        Assert.Equal(expected, hash.Value);
    }

    [Fact]
    public void Numbers_are_fed_little_endian()
    {
        var fromInt = new Fnv1a64();
        fromInt.Add(0x04030201);
        var fromBytes = new Fnv1a64();
        fromBytes.Add([0x01, 0x02, 0x03, 0x04]);

        Assert.Equal(fromBytes.Value, fromInt.Value);

        var fromLong = new Fnv1a64();
        fromLong.Add(0x0807060504030201L);
        var fromLongBytes = new Fnv1a64();
        fromLongBytes.Add([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);

        Assert.Equal(fromLongBytes.Value, fromLong.Value);
    }
}
