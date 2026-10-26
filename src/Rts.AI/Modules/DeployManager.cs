using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The deploying of a bot (M11 step 3, the bot that knows every weapon): its soldiers that fight better deployed (D) do it where it pays.
/// <list type="bullet">
/// <item><b>Digging in</b> (a weapon when deployed, <see cref="UnitDef.DeployedWeapon"/>: the GI behind sandbags): a unit standing or
/// fighting — not on its way, on no errand — digs in when an armed enemy in sight its deployed weapon can hurt is within its reach. Any order of the
/// squads gets it up again (the rules undeploy a unit given an order), so it moves on with them.</item>
/// <item><b>A blast when deployed</b> (<see cref="UnitDef.DeployWeapon"/>: the Desolator's poison, Yuri's psychic wave, once with
/// <see cref="UnitDef.DeployOnce"/>): the unit deploys when at least <see cref="FewestVictims"/> enemy soldiers in sight stand within the
/// reach of the blast — its weapon's range or its warhead's spread, the greater — and none of its own player's or its allies' soldiers,
/// whom it would hurt too, but those of its own kind its warhead spares.</item>
/// </list>
/// A deployed unit is not deployed again: a second order would get it up.
/// </summary>
public sealed class DeployManager : IAiModule
{
    /// <summary>The fewest enemy soldiers a blast is let off for.</summary>
    public const int FewestVictims = 2;

    public AiModuleKind Kind => AiModuleKind.Deploys;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules;
        foreach (var unit in view.OwnUnits)
        {
            var archetype = unit.Archetype;
            // On its way and not fighting it does not stop to deploy; a unit of an attack-move that fights does.
            if (unit.Deployed || (unit.MoveTarget is not null && !unit.Engaged) || unit.EnterTarget is not null || context.Plan.Errands.Contains(unit.Id.Value)
                || (archetype.DeployedWeapon < 0 && archetype.DeployWeapon < 0))
            {
                continue;
            }

            var deploy = archetype.DeployedWeapon >= 0
                ? DigsIn(context, unit, rules.Database.Weapons[archetype.DeployedWeapon])
                : Blasts(context, unit, rules.Database.Weapons[archetype.DeployWeapon]);
            if (deploy)
            {
                context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), unit.Id));
                context.Log?.Add(context.Tick, context.Module, $"{archetype.Id} deploys at {unit.Position.ToCell().X},{unit.Position.ToCell().Y}");
            }
        }
    }

    /// <summary>Whether an armed enemy in sight that <paramref name="weapon"/> hurts is within its reach of <paramref name="unit"/>.</summary>
    private static bool DigsIn(AiContext context, Unit unit, WeaponDef weapon)
    {
        var rules = context.View.Rules;
        var reach = weapon.Range.ToIntFloor();
        var here = unit.Position.ToCell();
        var warhead = rules.Database.IndexOfWarhead(weapon.Warhead);
        foreach (var enemy in context.View.VisibleEnemies)
        {
            var armed = enemy switch
            {
                Unit { Archetype.Armed: true, Harvester: null } => true,
                Building building => building.Weapons.Length > 0,
                _ => false,
            };
            var armor = enemy is Building target ? rules.ArmorOf(target) : ((Unit)enemy).Archetype.Armor;
            if (armed && warhead >= 0 && Distance(here, CellOf(enemy, here)) <= reach && (weapon.Targets & TargetingSystem.LayerOf(enemy)) != 0
                && rules.Verses(warhead, armor) > Fixed.Zero)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether the blast of <paramref name="weapon"/> where <paramref name="unit"/> stands takes enough enemy soldiers and none of its own.</summary>
    private static bool Blasts(AiContext context, Unit unit, WeaponDef weapon)
    {
        var view = context.View;
        var database = view.Rules.Database;
        var warhead = database.IndexOfWarhead(weapon.Warhead) is >= 0 and var index ? database.Warheads[index] : null;
        var reach = IntMath.Max(weapon.Range.ToIntFloor(), warhead?.Spread.ToIntFloor() ?? 0);
        var here = unit.Position.ToCell();
        var victims = 0;
        foreach (var enemy in view.VisibleEnemies)
        {
            victims += enemy is Unit { Archetype.Category: UnitCategory.Infantry, Archetype.Flies: false } soldier
                && Distance(here, soldier.Position.ToCell()) <= reach ? 1 : 0;
        }

        if (victims < FewestVictims)
        {
            return false;
        }

        foreach (var friend in view.OwnUnits)
        {
            if (Hurts(friend, unit, warhead) && Distance(here, friend.Position.ToCell()) <= reach)
            {
                return false;
            }
        }

        foreach (var friend in view.Allied)
        {
            if (friend is Unit ally && Hurts(ally, unit, warhead) && Distance(here, ally.Position.ToCell()) <= reach)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the blast of <paramref name="unit"/> hurts <paramref name="friend"/>: another soldier, not of its kind if the warhead spares it.</summary>
    private static bool Hurts(Unit friend, Unit unit, WarheadDef? warhead) =>
        friend != unit && friend.Archetype.Category == UnitCategory.Infantry && !friend.Archetype.Flies
        && !(warhead is { SparesOwnKind: true } && friend.Type == unit.Type);

    private static CellPos CellOf(Entity enemy, CellPos from) => enemy is Building building
        ? new CellPos(IntMath.Clamp(from.X, building.Cell.X, building.FarCell.X), IntMath.Clamp(from.Y, building.Cell.Y, building.FarCell.Y))
        : ((Unit)enemy).Position.ToCell();

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
