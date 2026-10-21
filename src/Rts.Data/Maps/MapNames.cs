using Rts.Simulation.Map;

namespace Rts.Data.Maps;

/// <summary>The characters the <c>.rtsmap</c> format allows in names and codes, and the codes of its layers.</summary>
public static class MapNames
{
    /// <summary>Height levels 0…14 as one character each: 0–9, then a–e.</summary>
    public const string HeightCodes = "0123456789abcde";

    /// <summary>Ore per cell: '.' none, 1–9 ore density, A–C gems (values 10–12).</summary>
    public const string OreCodes = ".123456789ABC";

    /// <summary>Lowercase letters, digits, '-' and '_', not empty: ids of maps, climates, materials and types.</summary>
    public static bool IsId(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A single line of text without control characters (it may be empty).</summary>
    public static bool IsText(string text)
    {
        foreach (var c in text)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Texture legend codes are ASCII letters and digits, so they never clash with YAML syntax.</summary>
    public static bool IsLegendCode(char code) => code is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    public static char LandCode(LandType land) => TestMapParser.CodeOf(land);

    /// <summary>The land type of a code of table 4.3, or null for an unknown code.</summary>
    public static LandType? LandOf(char code)
    {
        for (var land = LandType.Clear; land <= LandType.Ice; land++)
        {
            if (TestMapParser.CodeOf(land) == code)
            {
                return land;
            }
        }

        return null;
    }
}
