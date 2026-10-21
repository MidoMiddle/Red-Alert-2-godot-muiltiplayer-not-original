using System;
using System.Globalization;
using System.Text;

namespace Rts.Client.Hud;

/// <summary>Numbers as the sidebar shows them (docs/08_ui_ux.md, section 4).</summary>
public static class HudFormat
{
    /// <summary>Money as the docs write it: "$ 10 000", with a space between groups of three digits.</summary>
    public static string Credits(int amount)
    {
        var digits = Math.Abs((long)amount).ToString(CultureInfo.InvariantCulture);
        var text = new StringBuilder();
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                text.Append(' ');
            }

            text.Append(digits[i]);
        }

        return (amount < 0 ? "$ -" : "$ ") + text;
    }
}
