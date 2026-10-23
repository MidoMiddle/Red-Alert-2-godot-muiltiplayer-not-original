using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Matches;

/// <summary>
/// The starting state of a match (docs/implementation/M5_first_bot.md, 4.11, decision 1): every player of the setup plays its faction and
/// gets an MCV on its start and three conscripts nearby. A bot gets the bonuses of its level in the data (M8 step 2); a level the data
/// does not have gives none. The units are made by the world as it is built, not by commands: a replay makes
/// them again from the setup it keeps.
/// </summary>
internal static class MatchStart
{
    public const int Conscripts = 3;

    public static void Apply(GameWorld world, MatchSetup setup)
    {
        foreach (var player in setup.Players)
        {
            world.Rules.Database.TryGetFaction(player.Faction, out var faction);
            var state = world.Player(player.Id);
            state.Faction = faction;
            if (player.Kind == PlayerKind.Ai && player.Difficulty is >= AiDifficulty.Medium and <= AiDifficulty.Brutal
                && world.Rules.Database.TryGetDifficulty(Rules.DifficultyDef.IdOf(player.Difficulty), out var level))
            {
                state.Modifiers = PlayerModifiers.Of(level);
            }

            UnitTypes.TryGetById(faction.Mcv, out var mcv);
            world.SpawnUnit(player.Id, mcv, player.Start.Center);
            PlaceConscripts(world, player.Id, player.Start, faction.ConstructionYard);
        }
    }

    /// <summary>
    /// The conscripts in the nearest cells around the start where infantry can stand and there is room, ring by ring in a fixed order,
    /// outside the footprint of the yard the MCV deploys to, so that they never stand in its way.
    /// </summary>
    private static void PlaceConscripts(GameWorld world, PlayerId player, CellPos start, string yardId)
    {
        var rules = world.Rules;
        var yard = rules.Database.Buildings[rules.Database.IndexOfBuilding(yardId)];
        var firstRing = IntMath.Max(yard.Footprint.Width, yard.Footprint.Height) / 2 + 1;
        var archetype = rules.Unit(UnitType.Conscript);
        var placed = 0;
        for (var ring = firstRing; ring <= firstRing + 6 && placed < Conscripts; ring++)
        {
            for (var dy = -ring; dy <= ring && placed < Conscripts; dy++)
            {
                for (var dx = -ring; dx <= ring && placed < Conscripts; dx++)
                {
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) != ring)
                    {
                        continue;
                    }

                    var cell = new CellPos(start.X + dx, start.Y + dy);
                    while (placed < Conscripts && world.Map.Contains(cell) && world.Map.CanEnter(cell, archetype.Locomotor)
                           && world.Reservations.HasRoomFor(cell, archetype.Placement, EntityId.None))
                    {
                        world.SpawnUnit(player, UnitType.Conscript, cell.Center, snapToSubcell: true);
                        placed++;
                    }
                }
            }
        }
    }
}
