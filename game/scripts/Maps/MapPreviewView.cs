using System.Collections.Generic;
using Godot;
using Rts.Data.Maps;

namespace Rts.Client.Maps;

/// <summary>
/// Shows a map preview fitted into its rectangle without stretching, with the number of every start on its disc
/// (docs/implementation/M7_maps.md, 4.11). Empty until a texture is set.
/// </summary>
public partial class MapPreviewView : Control
{
    private Texture2D? _texture;
    private IReadOnlyList<MapStart> _starts = [];
    private Vector2I _mapSize;

    public override void _Ready() => Resized += QueueRedraw;

    public void Show(Texture2D? texture, MapDefinition? map)
    {
        _texture = texture;
        _starts = map?.Starts ?? [];
        _mapSize = map is null ? Vector2I.Zero : new Vector2I(map.Width, map.Height);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_texture is null || _mapSize.X == 0)
        {
            return;
        }

        var scale = Mathf.Min(Size.X / _mapSize.X, Size.Y / _mapSize.Y);
        var drawn = new Vector2(_mapSize.X, _mapSize.Y) * scale;
        var origin = (Size - drawn) / 2f;
        DrawTextureRect(_texture, new Rect2(origin, drawn), false);

        var font = GetThemeDefaultFont();
        var fontSize = GetThemeDefaultFontSize();
        foreach (var start in _starts)
        {
            var text = start.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var centre = origin + new Vector2(start.Cell.X + 0.5f, start.Cell.Y + 0.5f) * scale;
            var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
            var at = centre + new Vector2(-textSize.X / 2f, textSize.Y / 4f);
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, fontSize, 4, Colors.Black);
            DrawString(font, at, text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
    }
}
