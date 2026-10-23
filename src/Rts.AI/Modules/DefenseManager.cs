using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.AI.View;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The defense of the base of a bot (docs/implementation/M5_first_bot.md, 4.6, step 9), every 5 ticks:
/// <list type="bullet">
/// <item><b>the threat</b> — the armed enemy units the bot sees within <see cref="AiSettings.BaseCells"/> of a building of its own,
/// harvesters aside, as the bot has noticed them (<see cref="View.PerceptionBuffer"/>, M8 step 3) — in <see cref="AiPlan.Threat"/>:
/// the one nearest the start, which the squad at home goes for (<see cref="SquadManager"/>), their middle and their strength;</item>
/// <item><b>the defense</b> — when the base was hit lately (<see cref="PlayerState.LastUnderAttack"/>, the <c>UnderAttack</c> of M4, as the
/// bot learns it — late by its perception) and
/// the threat is stronger than the defenses, the squads at home and those called back, the strategist goes into defending
/// (<see cref="AiPlan.Defend"/>) and the squads out, the nearest first, are called back until they cover the threat; the middle of the
/// threat is kept as the side it came from (<see cref="AiPlan.AttackedFrom"/>) for a defense there;</item>
/// <item><b>safe again</b> — no enemy at the base for <see cref="AiSettings.ThreatClearTicks"/>: the threat is dropped, the strategist goes
/// back to what it did (<see cref="AiPlan.EndDefense"/>), the squads called back go home.</item>
/// </list>
/// </summary>
public sealed class DefenseManager(AiSettings settings) : IAiModule
{
    private readonly List<Squad> _out = new();
    private int _lastSeen;

    public AiModuleKind Kind => AiModuleKind.Defense;

    public void Update(AiContext context)
    {
        var plan = context.Plan;
        var tick = context.Tick;
        if (AtBase(context) is { } threat)
        {
            _lastSeen = tick;
            plan.Threat = threat;
        }
        else
        {
            if ((plan.Threat is not null || plan.Strategy == StrategyState.Defending) && tick - _lastSeen >= settings.ThreatClearTicks)
            {
                plan.EndDefense();
            }
        }

        if (plan.Threat is not { } known || !Attacked(context))
        {
            return;
        }

        var defense = Defense(context);
        if (known.Strength <= defense)
        {
            return;
        }

        plan.Defend();
        plan.AttackedFrom = known.Middle;
        Recall(plan, known, defense);
    }

    /// <summary>The armed enemy units at the base: the one nearest the start, their middle, their strength; null for none.</summary>
    private AiThreat? AtBase(AiContext context)
    {
        var view = context.View;
        var home = view.Start ?? new CellPos(view.Width / 2, view.Height / 2);
        var (strength, count, x, y) = (0, 0, 0, 0);
        CellPos? nearest = null;
        foreach (var enemy in view.VisibleEnemies)
        {
            if (enemy is not Unit { Archetype.Armed: true, Harvester: null } unit)
            {
                continue;
            }

            var cell = unit.Position.ToCell();
            if (!NearBase(view, cell))
            {
                continue;
            }

            strength += context.Memory.StrengthOf(unit);
            (count, x, y) = (count + 1, x + cell.X, y + cell.Y);
            if (nearest is null || Distance(cell, home) < Distance(nearest.Value, home))
            {
                nearest = cell;
            }
        }

        return nearest is { } at ? new AiThreat(at, new CellPos(x / count, y / count), strength) : null;
    }

    private bool NearBase(AiWorldView view, CellPos cell)
    {
        foreach (var building in view.OwnBuildings)
        {
            var dx = IntMath.Max(IntMath.Max(building.Cell.X - cell.X, cell.X - building.FarCell.X), 0);
            var dy = IntMath.Max(IntMath.Max(building.Cell.Y - cell.Y, cell.Y - building.FarCell.Y), 0);
            if (IntMath.Max(dx, dy) <= settings.BaseCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a building of the bot was hit lately.</summary>
    private bool Attacked(AiContext context) =>
        context.View.Perception.KnownAttack is { } last && context.Tick - last < settings.ThreatClearTicks + context.View.Perception.Delay;

    /// <summary>What holds the base: its defenses (cost × the share of hit points left), the squads at home and those called back.</summary>
    private static int Defense(AiContext context)
    {
        var rules = context.View.Rules.Database;
        var strength = 0;
        foreach (var building in context.View.OwnBuildings)
        {
            if (building.Weapons.Length > 0 && building.MaxHp > 0 && building.DefIndex >= 0)
            {
                strength += rules.Buildings[building.DefIndex].Cost * building.Hp / building.MaxHp;
            }
        }

        foreach (var squad in context.Plan.Squads)
        {
            if (squad.Recalled || squad.State is SquadState.Gathering or SquadState.Ready)
            {
                strength += squad.Strength;
            }
        }

        return strength;
    }

    /// <summary>Calls back the squads out, the nearest the threat first, until with <paramref name="defense"/> they cover it.</summary>
    private void Recall(AiPlan plan, AiThreat threat, int defense)
    {
        _out.Clear();
        foreach (var squad in plan.Squads)
        {
            if (!squad.Recalled && squad.State is SquadState.Moving or SquadState.Fighting)
            {
                _out.Add(squad);
            }
        }

        // Nearest first; equal ones by the number, the older first.
        _out.Sort((a, b) =>
        {
            var order = Distance(a.Centre, threat.Cell).CompareTo(Distance(b.Centre, threat.Cell));
            return order != 0 ? order : a.Number.CompareTo(b.Number);
        });
        foreach (var squad in _out)
        {
            if (defense >= threat.Strength)
            {
                return;
            }

            squad.Recalled = true;
            defense += squad.Strength;
        }
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
