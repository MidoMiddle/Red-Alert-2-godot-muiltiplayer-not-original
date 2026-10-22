namespace Rts.Simulation.Combat;

/// <summary>
/// How hurt a unit or a building is (docs/implementation/M4_combat.md, 4.4): the client colours the health bar and lights smoke and fire
/// by it. Values go into events: never renumber.
/// </summary>
public enum DamageState : byte
{
    /// <summary>More than half of its hit points.</summary>
    Healthy = 1,

    /// <summary>A quarter to a half.</summary>
    Damaged = 2,

    /// <summary>Less than a quarter.</summary>
    Critical = 3,

    /// <summary>None left.</summary>
    Destroyed = 4,
}

public static class DamageStates
{
    /// <summary>
    /// The state at <paramref name="hp"/> of <paramref name="maxHp"/>, in whole numbers: more than half (<c>hp × 2 &gt; max</c>) is
    /// healthy, less than a quarter (<c>hp × 4 &lt; max</c>) critical, between them damaged, nothing left destroyed.
    /// </summary>
    public static DamageState Of(int hp, int maxHp)
    {
        if (hp <= 0)
        {
            return DamageState.Destroyed;
        }

        if ((long)hp * 2 > maxHp)
        {
            return DamageState.Healthy;
        }

        return (long)hp * 4 < maxHp ? DamageState.Critical : DamageState.Damaged;
    }
}
