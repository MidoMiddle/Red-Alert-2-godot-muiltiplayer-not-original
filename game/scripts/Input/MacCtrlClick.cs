namespace Rts.Client.Controls;

/// <summary>
/// macOS takes Ctrl + left click for a right click, and Godot passes it on so: the right button, with Ctrl held. In the classic scheme
/// the left button orders and Ctrl with an order is the forced attack (a tank at a bridge, at the ground), so that right click is the
/// left one again, its release and its holding too. On another system, or in the modern scheme where the right button orders, the
/// buttons stay as they come. Client state only.
/// </summary>
public sealed class MacCtrlClick
{
    private readonly bool _active;
    private bool _held;

    public MacCtrlClick(bool mac, ControlScheme scheme) => _active = mac && scheme == ControlScheme.Classic;

    /// <summary>The button a press is: a right one with Ctrl held is the left one, until its release.</summary>
    public PointerButton Press(PointerButton button, bool ctrl)
    {
        _held = _active && button == PointerButton.Right && ctrl;
        return _held ? PointerButton.Left : button;
    }

    /// <summary>The button a release is: that of the press it ends.</summary>
    public PointerButton Release(PointerButton button)
    {
        if (_held && button == PointerButton.Right)
        {
            _held = false;
            return PointerButton.Left;
        }

        return button;
    }

    /// <summary>The buttons held now as the game takes them: the right one held for the left is the left.</summary>
    public (bool Left, bool Right) Held(bool left, bool right) => _held ? (left || right, false) : (left, right);
}
