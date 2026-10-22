using System.Collections.Generic;

namespace Rts.Simulation.World;

/// <summary>Kinds of buildings. Values are part of the rules (replays, hash): never renumber.</summary>
public enum BuildingKind : byte
{
    NeutralShed = 1,
    NeutralHouseA = 2,
    NeutralApartment = 3,
    TechOilDerrick = 4,
    TechHospital = 5,
    TechAirfield = 6,
}

/// <summary>
/// What a kind of building is (docs/implementation/M7_maps.md, 4.8): its footprint of <see cref="Width"/> × <see cref="Height"/>
/// cells from the north-west corner, its hit points, how many soldiers it would hold and whether an engineer would take it.
/// Garrisons and capture are M11: the fields are here, the logic is not.
/// </summary>
public sealed record BuildingType(BuildingKind Kind, string Id, int Width, int Height, int MaxHp, int Garrison, bool Capturable);

/// <summary>
/// The neutral buildings and tech buildings of the winter set, kept in code until the data files of M3 (game/data/neutral/).
/// Hit points are a rough guide until the balance of M4.
/// </summary>
public static class BuildingTypes
{
    private static readonly BuildingType[] Types =
    [
        new(BuildingKind.NeutralShed, "neutral_shed", 1, 1, MaxHp: 300, Garrison: 0, Capturable: false),
        new(BuildingKind.NeutralHouseA, "neutral_house_a", 2, 2, MaxHp: 800, Garrison: 3, Capturable: false),
        new(BuildingKind.NeutralApartment, "neutral_apartment", 3, 3, MaxHp: 1500, Garrison: 10, Capturable: false),
        new(BuildingKind.TechOilDerrick, "tech_oil_derrick", 2, 2, MaxHp: 1000, Garrison: 0, Capturable: true),
        new(BuildingKind.TechHospital, "tech_hospital", 3, 3, MaxHp: 1500, Garrison: 0, Capturable: true),
        new(BuildingKind.TechAirfield, "tech_airfield", 4, 3, MaxHp: 2000, Garrison: 0, Capturable: true),
    ];

    public static IReadOnlyList<BuildingType> All => Types;

    public static bool IsKnown(BuildingKind kind) => kind >= BuildingKind.NeutralShed && kind <= BuildingKind.TechAirfield;

    public static BuildingType Get(BuildingKind kind) =>
        IsKnown(kind) ? Types[(int)kind - 1] : throw new System.ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");

    /// <summary>The type of a map file id such as <c>neutral_house_a</c>.</summary>
    public static bool TryGetById(string id, out BuildingType type)
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
}
