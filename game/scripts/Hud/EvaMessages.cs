using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The notices of the base, the text of RA2's EVA (docs/implementation/M3_base_economy.md, 4.16): which event of the local player
/// says what, as a localization key, and how often the same notice may come. The voice comes in M6.
/// </summary>
public sealed class EvaMessages
{
    /// <summary>The same notice comes once in this many seconds at most.</summary>
    public const double RepeatSeconds = 3;

    /// <summary>"Insufficient funds" comes once in this many seconds at most: production waits on it every tick.</summary>
    public const double FundsRepeatSeconds = 10;

    private readonly Dictionary<string, double> _lastShown = new();

    /// <summary>The key of what an event says to <paramref name="player"/>, or null when it says nothing to them.</summary>
    public static string? KeyOf(SimEvent simEvent, PlayerId player) => simEvent switch
    {
        ProductionReady ready when ready.Player == player => "UI_EVA_CONSTRUCTION_COMPLETE",
        UnitProduced produced when produced.Player == player => "UI_EVA_UNIT_READY",
        InsufficientFunds funds when funds.Player == player => "UI_EVA_INSUFFICIENT_FUNDS",
        LowPower low when low.Player == player => "UI_EVA_LOW_POWER",
        PowerRestored restored when restored.Player == player => "UI_EVA_POWER_RESTORED",
        BuildingSold sold when sold.Player == player => "UI_EVA_BUILDING_SOLD",
        ProductionCancelled cancelled when cancelled.Player == player => "UI_EVA_CANCELLED",
        CannotDeploy refused when refused.Player == player => "UI_EVA_CANNOT_DEPLOY",
        CannotPlace refused when refused.Player == player => "UI_EVA_CANNOT_PLACE",
        OreDepleted => "UI_EVA_ORE_DEPLETED",
        UnderAttack attack when attack.Player == player => "UI_EVA_UNDER_ATTACK",
        PlayerDefeated defeated => defeated.Player == player ? "UI_EVA_YOU_ARE_DEFEATED" : "UI_EVA_PLAYER_DEFEATED",
        MatchEnded ended => ended.Winners.Contains(player) ? "UI_EVA_VICTORY" : "UI_EVA_DEFEAT",
        // Loose ends, stage 2: a bridge mended.
        BridgeRepaired => "UI_EVA_BRIDGE_REPAIRED",
        // Loose ends, stage 1: a building taken by the player, or from them.
        BuildingCaptured captured when captured.To == player => "UI_EVA_BUILDING_CAPTURED",
        BuildingCaptured captured when captured.From == player => "UI_EVA_BUILDING_LOST",
        // Stage 2d: every player hears of a superweapon ready and fired.
        SuperweaponReady ready => ready.Player == player ? "UI_EVA_SUPERWEAPON_READY" : "UI_EVA_ENEMY_SUPERWEAPON_READY",
        SuperweaponFired fired => fired.Kind switch
        {
            Rts.Simulation.Rules.SuperweaponKind.Curtain => "UI_EVA_CURTAIN_ACTIVATED",
            Rts.Simulation.Rules.SuperweaponKind.Missile => "UI_EVA_MISSILE_LAUNCHED",
            Rts.Simulation.Rules.SuperweaponKind.Gate => "UI_EVA_GATE_ACTIVATED",
            Rts.Simulation.Rules.SuperweaponKind.Paradrop => fired.Player == player ? "UI_EVA_REINFORCEMENTS" : null,
            _ => "UI_EVA_STORM_CREATED",
        },
        _ => null,
    };

    /// <summary>
    /// The key to show for an event at <paramref name="now"/> seconds, or null when the event says nothing to the player or the same
    /// notice was shown too recently.
    /// </summary>
    public string? Next(SimEvent simEvent, PlayerId player, double now)
    {
        if (KeyOf(simEvent, player) is not { } key)
        {
            return null;
        }

        var wait = key == "UI_EVA_INSUFFICIENT_FUNDS" ? FundsRepeatSeconds : RepeatSeconds;
        if (_lastShown.TryGetValue(key, out var last) && now - last < wait)
        {
            return null;
        }

        _lastShown[key] = now;
        return key;
    }
}
