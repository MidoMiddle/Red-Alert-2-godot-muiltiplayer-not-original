"""Converts a map of Red Alert 2 / Yuri's Revenge, unpacked by ra2_unpack.py, into our .rtsmap.

RA2 cells (x, y) are our cells: the same screen axes (x right-down, y left-down).
- Heights: the levels of the IsoMapPack5 tiles; a corner takes the highest of its cells, a corner by water is 0.
- Ground: every tile index takes the colour its cells have most often on the map's own preview (overlays and trees drawn over it
  left out): water, road, sand, dirt track, dark field or grass.
- Resources: ore (overlay 102-166) and gems (27-38) with their densities.
- Bridges: low bridges (74-101) over water, ours one cell wide along the middle line of each; high bridges (24-25) are
  bridge_concrete_high, three cells wide about the middle line, over whatever lies below (water or a valley, as in RA2), their ends
  carried on through the cliff cells to the flat shores.
- [Terrain]: trees (TREE*) become pines (the climate gives their look); the ore trees (TIBTRE*) nearest the starts become the gem
  drills, one an each start (M8 step 10b), the others are dropped; an ore drill stands by every start.
- [Structures]: by the table STRUCTURES (buildings) and OBJECTS (lamps, benches); what has no counterpart is listed and left out.
- Starts: waypoints 0 to MaxPlayer - 1 of [Basic] (4 when it is missing). The playable area: LocalSize, its top moved up by two of its units (RA2 shows a few rows above it).

Usage: ra2_to_rtsmap.py <unpacked dir> <out.rtsmap> --id ID --name NAME --author AUTHOR --description TEXT
                        [--climate temperate] [--lighting day]"""
import argparse, collections, json, math, os, re

parser = argparse.ArgumentParser()
parser.add_argument('dir'); parser.add_argument('out')
parser.add_argument('--id', required=True); parser.add_argument('--name', required=True)
parser.add_argument('--author', required=True); parser.add_argument('--description', required=True)
parser.add_argument('--climate', default='temperate'); parser.add_argument('--lighting', default='day')
args = parser.parse_args()
d = args.dir

tiles = json.load(open(d + '/iso.json'))
secs = json.load(open(d + '/sections.json'))
ov = open(d + '/overlay.bin', 'rb').read()
od = open(d + '/overlaydata.bin', 'rb').read()
N = max(max(t[0], t[1]) for t in tiles) + 1

# ---- The colours of the preview, per tile index.
raw = open(d + '/preview.ppm', 'rb').read()
header = raw[:raw.index(b'\n') + 1]
pw, ph = map(int, header.split()[1:3])
pixels = raw[len(header):]
umin = min(t[0] + t[1] for t in tiles)
vmin = min(t[0] - t[1] for t in tiles)

def colour(x, y):
    px, py = x - y - vmin, (x + y - umin) // 2
    if 0 <= px < pw and 0 <= py < ph:
        i = (py * pw + px) * 3
        return tuple(pixels[i:i + 3])
    return None

def ground_of(c):
    if c is None:
        return None
    r, g, b = c
    if (r > 240 and g > 240 and b < 40) or (r < 40 and g < 40 and b < 40):
        return None  # ore or an overlay drawn over the tile
    if b > r + 12 and b >= g:
        return 'water'
    if abs(r - g) < 10 and abs(g - b) < 16 and r < 140:
        return 'road'
    if r > 200 and g > 165:
        return 'sand'
    if r > 170 and g > 155:
        return 'track'
    if g > r and g > b:
        return 'grass' if g >= 155 else 'field'
    return None

votes = collections.defaultdict(collections.Counter)
for x, y, t, sub, lv in tiles:
    kind = ground_of(colour(x, y))
    if kind:
        votes[t][kind] += 1
tile_ground = {t: v.most_common(1)[0][0] for t, v in votes.items()}

level, kind = {}, {}
for x, y, t, sub, lv in tiles:
    if 0 <= x < N and 0 <= y < N:
        level[(x, y)] = lv
        kind[(x, y)] = 'grass' if t in (0, 65535) else tile_ground.get(t, 'grass')

# ---- Overlays.
LOW_BRIDGE, HIGH_BRIDGE = range(74, 102), (24, 25)
bridge_cells = {}
ore = {}
sandbags = []
walls = []
WALLS = (240,)  # the stone walls round the compounds of WAR Pro: walls of ours
for i, b in enumerate(ov):
    if b == 255:
        continue
    p = (i % 512, i // 512)
    if b in LOW_BRIDGE or b in HIGH_BRIDGE:
        bridge_cells[p] = b
        if b in LOW_BRIDGE:
            kind[p] = 'water'
    elif 102 <= b <= 166:
        ore[p] = 1 + round(od[i] * 8 / 11)
    elif 27 <= b <= 38:
        ore[p] = 10 + od[i] * 3 // 12  # gems A, B, C by their density
    elif b == 0:
        sandbags.append(p)  # GASAND: a low wall here is a wooden fence
    elif b in WALLS:
        walls.append(p)

# ---- Buildings and the sizes of ours (src/Rts.Simulation/World/BuildingTypes.cs).
here = os.path.dirname(os.path.abspath(__file__))
types = open(os.path.join(here, '..', '..', 'src', 'Rts.Simulation', 'World', 'BuildingTypes.cs')).read()
SIZES = {m[0]: (int(m[1]), int(m[2])) for m in re.findall(r'new\(BuildingKind\.\w+, "(\w+)", (\d+), (\d+)', types)}
STRUCTURES = {
    'CAOILD': 'tech_oil_derrick', 'CAAIRP': 'tech_airfield', 'CABHUT': 'tech_bridge_repair_hut', 'CALAB': 'tech_hospital',
    'CAMACH': 'neutral_office', 'CAPOWR': 'neutral_boiler_house', 'CAMISC01': 'neutral_fuel_tank', 'CAMISC02': 'neutral_silo',
    'CAHSE05': 'neutral_house_a', 'CAHSE06': 'neutral_house_a', 'CAARMY02': 'neutral_shed', 'CAARMY03': 'neutral_shed',
    'CAARMY04': 'neutral_shed', 'CAFRMB': 'neutral_barn', 'CATHOSP': 'tech_hospital',
    'CAOUTP': 'tech_outpost', 'CARUS02A': 'neutral_house_a', 'CARUS02B': 'neutral_house_a', 'CARUS07': 'neutral_house_a',
}
# The blocks of the towns of RA2 (New York, Chicago, Europe, Washington): offices here.
CITY = ('CANEWY', 'CANWY', 'CACHIG', 'CAEURO', 'CAWASH10')
OBJECTS = {'CASTRT03': 'lamp_post', 'CASTRT04': 'lamp_post', 'CASTRT05': 'lamp_post', 'TEMDAYLAMP': 'lamp_post',
           'CAPARK01': 'bench', 'CAPARK02': 'bench', 'CAPARK03': 'bench'}
structures, objects, skipped = [], [], collections.Counter()
footprints = set()
for _, value in secs.get('Structures', []):
    f = value.split(',')
    x0, y0 = int(f[3]), int(f[4])
    if f[1] in STRUCTURES or f[1].startswith(CITY):
        t = STRUCTURES.get(f[1], 'neutral_office'); w, h = SIZES[t]
        cells = {(x0 + dx, y0 + dy) for dx in range(w) for dy in range(h)}
        if cells & footprints or cells & bridge_cells.keys():
            skipped[f[1] + ' (overlaps)'] += 1; continue
        structures.append((t, x0, y0, (int(f[5]) // 32) % 8)); footprints |= cells
    elif f[1] in OBJECTS:
        objects.append((OBJECTS[f[1]], x0, y0, 0))
    else:
        skipped[f[1]] += 1

# A building of RA2 stands on the half-land of a shore tile; here its cells are land.
for p in footprints:
    if kind.get(p) == 'water' and p not in bridge_cells:
        kind[p] = 'sand'

# Cells the RA2 map leaves out (outside its diamond) take their nearest neighbour: scenery, never seen.
queue = collections.deque(level)
while queue:
    p = queue.popleft()
    for q in ((p[0] + 1, p[1]), (p[0] - 1, p[1]), (p[0], p[1] + 1), (p[0], p[1] - 1)):
        if 0 <= q[0] < N and 0 <= q[1] < N and q not in level:
            level[q] = level[p]; kind[q] = 'grass' if kind.get(p, 'grass') == 'water' else kind.get(p, 'grass'); queue.append(q)
for p in bridge_cells:
    level.setdefault(p, 0)
high_cells = {p for p, b in bridge_cells.items() if b in HIGH_BRIDGE}

water = {p for p, k in kind.items() if k == 'water' and 0 <= p[0] < N and 0 <= p[1] < N}
for p in water:
    level[p] = 0

def is_water(p):
    return p in water

def corner(x, y):
    if any((cx, cy) in water for cx in (x - 1, x) for cy in (y - 1, y)):
        return 0  # water lies low: a corner by it is at the water's level
    return max(level[(cx, cy)] for cx in (x - 1, x) for cy in (y - 1, y) if 0 <= cx < N and 0 <= cy < N)

heights = [[corner(x, y) for x in range(N + 1)] for y in range(N + 1)]

# RA2 lets a building stand where our corners make a slope: the ground under every building is levelled to the level most of its
# cells have.
for t, x0, y0, _ in structures:
    w, h = SIZES[t]
    flat = collections.Counter(level[(x0 + dx, y0 + dy)] for dx in range(w) for dy in range(h) if (x0 + dx, y0 + dy) in level)
    target = flat.most_common(1)[0][0] if flat else 0
    for cy in range(y0, y0 + h + 1):
        for cx in range(x0, x0 + w + 1):
            if 0 <= cx <= N and 0 <= cy <= N:
                heights[cy][cx] = target

land = {}
for y in range(N):
    for x in range(N):
        p = (x, y)
        if is_water(p):
            land[p] = 'W' if all(is_water(q) for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))) else '~'
        elif any(is_water((x + dx, y + dy)) for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
            land[p] = 'b'
        elif kind[p] == 'road':
            land[p] = '='
        elif kind[p] == 'track':
            land[p] = 'g'
        else:
            land[p] = '.'
TEXTURE = {'grass': 'g', 'field': 'f', 'sand': 'b', 'track': 'u', 'road': 'a', 'water': 'w'}
texture = {p: 'w' if l in '~W' else 'b' if l == 'b' else TEXTURE[kind[p]] for p, l in land.items()}

# ---- The playable area.
ms = dict(secs['Map'])
lx, ly, lw, lh = map(int, ms['LocalSize'].split(','))
area = (vmin + 2 * lx, umin + 1 + 2 * (ly - 2), vmin + 2 * (lx + lw), umin + 1 + 2 * (ly + lh))

def plays(x, y):
    u, v = x + y + 1, x - y
    return area[0] <= v <= area[2] and area[1] <= u <= area[3]

# ---- Starts, trees, drills.
max_players = int(dict(secs.get('Basic', [])).get('MaxPlayer', 4))
starts = sorted((int(k) + 1, int(v) % 1000, int(v) // 1000) for k, v in secs['Waypoints'] if int(k) < max_players)

def variant(x, y):
    return (x * 73856093 ^ y * 19349663) % 4

ore_trees = []
for key, value in secs.get('Terrain', []):
    n = int(key); x, y = n % 1000, n // 1000
    if not (0 <= x < N and 0 <= y < N) or is_water((x, y)) or (x, y) in footprints:
        continue
    if value.startswith('TIBTRE'):
        ore_trees.append((x, y))
    elif value.startswith('TREE'):
        objects.append(('pine_tree', x, y, variant(x, y)))
    elif value == 'LT_GEN':
        objects.append(('lamp_post', x, y, 0))
objects += [('fence_wood', x, y, 0) for x, y in sandbags if (x, y) not in footprints and not is_water((x, y))]
objects += [('wall_stone', x, y, 0) for x, y in walls if (x, y) not in footprints and not is_water((x, y))]
taken = {(x, y) for _, x, y, _ in objects} | footprints

# One gem drill and one ore drill by every start (M8 step 10b): the ore tree nearest it, and the field cell (or the free ground
# towards it) nearest it within 15.5 cells.
for _, sx, sy in starts:
    def far(p):
        return (p[0] - sx) ** 2 + (p[1] - sy) ** 2
    tree = None
    if ore_trees:
        tree = min(ore_trees, key=far); ore_trees.remove(tree)
        objects.append(('gem_source', tree[0], tree[1], variant(*tree))); taken.add(tree); ore.pop(tree, None)
        towards = tree
    else:
        towards = min(ore, key=far)
    best = min((p for p, value in ore.items() if value < 10 and p not in taken), key=far, default=towards)
    while far(best) > 15.5 ** 2 or best in taken or is_water(best) or best in footprints:
        best = (best[0] - (best[0] > sx) + (best[0] < sx), best[1] - (best[1] > sy) + (best[1] < sy))
    ore.pop(best, None)
    objects.append(('ore_source', best[0], best[1], variant(*best))); taken.add(best)
    if tree is None:
        # No ore tree on the map: the gem drill stands on free dry ground by the ore drill, on the side of the base.
        ring = sorted(((best[0] + dx, best[1] + dy) for dx in range(-3, 4) for dy in range(-3, 4) if max(abs(dx), abs(dy)) >= 2), key=far)
        gem = next(q for q in ring if q not in taken and q not in footprints and not is_water(q) and q not in ore)
        objects.append(('gem_source', gem[0], gem[1], variant(*gem))); taken.add(gem)

ore = {p: value for p, value in ore.items() if plays(*p) and not is_water(p) and p not in taken}
structures = [s for s in structures if all(plays(s[1] + dx, s[2] + dy) for dx in range(SIZES[s[0]][0]) for dy in range(SIZES[s[0]][1]))]
objects = [o for o in objects if 0 <= o[1] < N and 0 <= o[2] < N and not is_water((o[1], o[2]))]

# ---- Bridges: the middle line of every run of bridge overlay.
def rise(x, y):
    corners = [heights[y][x], heights[y][x + 1], heights[y + 1][x], heights[y + 1][x + 1]]
    return max(corners) - min(corners)

def flat_shore(p, lanes):
    # Flat dry land on every lane: where a high bridge's deck ends.
    return all(0 <= q[0] < N and 0 <= q[1] < N and rise(*q) == 0 and not is_water(q) for q in lanes(p))

runs, seen = [], set()
for p in sorted(bridge_cells):
    if p in seen:
        continue
    group, stack = [], [p]
    while stack:
        q = stack.pop()
        if q in seen or q not in bridge_cells:
            continue
        seen.add(q); group.append(q)
        stack += [(q[0] + 1, q[1]), (q[0] - 1, q[1]), (q[0], q[1] + 1), (q[0], q[1] - 1)]
    xs, ys = sorted({q[0] for q in group}), sorted({q[1] for q in group})
    if len(xs) >= len(ys):
        row = ys[len(ys) // 2]; cells = sorted(q[0] for q in group if q[1] == row)
        a, b, step, lanes = (cells[0], row), (cells[-1], row), (1, 0), lambda c: [(c[0], c[1] + k) for k in (-1, 0, 1)]
    else:
        col = xs[len(xs) // 2]; cells = sorted(q[1] for q in group if q[0] == col)
        a, b, step, lanes = (col, cells[0]), (col, cells[-1]), (0, 1), lambda c: [(c[0] + k, c[1]) for k in (-1, 0, 1)]
    high = p in high_cells
    if high:
        # The deck runs on until the cell beyond each end is a flat shore on all three lanes (RA2 marks the cells over the low
        # ground only; the cliffs it rests on are ours to cross).
        for _ in range(4):
            if flat_shore((a[0] - step[0], a[1] - step[1]), lanes):
                break
            a = (a[0] - step[0], a[1] - step[1])
        for _ in range(4):
            if flat_shore((b[0] + step[0], b[1] + step[1]), lanes):
                break
            b = (b[0] + step[0], b[1] + step[1])
    runs.append((a, b, 'bridge_concrete_high' if high else 'bridge_wood_low'))
runs = [r for r in runs if plays(*r[0]) and plays(*r[1])]

# ---- The file.
H, O = '0123456789abcde', '.123456789ABC'
quote = lambda text: '"' + text.replace('\\', '\\\\').replace('"', '\\"') + '"'
legend = sorted({c for c in texture.values()})
material = {'a': 'asphalt', 'b': 'shore', 'f': 'field', 'g': 'grass', 'u': 'track', 'w': 'water'}
lines = ['format: rtsmap/1', f'id: {args.id}', f'name: {quote(args.name)}', f'author: {quote(args.author)}',
         f'description: {quote(args.description)}', 'origin: ra2', f'size: [{N}, {N}]',
         f'playable: {{ left: {area[0]}, top: {area[1]}, right: {area[2]}, bottom: {area[3]} }}',
         f'climate: {args.climate}', f'lighting: {args.lighting}', f'players: {len(starts)}', 'starts:']
lines += [f'  - {{ slot: {s}, cell: [{x}, {y}] }}' for s, x, y in starts]
lines += ['legend:', '  texture:'] + [f'    {c}: {material[c]}' for c in legend] + ['layers:', '  height: |']
lines += ['    ' + ''.join(H[h] for h in row) for row in heights]
lines += ['  land: |'] + ['    ' + ''.join(land[(x, y)] for x in range(N)) for y in range(N)]
lines += ['  texture: |'] + ['    ' + ''.join(texture[(x, y)] for x in range(N)) for y in range(N)]
lines += ['  ore: |'] + ['    ' + ''.join(O[ore.get((x, y), 0)] for x in range(N)) for y in range(N)]
lines += ['objects:'] + [f'  - {{ type: {t}, cell: [{x}, {y}], variant: {v} }}' for t, x, y, v in objects]
lines += ['bridges:'] + [f'  - {{ type: {t}, from: [{a[0]}, {a[1]}], to: [{b[0]}, {b[1]}] }}' for a, b, t in runs]
lines += ['structures:'] + [f'  - {{ type: {t}, cell: [{x}, {y}], owner: neutral, facing: {f} }}' for t, x, y, f in structures]
open(args.out, 'w').write('\n'.join(lines) + '\n')
print(f'{N}x{N}, area {area}, starts {starts}, objects {len(objects)}, bridges {len(runs)}, structures {len(structures)}, '
      f'ore cells {len(ore)}, ore trees left out {len(ore_trees)}, skipped {dict(skipped)}')
