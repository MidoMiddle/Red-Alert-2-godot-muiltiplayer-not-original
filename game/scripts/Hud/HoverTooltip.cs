using System;
using System.Linq;
using Rts.Client.Units;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The tooltip of the field (the owner's wish, 2026-10-26): the cursor held for <see cref="Delay"/> over a unit or a building — the enemy's,
/// the player's own, an ally's or the map's — brings up its name by the pointer, as a tooltip of the interface does; the health bar shows
/// at once, as before. The name is the one the sidebar gives (<see cref="DisplayNames"/>). Nothing is told that the player does not see:
/// what the fog hides has no tooltip, a disguised spy is named as the soldier it passes for, a holo tank standing as a tree not at all.
/// Without Godot, for the tests; <see cref="HealthBars"/> draws it.
/// </summary>
public sealed class HoverTooltip
{
    /// <summary>How long the cursor stays on the same thing before its tooltip shows, in seconds.</summary>
    public const double Delay = 0.5;

    private EntityId? _target;
    private double _held;

    /// <summary>
    /// One frame of <paramref name="delta"/> seconds with the cursor on <paramref name="target"/> (null: on nothing with a tooltip): the
    /// entity whose tooltip shows now, or null. Another target starts the wait again.
    /// </summary>
    public EntityId? Update(EntityId? target, double delta)
    {
        if (target != _target)
        {
            _target = target;
            _held = 0;
        }
        else if (target is not null)
        {
            _held += delta;
        }

        return target is not null && _held >= Delay ? target : null;
    }

    /// <summary>
    /// What is under the cursor, as the health bars go by: the unit the pointer picked, else the building on the cell under it, whoever
    /// owns it; null for nothing, or for what <paramref name="shows"/> says the player does not see.
    /// </summary>
    public static Entity? Under(GameWorld world, EntityId? hovered, CellPos? cell, Func<Entity, bool> shows)
    {
        if (hovered is { } id && world.TryGet(id, out var unit) && unit.IsAlive)
        {
            return shows(unit) ? unit : null;
        }

        return cell is { } at && world.BuildingAt(at) is { IsAlive: true } building && shows(building) ? building : null;
    }

    /// <summary>
    /// The name of <paramref name="entity"/> on its tooltip for <paramref name="viewer"/>, or null for none: a holo tank that passes for a
    /// tree. A disguised spy is named as the soldier it passes for.
    /// </summary>
    public static string? NameOf(GameWorld world, PlayerId viewer, Entity entity, bool ra2, Func<string, string> translate)
    {
        switch (entity)
        {
            case Unit unit when DisguiseLooks.LooksLikeTree(world, viewer, unit):
                return null;
            case Unit unit:
                var shape = DisguiseLooks.PassesFor(world, viewer, unit) ?? unit.Archetype;
                var name = world.Rules.Database.TryGetUnit(shape.Id, out var def) ? def.Name : shape.Id;
                return DisplayNames.Of(shape.Id, name, ra2, translate);
            case Building { Def: { } building }:
                return DisplayNames.Of(building.Id, building.Name, ra2, translate);
            case Building map:
                // The buildings of the map have plain names (Hospital, Oil Derrick), the same either way; one without is named by its id.
                var key = DisplayNames.KeyOf(map.Type.Id);
                var text = translate(key);
                return text.Length > 0 && text != key ? text : PlainName(map.Type.Id);
            default:
                return null;
        }
    }

    /// <summary>A name made of an id of the map: <c>neutral_house_b</c> is «House B», <c>tech_outpost</c> «Outpost».</summary>
    public static string PlainName(string id)
    {
        var words = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var start = words.Length > 1 && words[0] is "neutral" or "tech" or "civilian" ? 1 : 0;
        return string.Join(' ', words.Skip(start).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
