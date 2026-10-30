using Rts.Client.Hud;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The tech buildings of the map (loose ends, stage 1): an engineer takes them; the oil derrick pays, the hospital heals, the airfield
/// sends a paradrop, the outpost of RA2 fires, mends vehicles and extends the base; a tech building keeps nobody in the match. By the
/// game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class TechBuildingTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos Derrick = new(20, 18);
    private static readonly CellPos Hospital = new(30, 18);
    private static readonly CellPos Airfield = new(10, 40);

    // An outpost of 4 x 3 cells: (40, 30) to (43, 32), its centre at (42, 31.5).
    private static readonly CellPos Outpost = new(40, 30);

    private static GameSimulation Match(string alliedCountry = "")
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null,
        [
            new BuildingState(BuildingKind.TechOilDerrick, Derrick, PlayerId.Neutral, 0),
            new BuildingState(BuildingKind.TechHospital, Hospital, PlayerId.Neutral, 0),
            new BuildingState(BuildingKind.TechAirfield, Airfield, PlayerId.Neutral, 0),
            new BuildingState(BuildingKind.TechOutpost, Outpost, PlayerId.Neutral, 0),
        ], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "tech",
                    47,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60))
                        {
                            Country = alliedCountry,
                        },
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static Building At(GameWorld world, CellPos cell) => world.BuildingAt(cell)!;

    private static List<SimEvent> Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    private static void Take(GameSimulation simulation, PlayerId player, UnitType engineer, CellPos building, uint sequence)
    {
        var unit = Spawn(simulation.World, player, engineer, building.X, building.Y + 4);
        simulation.Enqueue(new EnterCommand(player, simulation.CurrentTick, sequence, [unit.Id], simulation.World.BuildingAt(building)!.Id));
        Run(simulation, 300, () => !unit.IsAlive);
        Assert.Equal(player, simulation.World.BuildingAt(building)!.Owner);
    }

    [Fact]
    public void A_derrick_taken_from_the_neutral_side_gives_a_thousand_then_twenty_every_hundred_ticks()
    {
        var simulation = Match();
        var world = simulation.World;
        var before = world.Player(Union).Credits;
        Take(simulation, Union, UnitType.Engineer, Derrick, 0);
        Assert.Equal(before + TechBuildingSystem.DerrickStartup, world.Player(Union).Credits);

        Run(simulation, TechBuildingSystem.DerrickDelay * 3);
        Assert.Equal(before + TechBuildingSystem.DerrickStartup + (3 * TechBuildingSystem.DerrickCash), world.Player(Union).Credits);

        // Taken back from a player, it gives no thousand.
        var allied = world.Player(Allied).Credits;
        Take(simulation, Allied, UnitType.AllianceEngineer, Derrick, 1);
        Assert.Equal(allied, world.Player(Allied).Credits);
    }

    [Fact]
    public void A_taken_derrick_is_repaired_by_the_repair_mode_and_never_sold()
    {
        var simulation = Match();
        var world = simulation.World;
        var derrick = At(world, Derrick);
        world.SetHp(derrick, derrick.MaxHp / 2, null);

        // Nobody's, it is not repaired.
        simulation.Enqueue(new ToggleRepairCommand(Union, simulation.CurrentTick, 0, derrick.Id));
        Run(simulation, 2);
        Assert.False(derrick.Repairing);

        Take(simulation, Union, UnitType.Engineer, Derrick, 1);
        world.SetHp(derrick, derrick.MaxHp / 2, null);
        var before = world.Player(Union).Credits;
        simulation.Enqueue(new ToggleRepairCommand(Union, simulation.CurrentTick, 2, derrick.Id));
        Run(simulation, 1000, () => !derrick.Repairing && derrick.Hp == derrick.MaxHp);

        Assert.Equal(derrick.MaxHp, derrick.Hp);
        Assert.False(derrick.Repairing);
        // Half its hit points mended, paid as if it cost its hit points: half of the repair share of them (the derrick's income ran too).
        var share = world.Rules.Economy.RepairCost.Raw * (long)derrick.MaxHp / 2 / Rts.Simulation.Math.Fixed.OneRaw;
        var income = world.Player(Union).Credits - before;
        Assert.InRange(income + share, 0, TechBuildingSystem.DerrickCash * 20);

        // The sell mode leaves it.
        simulation.Enqueue(new SellCommand(Union, simulation.CurrentTick, 3, derrick.Id));
        Run(simulation, 2);
        Assert.True(derrick.IsAlive);
    }

    [Fact]
    public void A_hurt_soldier_is_healed_whole_at_its_own_hospital()
    {
        var simulation = Match();
        var world = simulation.World;
        var soldier = Spawn(world, Union, UnitType.Conscript, Hospital.X + 4, Hospital.Y + 6);
        world.SetHp(soldier, 20, null);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, soldier, At(world, Hospital)));

        Take(simulation, Union, UnitType.Engineer, Hospital, 0);
        Assert.Equal(EntryAction.Heal, EntrySystem.ActionOf(world, soldier, At(world, Hospital)));
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 1, [soldier.Id], At(world, Hospital).Id));
        var events = Run(simulation, 300, () => soldier.Hp == soldier.MaxHp);
        Assert.Equal(soldier.MaxHp, soldier.Hp);
        Assert.True(soldier.IsAlive);
    }

    [Fact]
    public void A_taken_airfield_drops_its_faction_s_soldiers_where_it_is_aimed()
    {
        var simulation = Match();
        var world = simulation.World;
        Take(simulation, Union, UnitType.Engineer, Airfield, 0);
        var airfield = At(world, Airfield);
        Assert.Equal(airfield.Type.Recharge, airfield.ChargeLeft);

        airfield.ChargeLeft = 0;
        var target = new CellPos(40, 30);
        simulation.Enqueue(new SuperweaponCommand(Union, simulation.CurrentTick, 1, airfield.Id, target, target));
        var events = Run(simulation, 1);
        Assert.Contains(events, e => e is SuperweaponFired { Kind: Rts.Simulation.Rules.SuperweaponKind.Paradrop });
        var plane = world.Entities.OfType<Unit>().Single(unit => unit.Type == UnitType.ParadropPlane);
        Assert.Equal(0, plane.Position.ToCell().X);

        // The player cannot send it elsewhere.
        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 2, [plane.Id], new CellPos(5, 5).Center));
        Run(simulation, 1500, () => !plane.IsAlive);
        Assert.False(plane.IsAlive);
        var dropped = world.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Type == UnitType.Conscript
            && System.Math.Abs(unit.Position.ToCell().X - target.X) <= 2 && System.Math.Abs(unit.Position.ToCell().Y - target.Y) <= 2);
        Assert.Equal(9, dropped);
        Assert.InRange(airfield.ChargeLeft, airfield.Type.Recharge - 1600, airfield.Type.Recharge - 1);
    }

    [Fact]
    public void The_usa_that_takes_an_airfield_has_two_paradrops_its_own_and_the_airfield_s()
    {
        // RA2: the American paradrop of the air force command (8 GIs) and the airfield's (6 GIs of the Allies) are two superweapons.
        var simulation = Match("usa");
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("alliance_air_force_command", out var def);
        var command = world.CreateBuilding(Allied, def, new CellPos(50, 50), true, null);
        Take(simulation, Allied, UnitType.AllianceEngineer, Airfield, 0);
        var airfield = At(world, Airfield);
        var paradrops = world.Entities.OfType<Building>()
            .Where(building => building.Owner == Allied && SuperweaponSystem.KindOf(world, building) == Rts.Simulation.Rules.SuperweaponKind.Paradrop)
            .Select(building => building.Id);
        Assert.Equal([airfield.Id, command.Id], paradrops.Order());

        // Each charges and fires on its own: the airfield's sent, the air command's is still ready.
        command.ChargeLeft = 0;
        airfield.ChargeLeft = 0;
        var near = new CellPos(30, 30);
        var far = new CellPos(40, 20);
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 1, airfield.Id, near, near));
        Run(simulation, 1);
        Assert.Equal(0, command.ChargeLeft);

        // The sidebar (the owner, 2026-10-30): both first on the Defense tab, which they open without a yard; the airfield's charging.
        Assert.True(SidebarModel.TabOpen(world, Allied, BuildGroup.Defense));
        var squares = SidebarModel.Cameos(world, Allied, BuildGroup.Defense);
        Assert.Equal(
            [("Paradrop", "Airfield", CameoState.Building, airfield.Id), ("Paradrop", "Air Command", CameoState.Ready, command.Id)],
            squares.Take(2).Select(square => (square.Name, square.From, square.State, square.Superweapon!.Value)));
        Assert.Equal(240, squares[0].SecondsLeft);
        Assert.Equal(CameoActionKind.None, SidebarModel.LeftClick(squares[1], shift: false).Kind);
        Assert.Null(SidebarModel.ReadyToPlace(squares));
        Assert.DoesNotContain(SidebarModel.Cameos(world, Allied, BuildGroup.Buildings), square => square.Superweapon is not null);

        Assert.InRange(airfield.ChargeLeft, airfield.Type.Recharge - 1, airfield.Type.Recharge);
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 2, command.Id, far, far));
        Run(simulation, 1);
        Assert.Equal(2, world.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Type == UnitType.ParadropPlane));

        Run(simulation, 2000, () => !world.Entities.OfType<Unit>().Any(unit => unit.IsAlive && unit.Type == UnitType.ParadropPlane));
        int Riflemen(CellPos at) => world.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Type == UnitType.AllianceRifleman
            && System.Math.Abs(unit.Position.ToCell().X - at.X) <= 2 && System.Math.Abs(unit.Position.ToCell().Y - at.Y) <= 2);
        Assert.Equal((6, 8), (Riflemen(near), Riflemen(far)));

        // Korea's air command has none: the airfield's alone.
        var korea = Match("korea");
        Assert.Equal(
            Rts.Simulation.Rules.SuperweaponKind.None,
            SuperweaponSystem.KindOf(korea.World, korea.World.CreateBuilding(Allied, def, new CellPos(50, 50), true, null)));
    }

    [Fact]
    public void A_plane_brought_down_drops_nobody()
    {
        var simulation = Match();
        var world = simulation.World;
        Take(simulation, Allied, UnitType.AllianceEngineer, Airfield, 0);
        var airfield = At(world, Airfield);
        airfield.ChargeLeft = 0;
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 1, airfield.Id, new CellPos(40, 30), new CellPos(40, 30)));
        Run(simulation, 5);
        var plane = world.Entities.OfType<Unit>().Single(unit => unit.Type == UnitType.ParadropPlane);
        DeathSystem.Kill(world, plane, EntityId.None, Union, DeathCause.Weapon, -1, null);
        Run(simulation, 600);
        Assert.DoesNotContain(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Type == UnitType.AllianceRifleman
            && System.Math.Abs(unit.Position.ToCell().X - 40) <= 3 && System.Math.Abs(unit.Position.ToCell().Y - 30) <= 3);
    }

    [Fact]
    public void A_tech_building_keeps_nobody_in_the_match()
    {
        var simulation = Match();
        var world = simulation.World;
        foreach (var unit in world.Entities.OfType<Unit>().Where(unit => unit.Owner == Union).ToList())
        {
            DeathSystem.Kill(world, unit, EntityId.None, Allied, DeathCause.Weapon, -1, null);
        }

        Take(simulation, Union, UnitType.Engineer, Derrick, 0);
        Run(simulation, 30);
        Assert.NotNull(world.Player(Union).DefeatedTick);
    }

    [Fact]
    public void A_neutral_outpost_is_silent_and_a_taken_one_fires_at_its_owner_s_enemies()
    {
        var simulation = Match();
        var world = simulation.World;
        // Unarmed, 4 cells east of the centre: within the turret's 6.
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 46, 31);
        Run(simulation, 120);
        Assert.Equal(mcv.MaxHp, mcv.Hp);

        Take(simulation, Union, UnitType.Engineer, Outpost, 0);
        Run(simulation, 200, () => mcv.Hp < mcv.MaxHp);
        Assert.True(mcv.Hp < mcv.MaxHp, "the turret hit the vehicle");
        Assert.Equal(world.Rules.Database.IndexOfArmor("concrete"), world.Rules.ArmorOf(At(world, Outpost)));
    }

    [Fact]
    public void A_taken_outpost_fires_at_an_aircraft()
    {
        var simulation = Match();
        var world = simulation.World;
        Take(simulation, Union, UnitType.Engineer, Outpost, 0);
        var helicopter = Spawn(world, Allied, UnitType.AllianceNighthawk, 42, 27);
        Run(simulation, 200, () => helicopter.Hp < helicopter.MaxHp);

        Assert.True(helicopter.Hp < helicopter.MaxHp, "the turret hit the helicopter");
    }

    [Fact]
    public void A_taken_outpost_mends_its_owner_s_vehicles_beside_it()
    {
        var simulation = Match();
        var world = simulation.World;
        // Beside the east side of the footprint.
        var tank = Spawn(world, Union, UnitType.HeavyTank, 44, 31);
        world.SetHp(tank, tank.MaxHp / 2, null);
        Run(simulation, 60);
        Assert.Equal(tank.MaxHp / 2, tank.Hp);

        Take(simulation, Union, UnitType.Engineer, Outpost, 0);
        var hurt = tank.Hp;
        Run(simulation, 30);
        Assert.True(tank.Hp > hurt, "mended on the pad");
    }

    [Fact]
    public void A_taken_outpost_extends_its_owner_s_base()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("barracks", out var barracks);
        var beside = new CellPos(45, 30);
        Assert.Equal(PlacementProblem.TooFar, PlacementRules.Check(world, Union, barracks, beside));

        Take(simulation, Union, UnitType.Engineer, Outpost, 0);
        Assert.Equal(PlacementProblem.None, PlacementRules.Check(world, Union, barracks, beside));
        Assert.Equal(PlacementProblem.TooFar, PlacementRules.Check(world, Allied, barracks, beside));
    }
    [Fact]
    public void A_taken_hospital_extends_its_owner_s_base()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("barracks", out var barracks);
        // The hospital (6 x 4 at (30, 18)) ends at x 35: the barracks 2 cells east of it.
        var beside = new CellPos(38, 18);
        Assert.Equal(PlacementProblem.TooFar, PlacementRules.Check(world, Union, barracks, beside));

        Take(simulation, Union, UnitType.Engineer, Hospital, 0);
        Assert.Equal(PlacementProblem.None, PlacementRules.Check(world, Union, barracks, beside));
    }
}
