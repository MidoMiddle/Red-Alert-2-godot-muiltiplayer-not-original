using System.Collections.Generic;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Map;

/// <summary>
/// What the kinds of a map are (docs/implementation/M3_base_economy.md, step 14a): its objects, bridges and buildings, made from the data
/// (<see cref="RulesDatabase.MapObjects"/>, <see cref="RulesDatabase.Bridges"/>, <see cref="RulesDatabase.MapBuildings"/>). The engine
/// keeps only the number and the id of each kind (<see cref="MapObjectTypes"/>, <see cref="BridgeTypes"/>, <see cref="BuildingTypes"/>):
/// the number is part of the rules, the rest is the data's. A record whose id the engine does not know is left out; the checks of the
/// rules report it (<see cref="GameRules.Problems"/>). Immutable.
/// </summary>
public sealed class MapTypes
{
    private readonly MapObjectType?[] _objects;
    private readonly BridgeType?[] _bridges;
    private readonly BuildingType?[] _buildings;
    private readonly SortedDictionary<string, CellSize> _ofData;

    private MapTypes(MapObjectType?[] objects, BridgeType?[] bridges, BuildingType?[] buildings, SortedDictionary<string, CellSize> ofData)
    {
        (_objects, _bridges, _buildings, _ofData) = (objects, bridges, buildings, ofData);
    }

    /// <summary>No kinds: a map of land and ore only.</summary>
    public static MapTypes Empty { get; } = new([], [], [], new SortedDictionary<string, CellSize>(System.StringComparer.Ordinal));

    /// <summary>
    /// The footprint of a building of a faction's data a map may place for nobody (2026-10-27: RA2's maps place the Neutral house's
    /// defenses and tech): any but a wall, a building on the water and one with a dock.
    /// </summary>
    public bool TryGetBuildingOfData(string id, out CellSize footprint) => _ofData.TryGetValue(id, out footprint);

    /// <summary>The kinds of map objects the data defines, in the order of their numbers.</summary>
    public IEnumerable<MapObjectType> Objects => Present(_objects);

    public IEnumerable<BridgeType> Bridges => Present(_bridges);

    public IEnumerable<BuildingType> Buildings => Present(_buildings);

    public bool IsKnown(MapObjectKind kind) => (int)kind >= 1 && (int)kind <= _objects.Length && _objects[(int)kind - 1] is not null;

    public bool IsKnown(BridgeKind kind) => (int)kind >= 1 && (int)kind <= _bridges.Length && _bridges[(int)kind - 1] is not null;

    public bool IsKnown(BuildingKind kind) => (int)kind >= 1 && (int)kind <= _buildings.Length && _buildings[(int)kind - 1] is not null;

    public MapObjectType Get(MapObjectKind kind) => IsKnown(kind)
        ? _objects[(int)kind - 1]!
        : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown map object kind: the data defines none of it.");

    public BridgeType Get(BridgeKind kind) => IsKnown(kind)
        ? _bridges[(int)kind - 1]!
        : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bridge kind: the data defines none of it.");

    public BuildingType Get(BuildingKind kind) => IsKnown(kind)
        ? _buildings[(int)kind - 1]!
        : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind: the data defines none of it.");

    /// <summary>The kind of a map file id such as <c>pine_tree</c>.</summary>
    public bool TryGetObject(string id, out MapObjectType type) => TryFind(_objects, candidate => candidate.Id == id, out type);

    public bool TryGetBridge(string id, out BridgeType type) => TryFind(_bridges, candidate => candidate.Id == id, out type);

    public bool TryGetBuilding(string id, out BuildingType type) => TryFind(_buildings, candidate => candidate.Id == id, out type);

    /// <summary>The kinds of <paramref name="database"/> the engine knows, each under its number.</summary>
    public static MapTypes From(RulesDatabase database)
    {
        var objects = new MapObjectType?[MapObjectTypes.Highest];
        foreach (var definition in database.MapObjects)
        {
            if (MapObjectTypes.TryGetKind(definition.Id, out var kind))
            {
                objects[(int)kind - 1] = new MapObjectType(
                    kind,
                    definition.Id,
                    MaskOf(definition.Blocks),
                    definition.Hp,
                    MaskOf(definition.CrushedBy),
                    definition.Stump,
                    SpeedsOf(definition.Speeds));
            }
        }

        var bridges = new BridgeType?[BridgeTypes.Highest];
        foreach (var definition in database.Bridges)
        {
            if (BridgeTypes.TryGetKind(definition.Id, out var kind))
            {
                bridges[(int)kind - 1] = new BridgeType(kind, definition.Id, definition.SegmentHp, definition.Width, definition.High);
            }
        }

        var buildings = new BuildingType?[BuildingTypes.Highest];
        foreach (var definition in database.MapBuildings)
        {
            if (BuildingTypes.TryGetKind(definition.Id, out var kind))
            {
                buildings[(int)kind - 1] = new BuildingType(
                    kind,
                    definition.Id,
                    definition.Footprint.Width,
                    definition.Footprint.Height,
                    definition.Hp,
                    definition.Garrison,
                    definition.Capturable,
                    definition.Superweapon,
                    definition.Recharge,
                    definition.Weapon,
                    definition.Armor,
                    definition.Sight,
                    definition.RepairsVehicles,
                    definition.ExtendsBase,
                    definition.DeathWeapon);
            }
        }

        var ofData = new SortedDictionary<string, CellSize>(System.StringComparer.Ordinal);
        foreach (var definition in database.Buildings)
        {
            if (!definition.Wall && !definition.Water && definition.DockCell is null)
            {
                ofData[definition.Id] = definition.Footprint;
            }
        }

        return new MapTypes(objects, bridges, buildings, ofData);
    }

    /// <summary>Whether both hold the same kinds of the map with the same values (the speeds of an object compared by content).</summary>
    public bool ContentEquals(MapTypes other)
    {
        if (_objects.Length != other._objects.Length || _bridges.Length != other._bridges.Length || _buildings.Length != other._buildings.Length)
        {
            return false;
        }

        for (var i = 0; i < _objects.Length; i++)
        {
            if (!ObjectsEqual(_objects[i], other._objects[i]))
            {
                return false;
            }
        }

        // The kinds of the map only: the buildings of the factions' data are the factions' (a test set has fewer of them).
        return System.Linq.Enumerable.SequenceEqual(_bridges, other._bridges) && System.Linq.Enumerable.SequenceEqual(_buildings, other._buildings);
    }

    private static bool ObjectsEqual(MapObjectType? a, MapObjectType? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            if (a.SpeedPercentFor(locomotor) != b.SpeedPercentFor(locomotor))
            {
                return false;
            }
        }

        return a with { Speeds = null } == b with { Speeds = null };
    }

    /// <summary>The bits of the locomotors named by id; the checks of the rules refuse an id the engine does not know.</summary>
    private static byte MaskOf(System.Collections.Immutable.ImmutableArray<string> locomotors)
    {
        byte mask = 0;
        foreach (var id in locomotors)
        {
            for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
            {
                if (LandSpeeds.IdOf(locomotor) == id)
                {
                    mask |= MapObjectTypes.Bit(locomotor);
                }
            }
        }

        return mask;
    }

    private static IReadOnlyDictionary<Locomotor, int>? SpeedsOf(System.Collections.Immutable.ImmutableArray<LocomotorSpeed> speeds)
    {
        if (speeds.IsDefaultOrEmpty)
        {
            return null;
        }

        var table = new SortedDictionary<Locomotor, int>();
        foreach (var speed in speeds)
        {
            for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
            {
                if (LandSpeeds.IdOf(locomotor) == speed.Locomotor)
                {
                    table[locomotor] = (int)IntMath.DivRound(speed.Multiplier.Raw * 100, Fixed.OneRaw);
                }
            }
        }

        return table;
    }

    private static IEnumerable<T> Present<T>(T?[] kinds)
        where T : class
    {
        foreach (var kind in kinds)
        {
            if (kind is not null)
            {
                yield return kind;
            }
        }
    }

    private static bool TryFind<T>(T?[] kinds, System.Func<T, bool> match, out T type)
        where T : class
    {
        foreach (var candidate in kinds)
        {
            if (candidate is not null && match(candidate))
            {
                type = candidate;
                return true;
            }
        }

        type = null!;
        return false;
    }
}
