using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>"Our base is under attack" (docs/implementation/M4_combat.md, 4.19, step 13): once in 10 seconds per player at most.</summary>
public sealed class UnderAttackTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld World() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    /// <summary>One shell of player <paramref name="shooter"/> on the target, at tick <paramref name="tick"/>.</summary>
    private static List<SimEvent> Shell(GameWorld world, Entity target, PlayerId shooter, int tick)
    {
        world.CurrentTick = tick;
        var point = target switch
        {
            Building building => Aim.CentreOf(building),
            Unit unit => unit.Position,
            _ => WorldPos.Zero,
        };
        world.Hits.Add(new Hit(new EntityId(900), new EntityId(900), shooter, TestRules.Standard.Database.IndexOfWeapon("heavy_tank_cannon"), point, target.Id));
        var events = new List<SimEvent>();
        DamageSystem.Update(world, events);
        return events;
    }

    [Fact]
    public void A_building_hit_by_an_enemy_warns_its_owner_once_in_ten_seconds()
    {
        var world = World();
        var reactor = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(10, 10), true, null);

        Assert.Equal(new UnderAttack(100, Player1, Aim.CentreOf(reactor)), Assert.Single(Shell(world, reactor, Player2, 100).OfType<UnderAttack>()));
        Assert.Empty(Shell(world, reactor, Player2, 100 + 75).OfType<UnderAttack>());                                     // 5 s later
        Assert.Single(Shell(world, reactor, Player2, 100 + DamageSystem.UnderAttackTicks).OfType<UnderAttack>());        // 10 s later
        Assert.Equal(250, world.Player(Player1).LastUnderAttack);
    }

    [Fact]
    public void Own_fire_and_hits_on_units_warn_nobody()
    {
        var world = World();
        var reactor = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(10, 10), true, null);
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(30, 30).Center);

        Assert.Empty(Shell(world, reactor, Player1, 10).OfType<UnderAttack>());
        Assert.Empty(Shell(world, tank, Player2, 20).OfType<UnderAttack>());
        Assert.Null(world.Player(Player1).LastUnderAttack);
    }
}
