using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// What the tech buildings of the map give the player who took them (loose ends, stage 1; numbers of RA2): an oil derrick
/// <see cref="DerrickStartup"/> credits when taken from the neutral side, then <see cref="DerrickCash"/> every
/// <see cref="DerrickDelay"/> ticks without power; the hospital heals the soldiers that go in (<see cref="EntrySystem"/>); the airfield
/// charges its paradrop (<see cref="SuperweaponSystem"/>).
/// </summary>
public static class TechBuildingSystem
{
    /// <summary>RA2 <c>ProduceCashStartup</c>.</summary>
    public const int DerrickStartup = 1000;

    /// <summary>RA2 <c>ProduceCashAmount</c>.</summary>
    public const int DerrickCash = 20;

    /// <summary>RA2 <c>ProduceCashDelay</c>.</summary>
    public const int DerrickDelay = 100;

    /// <summary>The derricks of the players pay.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Kind: BuildingKind.TechOilDerrick, CashTimer: > 0 } derrick && !derrick.Owner.IsNeutral
                && --derrick.CashTimer == 0)
            {
                world.Player(derrick.Owner).Credits += DerrickCash;
                derrick.CashTimer = DerrickDelay;
            }
        }
    }

    /// <summary>A building of the map changed hands from <paramref name="from"/>: what its kind gives starts for the new owner.</summary>
    internal static void Taken(GameWorld world, Building building, PlayerId from)
    {
        if (building.Owner.IsNeutral)
        {
            building.CashTimer = 0;
            building.ChargeLeft = 0;
            return;
        }

        if (building.Kind == BuildingKind.TechOilDerrick)
        {
            if (from.IsNeutral)
            {
                world.Player(building.Owner).Credits += DerrickStartup;
            }

            building.CashTimer = DerrickDelay;
        }

        building.ChargeLeft = building.Type.Recharge;
    }
}
