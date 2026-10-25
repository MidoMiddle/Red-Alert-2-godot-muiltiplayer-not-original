namespace Rts.Net.Protocol;

/// <summary>
/// The first byte of every network message (docs/implementation/M9_network.md, 4.4). Values are part of the wire format: never
/// renumber; a new message takes the next free number.
/// </summary>
public enum MessageType : byte
{
    Hello = 1,
    Welcome = 2,
    Reject = 3,
    LobbyState = 4,
    LobbyChange = 5,
    StartGame = 6,
    LoadProgress = 7,
    AllLoaded = 8,
    TickBundle = 9,
    Checksum = 10,
    Ping = 11,
    Pong = 12,
    PauseRequest = 13,
    Pause = 14,
    Resume = 15,
    PlayerLeft = 16,
    Surrender = 17,
    DesyncReport = 18,
    InputDelayChange = 19,
    KickVote = 20,
}
