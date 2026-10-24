namespace Rts.Net.Protocol;

/// <summary>
/// The constants of the wire format (docs/implementation/M9_network.md, 4.4). Every message starts with
/// <c>u8 messageType | u16 protocolVersion</c>; all numbers are little-endian regardless of the CPU.
/// </summary>
public static class NetProtocol
{
    /// <summary>
    /// Raised on any change of a message; peers of different versions refuse each other (4.8). 2: the fog of war in the options of a
    /// match; 3: the commands Garrison and Evacuate (stage 2e); 4: Superweapon (stage 2d); 5: the country of a player (loose ends,
    /// stage 5); 6: the options MCV Repacks, Superweapons and Destroyable Bridges.
    /// </summary>
    public const ushort Version = 6;

    /// <summary>The largest message, header included: the limit of the relay (4.14).</summary>
    public const int MaxMessageSize = 4096;

    /// <summary>The header: <c>u8 messageType | u16 protocolVersion</c>.</summary>
    public const int HeaderSize = 1 + 2;

    /// <summary>The longest string in UTF-8 bytes: names, versions, factions, codes, reasons.</summary>
    public const int MaxStringBytes = 256;
}
