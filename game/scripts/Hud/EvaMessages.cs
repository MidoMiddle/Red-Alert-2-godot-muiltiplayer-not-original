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
