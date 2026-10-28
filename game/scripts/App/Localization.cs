using Godot;

namespace Rts.Client.App;

/// <summary>
/// Loads <c>localization/en.csv</c> into the <see cref="TranslationServer"/> at startup and sets the locale.
/// The CSV is read at runtime instead of through Godot's import: the imported <c>*.translation</c> files are
/// git-ignored and cannot be listed in project settings, so a clean clone would fail to load them.
/// Only English ships for now.
/// </summary>
public static class Localization
{
    /// <summary>The only locale that ships for now.</summary>
    public const string Locale = "en";

    private const string CsvPath = "res://localization/en.csv";

    private static bool _loaded;

    /// <summary>
    /// Loads the texts once: Boot does it at startup, and a match scene started on its own (the editor, <c>res://scenes/Main.tscn</c>
    /// from the command line) does it too, so that its texts are not raw keys.
    /// </summary>
    public static void Load()
    {
        if (_loaded)
        {
            return;
        }

        var file = FileAccess.Open(CsvPath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushError($"[i18n] cannot open {CsvPath}: {FileAccess.GetOpenError()}");
            return;
        }

        var translation = new Translation { Locale = Locale };
        var isHeader = true;
        while (!file.EofReached())
        {
            var line = file.GetLine();
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            var comma = line.IndexOf(',');
            if (comma <= 0)
            {
                continue;
            }

            // A text with a comma is quoted, as in a CSV: its quotes are not part of it, and a doubled quote inside is one.
            var text = line[(comma + 1)..];
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            {
                text = text[1..^1].Replace("\"\"", "\"");
            }

            translation.AddMessage(line[..comma], text);
        }

        TranslationServer.AddTranslation(translation);
        TranslationServer.SetLocale(Locale);
        _loaded = true;
    }
}
