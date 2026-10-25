using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// What the tech buildings of the map give the player who took them (loose ends, stage 1; numbers of RA2): an oil derrick
/// <see cref="DerrickStartup"/> credits when taken from the neutral side, then <see cref="DerrickCash"/> every
/// <see cref="DerrickDelay"/> ticks without power; the hospital heals the soldiers that go in (<see cref="EntrySystem"/>); the airfield
/// charges its paradrop (<see cref="SuperweaponSystem"/>); the machine shop gives every vehicle of its owner, wherever it is, the
/// self-repair of RA2 — <see cref="MachineShopHeal"/> hit points a second (<see cref="RepairSystem"/>); the power plant adds
/// <see cref="PowerPlantOutput"/> to its owner's power (<see cref="PowerSystem"/>). A tech building lost or taken takes its gift along.
/// </summary>
public static class TechBuildingSystem
{
    /// <summary>RA2 <c>ProduceCashStartup</c>.</summary>
    public const int DerrickStartup = 1000;

    /// <summary>RA2 <c>ProduceCashAmount</c>.</summary>
    public const int DerrickCash = 20;

    /// <summary>RA2 <c>ProduceCashDelay</c>.</summary>
    public const int DerrickDelay = 100;

    /// <summary>The hit points a second the machine shop gives the owner's vehicles: RA2 <c>UnitsGainSelfHeal</c>, as self-repair.</summary>
    public const int MachineShopHeal = 1;

    /// <summary>The power the power plant gives its owner: a guide, between our reactors (docs/map_elements/12_tech_buildings).</summary>
    public const int PowerPlantOutput = 150;

    /// <summary>The players with a machine shop of theirs standing, as a mask of bit <c>player − 1</c>.</summary>
    public static int MachineShopOwners(GameWorld world)
    {
        var mask = 0;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Kind: BuildingKind.TechMachineShop } shop && !shop.Owner.IsNeutral)
            {
                mask |= 1 << (shop.Owner.Value - 1);
            }
        }

        return mask;
    }

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
