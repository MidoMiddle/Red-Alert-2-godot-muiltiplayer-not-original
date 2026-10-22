using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Rts.Client.Maps;

/// <summary>
/// Previews for the screens (docs/implementation/M7_maps.md, 4.11, step 10): from the cache
/// (<see cref="MapPreview.CachePath"/>) when the map's hash has one, otherwise drawn by <see cref="MapPreview"/> on a worker
/// thread, saved to the cache and handed back on the main thread. Nothing of it goes into the repository. Also reads the map
/// files of <see cref="MapCatalog.Directory"/> for the catalog.
/// </summary>
public static class MapPreviews
{
    /// <summary>The texture of a cached preview, or null when there is none yet.</summary>
    public static ImageTexture? TryLoad(MapEntry entry)
    {
        var path = MapPreview.CachePath(entry.Hash);
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        var image = Image.LoadFromFile(path);
        return image is null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    /// <summary>Draws the preview in the background, stores it in the cache and returns it (the await resumes on the main thread).</summary>
    public static async Task<ImageTexture> RenderAsync(MapEntry entry)
    {
        var pixels = await Task.Run(() => MapPreview.Render(entry.Map));
        var image = Image.CreateFromData(
            entry.Map.Width * MapPreview.PixelsPerCell, entry.Map.Height * MapPreview.PixelsPerCell, false, Image.Format.Rgb8, pixels);
        DirAccess.MakeDirRecursiveAbsolute(MapPreview.CacheDirectory);
        var saved = image.SavePng(MapPreview.CachePath(entry.Hash));
        if (saved != Error.Ok)
        {
            GD.PushWarning($"Map preview of {entry.File} not cached: {saved}.");
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Every <c>.rtsmap</c> file with its text: the maps of the game (<see cref="MapCatalog.Directory"/>), then the player's own
    /// (<see cref="MapCatalog.UserDirectory"/>, the map editor saves there), each in name order.
    /// </summary>
    public static List<(string File, string Text)> ReadMapFiles()
    {
        var files = new List<(string File, string Text)>();
        foreach (var directory in new[] { MapCatalog.Directory, MapCatalog.UserDirectory })
        {
            if (!DirAccess.DirExistsAbsolute(directory))
            {
                continue;
            }

            var names = DirAccess.GetFilesAt(directory);
            System.Array.Sort(names, System.StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (name.EndsWith(".rtsmap", System.StringComparison.Ordinal))
                {
                    var path = $"{directory}/{name}";
                    files.Add((path, FileAccess.GetFileAsString(path)));
                }
            }
        }

        return files;
    }
}
