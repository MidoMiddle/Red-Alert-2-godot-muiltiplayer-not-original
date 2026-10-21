namespace Rts.Client.Controls;

/// <summary>The two mouse schemes of the settings (docs/implementation/M6_menus_skirmish.md, 4.6).</summary>
public enum ControlScheme
{
    /// <summary>RA2: left click selects and orders, a right click cancels the selection, a right drag moves the camera.</summary>
    Classic,

    /// <summary>Right click orders, a left click on empty ground cancels the selection, a middle drag moves the camera.</summary>
    Modern,
}
