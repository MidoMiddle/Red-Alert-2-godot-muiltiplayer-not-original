// dotnet run --project tools/Rts.Arena -- run --a medium:balanced --b easy:balanced --map frozen-crossing --games 2
//     [--minutes 60] [--seed 1000] [--out arena-results/<time>] [--data game/data]
//         plays the matches, writes results.csv, timings.csv and the replays, checks every replay
// dotnet run --project tools/Rts.Arena -- replay <game.rtsreplay> [--data game/data]
//         plays a replay back without its bots and with them run again; 0 when both match
// A profile is level[:personality[:faction]] — easy, medium, hard, brutal; balanced and union by default.
return Rts.Arena.ArenaCommand.Execute(args, Console.Out, Console.Error);
