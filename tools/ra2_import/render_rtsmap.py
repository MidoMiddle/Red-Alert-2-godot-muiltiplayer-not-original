"""Draws an .rtsmap in the screen's axes (v across, u down) like an RA2 preview: one pixel per v step and per two u steps."""
import sys, re
t = open(sys.argv[1]).read(); out = sys.argv[2]
n = int(re.search(r'size: \[(\d+),', t).group(1))
def layer(name):
    rows = re.search(r'  ' + name + r': \|\n((?:    .*\n)+)', t).group(1).split('\n')
    return [r.strip() for r in rows if r.strip()]
H, L, T, O = layer('height'), layer('land'), layer('texture'), layer('ore')
trees = {tuple(map(int, m)) for m in re.findall(r'type: pine_tree, cell: \[(\d+), (\d+)\]', t)}
drills = {tuple(map(int, m)) for m in re.findall(r'type: ore_source, cell: \[(\d+), (\d+)\]', t)}
builds = [(m[0], int(m[1]), int(m[2])) for m in re.findall(r'type: (\w+), cell: \[(\d+), (\d+)\], owner', t)]
starts = [tuple(map(int, m)) for m in re.findall(r'slot: \d+, cell: \[(\d+), (\d+)\]', t)]
bridges = [tuple(map(int, m)) for m in re.findall(r'from: \[(\d+), (\d+)\], to: \[(\d+), (\d+)\]', t)]
bcells = set()
for a, b, c, d in bridges:
    for x in range(min(a, c), max(a, c) + 1):
        for y in range(min(b, d), max(b, d) + 1):
            bcells.add((x, y))
area = tuple(map(int, re.search(r'playable: \{ left: (-?\d+), top: (\d+), right: (-?\d+), bottom: (\d+)', t).groups()))
left, top, right, bottom = area
W, Hh = right - left + 1, (bottom - top) // 2 + 1
H2 = '0123456789abcde'
base = {'g': (150, 170, 80), 'f': (120, 135, 50), 'b': (220, 190, 120), 'u': (170, 140, 90), 'w': (60, 90, 160), 'k': (120, 120, 120), 'a': (90, 92, 96), 'p': (160, 100, 80)}
img = bytearray()
for py in range(Hh):
    for px in range(W):
        v = left + px; u = top + 2 * py
        if (u + v) % 2 == 0:
            u += 1
        x, y = (u - 1 + v) // 2, (u - 1 - v) // 2
        if not (0 <= x < n and 0 <= y < n):
            img += bytes((0, 0, 0)); continue
        c = base.get(T[y][x], (255, 0, 255))
        lv = H2.index(H[y][x])
        c = tuple(min(255, int(ch * (0.8 + 0.07 * lv))) for ch in c)
        if L[y][x] == 'W': c = (30, 50, 120)
        if O[y][x] != '.': c = (250, 250, 0) if O[y][x] in '123456789' else (200, 60, 220)
        if (x, y) in trees: c = (40, 80, 30)
        if (x, y) in bcells: c = (110, 80, 50)
        if (x, y) in drills: c = (0, 0, 0)
        for bt, bx, by in builds:
            if bx <= x < bx + 2 and by <= y < by + 2: c = (90, 90, 90)
        for sx, sy in starts:
            if abs(sx - x) <= 1 and abs(sy - y) <= 1: c = (230, 30, 30)
        img += bytes(c)
open(out, 'wb').write(b'P6 %d %d 255\n' % (W, Hh) + bytes(img))
