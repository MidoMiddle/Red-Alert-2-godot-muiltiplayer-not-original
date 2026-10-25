namespace Rts.Simulation.World;

/// <summary>A crate on the map (M11 a6, <see cref="Systems.CrateSystem"/>): its number, in the order crates came, and its cell.</summary>
public sealed record Crate(int Id, Geometry.CellPos Cell);

/// <summary>What the crates gave a unit for good (M11 a6): Armor, Firepower, Speed; they do not add up, a second of one is nothing.</summary>
[System.Flags]
public enum CrateUpgrade : byte
{
    None = 0,
    Armor = 1,
    Firepower = 2,
    Speed = 4,
}
