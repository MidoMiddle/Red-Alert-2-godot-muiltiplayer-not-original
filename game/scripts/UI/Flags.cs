using System.Collections.Generic;
using Godot;

namespace Rts.Client.UI;

/// <summary>
/// The flags of the countries beside the Side column of the skirmish screen (as in RA2), drawn by code until there are pictures: simple
/// stripes and shapes, 48 × 32. A side without a country (random, a faction) has none: the screen writes «???» there.
/// </summary>
public static class Flags
{
    public const int Width = 48;
    public const int Height = 32;

    private static readonly Dictionary<string, ImageTexture> Cache = new();

    private static readonly Color Red = new("#C8102E");
    private static readonly Color White = new("#F4F4F4");
    private static readonly Color Blue = new("#1F3A93");
    private static readonly Color Black = new("#111111");
    private static readonly Color Gold = new("#F2C230");
    private static readonly Color Green = new("#1E8C3A");

    /// <summary>The flag of a country of the data, or null for a side without one.</summary>
    public static ImageTexture? Of(string country)
    {
        if (Cache.TryGetValue(country, out var cached))
        {
            return cached;
        }

        var image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgb8);
        switch (country)
        {
            case "usa":
                for (var stripe = 0; stripe < 7; stripe++)
                {
                    Fill(image, 0, stripe * Height / 7, Width, (stripe + 1) * Height / 7, stripe % 2 == 0 ? Red : White);
                }

                Fill(image, 0, 0, Width * 2 / 5, Height * 4 / 7, Blue);
                break;
            case "korea":
                Fill(image, 0, 0, Width, Height, White);
                Circle(image, Width / 2, Height / 2, 8, Red, Blue);
                break;
            case "france":
                Vertical(image, Blue, White, Red);
                break;
            case "germany":
                Horizontal(image, Black, Red, Gold);
                break;
            case "britain":
                Fill(image, 0, 0, Width, Height, Blue);
                Fill(image, Width / 2 - 5, 0, Width / 2 + 5, Height, White);
                Fill(image, 0, Height / 2 - 5, Width, Height / 2 + 5, White);
                Fill(image, Width / 2 - 2, 0, Width / 2 + 2, Height, Red);
                Fill(image, 0, Height / 2 - 2, Width, Height / 2 + 2, Red);
                break;
            case "russia":
                Horizontal(image, White, Blue, Red);
                break;
            case "libya":
                Fill(image, 0, 0, Width, Height, Green);
                break;
            case "iraq":
                Horizontal(image, Red, White, Black);
                break;
            case "cuba":
                for (var stripe = 0; stripe < 5; stripe++)
                {
                    Fill(image, 0, stripe * Height / 5, Width, (stripe + 1) * Height / 5, stripe % 2 == 0 ? Blue : White);
                }

                for (var x = 0; x < Width / 2; x++)
                {
                    var reach = Height / 2 - x * Height / Width;
                    Fill(image, x, Height / 2 - reach, x + 1, Height / 2 + reach, Red);
                }

                break;
            default:
                return null;
        }

        var texture = ImageTexture.CreateFromImage(image);
        Cache[country] = texture;
        return texture;
    }

    private static void Horizontal(Image image, Color top, Color middle, Color bottom)
    {
        Fill(image, 0, 0, Width, Height / 3, top);
        Fill(image, 0, Height / 3, Width, Height * 2 / 3, middle);
        Fill(image, 0, Height * 2 / 3, Width, Height, bottom);
    }

    private static void Vertical(Image image, Color left, Color middle, Color right)
    {
        Fill(image, 0, 0, Width / 3, Height, left);
        Fill(image, Width / 3, 0, Width * 2 / 3, Height, middle);
        Fill(image, Width * 2 / 3, 0, Width, Height, right);
    }

    private static void Fill(Image image, int fromX, int fromY, int toX, int toY, Color color) =>
        image.FillRect(new Rect2I(fromX, fromY, toX - fromX, toY - fromY), color);

    // A disc, its upper half one colour and its lower half the other.
    private static void Circle(Image image, int cx, int cy, int radius, Color upper, Color lower)
    {
        for (var y = cy - radius; y <= cy + radius; y++)
        {
            for (var x = cx - radius; x <= cx + radius; x++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                {
                    image.SetPixel(x, y, y < cy ? upper : lower);
                }
            }
        }
    }
}
