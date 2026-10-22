using System;
using System.Collections;
using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>
/// The economy of the players 1…8 in order, read-only (M4 step 15). A <c>foreach</c> over it takes a struct enumerator, so the systems
/// that walk the players every tick allocate nothing; through the interface it is a plain list.
/// </summary>
public readonly struct PlayerList : IReadOnlyList<PlayerState>
{
    private readonly PlayerState[] _players;

    internal PlayerList(PlayerState[] players) => _players = players;

    public int Count => _players.Length;

    public PlayerState this[int index] => _players[index];

    public ReadOnlySpan<PlayerState>.Enumerator GetEnumerator() => new ReadOnlySpan<PlayerState>(_players).GetEnumerator();

    IEnumerator<PlayerState> IEnumerable<PlayerState>.GetEnumerator() => ((IEnumerable<PlayerState>)_players).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _players.GetEnumerator();
}
