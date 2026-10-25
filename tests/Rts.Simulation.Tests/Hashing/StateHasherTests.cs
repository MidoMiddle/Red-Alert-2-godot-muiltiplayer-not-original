using System.Reflection;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Math;
using Rts.Simulation.Random;
using Rts.Simulation.World;
using Rts.Simulation.Tests.World;

namespace Rts.Simulation.Tests.Hashing;

public sealed class StateHasherTests
{
    private static readonly PlayerId Player1 = new(1);

    /// <summary>Two units, one with a move target: covers every Unit field.</summary>
    private static (GameWorld World, Unit Moving, Unit Idle) BuildWorld(int idleOwner = 2, UnitType idleType = UnitType.Conscript)
    {
        var world = new GameWorld(seed: 7, widthCells: 64, heightCells: 64, TestRules.Standard);
        var moving = world.SpawnForTest(Player1, new WorldPos(100, 200), speed: 32);
        moving.MoveTarget = new WorldPos(900, 800);
        var idle = world.SpawnForTest(new PlayerId(idleOwner), new WorldPos(300, 400), speed: 16, idleType);
        return (world, moving, idle);
    }

    [Fact]
    public void Same_state_gives_the_same_hash()
    {
        Assert.Equal(StateHasher.Compute(BuildWorld().World), StateHasher.Compute(BuildWorld().World));
    }

    public static TheoryData<string> Mutations => new()
    {
        "tick", "random", "next id", "owner", "alive", "position x", "position y",
        "target removed", "target added", "target x", "target y", "speed", "unit hp", "attack target", "stance", "anchor", "target order", "target point", "engaged", "chase cell", "reload", "burst", "burst delay", "projectile", "projectile position", "projectile ticks", "projectile hp", "entity count",
        "type", "turn rate", "facing", "turret facing", "land", "obstacle", "height",
        "path pending", "path", "path index", "path request", "path search progress", "path budget",
        "wait ticks", "replans", "hold ticks", "resume target", "occupied cell", "path request avoid", "returning",
        "soldier spot", "subcell", "route", "route point", "attack-move", "order index", "loop",
        "vision", "explored", "veil", "explore all", "credits", "under attack", "defeated", "building", "building ready", "production", "production progress", "rally point", "ore", "ore stage", "harvester load", "harvester mode", "repair", "enter target", "occupant", "occupant hp", "erased by", "erase weapon", "erase points", "bomb ticks", "bomb weapon", "bomb shooter", "bomb owner", "deployed", "warp ticks", "passenger", "parasite", "parasite ticks", "ammo", "rearm ticks", "mother", "spawns docked", "spawn regen", "drop", "cash timer", "experience", "crate upgrades", "controlled by", "controlling", "undeploy in", "veteran infantry", "veteran vehicles", "invulnerable", "storm", "bridge bomb", "blackout", "radiation",
        "building hp", "building target", "building reload",
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public void Any_change_gives_a_different_hash(string mutation)
    {
        var baseline = StateHasher.Compute(BuildWorld().World);
        // Owner and type are immutable: those mutations are worlds built with a different second unit.
        var (world, moving, idle) = BuildWorld(
            idleOwner: mutation == "owner" ? 3 : 2,
            idleType: mutation == "type" ? UnitType.Engineer : UnitType.Conscript);

        switch (mutation)
        {
            case "tick": world.CurrentTick++; break;
            case "random": world.Random.NextUInt(); break;
            case "next id": idle.MarkDead(); world.RemoveDead(); world.SpawnForTest(new PlayerId(2), new WorldPos(300, 400), speed: 16); break;
            case "owner": break;
            case "alive": idle.MarkDead(); break;
            case "position x": moving.Position += new WorldPos(1, 0); break;
            case "position y": moving.Position += new WorldPos(0, 1); break;
            case "target removed": moving.MoveTarget = null; break;
            case "target added": idle.MoveTarget = new WorldPos(0, 0); break;
            case "target x": moving.MoveTarget = new WorldPos(901, 800); break;
            case "target y": moving.MoveTarget = new WorldPos(900, 801); break;
            case "speed": moving.Speed++; break;
            case "unit hp": moving.Hp--; break;
            case "attack target": moving.Target = idle.Id; break;
            case "under attack": world.Player(new PlayerId(2)).LastUnderAttack = 0; break;
            case "defeated": world.Player(new PlayerId(2)).DefeatedTick = 0; break;
            case "vision": world.VisionOf(new PlayerId(1)).Reveal(new CellPos(1, 1), 0); world.VisionOf(new PlayerId(1)).Hide(new CellPos(1, 1), 0); world.VisionOf(new PlayerId(2)).Reveal(new CellPos(30, 30), 1); break;
            case "explored": world.VisionOf(new PlayerId(3)).Reveal(new CellPos(2, 2), 0); world.VisionOf(new PlayerId(3)).Hide(new CellPos(2, 2), 0); break;
            case "veil": world.VisionOf(new PlayerId(4)).Veil(new CellPos(5, 5), 2, 1); break;
            case "explore all": world.VisionOf(new PlayerId(5)).ExploreAll(); break;
            case "enter target": moving.EnterTarget = idle.Id; break;
            case "erased by": moving.ErasedBy = idle.Id; break;
            case "bomb ticks": moving.BombTicks = 9; break;
            case "bomb weapon": moving.BombWeapon = 0; break;
            case "bomb shooter": moving.BombShooter = idle.Id; break;
            case "bomb owner": moving.BombOwner = new PlayerId(2); break;
            case "deployed": moving.Deployed = true; break;
            case "passenger": moving.Passengers.Add(new Occupant(world.Rules.Unit(UnitType.Conscript), 125)); break;
            case "parasite": moving.Parasite = UnitType.Conscript; break;
            case "parasite ticks": moving.ParasiteTicks = 7; break;
            case "ammo": moving.Ammo = 2; break;
            case "rearm ticks": moving.RearmTicks = 5; break;
            case "mother": moving.Mother = idle.Id; break;
            case "invulnerable": moving.InvulnerableUntil = 100; break;
            case "storm": world.Storms.Add(new Storm(Player1, idle.Id, 0, new CellPos(3, 3), 10, 20)); break;
            case "bridge bomb": world.BridgeBombs.Add(new BridgeBomb(Player1, idle.Id, 1, 0, 20)); break;
            case "spawns docked": moving.SpawnsDocked = 2; break;
            case "spawn regen": moving.SpawnRegenTicks = 4; break;
            case "drop": moving.Drop = new Paradrop(UnitType.Conscript, 3, new WorldPos(5, 5), new WorldPos(9, 9)); break;
            case "experience": moving.Experience = 50; break;
            case "crate upgrades": moving.Upgrades = CrateUpgrade.Armor; break;
            case "controlled by": moving.ControlledBy = idle.Id; break;
            case "controlling": moving.Controlling = idle.Id; break;
            case "undeploy in": moving.UndeployIn = 5; break;
            case "veteran infantry": world.Player(Player1).VeteranInfantry = true; break;
            case "veteran vehicles": world.Player(Player1).VeteranVehicles = true; break;
            case "cash timer": world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(44, 44), ready: true, null).CashTimer = 5; break;
            case "warp ticks": moving.WarpTicks = 3; break;
            case "blackout": world.Player(Player1).BlackoutUntil = 50; break;
            case "radiation": world.Radiation[7] = 100; break;
            case "erase weapon": moving.EraseWeapon = 0; break;
            case "erase points": moving.ErasePoints = 8; break;
            case "occupant":
            case "occupant hp":
                var bunker = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(44, 44), ready: true, null);
                bunker.Occupants.Add(new Occupant(world.Rules.Unit(UnitType.Conscript), 125));
                var held = StateHasher.Compute(world);
                bunker.Occupants[0] = new Occupant(world.Rules.Unit(mutation == "occupant" ? UnitType.Engineer : UnitType.Conscript), mutation == "occupant" ? 125 : 124);
                Assert.NotEqual(held, StateHasher.Compute(world));
                break;
            case "stance": moving.Stance = Stance.Guard; break;
            case "anchor": moving.Anchor += new WorldPos(1, 0); break;
            case "target order": moving.TargetOrder = TargetOrder.Attack; break;
            case "target point": moving.TargetPoint = new WorldPos(5, 5); break;
            case "engaged": moving.Engaged = true; break;
            case "chase cell": moving.ChaseCell = new CellPos(3, 4); break;
            case "reload": moving.Weapons[0].ReloadLeft = 5; break;
            case "burst": moving.Weapons[0].BurstLeft = 1; break;
            case "burst delay": moving.Weapons[0].BurstDelayLeft = 2; break;
            case "projectile": world.Launch(Player1, moving.Id, 0, idle.Id, moving.Position, idle.Position); break;
            case "projectile position": world.Launch(Player1, moving.Id, 0, idle.Id, moving.Position, idle.Position).Position = new WorldPos(1, 1); break;
            case "projectile ticks": world.Launch(Player1, moving.Id, 0, idle.Id, moving.Position, idle.Position).TicksLeft = 3; break;
            case "projectile hp": world.Launch(Player1, moving.Id, 0, idle.Id, moving.Position, idle.Position).Hp = 50; break;
            case "entity count": world.SpawnForTest(Player1, new WorldPos(0, 0), speed: 1); break;
            case "type": break;
            case "turn rate": moving.TurnRate++; break;
            case "facing": moving.Facing = BAngle.FromUnits(1); break;
            case "turret facing": moving.TurretFacing = BAngle.FromUnits(1); break;
            case "land": world.Map.SetLand(new CellPos(63, 0), LandType.Ice); break;
            case "obstacle": world.Map.SetBlocked(new CellPos(0, 63), true); break;
            case "height": world.Map.SetCornerLevel(64, 64, 1); break;
            case "path pending": moving.PathPending = true; break;
            case "path": moving.Path = AStar.FindPath(world.Map, new CellPos(0, 0), new CellPos(2, 1), Locomotor.Foot); break;
            case "path index": moving.PathIndex = 3; break;
            case "path request": world.PathRequests.Request(idle, 0, new CellPos(5, 5)); break;
            case "path search progress":
                // Same request in both worlds would be a different test; here progress alone must change the hash.
                world.PathRequests.Request(idle, 0, new CellPos(40, 40));
                var baselineWithRequest = StateHasher.Compute(world);
                world.PathRequests.Requests[0].Search.Advance(3);
                Assert.NotEqual(baselineWithRequest, StateHasher.Compute(world));
                break;
            case "path budget": world.PathRequests.Budget = 1; break;
            case "wait ticks": moving.WaitTicks = 4; break;
            case "replans": moving.Replans = 1; break;
            case "returning": idle.Returning = true; break;
            case "hold ticks": idle.HoldTicks = 5; break;
            case "resume target": idle.ResumeTarget = new WorldPos(10, 10); break;
            case "occupied cell": world.Reservations.SetVehicle(new CellPos(60, 60), new EntityId(99)); break;
            case "soldier spot":
                var soldier = world.SpawnForTest(Player1, new CellPos(50, 50).Center, speed: 1);
                var withSoldier = StateHasher.Compute(world);
                world.Reservations.Leave(new CellPos(50, 50), soldier);
                world.Reservations.TryEnter(new CellPos(50, 50), soldier, new CellPos(50, 50).Center + new WorldPos(64, 64));
                Assert.NotEqual(withSoldier, StateHasher.Compute(world));
                break;
            case "subcell": idle.Subcell = (idle.Subcell + 1) % 5; break;
            case "route": idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)]; break;
            case "route point":
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)];
                var withRoute = StateHasher.Compute(world);
                idle.Orders = [new UnitOrder(new WorldPos(5, 6), false)];
                Assert.NotEqual(withRoute, StateHasher.Compute(world));
                break;
            case "attack-move":
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), false)];
                var plain = StateHasher.Compute(world);
                idle.Orders = [new UnitOrder(new WorldPos(5, 5), true)];
                Assert.NotEqual(plain, StateHasher.Compute(world));
                break;
            case "order index": idle.OrderIndex = 1; break;
            case "loop": idle.LoopOrders = true; break;
            case "credits": world.Player(Player1).Credits--; break;
            case "repair":
                var damaged = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(44, 44), ready: true, null);
                var off = StateHasher.Compute(world);
                damaged.Repairing = true;
                Assert.NotEqual(off, StateHasher.Compute(world));
                break;
            case "harvester load":
                var carrier = world.SpawnForTest(Player1, new WorldPos(2000, 2000), speed: 0, UnitType.Harvester);
                var empty = StateHasher.Compute(world);
                carrier.Harvester!.Load = 25;
                Assert.NotEqual(empty, StateHasher.Compute(world));
                break;
            case "harvester mode":
                var miner = world.SpawnForTest(Player1, new WorldPos(2000, 2000), speed: 0, UnitType.Harvester);
                var seeking = StateHasher.Compute(world);
                miner.Harvester!.Mode = HarvesterMode.Waiting;
                Assert.NotEqual(seeking, StateHasher.Compute(world));
                break;
            case "ore": world.Map.SetOre(new CellPos(5, 5), new OreCell(OreKind.Ore, 0)); break;
            case "ore stage":
                world.Map.SetOre(new CellPos(5, 5), new OreCell(OreKind.Ore, 3));
                var stage3 = StateHasher.Compute(world);
                world.Map.SetOre(new CellPos(5, 5), new OreCell(OreKind.Ore, 4));
                Assert.NotEqual(stage3, StateHasher.Compute(world));
                break;
            case "production":
                world.Player(Player1).QueueList(Rules.ProductionCategory.Vehicle).Add(new ProductionItem(ProductionKind.Unit, 0));
                break;
            case "production progress":
                var item = new ProductionItem(ProductionKind.Unit, 0);
                world.Player(Player1).QueueList(Rules.ProductionCategory.Vehicle).Add(item);
                var before = StateHasher.Compute(world);
                item.Progress++;
                Assert.NotEqual(before, StateHasher.Compute(world));
                break;
            case "rally point":
                var factory = world.CreateBuilding(Player1, TestRules.BuildingDef("war_factory"), new CellPos(40, 40), ready: true, null);
                var without = StateHasher.Compute(world);
                factory.RallyPoint = new WorldPos(5, 5);
                Assert.NotEqual(without, StateHasher.Compute(world));
                break;
            case "building": world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(40, 40), ready: false, null); break;
            case "building ready":
                var reactor = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(40, 40), ready: false, null);
                var growing = StateHasher.Compute(world);
                world.CompleteBuildUp(reactor);
                Assert.NotEqual(growing, StateHasher.Compute(world));
                break;
            case "building hp":
                var hurt = world.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(44, 44), ready: true, null);
                var whole = StateHasher.Compute(world);
                hurt.Hp--;
                Assert.NotEqual(whole, StateHasher.Compute(world));
                break;
            case "building target":
                var aiming = world.CreateBuilding(Player1, TestRules.BuildingDef("tesla_coil"), new CellPos(44, 44), ready: true, null);
                var idleCoil = StateHasher.Compute(world);
                aiming.Target = idle.Id;
                Assert.NotEqual(idleCoil, StateHasher.Compute(world));
                break;
            case "building reload":
                var coil = world.CreateBuilding(Player1, TestRules.BuildingDef("tesla_coil"), new CellPos(44, 44), ready: true, null);
                var loaded = StateHasher.Compute(world);
                coil.Weapons[0].ReloadLeft = 5;
                Assert.NotEqual(loaded, StateHasher.Compute(world));
                break;
            case "path request avoid":
                world.PathRequests.Request(idle, 0, new CellPos(5, 5));
                var withoutAvoid = StateHasher.Compute(world);
                world.PathRequests.Request(idle, 0, new CellPos(5, 5), [new CellPos(4, 4)]);
                Assert.NotEqual(withoutAvoid, StateHasher.Compute(world));
                break;
            default: throw new ArgumentException(mutation);
        }

        Assert.NotEqual(baseline, StateHasher.Compute(world));
    }

    /// <summary>
    /// Every instance field of the simulation state, as declared by each type. If this test fails,
    /// a field was added or removed: update StateHasher first, then this list.
    /// </summary>
    [Theory]
    [InlineData(typeof(Entity), new[]
    {
        "<Id>k__BackingField", "<Owner>k__BackingField", "<IsAlive>k__BackingField",
        // The sight circle an entity added (M4 step 11): derived from where it stands; the grids it went into are hashed.
        "<SightCell>k__BackingField", "<SightRadius>k__BackingField",
        // The temporal beam that holds it (stage 3a).
        "<ErasedBy>k__BackingField", "<EraseWeapon>k__BackingField", "<ErasePoints>k__BackingField",
        // The bomb planted on it (stage 3b).
        "<BombTicks>k__BackingField", "<BombWeapon>k__BackingField", "<BombShooter>k__BackingField", "<BombOwner>k__BackingField",
        // The Bastion Field Projector's cover (stage 2d).
        "<InvulnerableUntil>k__BackingField",
    })]
    [InlineData(typeof(Rts.Simulation.Visibility.VisibilityGrid), new[] { "_counts", "_explored", "_veils", "<Width>k__BackingField", "<Height>k__BackingField" })]
    [InlineData(typeof(Unit), new[]
    {
        "<Archetype>k__BackingField", "<Harvester>k__BackingField", "<Position>k__BackingField", "<MoveTarget>k__BackingField", "<Speed>k__BackingField",
        "<TurnRate>k__BackingField", "<Facing>k__BackingField", "<TurretFacing>k__BackingField",
        "<PathPending>k__BackingField", "<Path>k__BackingField", "<PathIndex>k__BackingField",
        "<WaitTicks>k__BackingField", "<Replans>k__BackingField", "<ResumeTarget>k__BackingField", "<HoldTicks>k__BackingField",
        "<Returning>k__BackingField", "<Subcell>k__BackingField",
        "<Orders>k__BackingField", "<OrderIndex>k__BackingField", "<LoopOrders>k__BackingField", "<Hp>k__BackingField",
        "<Target>k__BackingField", "<Weapons>k__BackingField", "<Stance>k__BackingField", "<Anchor>k__BackingField",
        "<TargetOrder>k__BackingField", "<TargetPoint>k__BackingField", "<Engaged>k__BackingField", "<ChaseCell>k__BackingField", "<EnterTarget>k__BackingField",
        "<Deployed>k__BackingField", "<WarpTicks>k__BackingField",
        "<Passengers>k__BackingField", "<Parasite>k__BackingField", "<ParasiteOwner>k__BackingField", "<ParasiteHp>k__BackingField",
        "<ParasiteWeapon>k__BackingField", "<ParasiteTicks>k__BackingField", "<Ammo>k__BackingField", "<RearmTicks>k__BackingField",
        "<Mother>k__BackingField", "<SpawnsDocked>k__BackingField", "<SpawnRegenTicks>k__BackingField", "<Drop>k__BackingField", "<Experience>k__BackingField",
        "<Upgrades>k__BackingField", "<ControlledBy>k__BackingField", "<ControlledFrom>k__BackingField", "<Controlling>k__BackingField",
        "<UndeployIn>k__BackingField",
    })]
    [InlineData(typeof(Projectile), new[]
    {
        "<Shooter>k__BackingField", "<Weapon>k__BackingField", "<Target>k__BackingField", "<Origin>k__BackingField",
        "<Position>k__BackingField", "<AimPoint>k__BackingField", "<LastTargetPoint>k__BackingField", "<Facing>k__BackingField",
        "<FlightTicks>k__BackingField", "<TicksLeft>k__BackingField", "<LaunchTick>k__BackingField", "<Hp>k__BackingField",
    })]
    [InlineData(typeof(Rts.Simulation.Combat.WeaponInstance), new[]
    {
        "<ReloadLeft>k__BackingField", "<BurstLeft>k__BackingField", "<BurstDelayLeft>k__BackingField",
    })]
    [InlineData(typeof(GameWorld), new[]
    {
        "_entities", "_nextEntityId", "<CurrentTick>k__BackingField", "<Random>k__BackingField", "<Map>k__BackingField", "_vision",
        "<PathRequests>k__BackingField", "<Reservations>k__BackingField",
        // The toxin on the ground (stage 3b); the storms (stage 2d).
        "<Radiation>k__BackingField", "<Storms>k__BackingField", "<BridgeBombs>k__BackingField",
        // The crates (M11 a6): hashed in a match with them (CrateSystemTests).
        "<Crates>k__BackingField", "<CrateTimer>k__BackingField", "<NextCrateId>k__BackingField",
        // The death weapons of a tick, launched in the same tick (stage 3b): never state.
        "<DeathBlasts>k__BackingField",
        // The sensors of the moment (stage 3e), rebuilt from the units with the sight: derived.
        "<Sensors>k__BackingField",
        // The psychic sensors of the moment (RA2), rebuilt with the sight: derived.
        "<DisguiseDetectors>k__BackingField", "<IntentReaders>k__BackingField",
        // Derived from unit positions and rebuilt from them; not hashed (docs/implementation/M2_unit_control.md, 4.9).
        "<Spatial>k__BackingField",
        // The hits of a tick, emptied in the same tick (M4 step 5): never state.
        "<Hits>k__BackingField",
        // The buildings that fell this tick, their cells opened at its end (M4 step 9): never state.
        "<Wrecks>k__BackingField",
        // Lists and tables the systems fill and empty within one call, kept to allocate nothing (M4 step 15): never state.
        "<Nearby>k__BackingField", "<Area>k__BackingField", "<Shots>k__BackingField", "<FactoryCounts>k__BackingField", "<MainFactories>k__BackingField",
        // The rules of the match: data, not state; the replay keeps their hash (M3 step 3).
        "<Rules>k__BackingField",
        // The players (M3 step 5): their credits are hashed; the marks that power and technologies need working out are derived.
        "_players", "<PowerDirty>k__BackingField", "<TechDirty>k__BackingField",
        // The match setup: fixed, the replay keeps it; its result follows from the ticks of defeat (M5 step 10).
        "<Match>k__BackingField", "<Result>k__BackingField",
        // The allies (M8 step 11): fixed by the teams of the match setup as the world is made.
        "_allies",
        // The statistics (M5 step 11): what the players did, worked out from the game; it does not depend on them.
        "<Stats>k__BackingField",
    })]
    [InlineData(typeof(Harvester), new[]
    {
        "<Mode>k__BackingField", "<Field>k__BackingField", "<Target>k__BackingField", "<Refinery>k__BackingField", "<Preferred>k__BackingField",
        "<Load>k__BackingField", "<Timer>k__BackingField", "<WaitTicks>k__BackingField", "<WaitingForRefinery>k__BackingField",
        "<QueueTick>k__BackingField",
        // A counter for the tests that a harvester does not search every tick; not state.
        "<Searches>k__BackingField",
    })]
    [InlineData(typeof(ProductionItem), new[]
    {
        "<Kind>k__BackingField", "<DefIndex>k__BackingField", "<Progress>k__BackingField", "<Paid>k__BackingField",
        "<Paused>k__BackingField", "<WaitingForFunds>k__BackingField", "<Ready>k__BackingField",
    })]
    [InlineData(typeof(PlayerState), new[]
    {
        "<Credits>k__BackingField", "_queues", "<LastUnderAttack>k__BackingField", "<DefeatedTick>k__BackingField", "<BlackoutUntil>k__BackingField",
        "<VeteranInfantry>k__BackingField", "<VeteranVehicles>k__BackingField",
        "<Modifiers>k__BackingField", "<IncomeCarry>k__BackingField",
        // Fixed by the order of the players and the rules of the match.
        "<Id>k__BackingField", "<Faction>k__BackingField", "<Country>k__BackingField",
        // Derived from the buildings by PowerSystem and TechTree (M3 step 5).
        "<BuildingList>k__BackingField", "<PowerOutput>k__BackingField", "<PowerDrain>k__BackingField", "<LowPower>k__BackingField",
        "_units", "_buildings",
        // Derived from the ready buildings by TechTree (stage 2c): the bonus of the ore.
        "<BuildingIncomeBonusPercent>k__BackingField",
    })]
    [InlineData(typeof(Rts.Simulation.Movement.Reservations), new[] { "_vehicle", "_infantry", "<Width>k__BackingField", "<Height>k__BackingField" })]
    [InlineData(typeof(PathRequestQueue), new[] { "_pathfinder", "_requests", "<Budget>k__BackingField", "<ExpandedLastTick>k__BackingField" })]
    [InlineData(typeof(PathRequest), new[]
    {
        "<Unit>k__BackingField", "<RequestTick>k__BackingField", "<Start>k__BackingField", "<Goal>k__BackingField",
        "<Locomotor>k__BackingField", "<Search>k__BackingField", "<Avoid>k__BackingField",
    })]
    [InlineData(typeof(MapGrid), new[]
    {
        "_land", "_blocked", "_heights", "_objects", "_bridges", "_startBuildings", "_building", "_ore", "<Width>k__BackingField", "<Height>k__BackingField",
        // Indexes of the objects and the bridge segments by cell, and of the ore and gem sources (M3 step 16), built from the lists above.
        "_objectsByCell", "_segmentsByCell", "_sources",
        // Caches of the layers above: their digest for the hash, a stamp for A* step costs (M2 step 15), and the speed of
        // every locomotor on every cell (M7 step 3).
        "_digest", "<Version>k__BackingField", "_passability",
        // The land speeds the cells above are read by: rules of the match, not state (M3 step 4).
        "<Speeds>k__BackingField",
        // A counter of ore changes for the client to redraw (M3 step 9).
        "<OreVersion>k__BackingField",
        // A counter of destroyed objects, for the client to hide them without rebuilding the map.
        "<ObjectDeaths>k__BackingField",
    })]
    [InlineData(typeof(CornerHeightGrid), new[] { "_levels", "<Width>k__BackingField", "<Height>k__BackingField" })]
    [InlineData(typeof(MapObject), new[]
    {
        "<Id>k__BackingField", "<Kind>k__BackingField", "<Cell>k__BackingField", "<Variant>k__BackingField", "<Hp>k__BackingField",
        "<Destroyed>k__BackingField",
    })]
    [InlineData(typeof(Bridge), new[] { "_segments", "<Id>k__BackingField", "<Kind>k__BackingField", "<From>k__BackingField", "<To>k__BackingField" })]
    // Only the hit points are hashed: the bridge, the place in the chain and the cells follow from the bridge's ends and the water.
    [InlineData(typeof(BridgeSegment), new[] { "<Hp>k__BackingField", "<Bridge>k__BackingField", "<Index>k__BackingField", "<Cells>k__BackingField" })]
    [InlineData(typeof(Building), new[]
    {
        "<Kind>k__BackingField", "<Cell>k__BackingField", "<Facing>k__BackingField", "<Hp>k__BackingField",
        "<DefIndex>k__BackingField", "<BuildUpLeft>k__BackingField", "<RallyPoint>k__BackingField", "<Repairing>k__BackingField",
        "<RepairCarry>k__BackingField", "<Target>k__BackingField", "<Weapons>k__BackingField", "<Occupants>k__BackingField", "<ChargeLeft>k__BackingField",
        "<CashTimer>k__BackingField",
        // From the kind or the definition (by DefIndex): rules, not state.
        "<Def>k__BackingField", "<Width>k__BackingField", "<Height>k__BackingField",
        // The veil it added (stage 2d): derived from the building and its owner's power; the grids it went into are hashed.
        "<VeilRadius>k__BackingField",
    })]
    [InlineData(typeof(Occupant), new[] { "<Type>k__BackingField", "<Hp>k__BackingField", "<Weapons>k__BackingField" })]
    [InlineData(typeof(DeterministicRandom), new[] { "_state", "_increment" })]
    public void Every_state_field_is_known_to_the_hasher(Type type, string[] hashedFields)
    {
        var actualFields = type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(field => field.Name)
            .Order()
            .ToList();

        Assert.Equal(hashedFields.Order().ToList(), actualFields);
    }

    [Fact]
    public void A_path_digest_follows_its_content_through_with()
    {
        var path = new CellPath(PathStatus.Found, [new CellPos(0, 0), new CellPos(1, 1), new CellPos(2, 2)], 282, 3);
        var digest = path.Digest;
        var smoothed = path with { Waypoints = [new CellPos(0, 0), new CellPos(2, 2)] };
        var fresh = new CellPath(PathStatus.Found, path.Cells, 282, 3) { Waypoints = [new CellPos(0, 0), new CellPos(2, 2)] };

        Assert.Equal(digest, path.Digest);
        Assert.NotEqual(digest, smoothed.Digest);
        Assert.Equal(fresh.Digest, smoothed.Digest);
        Assert.NotEqual(digest, (path with { Cost = 283 }).Digest);
    }

    [Fact]
    public void The_map_digest_changes_with_any_cell_and_comes_back_with_it()
    {
        var map = new MapGrid(8, 8);
        var clear = map.Digest;

        map.SetLand(new CellPos(3, 4), LandType.Rough);
        var rough = map.Digest;
        map.SetLand(new CellPos(3, 4), LandType.Clear);
        map.SetBlocked(new CellPos(7, 7), true);

        Assert.NotEqual(clear, rough);
        Assert.NotEqual(clear, map.Digest);
        map.SetBlocked(new CellPos(7, 7), false);
        Assert.Equal(clear, map.Digest);
    }
}
