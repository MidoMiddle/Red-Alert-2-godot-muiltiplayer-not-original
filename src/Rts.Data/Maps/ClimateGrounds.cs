using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Map;

namespace Rts.Data.Maps;

/// <summary>
/// What the ground of a climate is made of, by role (the climates of RA2: winter — its Snow —, temperate, urban, desert): the plain
/// ground, two kinds of patches over it, rough land, the road and the pavement beside it in a town, the shore, the rock, the water and
/// the ice. The generator paints by these roles, a map takes its climate's legend, the editor carries the texture over to another
/// climate role by role. The materials are the ids of <c>game/data/climates/&lt;id&gt;/look.yaml</c>. Visual only.
/// </summary>
public sealed record ClimateGround(
    string Id,
    string Plain,
    string PatchA,
    string PatchB,
    string Rough,
    string Road,
    string Sidewalk,
    string Shore,
    string Rock,
    string Water,
    string Ice,
    ImmutableArray<TextureEntry> Legend)
{
    /// <summary>The material of a role, in the order of <see cref="ClimateGrounds.Roles"/>.</summary>
    internal string Of(int role) => role switch
    {
        0 => Plain,
        1 => PatchA,
        2 => PatchB,
        3 => Rough,
        4 => Road,
        5 => Sidewalk,
        6 => Shore,
        7 => Rock,
        8 => Water,
        _ => Ice,
    };
}

public static class ClimateGrounds
{
    /// <summary>The number of roles of a ground (<see cref="ClimateGround.Of"/>).</summary>
    internal const int Roles = 10;

    public const string Winter = "winter";
    public const string Temperate = "temperate";
    public const string Urban = "urban";
    public const string Desert = "desert";

    /// <summary>The climates there are, winter first.</summary>
    public static readonly ImmutableArray<string> Ids = [Winter, Temperate, Urban, Desert];

    private static readonly Dictionary<string, ClimateGround> Grounds = new(StringComparer.Ordinal)
    {
        [Winter] = new(Winter, "snow", "dirt", "grass", "dirt", "asphalt", "asphalt", "shore", "rock", "water", "ice", TestMapConverter.Legend),
        [Temperate] = new(Temperate, "grass", "dirt", "field", "dirt", "asphalt", "asphalt", "shore", "rock", "water", "water",
        [
            new('a', "asphalt"), new('b', "shore"), new('d', "dirt"), new('f', "field"), new('g', "grass"), new('k', "rock"), new('w', "water"),
        ]),
        [Urban] = new(Urban, "grass", "dirt", "pavers", "dirt", "asphalt", "slabs", "shore", "rock", "water", "water",
        [
            new('a', "asphalt"), new('b', "shore"), new('d', "dirt"), new('g', "grass"), new('k', "rock"), new('l', "slabs"), new('p', "pavers"),
            new('w', "water"),
        ]),
        [Desert] = new(Desert, "sand", "clay", "gravel", "gravel", "asphalt", "asphalt", "shore", "rock", "water", "water",
        [
            new('a', "asphalt"), new('b', "shore"), new('c', "clay"), new('k', "rock"), new('n', "sand"), new('v', "gravel"), new('w', "water"),
        ]),
    };

    public static bool IsKnown(string climate) => Grounds.ContainsKey(climate);

    /// <summary>The ground of a climate; winter's for one there is none of.</summary>
    public static ClimateGround Of(string climate) => Grounds.TryGetValue(climate, out var ground) ? ground : Grounds[Winter];

    /// <summary>The material of a cell of <paramref name="land"/> that has none of its own in the climate.</summary>
    public static string MaterialOf(string climate, LandType land)
    {
        var ground = Of(climate);
        return land switch
        {
            LandType.Rough => ground.Rough,
            LandType.Road => ground.Road,
            LandType.Beach => ground.Shore,
            LandType.Shallow or LandType.Deep => ground.Water,
            LandType.Rock => ground.Rock,
            LandType.Ice => ground.Ice,
            _ => ground.Plain,
        };
    }

    /// <summary>
    /// The material of the climate <paramref name="to"/> that plays the role <paramref name="material"/> plays in <paramref name="from"/>;
    /// its plain ground for one that plays none.
    /// </summary>
    public static string Translate(string material, string from, string to)
    {
        var source = Of(from);
        var target = Of(to);
        for (var role = 0; role < Roles; role++)
        {
            if (source.Of(role) == material)
            {
                return target.Of(role);
            }
        }

        return target.Plain;
    }
}
