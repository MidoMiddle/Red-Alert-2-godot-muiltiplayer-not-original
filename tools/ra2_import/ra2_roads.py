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
No guardrails: RA2's maps have none. Nothing else of the map changes. Afterwards: Rts.MapTool -- canon and check.
"""
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


for map_path in (a for a in sys.argv[1:] if not a.startswith('--')):
    convert(map_path, '--no-highway' not in sys.argv, '--dirt-road' in sys.argv, '--pave' in sys.argv)
