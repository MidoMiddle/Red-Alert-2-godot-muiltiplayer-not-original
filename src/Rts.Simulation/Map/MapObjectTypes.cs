using System.Collections.Generic;

namespace Rts.Simulation.Map;

/// <summary>Kinds of map objects. Values are part of the rules (replays, hash): never renumber.</summary>
public enum MapObjectKind : byte
{
    PineTree = 1,
    Boulder = 2,
    FenceWood = 3,
    WallStone = 4,
    LampPost = 5,
    AntiTankHedgehog = 6,
    Pipeline = 7,
    Snowdrift = 8,
    OreSource = 9,
    GemSource = 10,

    // The props of the element documents (docs/*_elements), 2026-10-25: street, beach, desert, farm, obstacles.
    Bench = 11,
    TrashBin = 12,
    Mailbox = 13,
    PhoneBooth = 14,
    BusStop = 15,
    Billboard = 16,
    RoadSign = 17,
    TrafficLight = 18,
    Flowerbed = 19,
    StreetStall = 20,
    BeachUmbrella = 21,
    SunLounger = 22,
    BeachShower = 23,
    VolleyballNet = 24,
    FishingNets = 25,
    Driftwood = 26,
    BeachedBoat = 27,
    DryShrub = 28,
    Hedge = 29,
    HayBale = 30,
    Well = 31,
    BarbedWire = 32,
    FarmTractor = 33,
}

/// <summary>
/// What a kind of map object does (docs/implementation/M7_maps.md, 4.7; map_elements/07–09), as the data defines it
/// (<see cref="Rules.MapObjectDef"/>, <c>game/data/map/objects.yaml</c>; M3 step 14a) and <see cref="MapTypes"/> holds it. Masks have one bit per
/// <see cref="Locomotor"/> (bit = locomotor − 1): <see cref="BlockMask"/> are the movers it stops, <see cref="CrushMask"/>
/// the ones that crush it by driving in. <see cref="MaxHp"/> 0 means it cannot be destroyed. A tree leaves a stump.
/// </summary>
public sealed record MapObjectType(
    MapObjectKind Kind,
    string Id,
    byte BlockMask,
    int MaxHp,
    byte CrushMask,
    bool LeavesStump,
    IReadOnlyDictionary<Locomotor, int>? Speeds = null)
{
    public bool Destructible => MaxHp > 0;

    public bool Blocks(Locomotor locomotor) => (BlockMask & MapObjectTypes.Bit(locomotor)) != 0;

    public bool CrushedBy(Locomotor locomotor) => (CrushMask & MapObjectTypes.Bit(locomotor)) != 0;

    /// <summary>The speed this kind gives <paramref name="locomotor"/> on its cell, in percent: 100 unless the kind slows it.</summary>
    public int SpeedPercentFor(Locomotor locomotor) => Speeds is { } table && table.TryGetValue(locomotor, out var percent) ? percent : 100;
}

/// <summary>
/// The kinds of map objects the engine knows (docs/implementation/M3_base_economy.md, step 14a): each number with its data id, as
/// <see cref="World.UnitTypes"/> does for units. What a kind does is the data's (<c>game/data/map/objects.yaml</c>), held by
/// <see cref="MapTypes"/>; the number is part of the rules (replays, hash), so a new kind needs both.
/// </summary>
public static class MapObjectTypes
{
    /// <summary>Every ground mover; ships never stand where map objects are.</summary>
    public const byte AllMovers = 0b111111;

    // The ids by number, from 1.
    private static readonly string[] Ids =
    [
        "pine_tree",
        "boulder",
        "fence_wood",
        "wall_stone",
        "lamp_post",
        "anti_tank_hedgehog",
        "pipeline",
        "snowdrift",
        "ore_source",
        "gem_source",
        "bench",
        "trash_bin",
        "mailbox",
        "phone_booth",
        "bus_stop",
        "billboard",
        "road_sign",
        "traffic_light",
        "flowerbed",
        "street_stall",
        "beach_umbrella",
        "sun_lounger",
        "beach_shower",
        "volleyball_net",
        "fishing_nets",
        "driftwood",
        "beached_boat",
        "dry_shrub",
        "hedge",
        "hay_bale",
        "well",
        "barbed_wire",
        "farm_tractor",
    ];

    private static readonly MapObjectKind[] Kinds = System.Linq.Enumerable.ToArray(
        System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(1, Ids.Length), number => (MapObjectKind)number));

    /// <summary>Every kind, in the order of its number.</summary>
    public static IReadOnlyList<MapObjectKind> All => Kinds;

    /// <summary>The highest number of a kind the engine knows.</summary>
    public static int Highest => Ids.Length;

    public static bool IsKnown(MapObjectKind kind) => kind >= MapObjectKind.PineTree && (int)kind <= Ids.Length;

    /// <summary>The data id of a kind, such as <c>pine_tree</c>.</summary>
    public static string IdOf(MapObjectKind kind) => IsKnown(kind)
        ? Ids[(int)kind - 1]
        : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map object kind.");

    /// <summary>The kind the engine knows by the id <paramref name="id"/>.</summary>
    public static bool TryGetKind(string id, out MapObjectKind kind)
    {
        var index = System.Array.IndexOf(Ids, id);
        kind = (MapObjectKind)(index + 1);
        return index >= 0;
    }

    internal static byte Bit(Locomotor locomotor) => (byte)(1 << ((int)locomotor - 1));
}
