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
    /// stage 5); 6: the options MCV Repacks, Superweapons and Destroyable Bridges; 7: InputDelayChange (M9 step 7); 8: the command PlayerLeft and KickVote
    /// (step 10); 9: the relay — RoomRequest, RoomJoined, RoomMember, Relayed, the refusal ServerBusy (step 12); 10: the option Unit Count
    /// (M6 step 8); 11: the option Crates Appear (M11 a6).
    /// </summary>
    public const ushort Version = 11;

    /// <summary>The largest message, header included: the limit of the relay (4.14).</summary>
    public const int MaxMessageSize = 4096;

    /// <summary>
    /// The largest frame of the relay (<see cref="Messages.Relayed"/>): a message of <see cref="MaxMessageSize"/> and who it is from or
    /// for.
    /// </summary>
    public const int MaxFrameSize = MaxMessageSize + HeaderSize + 1 + 2;

    /// <summary>The header: <c>u8 messageType | u16 protocolVersion</c>.</summary>
    public const int HeaderSize = 1 + 2;

    /// <summary>The longest string in UTF-8 bytes: names, versions, factions, codes, reasons.</summary>
    public const int MaxStringBytes = 256;
}
