"""Two rules of the owner (2026-10-30) on a converted RA2 map, in place: ra2_tidy.py <map.rtsmap> ...

- No ore on a road. The converter reads the ground off the preview, where the ore drawn over a field (and the gold on the pavements
  of RA2's towns) often comes out as a road or a dirt road: a cell of road under ore whose neighbours are not mostly road without ore
  is ground (land `.`, the texture its unpaved neighbours have most, else grass). A cell inside a band of road keeps its road and
  loses its ore later, to `Rts.MapTool -- tidy`.
- No land under the piers of a high bridge. A pier of RA2 stands on its own raised ground in the water; ra2_piers.py brings it
  down, and it stayed an island of sand under the bridge. Our bridge draws its piers going down into the water: a cell of land under
  the span of a high bridge (its three lanes and two beside them) with water on both sides along the bridge becomes water, the flat
  land beside it shore, as the converter makes them.
Afterwards: Rts.MapTool -- tidy, canon and check.
"""
import collections
import re
import sys

ROAD = '=Hgp'  # Road, Highway, Track, Path
WET = '~W'


def layer(text, name):
    m = re.search(r'^  %s: \|\n((?:    .*\n)+)' % name, text, re.M)
    return m, [list(line[4:]) for line in m.group(1).splitlines()]


def put(text, m, rows):
    return text[:m.start(1)] + ''.join('    ' + ''.join(r) + '\n' for r in rows) + text[m.end(1):]


def tidy(path):
    text = open(path, encoding='utf-8').read()
    _, land = layer(text, 'land')
    _, tex = layer(text, 'texture')
    _, ore = layer(text, 'ore')
    _, height = layer(text, 'height')
    n = len(land)
    legend = dict(re.findall(r'^    (\S): (\S+)$', re.search(r'^  texture:\n((?:    \S+: \S+\n)+)', text, re.M).group(1), re.M))
    grass = next((c for c, name in legend.items() if name == 'grass'), None)
    shore = next((c for c, name in legend.items() if name == 'shore'), None)
    water = next(c for c, name in legend.items() if name == 'water')

    def inside(x, y):
        return 0 <= x < n and 0 <= y < n

    def around(x, y):
        return [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx or dy) and inside(x + dx, y + dy)]

    # ---- Ore on a road: ground, unless the cell lies inside a band of road.
    grounded = []
    for y in range(n):
        for x in range(n):
            if ore[y][x] == '.' or land[y][x] not in ROAD:
                continue
            roads = sum(1 for qx, qy in around(x, y) if land[qy][qx] in ROAD and ore[qy][qx] == '.')
            if roads >= 4:
                continue
            kinds = collections.Counter(tex[qy][qx] for qx, qy in around(x, y) if land[qy][qx] in '.,')
            grounded.append((x, y, kinds.most_common(1)[0][0] if kinds else grass or tex[y][x]))
    for x, y, t in grounded:
        land[y][x] = '.'
        tex[y][x] = t

    # ---- Land under the piers of high bridges.
    flooded = set()
    for b in re.finditer(r'type: bridge_concrete_high, from: \[(\d+), (\d+)\], to: \[(\d+), (\d+)\]', text):
        x0, y0, x1, y1 = map(int, b.groups())
        along_x = y0 == y1
        a, z = (min(x0, x1), max(x0, x1)) if along_x else (min(y0, y1), max(y0, y1))
        at = (lambda s, lane: (s, y0 + lane)) if along_x else (lambda s, lane: (x0 + lane, s))
        for lane in range(-2, 3):
            for s in range(a + 1, z):
                x, y = at(s, lane)
                if not inside(x, y) or land[y][x] in WET:
                    continue

                def hits(step):
                    t = s
                    while a < t < z:
                        qx, qy = at(t, lane)
                        if not inside(qx, qy):
                            return False
                        if land[qy][qx] in WET:
                            return True
                        t += step
                    return False

                if hits(-1) and hits(1):
                    flooded.add((x, y))
    for x, y in flooded:
        land[y][x] = '~'
        tex[y][x] = water
        ore[y][x] = '.'
        for cx, cy in ((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)):
            height[cy][cx] = '0'
    for x, y in flooded | {q for p in flooded for q in around(*p)}:
        if land[y][x] in WET:
            land[y][x] = 'W' if all(inside(qx, qy) and land[qy][qx] in WET for qx, qy in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))) else '~'
        elif land[y][x] in '.,' and shore and all(height[cy][cx] == '0' for cx, cy in ((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1))):
            land[y][x] = 'b'
            tex[y][x] = shore

    gone = [o for o in re.finditer(r'^  - \{ type: [a-z_]+, cell: \[(\d+), (\d+)\].*\n', text, re.M)
            if (int(o.group(1)), int(o.group(2))) in flooded]
    for o in reversed(gone):
        text = text[:o.start()] + text[o.end():]
    for name, rows in (('height', height), ('land', land), ('texture', tex), ('ore', ore)):
        m, _ = layer(text, name)
        text = put(text, m, rows)
    open(path, 'w', encoding='utf-8').write(text)
    print(f'{path}: {len(grounded)} cells of road under ore made ground, {len(flooded)} cells of land under piers flooded, '
          f'{len(gone)} objects and buildings on them taken off')


for map_path in sys.argv[1:]:
    tidy(map_path)
