# Rts.Arena

Matches of bots against bots with no window, as fast as the machine goes (docs/implementation/M8_ai_difficulty.md, 4.10). The levels
are tuned by them (M8, step 10). Step 4 made the minimal Arena: one pair of bots on one map, results as CSV, every replay checked.
Matrices from YAML, parallel runs and `summary.md` come with step 9.

## Run

```bash
dotnet run --project tools/Rts.Arena -c Release -- run --a medium:balanced --b easy:balanced --map frozen-crossing --games 2
```

| Option | Meaning | Default |
|---|---|---|
| `--a`, `--b` | The bots: `level[:personality[:faction]]` — `easy`, `medium`, `hard`, `brutal`; personalities of `game/data/ai/personalities` | personality `balanced`, faction `union` |
| `--map` | A map of `game/data/maps`, by id | — |
| `--games` | How many matches | 1 |
| `--minutes` | A match not won by then is a draw | 60 |
| `--seed` | The seed of the first match; the next ones +1 | 1000 |
| `--out` | The folder of the results | `arena-results/<date-time>` |
| `--data` | The data of the game | `game/data` here or above |

- A plays slot 1 (blue), B slot 2 (red). On every second match A and B swap starts.
- Every seat is a bot: the replay keeps them as bots.
- Each replay is played back twice right after its match: without the bots, from the recorded commands, and with the bots run again. A replay that does not match makes the code 1.

## Check a replay

```bash
dotnet run --project tools/Rts.Arena -c Release -- replay arena-results/<folder>/game-0001.rtsreplay --a medium:balanced --b easy:balanced
```

- Give the personalities the match was played with: the replay does not keep them yet (M8, step 13).
- Code 0: the replay matches. Code 1: it does not, with the reason (`without-bots:desync@<tick>`, `with-bots:botsdiffer`…).

Code 2 for every run: a wrong command line, profile, map or data, said in plain words.

## `results.csv`

One line per match. The file is the same, byte for byte, on every run of the same matches:

| Column | Meaning |
|---|---|
| `game`, `map`, `seed` | The number of the match, its map and seed |
| `a`, `b` | The profiles in full: `hard:balanced:union` |
| `a_start`, `b_start` | The start of the map each side had |
| `winner` | `a`, `b` or `draw` |
| `end_tick`, `end_time` | When the match ended (15 ticks a second), and as minutes:seconds |
| `*_units_built`, `*_units_lost`, `*_buildings_built`, `*_buildings_lost` | From the statistics of the match (M5, step 11) |
| `*_harvested`, `*_spent` | Credits from ore; credits paid |
| `*_first_attack` | The tick the side's first squad set out; `-1` — none did |
| `replay` | `match`, or what went wrong in the check |

## `timings.csv`

How long the bots thought, in milliseconds: the mean and the longest turn of each side, and the seconds the whole match took. It is
different on every run and every machine, so it is kept apart from `results.csv`.
