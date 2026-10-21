using System;

namespace Rts.Client.App;

/// <summary>
/// A new window size that waits for the player to keep it (docs/implementation/M6_menus_skirmish.md, 4.5). The new size
/// stays only if the player confirms it within <see cref="KeepSeconds"/>; otherwise the previous size comes back. No Godot
/// types here, so the rule is covered by unit tests.
/// </summary>
public sealed class ResolutionChange
{
    /// <summary>Seconds the player has to confirm a new size.</summary>
    public const double KeepSeconds = 15;

    private WindowSize _previous;
    private WindowSize _next;
    private double _remaining;

    /// <summary>True from <see cref="Begin"/> until <see cref="Keep"/>, <see cref="Revert"/> or the end of the time.</summary>
    public bool IsPending { get; private set; }

    /// <summary>Whole seconds to show in the dialog: 15, 14, …, 1. Zero when nothing is pending.</summary>
    public int SecondsLeft => IsPending ? (int)Math.Ceiling(_remaining) : 0;

    /// <summary>Starts the countdown. Returns false and changes nothing while another change is pending.</summary>
    public bool Begin(WindowSize previous, WindowSize next)
    {
        if (IsPending)
        {
            return false;
        }

        _previous = previous;
        _next = next;
        _remaining = KeepSeconds;
        IsPending = true;
        return true;
    }

    /// <summary>Moves the countdown on. Returns the previous size when the time is up; the change is over then.</summary>
    public WindowSize? Tick(double seconds)
    {
        if (!IsPending)
        {
            return null;
        }

        _remaining -= seconds;
        return _remaining > 0 ? null : End(_previous);
    }

    /// <summary>The player keeps the new size. Returns it, or null if nothing was pending.</summary>
    public WindowSize? Keep() => End(_next);

    /// <summary>The player declines the new size. Returns the previous one, or null if nothing was pending.</summary>
    public WindowSize? Revert() => End(_previous);

    private WindowSize? End(WindowSize size)
    {
        if (!IsPending)
        {
            return null;
        }

        IsPending = false;
        return size;
    }
}
