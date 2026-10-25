using System.Collections.Immutable;
using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>What a crate gives (M11 a6; RA2 <c>[Powerups]</c>), rolled by weight when a unit picks it up.</summary>
public enum CrateKind : byte
{
    Money = 1,
    Heal = 2,
    Veteran = 3,
    Reveal = 4,
    Armor = 5,
    Firepower = 6,
    Speed = 7,
    Unit = 8,
}

/// <summary>
/// The crates of a match with the option «Crates Appear» (docs/implementation/M11_content.md, a6; the one <c>crates</c> record of the
/// data, <see cref="Default"/> without one): how many lie on the map, how soon a new one comes, what each bonus is worth and how often
/// it falls. The numbers are those of RA2's <c>[Powerups]</c> as a guide.
/// </summary>
public sealed record CratesDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Crates on the map at most: this many for every player of the match …</summary>
    public required int PerPlayer { get; init; }

    /// <summary>… and never fewer than this.</summary>
    public required int AtLeast { get; init; }

    /// <summary>Ticks after a crate is taken before another comes, while there are fewer than the most.</summary>
    public required int Regen { get; init; }

    /// <summary>The cells round the crate whose units of the picker get Veteran, Armor, Firepower and Speed.</summary>
    public required int Radius { get; init; }

    /// <summary>The credits of the Money crate.</summary>
    public required int Money { get; init; }

    /// <summary>The weight of every kind, by <see cref="CrateKind"/> − 1; a kind of weight 0 never falls.</summary>
    public required ImmutableArray<int> Weights { get; init; }

    /// <summary>A unit with Armor takes the damage divided by this.</summary>
    public required Fixed Armor { get; init; }

    /// <summary>A unit with Firepower deals the damage times this.</summary>
    public required Fixed Firepower { get; init; }

    /// <summary>A unit with Speed moves this times faster.</summary>
    public required Fixed Speed { get; init; }

    /// <summary>The units the Unit crate may give; the picker gets one of its faction's.</summary>
    public required ImmutableArray<string> Units { get; init; }

    /// <summary>The crates of rules without a <c>crates</c> record (the rules the tests build): the numbers of the game's data.</summary>
    public static readonly CratesDef Default = new()
    {
        Id = "crates",
        Name = "Crates",
        PerPlayer = 1,
        AtLeast = 2,
        Regen = 60 * SimulationSettings.DefaultTicksPerSecond,
        Radius = 4,
        Money = 2000,
        Weights = [20, 10, 20, 10, 10, 10, 10, 10],
        Armor = Fixed.FromRatio(3, 2),
        Firepower = Fixed.FromInt(2),
        Speed = Fixed.FromRatio(6, 5),
        Units = ["heavy_tank", "support_vehicle", "conscript", "alliance_light_tank", "alliance_rifleman"],
    };
}
