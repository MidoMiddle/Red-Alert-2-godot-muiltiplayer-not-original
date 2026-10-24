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
/// What a kind of map object does (docs/implementation/M7_maps.md, 4.7; map_elements/07–09). Masks have one bit per
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
/// The map objects of the rules so far (winter and industrial sets, the ore and gem sources, the props of the element documents), kept
/// in code until the data files of M3 (map_objects.yaml) like the land table. Hit points are a rough guide until the balance of M4: the RA2 numbers for map
/// objects are not confirmed in our documents.
/// </summary>
public static class MapObjectTypes
{
    /// <summary>Every ground mover; ships never stand where map objects are.</summary>
    public const byte AllMovers = 0b111111;

    /// <summary>Tracks and wheels: what a hedgehog or a pipe stops. Foot units pass; hover and amphibious units pass too (an open question).</summary>
    private static readonly byte TrackAndWheel = (byte)(Bit(Locomotor.Track) | Bit(Locomotor.Wheel));

    /// <summary>A snowdrift slows foot, tracks and wheels; the rest drive at full speed (docs/map_elements/20_snowdrift).</summary>
    private static readonly IReadOnlyDictionary<Locomotor, int> SnowdriftSpeeds = new Dictionary<Locomotor, int>
    {
        [Locomotor.Foot] = 60,
        [Locomotor.Track] = 80,
        [Locomotor.Wheel] = 50,
    };

    private static readonly MapObjectType[] Types =
    [
        new(MapObjectKind.PineTree, "pine_tree", AllMovers, MaxHp: 400, CrushMask: 0, LeavesStump: true),
        new(MapObjectKind.Boulder, "boulder", AllMovers, MaxHp: 0, CrushMask: 0, LeavesStump: false),
        // A fence stops infantry and wheels (and the other light movers); tracks drive through and flatten it.
        new(MapObjectKind.FenceWood, "fence_wood", (byte)(AllMovers & ~(1 << ((int)Locomotor.Track - 1))), MaxHp: 150, CrushMask: 1 << ((int)Locomotor.Track - 1), LeavesStump: false),
        new(MapObjectKind.WallStone, "wall_stone", AllMovers, MaxHp: 1500, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.LampPost, "lamp_post", 0, MaxHp: 50, CrushMask: AllMovers, LeavesStump: false),
        new(MapObjectKind.AntiTankHedgehog, "anti_tank_hedgehog", TrackAndWheel, MaxHp: 400, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.Pipeline, "pipeline", TrackAndWheel, MaxHp: 50, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.Snowdrift, "snowdrift", 0, MaxHp: 0, CrushMask: Bit(Locomotor.Track), LeavesStump: false, Speeds: SnowdriftSpeeds),
        // The drill towers of ore and gem fields (M3 step 16): in everyone's way, never destroyed; OreSourceSystem makes them erupt.
        new(MapObjectKind.OreSource, "ore_source", AllMovers, MaxHp: 0, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.GemSource, "gem_source", AllMovers, MaxHp: 0, CrushMask: 0, LeavesStump: false),

        // The element documents (2026-10-25): what blocks and what a vehicle drives over by section 3 of each; small things of the
        // street, the beach and the farm a vehicle crushes, a hedge, a well or a billboard stops everyone, barbed wire stops all but
        // tracks, which flatten it (RA2).
        new(MapObjectKind.Bench, "bench", 0, MaxHp: 50, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.TrashBin, "trash_bin", 0, MaxHp: 40, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.Mailbox, "mailbox", 0, MaxHp: 40, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.PhoneBooth, "phone_booth", AllMovers, MaxHp: 100, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.BusStop, "bus_stop", 0, MaxHp: 100, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.Billboard, "billboard", AllMovers, MaxHp: 150, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.RoadSign, "road_sign", 0, MaxHp: 30, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.TrafficLight, "traffic_light", AllMovers, MaxHp: 200, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.Flowerbed, "flowerbed", 0, MaxHp: 50, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.StreetStall, "street_stall", TrackAndWheel, MaxHp: 80, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.BeachUmbrella, "beach_umbrella", 0, MaxHp: 30, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.SunLounger, "sun_lounger", 0, MaxHp: 30, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.BeachShower, "beach_shower", 0, MaxHp: 50, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.VolleyballNet, "volleyball_net", 0, MaxHp: 40, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.FishingNets, "fishing_nets", 0, MaxHp: 20, CrushMask: TrackAndWheel, LeavesStump: false),
        new(MapObjectKind.Driftwood, "driftwood", 0, MaxHp: 50, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.BeachedBoat, "beached_boat", AllMovers, MaxHp: 300, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.DryShrub, "dry_shrub", 0, MaxHp: 30, CrushMask: AllMovers, LeavesStump: false),
        new(MapObjectKind.Hedge, "hedge", AllMovers, MaxHp: 300, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.HayBale, "hay_bale", AllMovers, MaxHp: 100, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.Well, "well", AllMovers, MaxHp: 1500, CrushMask: 0, LeavesStump: false),
        new(MapObjectKind.BarbedWire, "barbed_wire", (byte)(AllMovers & ~Bit(Locomotor.Track)), MaxHp: 100, CrushMask: Bit(Locomotor.Track), LeavesStump: false),
        new(MapObjectKind.FarmTractor, "farm_tractor", AllMovers, MaxHp: 400, CrushMask: 0, LeavesStump: false),
    ];

    public static IReadOnlyList<MapObjectType> All => Types;

    public static MapObjectType Get(MapObjectKind kind) =>
        IsKnown(kind)
            ? Types[(int)kind - 1]
            : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map object kind.");

    public static bool IsKnown(MapObjectKind kind) => kind >= MapObjectKind.PineTree && (int)kind <= Types.Length;

    /// <summary>The type of a map file id such as <c>pine_tree</c>.</summary>
    public static bool TryGetById(string id, out MapObjectType type)
    {
        foreach (var candidate in Types)
        {
            if (candidate.Id == id)
            {
                type = candidate;
                return true;
            }
        }

        type = null!;
        return false;
    }

    internal static byte Bit(Locomotor locomotor) => (byte)(1 << ((int)locomotor - 1));
}
