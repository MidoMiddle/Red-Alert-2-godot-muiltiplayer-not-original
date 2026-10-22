using System;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The credits counter of the sidebar rolls to the real amount instead of jumping, as in RA2 (docs/implementation/M3_base_economy.md,
/// 4.17): every frame it covers an eighth of what is left, at least one credit, and never goes past the amount.
/// </summary>
public static class CreditsRoll
{
    public static int Next(int shown, int actual)
    {
        var left = actual - shown;
        if (left == 0)
        {
            return actual;
        }

        var step = Math.Max(1, Math.Abs(left) / 8);
        return left > 0 ? Math.Min(actual, shown + step) : Math.Max(actual, shown - step);
    }
}

/// <summary>How the power strip looks: normal, close to the limit, or short of power.</summary>
public enum PowerLevel
{
    Normal,
    Tight,
    Low,
}

/// <summary>
/// The power strip of the sidebar (4.17): the output and the drain as shares of a scale that grows in steps of 100 with some room on
/// top, and the colour of the strip.
/// </summary>
public readonly record struct PowerGauge(int Scale, float Output, float Drain, PowerLevel Level)
{
    /// <summary>The share of the output above which the drain counts as close to the limit.</summary>
    public const int TightPercent = 85;

    public static PowerGauge Of(int output, int drain, bool low)
    {
        var top = Math.Max(output, drain) * 5 / 4;
        var scale = Math.Max(100, (top + 99) / 100 * 100);
        var level = low ? PowerLevel.Low : drain * 100 > output * TightPercent ? PowerLevel.Tight : PowerLevel.Normal;
        return new PowerGauge(scale, output / (float)scale, drain / (float)scale, level);
    }
}

/// <summary>
/// Whether the minimap works (4.17, decision of the owner 2026-10-22): in a match only with a ready radar building of the player and
/// enough power, as in RA2; in the sandbox the rule is off and the minimap is always open.
/// </summary>
public static class MinimapRule
{
    public static bool Online(GameWorld world, PlayerId player, bool ruleOn)
    {
        if (!ruleOn)
        {
            return true;
        }

        if (world.Player(player).LowPower)
        {
            return false;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def.Radar: true } building && building.Owner == player)
            {
                return true;
            }
        }

        return false;
    }
}
