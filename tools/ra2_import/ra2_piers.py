"""Lowers the piers of RA2's high bridges that stand up through the deck: ra2_piers.py <map.rtsmap> ...

RA2 builds a high bridge on piers of raised ground between its spans. The converter brings a single pier cell down to the ground beside
it (our bridge draws its own piers), but a pier two cells long (Route 66) stays up at the level of the deck, and its ground comes
through the deck. Here every corner under the span of a high bridge (all its cells but the two on the shores, across its three cells)
that reaches the level of the deck goes down to the lowest corner under that span: the water's or the valley's. Nothing else changes.
Afterwards: Rts.MapTool -- canon and check.
"""
import re
import sys

DIGITS = '0123456789abcdefghijklmnopqrstuvwxyz'


def lower(path):
    text = open(path, encoding='utf-8').read()
    m = re.search(r'^  height: \|\n((?:    .*\n)+)', text, re.M)
    height = [[DIGITS.index(c) for c in line[4:]] for line in m.group(1).splitlines()]
    lowered = 0
    for b in re.finditer(r'type: bridge_concrete_high, from: \[(\d+), (\d+)\], to: \[(\d+), (\d+)\]', text):
        x0, y0, x1, y1 = map(int, b.groups())
        along_x = y0 == y1
        span = [(x, y0) for x in range(x0 + 1, x1)] if along_x else [(x0, y) for y in range(y0 + 1, y1)]
        level = max(height[y0][x0], height[y0][x0 + 1], height[y0 + 1][x0], height[y0 + 1][x0 + 1])
        corners = set()
        for x, y in span:
            for across in (-1, 0, 1):
                cx, cy = (x, y + across) if along_x else (x + across, y)
                corners.update(((cx, cy), (cx + 1, cy), (cx, cy + 1), (cx + 1, cy + 1)))
        floor = min(height[cy][cx] for cx, cy in corners)
        for cx, cy in corners:
            if height[cy][cx] >= level:
                height[cy][cx] = floor
                lowered += 1
    body = ''.join('    ' + ''.join(DIGITS[v] for v in row) + '\n' for row in height)
    open(path, 'w', encoding='utf-8').write(text[:m.start(1)] + body + text[m.end(1):])
    print(f'{path}: {lowered} corners lowered')


for map_path in sys.argv[1:]:
    lower(map_path)
