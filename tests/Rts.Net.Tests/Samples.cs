using System.Collections.Immutable;
using Rts.Net.Lockstep;
using Rts.Net.Protocol;
using Rts.Net.Protocol.Messages;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Net.Tests;

/// <summary>One message of every type and one command of every type, with fields that are not zero, for the round trips.</summary>
internal static class Samples
{
    public static readonly PlayerId P2 = new(2);

    public static ImmutableArray<EntityId> Units(params int[] ids) => ids.Select(id => new EntityId(id)).ToImmutableArray();

    /// <summary>A command of every <see cref="CommandType"/> of <paramref name="player"/> for <paramref name="tick"/>.</summary>
    public static ImmutableArray<Command> EveryCommand(PlayerId player, int tick)
    {
        uint sequence = 7;
        var point = new WorldPos(1234, -56);
        var cell = new CellPos(12, 34);
        return
        [
            new SpawnTestUnitCommand(player, tick, sequence++, UnitType.HeavyTank, point),
            new MoveCommand(player, tick, sequence++, Units(1, 2, 300), point, Queue: true),
            new StopCommand(player, tick, sequence++, Units(4)),
            new AttackMoveCommand(player, tick, sequence++, Units(5, 6), point),
            new WaypointsCommand(player, tick, sequence++, Units(7), [point, new WorldPos(9, 10)], loop: true),
            new ScatterCommand(player, tick, sequence++, Units()),
            new DamageMapObjectCommand(player, tick, sequence++, cell, 250),
            new PlaceBuildingCommand(player, tick, sequence++, 3, cell),
            new DeployCommand(player, tick, sequence++, new EntityId(8)),
            new ProduceCommand(player, tick, sequence++, ProductionKind.Unit, 4, 5),
            new PauseProductionCommand(player, tick, sequence++, ProductionKind.Building, 2),
            new CancelProductionCommand(player, tick, sequence++, ProductionKind.Unit, 1),
            new SetRallyPointCommand(player, tick, sequence++, new EntityId(9), point),
            new HarvestCommand(player, tick, sequence++, Units(10, 11), cell),
            new SellCommand(player, tick, sequence++, new EntityId(12)),
            new ToggleRepairCommand(player, tick, sequence++, new EntityId(13)),
            new PlaceWallsCommand(player, tick, sequence++, 6, [cell, new CellPos(13, 34)]),
            new AttackCommand(player, tick, sequence++, Units(14), new EntityId(15)),
            new ForceAttackCommand(player, tick, sequence++, Units(16), null, point),
            new SetStanceCommand(player, tick, sequence++, Units(17, 18), Stance.HoldPosition),
            new EnterCommand(player, tick, sequence++, Units(19, 20), new EntityId(21)),
            new EvacuateCommand(player, tick, sequence++, new EntityId(21)),
            new SuperweaponCommand(player, tick, sequence++, new EntityId(22), cell, new CellPos(13, 34)),
            new SurrenderCommand(player, tick, sequence++),
            new PlayerLeftCommand(player, tick, sequence),
        ];
    }

    public static MatchSetup Setup() => new(
        "frozen_crossing",
        0xDEAD_BEEF_0042UL,
        new MatchOptions(StartingCredits: 7500, ShortGame: true, GameSpeed: 5, StartingUnits: 7),
        [
            new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", 0, 1, 0, new CellPos(10, 10)) { Country = "russia" },
            new MatchPlayer(2, PlayerKind.Ai, AiDifficulty.Medium, "union", 3, 2, 1, new CellPos(50, 50)),
        ],
        [new CellPos(10, 10), new CellPos(50, 50), new CellPos(10, 50)]);

    /// <summary>One message of every <see cref="MessageType"/>.</summary>
    public static IReadOnlyList<NetMessage> EveryMessage() =>
    [
        new Hello("0.9.0+abc", 38, 0x0123_4567_89AB_CDEFUL, "Петя", "K7QX9P"),
        new Welcome(3),
        new Reject(RejectReason.DataHash, "data differ"),
        new LobbyState("frozen_crossing", 0xFEED_F00DUL, new MatchOptions(10_000, false, 4),
        [
            new LobbySlot(1, PlayerKind.Human, AiDifficulty.Medium, "Петя", "union", "", 0, 1, LobbySlot.RandomStart, true),
            new LobbySlot(2, PlayerKind.Ai, AiDifficulty.Medium, "Bot", "union", "russia", 5, 2, 1, false),
        ]),
        new LobbyChange("union", "russia", 2, 1, LobbySlot.RandomStart, true),
        new StartGame(Setup(), 15, 15, 0xABCDUL, 4),
        new LoadProgress(P2, 64),
        new AllLoaded(),
        new TickBundle(P2, 1234, EveryCommand(P2, 1234)),
        new Checksum(P2, 1500, 0x1122_3344_5566_7788UL),
        new Ping(1_700_000_000_123L),
        new Pong(-5L),
        new PauseRequest(P2, Pause: true),
        new Pause(P2, PauseReason.Player, 900),
        new Resume(new PlayerId(1), 1800),
        new PlayerLeft(P2, LeaveReason.Timeout, 4321),
        new Surrender(P2),
        new DesyncReport(P2, 3000, 42UL),
        new InputDelayChange(4500, 6),
        new KickVote(P2, new PlayerId(3)),
        new RoomRequest("K7MX2P"),
        new RoomJoined("K7MX2P", 3),
        new RoomMember(2, false),
        new Relayed(3, MessageCodec.Write(new Ping(7))),
    ];
}
