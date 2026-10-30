"""Gives the roads of a converted RA2 map the kinds of docs/map_elements/04_roads, in place:
    ra2_roads.py [--no-highway] [--dirt-road] [--pave] <map.rtsmap> ...

The converter (ra2_to_rtsmap.py) makes every grey road of the preview one asphalt road. RA2 has no road kinds in its cells we can read
(the tile sets are in the game's theater files, which we do not have), so the kind goes by the shape of the band, measured per cell
as the shorter of its two runs of asphalt along the axes:
- a band 3 to 6 cells wide and at least 8 long is a highway (land H, texture highway): the main roads of RA2's maps, three cells
  wide, and the avenues of a town;
- wider than that it is a square or a car park: it stays asphalt;
- 1 or 2 cells wide, in a town (climate urban), it is a street (texture street); elsewhere it stays an asphalt road.
--no-highway: no highway on the map. --dirt-road: a narrow band of asphalt by unpaved land is the edge of a dirt road (land g, texture
track): RA2's dirt road is three cells wide, brown in the middle and grey at its edges on the preview. Both for Route 66, one long dirt
road whose grey bands are its edges and the ends of its bridges. --pave: every unpaved and asphalt cell becomes highway: Route 66, whose
road leads to high concrete bridges three cells wide, which a dirt road does not (the owner, 2026-10-30).
--wane N (after the others): off a town the asphalt lies in a few places only (the owner, 2026-10-30): the asphalt and the highway
reach about N cells along the road from the ends of every bridge — one cell more or less from cell to cell, so the end is ragged —
and further on the road goes into the ground as a dirt road (land g, texture track), like an abandoned road.
--narrow-dirt: a dirt road is one cell wide, room for one vehicle (the owner, 2026-10-30): of a band of dirt road 2 to 6 cells
across and at least 3 long only its middle line stays, the cells beside it are ground again (land ., the texture their unpaved
neighbours have most, else grass).
No guardrails: RA2's maps have none. Nothing else of the map changes. Afterwards: Rts.MapTool -- canon and check.
"""
import collections
import re
import sys

HIGHWAY_WIDTH = (3, 6)
HIGHWAY_LENGTH = 8


def layer(text, name):
    m = re.search(r'^  %s: \|\n((?:    .*\n)+)' % name, text, re.M)
    return m, [list(line[4:]) for line in m.group(1).splitlines()]


def put(text, m, rows):
    return text[:m.start(1)] + ''.join('    ' + ''.join(r) + '\n' for r in rows) + text[m.end(1):]


def legend_code(text, material, wanted):
    """The code of the material in the map's texture legend, added with the wanted letter when it is not there."""
    block = re.search(r'^  texture:\n((?:    \S+: \S+\n)+)', text, re.M)
    entries = dict(re.findall(r'    (\S+): (\S+)\n', block.group(1)))
    for code, name in entries.items():
        if name == material:
            return text, code
    if wanted in entries:
        raise SystemExit(f'the legend has {wanted} for {entries[wanted]}, not {material}')
    entries[wanted] = material
    body = ''.join(f'    {c}: {entries[c]}\n' for c in sorted(entries))
    return text[:block.start(1)] + body + text[block.end(1):], wanted


def convert(path, highways, dirt_road, pave=False):
    text = open(path, encoding='utf-8').read()
    urban = re.search(r'^climate: urban$', text, re.M) is not None
    _, land = layer(text, 'land')
    _, tex = layer(text, 'texture')
    n = len(land)

    def road(x, y):
        return 0 <= x < n and 0 <= y < n and land[y][x] == '='

    def run(x, y, dx, dy):
        """Asphalt cells from (x, y) on in the direction (dx, dy), not counting it."""
        count = 0
        while road(x + dx * (count + 1), y + dy * (count + 1)):
            count += 1
        return count

    def runs(x, y):
        """The shorter and the longer run of asphalt through the cell, along x and along y."""
        h = 1 + run(x, y, -1, 0) + run(x, y, 1, 0)
        v = 1 + run(x, y, 0, -1) + run(x, y, 0, 1)
        return min(h, v), max(h, v)

    def is_land(x, y, code):
        return 0 <= x < n and 0 <= y < n and land[y][x] == code

    def beside(x, y, code):
        return any(is_land(x + dx, y + dy, code) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

    legend = re.search(r'^  texture:\n((?:    \S+: \S+\n)+)', text, re.M).group(1)
    asphalt = {c for c, m in re.findall(r'    (\S+): (\S+)\n', legend) if m == 'asphalt'}
    text, highway_code = legend_code(text, 'highway', 'h')
    street_code = track_code = None
    plan = {}
    for y in range(n):
        for x in range(n):
            if not road(x, y) or tex[y][x] not in asphalt:
                continue
            width, length = runs(x, y)
            if pave:
                plan[(x, y)] = ('H', 'highway')
            elif highways and HIGHWAY_WIDTH[0] <= width <= HIGHWAY_WIDTH[1] and length >= HIGHWAY_LENGTH:
                plan[(x, y)] = ('H', 'highway')
            elif width <= 2 and dirt_road and beside(x, y, 'g'):
                plan[(x, y)] = ('g', 'track')
            elif width <= 2 and urban:
                plan[(x, y)] = ('=', 'street')
    if pave:
        for y in range(n):
            for x in range(n):
                if land[y][x] == 'g':
                    plan[(x, y)] = ('H', 'highway')
    if any(kind == 'street' for _, kind in plan.values()):
        text, street_code = legend_code(text, 'street', 'e')
    if any(kind == 'track' for _, kind in plan.values()):
        text, track_code = legend_code(text, 'track', 'u')
    codes = {'highway': highway_code, 'street': street_code, 'track': track_code}
    for (x, y), (code, kind) in plan.items():
        land[y][x] = code
        tex[y][x] = codes[kind]
    m, _ = layer(text, 'land')
    text = put(text, m, land)
    m, _ = layer(text, 'texture')
    text = put(text, m, tex)
    open(path, 'w', encoding='utf-8').write(text)
    counts = {k: sum(1 for _, kind in plan.values() if kind == k) for k in codes}
    print(f'{path}: {counts}')


def wane(path, reach):
    text = open(path, encoding='utf-8').read()
    _, land = layer(text, 'land')
    _, tex = layer(text, 'texture')
    n = len(land)

    def paved(x, y):
        return 0 <= x < n and 0 <= y < n and land[y][x] in '=H'

    ends = []
    for b in re.finditer(r'type: bridge_\w+, from: \[(\d+), (\d+)\], to: \[(\d+), (\d+)\]', text):
        x0, y0, x1, y1 = map(int, b.groups())
        ends += [(x0, y0), (x1, y1)]
    distance = {}
    queue = collections.deque()
    for ex, ey in ends:
        for y in range(ey - 2, ey + 3):
            for x in range(ex - 2, ex + 3):
                if paved(x, y) and (x, y) not in distance:
                    distance[(x, y)] = 0
                    queue.append((x, y))
    while queue:
        x, y = queue.popleft()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                q = (x + dx, y + dy)
                if paved(*q) and q not in distance:
                    distance[q] = distance[(x, y)] + 1
                    queue.append(q)
    text, track_code = legend_code(text, 'track', 'u')
    waned = 0
    for y in range(n):
        for x in range(n):
            ragged = reach - 1 + (x * 73856093 ^ y * 19349663) % 3
            if paved(x, y) and distance.get((x, y), reach + 9) > ragged:
                land[y][x] = 'g'
                tex[y][x] = track_code
                waned += 1
    m, _ = layer(text, 'land')
    text = put(text, m, land)
    m, _ = layer(text, 'texture')
    text = put(text, m, tex)
    open(path, 'w', encoding='utf-8').write(text)
    print(f'{path}: {waned} cells of asphalt and highway now a dirt road')


def narrow(path):
    text = open(path, encoding='utf-8').read()
    _, land = layer(text, 'land')
    _, tex = layer(text, 'texture')
    n = len(land)
    legend = dict(re.findall(r'    (\S+): (\S+)\n', re.search(r'^  texture:\n((?:    \S+: \S+\n)+)', text, re.M).group(1)))
    grass = next((c for c, name in legend.items() if name == 'grass'), None)

    def dirt(x, y):
        return 0 <= x < n and 0 <= y < n and land[y][x] == 'g'

    def run(x, y, dx, dy):
        count = 0
        while dirt(x + dx * (count + 1), y + dy * (count + 1)):
            count += 1
        return count

    beside = []
    for y in range(n):
        for x in range(n):
            if not dirt(x, y):
                continue
            west, east, north, south = run(x, y, -1, 0), run(x, y, 1, 0), run(x, y, 0, -1), run(x, y, 0, 1)
            along_x, along_y = 1 + west + east, 1 + north + south
            across = min(along_x, along_y)
            if 2 <= across <= 6 and max(along_x, along_y) >= 3 and (north if along_x >= along_y else west) != across // 2:
                beside.append((x, y))
    for x, y in beside:
        kinds = collections.Counter(tex[qy][qx] for qx in (x - 1, x, x + 1) for qy in (y - 1, y, y + 1)
                                    if 0 <= qx < n and 0 <= qy < n and land[qy][qx] in '.,')
        land[y][x] = '.'
        tex[y][x] = kinds.most_common(1)[0][0] if kinds else grass or tex[y][x]
    m, _ = layer(text, 'land')
    text = put(text, m, land)
    m, _ = layer(text, 'texture')
    text = put(text, m, tex)
    open(path, 'w', encoding='utf-8').write(text)
    print(f'{path}: {len(beside)} cells beside the middle of a dirt road made ground')


reach = int(sys.argv[sys.argv.index('--wane') + 1]) if '--wane' in sys.argv else None
paths = [a for i, a in enumerate(sys.argv[1:], 1) if not a.startswith('--') and sys.argv[i - 1] != '--wane']
for map_path in paths:
    if '--narrow-dirt' in sys.argv:
        narrow(map_path)
    elif reach is None:
        convert(map_path, '--no-highway' not in sys.argv, '--dirt-road' in sys.argv, '--pave' in sys.argv)
    else:
        wane(map_path, reach)
