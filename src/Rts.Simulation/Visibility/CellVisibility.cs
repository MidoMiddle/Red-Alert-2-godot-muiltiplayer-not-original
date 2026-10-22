namespace Rts.Simulation.Visibility;

/// <summary>What a player knows of a cell (docs/implementation/M4_combat.md, 4.14).</summary>
public enum CellVisibility : byte
{
    /// <summary>Never seen: black.</summary>
    Shroud,

    /// <summary>Seen before, not now: the land and the last buildings seen.</summary>
    Fog,

    /// <summary>In sight now.</summary>
    Visible,
}
