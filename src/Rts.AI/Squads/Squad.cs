using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Squads;

/// <summary>
/// A squad of a bot (docs/implementation/M5_first_bot.md, 4.6, step 8): its units, what it does, where it goes. Only
/// <see cref="Modules.SquadManager"/> changes it; the strategist, the defense and the debug overlay read it.
/// </summary>
public sealed class Squad
{
    private readonly List<EntityId> _units = new();

    internal Squad(int number) => Number = number;

    /// <summary>The number of the squad, from 1, in the order they were formed.</summary>
    public int Number { get; }

    public SquadState State { get; internal set; }

    /// <summary>Its units, in the order they joined.</summary>
    public IReadOnlyList<EntityId> Units => _units;

    /// <summary>The enemy building it attacks, from the memory; null when it goes to <see cref="TargetCell"/> only.</summary>
    public EntityId? TargetBuilding { get; internal set; }

    /// <summary>Where it goes: the cell of its target building, else a place where the enemy may be; null without a target.</summary>
    public CellPos? TargetCell { get; internal set; }

    /// <summary>Its strength when it set out: it turns back below <see cref="AiSettings.RetreatAtStrengthPercent"/> of this.</summary>
    public int StartStrength { get; internal set; }

    /// <summary>The middle of its units on the last turn of the squads.</summary>
    public CellPos Centre { get; internal set; }

    /// <summary>Its strength on the last turn of the squads: Σ cost × hit points / most hit points.</summary>
    public int Strength { get; internal set; }

    /// <summary>Whether it turned back for its losses, not because nothing was left to attack.</summary>
    public bool Beaten { get; internal set; }

    /// <summary>Where a beaten squad falls back to instead of the gathering point: a defense of its own, for «Brutal» (M8 step 5).</summary>
    public CellPos? RetreatPoint { get; internal set; }

    /// <summary>Where it helps an ally whose base is attacked (M8 step 11); null for a squad of the bot's own.</summary>
    public CellPos? HelpCell { get; internal set; }

    /// <summary>Called back by the defense to fight the threat at the base (step 9).</summary>
    public bool Recalled { get; internal set; }

    /// <summary>Where it comes together while some of it lag behind; null when it does not wait.</summary>
    public CellPos? Regroup { get; internal set; }

    /// <summary>The tick the waiting for the laggards ends at the latest (or, with no <see cref="Regroup"/>, before which no new one starts).</summary>
    internal int RegroupUntil { get; set; }

    /// <summary>The places it swept for an enemy it knows nothing of (step 14), so it does not come back to them.</summary>
    internal List<CellPos> Swept { get; } = new();

    internal void Add(EntityId unit) => _units.Add(unit);

    internal void AddAll(Squad other) => _units.AddRange(other._units);

    internal int RemoveAll(System.Predicate<EntityId> match) => _units.RemoveAll(match);
}
