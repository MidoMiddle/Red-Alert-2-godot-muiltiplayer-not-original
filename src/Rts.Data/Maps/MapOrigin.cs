using System;
using System.Collections.Immutable;

namespace Rts.Data.Maps;

/// <summary>
/// Where a map comes from (the header's <c>origin</c>): the menus filter by it and show it, so it is plain which maps are kept and
/// which may go. <see cref="Ra2"/> a map of Red Alert 2 carried over (tools/ra2_import), <see cref="Ready"/> one made by hand (the
/// ready maps of M7 step 14), <see cref="Generated"/> one the random map generator made, <see cref="Test"/> a test ground,
/// <see cref="User"/> one saved in the editor. Not read by the rules.
/// </summary>
public static class MapOrigin
{
    public const string Ra2 = "ra2";
    public const string Ready = "ready";
    public const string Generated = "generated";
    public const string Test = "test";
    public const string User = "user";

    /// <summary>A map without the key: one saved in the editor before there was one.</summary>
    public const string Default = User;

    /// <summary>In the order the menus offer them.</summary>
    public static readonly ImmutableArray<string> Ids = [Ra2, Ready, Generated, Test, User];

    public static bool IsKnown(string id) => Ids.IndexOf(id, 0, Ids.Length, StringComparer.Ordinal) >= 0;
}
