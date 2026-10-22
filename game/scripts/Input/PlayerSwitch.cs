using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>
/// The debug switch of the player at the keyboard (docs/implementation/M4_combat.md, 4.20, step 13; F11 in debug builds): the next
/// player after <paramref name="current"/>, round from 8 to 1, that has a unit or a building; the current one when nobody else has.
/// Pure, tested in Rts.Simulation.Tests.
/// </summary>
public static class PlayerSwitch
{
    public static PlayerId Next(GameWorld world, PlayerId current)
    {
        for (var step = 1; step < PlayerId.MaxPlayers; step++)
        {
            var candidate = new PlayerId((current.Value - 1 + step) % PlayerId.MaxPlayers + 1);
            foreach (var entity in world.Entities)
            {
                if (entity.IsAlive && entity.Owner == candidate && entity is Unit or Building)
                {
                    return candidate;
                }
            }
        }

        return current;
    }
}
