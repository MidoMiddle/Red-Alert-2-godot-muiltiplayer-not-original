using System.Collections.Generic;
using Godot;
using Rts.Data.Maps;

namespace Rts.Client.Maps;

/// <summary>
/// Shows a map preview fitted into its rectangle without stretching, with the number of every start on its disc
/// (docs/implementation/M7_maps.md, 4.11). Empty until a texture is set. The picture of a playable area is the screen's
/// rectangle, twice as wide as tall (<see cref="MapPreview.SizeOf"/>), not the square of the grid: it is fitted by its own
/// pixels, and the numbers go where the picture has the discs (<see cref="MapPreview.StartCentre"/>).
/// </summary>
public partial class MapPreviewView : Control
{
    private Texture2D? _texture;
    private IReadOnlyList<MapStart> _starts = [];
    private MapDefinition? _map;
    private Vector2I _pictureSize;

    public override void _Ready() => Resized += QueueRedraw;

    public void Show(Texture2D? texture, MapDefinition? map)
    {
        _texture = texture;
        _starts = map?.Starts ?? [];
        _map = map;
        _pictureSize = map is null ? Vector2I.Zero : new Vector2I(MapPreview.SizeOf(map).Width, MapPreview.SizeOf(map).Height);
        QueueRedraw();
    }

    public override void _Draw()
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("MapPreviewView.draw");
        if (_texture is null || _map is null || _pictureSize.X == 0)
        {
            return;
        }

        var scale = Mathf.Min(Size.X / _pictureSize.X, Size.Y / _pictureSize.Y);
        var drawn = new Vector2(_pictureSize.X, _pictureSize.Y) * scale;
        var origin = (Size - drawn) / 2f;
        DrawTextureRect(_texture, new Rect2(origin, drawn), false);

        var font = GetThemeDefaultFont();
        var fontSize = GetThemeDefaultFontSize();
        foreach (var start in _starts)
        {
            var text = start.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var (x, y) = MapPreview.StartCentre(_map, start.Cell);
            var centre = origin + new Vector2(x, y) * scale;
            var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize);
            var at = centre + new Vector2(-textSize.X / 2f, textSize.Y / 4f);
            DrawStringOutline(font, at, text, HorizontalAlignment.Left, -1, fontSize, 4, Colors.Black);
            DrawString(font, at, text, HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
    }
}
