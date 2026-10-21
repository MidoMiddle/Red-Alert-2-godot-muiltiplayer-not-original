namespace Rts.Client.Controls;

/// <summary>
/// A double press: the second press of the same key within <see cref="WindowMs"/> counts as a double tap. A third press starts
/// over. Client state only; the keys are plain numbers the caller chooses (a digit for a control group, one code for the type
/// select key).
/// </summary>
public sealed class DoubleTap
{
    /// <summary>Milliseconds within which a second press of the same key is a double tap.</summary>
    public const ulong WindowMs = 400;

    private int _lastKey = -1;
    private ulong _lastAt;

    /// <summary>A press of <paramref name="key"/> at <paramref name="nowMs"/>. True when it completes a double tap.</summary>
    public bool Press(int key, ulong nowMs)
    {
        var isDouble = key == _lastKey && nowMs - _lastAt <= WindowMs;
        _lastKey = isDouble ? -1 : key;
        _lastAt = nowMs;
        return isDouble;
    }
}
