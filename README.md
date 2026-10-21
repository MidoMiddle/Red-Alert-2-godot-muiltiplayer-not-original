# RA2 Godot (working title)

A real-time strategy game inspired by Red Alert 2, built with Godot 4 (.NET / C#). It plays like the original and looks like a 2026 remaster. Runs natively on macOS and Windows, with skirmish against bots and online multiplayer.

> Unofficial fan project. It does not use any original Command & Conquer assets and is not affiliated with or endorsed by Electronic Arts.

## Goals

- **The Red Alert 2 feel:** familiar controls, sidebar, pacing, unit voices and humor.
- **Modern visuals:** real 3D with the classic isometric camera, lighting, shadows and effects.
- **Fair depth:** clear unit counters, meaningful economy, bots that win by making decisions, not by cheating.
- **Play together:** cross-platform multiplayer (macOS ↔ Windows), lobby, ranking and replays.

## Status

Milestone M0 (foundation) in progress on the `m0/foundation` branch: deterministic simulation core and a debug Godot client. All design documentation lives in [docs/](docs/README.md); start with the [top-level concept](docs/00_concept.md).

**How to run the game for testing:** [docs/how_to_run.md](docs/how_to_run.md).

> Design documents are currently written in Russian.

## Tech stack

- Godot 4.7 .NET, C# (.NET 8+)
- Deterministic lockstep simulation with fixed-point math (for cross-platform multiplayer)
- Blender for 3D assets
- Git LFS for binary files (models, textures, audio)

## Getting started

Requirements: [Godot 4.7 .NET](https://godotengine.org/download), [.NET SDK 8+](https://dotnet.microsoft.com/download), [Git LFS](https://git-lfs.com).

```bash
git lfs install
git clone git@github.com:MidoMiddle/Red-Alert-2-godot-muiltiplayer-not-original.git
```

## License

Code is licensed under the [GNU General Public License v3.0](LICENSE). Asset licensing will be defined separately.
