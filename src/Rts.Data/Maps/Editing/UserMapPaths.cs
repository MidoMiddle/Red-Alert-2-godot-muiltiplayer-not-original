using System.Text;

namespace Rts.Data.Maps.Editing;

/// <summary>
/// Where the editor saves (docs/implementation/M7_maps.md, 4.13): the player's maps in <see cref="UserDirectory"/>, one file
/// per id. The maps that come with the game are only read; a changed one is saved as a copy among the player's.
/// </summary>
public static class UserMapPaths
{
    public const string UserDirectory = "user://maps";

    /// <summary>The file of a map with this id.</summary>
    public static string SavePath(string id) => $"{UserDirectory}/{id}.rtsmap";

    /// <summary>A map id from its name: lowercase letters and digits, spaces and the like as single dashes; "map" when nothing is left.</summary>
    public static string IdFromName(string name)
    {
        var id = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                id.Append(c);
            }
            else if ((c is ' ' or '-' or '_') && id.Length > 0 && id[^1] != '-')
            {
                id.Append('-');
            }
        }

        var text = id.ToString().TrimEnd('-');
        return text.Length == 0 ? "map" : text;
    }
}
