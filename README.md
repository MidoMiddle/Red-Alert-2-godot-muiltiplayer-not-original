# RA2 Godot (working title)

[![CI](https://github.com/MidoMiddle/Red-Alert-2-godot-muiltiplayer-not-original/actions/workflows/ci.yml/badge.svg)](https://github.com/MidoMiddle/Red-Alert-2-godot-muiltiplayer-not-original/actions/workflows/ci.yml)

A real-time strategy game inspired by Red Alert 2, built with Godot 4 (.NET / C#). It plays like the original and looks like a 2026 remaster. Runs natively on macOS and Windows, with skirmish against bots and online multiplayer.

> Unofficial fan project. It does not use any original Command & Conquer assets and is not affiliated with or endorsed by Electronic Arts.

## Goals

- **The Red Alert 2 feel:** familiar controls, sidebar, pacing, unit voices and humor.
- **Modern visuals:** real 3D with the classic isometric camera, lighting, shadows and effects.
- **Fair depth:** clear unit counters, meaningful economy, bots that win by making decisions, not by cheating.
- **Play together:** cross-platform multiplayer (macOS ↔ Windows), lobby, ranking and replays.

## Status

Milestones **M0 (foundation)** and **M2 (unit control)** are complete: a deterministic simulation core verified bit for bit on Linux, macOS and Windows, and units that move like in a good RTS — turning, A* pathfinding with smoothing, cell and infantry-spot reservations, group destinations, giving way, waypoints and patrols — with RA2-style selection, control groups, cursors, a minimap and debug overlays in a symbolic debug client. 300 units cross a 128×128 map at under 0.2 ms per tick. Next: M7, maps. All design documentation lives in [docs/](docs/README.md); start with the [top-level concept](docs/00_concept.md).

**How to run the game for testing:** [docs/how_to_run.md](docs/how_to_run.md).

> Design documents are currently written in Russian.

## Tech stack

- Godot 4.7 .NET, C# (.NET 8+)
- Deterministic lockstep simulation with fixed-point math (for cross-platform multiplayer)
- Blender for 3D assets
- Git LFS for binary files (models, textures, audio)

## Getting started

Requirements: [Godot 4.7 .NET](https://godotengine.org/download), [.NET SDK 10](https://dotnet.microsoft.com/download) (the projects target .NET 8; the SDK version is pinned in `global.json`), [Git LFS](https://git-lfs.com).

```bash
git lfs install
git clone git@github.com:MidoMiddle/Red-Alert-2-godot-muiltiplayer-not-original.git
```

Build and test everything:

```bash
dotnet build RtsGame.sln
dotnet test RtsGame.sln
```

Run the game: open `game/project.godot` in Godot 4.7 .NET and press F5, or from the repository root:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --path game
```

Scripted smoke check without a window (exit code 0 on success):

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path game -- --smoke
```

Performance of the load scenario (300 units) and BenchmarkDotNet benchmarks:

```bash
dotnet run -c Release --project src/Rts.Simulation.Benchmarks -- --load
```

Step-by-step guide with controls and a test checklist (in Russian): [docs/how_to_run.md](docs/how_to_run.md).

## License

Code is licensed under the [GNU General Public License v3.0](LICENSE). Asset licensing will be defined separately.
