using Rts.Simulation;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The buildings of the factions a map places for nobody (2026-10-27, the maps of RA2): an armed one fires at every player and every
/// player's units at it (the owner's choice; <see cref="Hostility"/>), an engineer takes the ones the data lets him — the nuclear
/// reactor, a construction yard — and not the defenses, the superweapons or the cloning vats; nobody's superweapon never charges. The
/// civilian bunker of RA2's maps holds eight soldiers. By the game data; two people play, no bot.
/// </summary>
public sealed class NeutralDefenseTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos Bunker = new(30, 50);

    private static GameSimulation Match(bool fog = false)
    {
        var land = new byte[Size * Size];
        Array.Fill(land, (byte)LandType.Clear);
        var map = MapGrid.FromLayers(Size, Size, land, new byte[land.Length], default, null, [],
            [
                new BuildingState(BuildingKind.NeutralHouseA, new CellPos(26, 26), PlayerId.Neutral, 0),
                new BuildingState(BuildingKind.NeutralCivilianBunker, Bunker, PlayerId.Neutral, 0),
            ], types: GameDataFiles.Rules.MapTypes);
        return new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "neutral defense",
                    111,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: fog, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 4)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 4)]),
                map),
            GameDataFiles.Rules);
    }

    private static Building Nobody(GameWorld world, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(PlayerId.Neutral, def, new CellPos(x, y), ready: true, null);
    }

    private static Unit Spawn(GameWorld world, PlayerId owner, UnitType type, int x, int y) =>
        world.SpawnUnit(owner, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void A_prism_tower_of_nobody_fires_at_the_units_of_both_players_and_not_at_a_house_of_nobody()
    {
        var simulation = Match();
        var world = simulation.World;
        var tower = Nobody(world, "alliance_prism_tower", 30, 30);
        var soviet = Spawn(world, Union, UnitType.HeavyTank, 36, 30);
        var allied = Spawn(world, Allied, UnitType.AllianceLightTank, 30, 36);
        var house = world.BuildingAt(new CellPos(26, 26))!;

        Run(simulation, 300);

        Assert.True(Hostility.IsHostileNeutral(tower));
        Assert.True(soviet.Hp < soviet.MaxHp, "the Soviet tank was hit");
        Assert.True(allied.Hp < allied.MaxHp, "the allied tank was hit");
        Assert.Equal(house.MaxHp, house.Hp);
    }

    [Fact]
    public void A_tesla_coil_of_nobody_fires_with_no_power_of_its_own_and_through_the_fog()
    {
        var simulation = Match(fog: true);
        var world = simulation.World;
        Nobody(world, "tesla_coil", 30, 30);
        var tank = Spawn(world, Union, UnitType.HeavyTank, 34, 30);

        Run(simulation, 300);

        Assert.True(tank.Hp < tank.MaxHp);
    }

    [Fact]
    public void A_disguised_spy_walks_past_a_pillbox_of_nobody_and_a_soldier_does_not()
    {
        var simulation = Match();
        var world = simulation.World;
        Nobody(world, "alliance_pillbox", 30, 30);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 32, 30);
        var conscript = Spawn(world, Union, UnitType.Conscript, 30, 32);

        Run(simulation, 200);

        Assert.Equal(spy.MaxHp, spy.Hp);
        Assert.True(!conscript.IsAlive || conscript.Hp < conscript.MaxHp);
    }

    [Fact]
    public void A_tank_turns_its_gun_on_a_tower_of_nobody_and_an_attack_is_taken_on_it_but_not_on_an_unarmed_building_of_nobody()
    {
        var simulation = Match();
        var world = simulation.World;
        // The Patriot fires at the air only: the tank is not hurt, and still fights it.
        var patriot = Nobody(world, "alliance_patriot_missile_system", 30, 30);
        var curtain = Nobody(world, "iron_curtain", 40, 40);
        var idle = Spawn(world, Union, UnitType.HeavyTank, 34, 30);
        var ordered = Spawn(world, Union, UnitType.HeavyTank, 44, 36);

        Run(simulation, 60);
        Assert.Equal(patriot.Id, idle.Target);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [ordered.Id], curtain.Id));
        Run(simulation, 2);
        Assert.NotEqual(curtain.Id, ordered.Target);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 1, [ordered.Id], patriot.Id));
        Run(simulation, 2);
        Assert.Equal(patriot.Id, ordered.Target);
        // A forced attack takes what is nobody's (RA2).
        simulation.Enqueue(new ForceAttackCommand(Union, simulation.CurrentTick, 2, [ordered.Id], curtain.Id, Aim.CentreOf(curtain)));
        Run(simulation, 2);
        Assert.Equal(curtain.Id, ordered.Target);
    }

    [Fact]
    public void An_iron_curtain_of_nobody_never_charges_nor_fires()
    {
        var simulation = Match();
        var world = simulation.World;
        var curtain = Nobody(world, "iron_curtain", 30, 30);
        var charge = curtain.ChargeLeft;

        Run(simulation, 120);

        Assert.True(charge > 0);
        Assert.Equal(charge, curtain.ChargeLeft);
        Assert.False(Hostility.IsHostileNeutral(curtain));
    }

    [Fact]
    public void An_engineer_takes_a_reactor_of_nobody_and_its_power_is_his()
    {
        var simulation = Match();
        var world = simulation.World;
        var reactor = Nobody(world, "nuclear_reactor", 30, 30);
        var engineer = Spawn(world, Union, UnitType.Engineer, 28, 28);
        Assert.Equal(EntryAction.Capture, EntrySystem.ActionOf(world, engineer, reactor));

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [engineer.Id], reactor.Id));
        Run(simulation, 400, () => reactor.Owner == Union);
        Run(simulation, 2);

        Assert.Equal(Union, reactor.Owner);
        Assert.True(world.Player(Union).PowerOutput > 0);
    }

    [Fact]
    public void An_engineer_takes_an_allied_yard_of_nobody_and_with_it_the_allied_base_and_the_yard_packs_up()
    {
        var simulation = Match();
        var world = simulation.World;
        var yard = Nobody(world, "alliance_construction_yard", 30, 30);
        var engineer = Spawn(world, Union, UnitType.Engineer, 28, 28);

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [engineer.Id], yard.Id));
        Run(simulation, 400, () => yard.Owner == Union);
        Run(simulation, 2);
        Assert.True(world.Player(Union).CanBuildBuilding(world.Rules.Database.IndexOfBuilding("alliance_power_plant")));

        simulation.Enqueue(new DeployCommand(Union, simulation.CurrentTick, 1, yard.Id));
        Run(simulation, 2);
        Assert.False(yard.IsAlive);
        Assert.Contains(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Owner == Union && unit.Type == UnitType.AllianceMcv);
    }

    [Theory]
    [InlineData("alliance_prism_tower")]
    [InlineData("tesla_coil")]
    [InlineData("iron_curtain")]
    [InlineData("alliance_chronosphere")]
    [InlineData("cloning_vats")]
    public void No_engineer_takes_a_defense_a_superweapon_or_the_vats_of_nobody(string id)
    {
        var simulation = Match();
        var world = simulation.World;
        var building = Nobody(world, id, 30, 30);
        var engineer = Spawn(world, Union, UnitType.Engineer, 28, 28);

        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, building));
    }

    [Fact]
    public void The_bunker_holds_eight_sees_six_and_falls_to_ruins()
    {
        var simulation = Match(fog: true);
        var world = simulation.World;
        var bunker = world.BuildingAt(Bunker)!;
        Assert.Equal(8, GarrisonSystem.Capacity(bunker));
        var gis = Enumerable.Range(0, 9).Select(i => Spawn(world, Allied, UnitType.AllianceRifleman, 26 + i % 5, 46 + i / 5)).ToList();

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [.. gis.Select(gi => gi.Id)], bunker.Id));
        Run(simulation, 600, () => bunker.Occupants.Count == 8);
        Run(simulation, 30);
        Assert.Equal(8, bunker.Occupants.Count);
        Assert.Single(gis, gi => gi.IsAlive);

        // Seen from the middle of the bunker: 6 cells off, not 9 (a house sees 4).
        var middle = new CellPos(Bunker.X + 1, Bunker.Y + 1);
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Allied, new CellPos(middle.X + 6, middle.Y)));
        Assert.NotEqual(CellVisibility.Visible, Vision.StateAt(world, Allied, new CellPos(middle.X + 9, middle.Y)));

        world.DestroyBuilding(bunker, null);
        world.OpenWrecks(null);
        Assert.False(world.Map.CanEnter(Bunker, Locomotor.Foot));
    }
}
