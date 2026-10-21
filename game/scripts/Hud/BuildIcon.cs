using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// One square of the item grid (docs/08_ui_ux.md, section 4). Empty until production exists (M3): then it shows the item, its
/// progress and its state. For now every square only shows its frame.
/// </summary>
public partial class BuildIcon : Panel
{
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(100f, 58f);
    }
}
