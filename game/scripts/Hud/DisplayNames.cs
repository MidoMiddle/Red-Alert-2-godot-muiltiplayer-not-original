using System;

namespace Rts.Client.Hud;

/// <summary>
/// The names of units and buildings on screen. The data and the code keep our own names; while there are no models and icons, the
/// interface shows the names of RA2 instead (the owner's wish, 2026-10-24) — the keys <c>RA2_NAME_&lt;ID&gt;</c> of en.csv — unless the
/// setting turns them off. Temporary: the names of EA do not ship (docs/15_legal.md).
/// </summary>
public static class DisplayNames
{
    /// <summary>The key of the RA2 name of the unit or building <paramref name="id"/>.</summary>
    public static string KeyOf(string id) => "RA2_NAME_" + id.ToUpperInvariant();

    /// <summary>
    /// What the screen calls <paramref name="id"/>: its RA2 name when <paramref name="ra2"/> and <paramref name="translate"/> knows the
    /// key, else <paramref name="name"/>, ours.
    /// </summary>
    public static string Of(string id, string name, bool ra2, Func<string, string> translate)
    {
        if (!ra2)
        {
            return name;
        }

        var key = KeyOf(id);
        var translated = translate(key);
        return translated.Length > 0 && translated != key ? translated : name;
    }
}
