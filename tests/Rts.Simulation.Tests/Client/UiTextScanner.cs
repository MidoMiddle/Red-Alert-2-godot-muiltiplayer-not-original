using System.Text.RegularExpressions;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Finds player-visible text that does not come from a localization key. It works on file text only,
/// so the rules are also tested with inline fixtures (see <c>UiKeysTests</c>).
/// </summary>
internal static class UiTextScanner
{
    private static readonly Regex KeyPattern = new(@"^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);

    // text = "..." in a scene, for the properties that show text to the player.
    private static readonly Regex SceneText = new(
        @"^\s*(text|tooltip_text|placeholder_text|title)\s*=\s*""((?:[^""\\]|\\.)*)""\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    // Text = ...; and similar assignments in code.
    private static readonly Regex CodeAssignment = new(
        @"\b(?:Text|TooltipText|PlaceholderText|Title)\s*=\s*([^;]+);",
        RegexOptions.CultureInvariant);

    // .AddItem(...); .SetItemText(...); .SetTabTitle(...); in code.
    private static readonly Regex CodeCall = new(
        @"\.(?:AddItem|SetItemText|SetTabTitle)\(([^;]*)\);",
        RegexOptions.CultureInvariant);

    private static readonly Regex TrCall = new(@"\bTr\(""([^""]+)""\)", RegexOptions.CultureInvariant);

    public static bool IsKey(string text) => KeyPattern.IsMatch(text);

    /// <summary>Violations in a scene: literal text in a player-visible property that is not a key.</summary>
    public static IEnumerable<string> ScanScene(string fileName, string text)
    {
        foreach (Match match in SceneText.Matches(text))
        {
            var value = match.Groups[2].Value;
            if (value.Length > 0 && !IsKey(value))
            {
                yield return $"{fileName}:{LineOf(text, match.Index)}: literal text \"{value}\"";
            }
        }
    }

    /// <summary>Violations in code: a literal string given to a text sink without <c>Tr</c>.</summary>
    public static IEnumerable<string> ScanCode(string fileName, string text)
    {
        foreach (Match match in CodeAssignment.Matches(text))
        {
            var value = match.Groups[1].Value.Trim();
            if (IsLiteralWithoutTr(value))
            {
                yield return $"{fileName}:{LineOf(text, match.Index)}: literal text {value}";
            }
        }

        foreach (Match match in CodeCall.Matches(text))
        {
            var arguments = match.Groups[1].Value.Trim();
            if (IsLiteralWithoutTr(arguments))
            {
                yield return $"{fileName}:{LineOf(text, match.Index)}: literal text {arguments}";
            }
        }
    }

    /// <summary>Keys used as <c>Tr("KEY")</c> in code.</summary>
    public static IEnumerable<string> KeysInCode(string text) =>
        TrCall.Matches(text).Select(match => match.Groups[1].Value);

    /// <summary>Keys written as text in a scene.</summary>
    public static IEnumerable<string> KeysInScene(string text) =>
        SceneText.Matches(text).Select(match => match.Groups[2].Value).Where(IsKey);

    private static bool IsLiteralWithoutTr(string expression) =>
        expression.Contains('"', StringComparison.Ordinal)
        && !expression.Contains("Tr(", StringComparison.Ordinal)
        && expression != "\"\"";

    private static int LineOf(string text, int index) => text.Take(index).Count(c => c == '\n') + 1;
}
