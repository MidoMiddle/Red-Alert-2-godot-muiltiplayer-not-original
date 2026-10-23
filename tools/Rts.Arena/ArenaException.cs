namespace Rts.Arena;

/// <summary>What is wrong with the command line, a profile, the map or the data: told as it is, and the Arena stops with code 2.</summary>
public sealed class ArenaException(string message) : Exception(message);
