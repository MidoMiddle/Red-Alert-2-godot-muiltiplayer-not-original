# Rts.Arena

Matches of bots against bots with no window, as fast as the machine goes (docs/implementation/M8_ai_difficulty.md, 4.10). The levels
are tuned by them (M8, step 10). Step 4 made the minimal Arena — one pair on one map; step 9 the full one — matrices from YAML, matches in
parallel, `summary.md`, the replays the matrix keeps, the journals of their bots.

## Run a matrix

```bash
dotnet run --project tools/Rts.Arena -c Release -- run --config tools/Rts.Arena/configs/acceptance.yaml
```

| Option | Meaning | Default |
|---|---|---|
| `--config` | The matrix | — |
| `--out` | The folder of the results | `arena-results/<date-time>` |
| `--jobs` | Matches at once; the results do not depend on it | the cores of the machine |
| `--data` | The data of the game | `game/data` here or above |

A matrix, the keys of 4.10:

```yaml
games: 50                       # matches for every pair on every map
swapSides: true                 # every second match of a pair on a map the sides swap starts
maxGameTime: 60m                # then a draw; 90s works too
baseSeed: 1000                  # the seed of the first match, the next ones +1
maps: [frozen-crossing, twin-rivers, random:size=96,players=2]
pairs:
  - { a: hard:balanced, b: medium:balanced }
saveReplays: [draws, longest:3, upsets]
```

- **`maps`** — the id of a ready map of `game/data/maps`, or `random:` and the settings of the map generator: `size` (64, 96, 128,
  160), `players` (2, 4), `relief`, `water`, `forest`, `resources`. A random map is made anew for every match with its seed, so every
  match plays back; the map is in the replay.
- **`saveReplays`** — `all`, `draws`, `upsets` (a lower level won), `longest:N`. Only these replays are written, and each is checked:
  played back without the bots and with them run again. The others are not — that takes the time of two more matches.
- The matches are numbered pair by pair, map by map; match `k` (from 0) has the seed `baseSeed + k`.
- A mistake in the matrix is told with the file, the line and the column; code 2.

Ready matrices, in `configs/`:

| Matrix | What for |
|---|---|
| `acceptance.yaml` | The criteria of M8, section 7: the levels against each other, the personalities of «Hard» against «Balanced»; 900 matches |
| `personalities.yaml` | Every personality against every other, all «Hard» |
| `smoke.yaml` | Four matches of ten minutes: a test runs it on every push |
| `tuning.yaml` | The levels against each other and «Hard» against «Easy», 60 matches a pair: a tuning step (M8 step 10) |

CI runs `acceptance.yaml` once a day and by hand (`.github/workflows/arena.yml`); the results are the artifact `arena-acceptance`.

## Tune the levels

How step 10 of M8 tuned them, to do again:

1. Copy `game/data` somewhere and change the keys of the level or the personality there (`ai/difficulty.yaml`, `ai/personalities`).
2. Run the same matrix on both: `run --config configs/tuning.yaml --data <copy>`. The seeds are the same, so the matches differ by the
   change only.
3. Compare the wins of A among the matches decided. Sixty matches with a third of draws leave about forty decided: a difference below ten
   points is noise. For a decision, 60 matches a map (`games: 60`) — 180 a pair, about three minutes.
4. To find what makes a level weak, give it the keys of the other level and bring its own back one group at a time: the group that
   changes the wins is the one to look at — then in the journals of the kept matches (`game-NNNN-pN.log`), minute by minute.
5. Confirm with `acceptance.yaml` before the data goes to the game; the summary goes into the journal of the milestone.

## `summary.md`

- **Wins** — every pair, over all maps and map by map: the wins of A and B, the draws, the share of A with its 95 % interval of Wilson,
  the mean length of the matches decided, the share of draws.
- **Profiles** — the first attack on average, the sides that never attacked, the harvesters of the enemy destroyed, the army units built
  most.
- **Personalities** — the first attack and the harvesters destroyed, for the criteria of 4.7.
- **Replays kept** — why each, and its check.
- **Time of the bots** — the mean and the longest turn by profile, the turns over 4 ms, whether the budget of 4.11 holds. The only part
  that differs from run to run.

## Journals

For every kept match, `game-NNNN-pN.log` for each bot: what it thinks — the snapshot of the overlay F12 — once a minute. The journal of
its decisions comes with the debug window, M8 step 12.

## One pair

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
| `--jobs` | Matches at once | the cores of the machine |
| `--data` | The data of the game | `game/data` here or above |

- A plays slot 1 (blue), B slot 2 (red). On every second match A and B swap starts.
- Every seat is a bot: the replay keeps them as bots.
- One pair is a matrix of one pair and one map that keeps every replay: each is played back twice after the matches, without the bots,
  from the recorded commands, and with the bots run again. A replay that does not match makes the code 1.

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
| `*_units` | The army units it built, by id: `heavy_tank:5;conscript:3` |
| `*_harvesters_killed` | The harvesters of the enemy it destroyed |
| `replay` | `match`, or what went wrong in the check; `-` — not kept |

## `timings.csv`

How long the bots thought, in milliseconds: the mean and the longest turn of each side, its turns over 4 ms, and the seconds the whole
match took. It is different on every run and every machine, so it is kept apart from `results.csv`.
