using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;

namespace Rts.Net.Session;

/// <summary>
/// What a computer is, for the checks before a match (docs/implementation/M9_network.md, 4.8): the build of the game, the version of
/// the simulation and the hash of the game data. Two computers play together only when all three are the same.
/// </summary>
public sealed record NetIdentity(string GameVersion, uint SimVersion, ulong DataHash)
{
    /// <summary>The first message of a client with this identity.</summary>
    public Hello Hello(string playerName, string roomCode = "") => new(GameVersion, SimVersion, DataHash, playerName, roomCode);
}

/// <summary>
/// The checks of a client by the host before it takes a seat (docs/implementation/M9_network.md, 4.8), each with its own
/// <see cref="RejectReason"/>: the protocol (in the header of every message), the game, the simulation, the data. The map is checked by
/// the client itself against the lobby (<see cref="ClientSession"/>): only it knows which maps it has.
/// </summary>
public static class Handshake
{
    /// <summary>The refusal of a client that said <paramref name="hello"/> to a host of <paramref name="host"/>; null to take it.</summary>
    public static Reject? Check(Hello hello, NetIdentity host)
    {
        if (hello.GameVersion != host.GameVersion)
        {
            return new Reject(RejectReason.GameVersion, $"The host plays {host.GameVersion}, you {hello.GameVersion}: update the game.");
        }

        if (hello.SimVersion != host.SimVersion)
        {
            return new Reject(RejectReason.SimVersion, $"The host's simulation is {host.SimVersion}, yours {hello.SimVersion}.");
        }

        return hello.DataHash != host.DataHash
            ? new Reject(RejectReason.DataHash, $"The game data differ: the host's {host.DataHash:x16}, yours {hello.DataHash:x16}.")
            : null;
    }

    /// <summary>The refusal of a client whose first message could not be read because it speaks another version of the protocol.</summary>
    public static Reject? Check(MessageFormatException error) =>
        error.Error == MessageError.WrongProtocolVersion
            ? new Reject(RejectReason.ProtocolVersion, $"The host speaks the network protocol {NetProtocol.Version}, you {error.ProtocolVersion}.")
            : null;
}
