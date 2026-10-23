using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Matches;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.UI;

/// <summary>
/// The survey of the tests by people on the results screen (docs/implementation/M8_ai_difficulty.md, section 6, step 14), when
/// Settings → Game → Playtest survey is on: newcomer or veteran (kept for the next match), how hard (1–5), where the bot was stupid and
/// where unfair, the personality of every enemy bot as the person guesses it — the results hide it until then — how much a bot ally
/// helped, notes. Submit hands the answers on; Skip — none. The controls are named for the input scripts.
/// </summary>
public partial class PlaytestSurvey : VBoxContainer
{
    private const float FieldWidth = 420f;
    private const float FieldHeight = 40f;
    private const int DefaultDifficulty = 3;
    private const string RoleFile = PlaytestFiles.Folder + "/role.txt";

    private static readonly string[] Roles = [PlaytestRecord.Newcomer, PlaytestRecord.Veteran];

    private readonly Dictionary<int, OptionButton> _guesses = [];
    private List<string> _personalities = [];
    private OptionButton _role = null!;
    private OptionButton _difficulty = null!;
    private LineEdit _stupid = null!;
    private LineEdit _unfair = null!;
    private OptionButton? _ally;
    private LineEdit _notes = null!;

    /// <summary>The answers of the person.</summary>
    public event Action<PlaytestAnswers>? Submitted;

    /// <summary>The person went on without answering.</summary>
    public event Action? Skipped;

    /// <summary>
    /// The questions for the match of <paramref name="match"/> as <paramref name="local"/> played it; <paramref name="botName"/> names
    /// a bot as the results table does, its personality hidden.
    /// </summary>
    public void Build(MatchSetup match, PlayerId local, Func<MatchPlayer, string> botName)
    {
        Name = "PlaytestSurvey";
        AddThemeConstantOverride("separation", 8);
        AddChild(new Label { Text = Tr("UI_SURVEY_TITLE"), HorizontalAlignment = HorizontalAlignment.Center });
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(grid);

        _role = Option("SurveyRole", Tr("UI_SURVEY_NEWCOMER"), Tr("UI_SURVEY_VETERAN"));
        _role.Selected = Math.Max(0, Array.IndexOf(Roles, ReadRole()));
        Row(grid, Tr("UI_SURVEY_ROLE"), _role);
        _difficulty = Option("SurveyDifficulty", [.. Enumerable.Range(1, 5).Select(n => n.ToString(CultureInfo.InvariantCulture))]);
        _difficulty.Selected = DefaultDifficulty - 1;
        Row(grid, Tr("UI_SURVEY_DIFFICULTY"), _difficulty);
        _stupid = Line("SurveyStupid", Tr("UI_SURVEY_WHERE_HINT"));
        Row(grid, Tr("UI_SURVEY_STUPID"), _stupid);
        _unfair = Line("SurveyUnfair", Tr("UI_SURVEY_WHERE_HINT"));
        Row(grid, Tr("UI_SURVEY_UNFAIR"), _unfair);

        _personalities = [PlaytestRecord.CannotTell, .. DataFiles.Current.Rules.Database.Personalities.Select(personality => personality.Id)];
        var team = match.PlayerOf(local)?.Team;
        foreach (var bot in match.Players.Where(player => player.Kind == PlayerKind.Ai).OrderBy(player => player.Slot))
        {
            if (bot.Team == team)
            {
                continue;
            }

            var guess = Option($"SurveyGuess{bot.Slot}");
            foreach (var id in _personalities)
            {
                guess.AddItem(id == PlaytestRecord.CannotTell ? Tr("UI_SURVEY_CANNOT_TELL") : SkirmishNames.Personality(this, id));
            }

            _guesses[bot.Slot] = guess;
            Row(grid, string.Format(Tr("UI_SURVEY_GUESS"), botName(bot)), guess, PlayerColors.Of(bot.Id));
        }

        if (match.Players.Any(player => player.Kind == PlayerKind.Ai && player.Team == team))
        {
            _ally = Option("SurveyAlly", [.. Enumerable.Range(1, 5).Select(n => n.ToString(CultureInfo.InvariantCulture))]);
            _ally.Selected = DefaultDifficulty - 1;
            Row(grid, Tr("UI_SURVEY_ALLY"), _ally);
        }

        _notes = Line("SurveyNotes", "");
        Row(grid, Tr("UI_SURVEY_NOTES"), _notes);

        var buttons = new HBoxContainer { Alignment = AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        AddChild(buttons);
        var submit = new Button { Name = "SurveySubmit", Text = Tr("UI_SURVEY_SUBMIT"), CustomMinimumSize = new Vector2(200, 44) };
        submit.Pressed += Submit;
        buttons.AddChild(submit);
        var skip = new Button { Name = "SurveySkip", Text = Tr("UI_SURVEY_SKIP"), CustomMinimumSize = new Vector2(200, 44) };
        skip.Pressed += () => Skipped?.Invoke();
        buttons.AddChild(skip);
    }

    private void Submit()
    {
        var role = Roles[Math.Max(0, _role.Selected)];
        WriteRole(role);
        Submitted?.Invoke(new PlaytestAnswers(
            role,
            _difficulty.Selected + 1,
            _stupid.Text,
            _unfair.Text,
            _ally is null ? 0 : _ally.Selected + 1,
            _notes.Text,
            _guesses.ToDictionary(pair => pair.Key, pair => _personalities[Math.Max(0, pair.Value.Selected)])));
    }

    private static string ReadRole() =>
        Godot.FileAccess.FileExists(RoleFile) ? Godot.FileAccess.GetFileAsString(RoleFile).Trim() : PlaytestRecord.Newcomer;

    private static void WriteRole(string role)
    {
        DirAccess.MakeDirRecursiveAbsolute(PlaytestFiles.Folder);
        using var file = Godot.FileAccess.Open(RoleFile, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(role);
    }

    private static void Row(GridContainer grid, string caption, Control field, Color? swatch = null)
    {
        var label = new HBoxContainer();
        if (swatch is { } color)
        {
            label.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        }

        label.AddChild(new Label { Text = caption, VerticalAlignment = VerticalAlignment.Center });
        grid.AddChild(label);
        grid.AddChild(field);
    }

    private static OptionButton Option(string name, params string[] items)
    {
        var option = new OptionButton { Name = name, CustomMinimumSize = new Vector2(FieldWidth, FieldHeight) };
        foreach (var item in items)
        {
            option.AddItem(item);
        }

        return option;
    }

    private static LineEdit Line(string name, string placeholder) =>
        new() { Name = name, PlaceholderText = placeholder, CustomMinimumSize = new Vector2(FieldWidth, FieldHeight) };
}
