"""Small things of the farm (docs/temperate_elements): the hedge (02_hedge), the hay bale (05_hay_bale), the well (06_well) and the
tractor (12_farm_tractor). Temperate climate: warm daylight, no snow.

Map objects of game/data/map/objects.yaml, each on one cell, all stopping everybody (the hay bale crushed by tracks). Made at the size the
game draws a thing on a cell, as its placeholder (game/scripts/Terrain/PropLooks.cs). Origin at the centre of the cell on the ground. A map
object has one model: the broken, burnt and fallen looks of the documents are left out (the game hides a destroyed one). One GLB per
variant, the game picks one per cell:
    hedge_section           a block of clipped hedge filling its cell, 0.56 tall, its top a little uneven
    hedge_section_rounded   the same, its top and sides swelling a little between the cell's edges, as bushes grown into one
    hedge_section_ragged    the same, shaggier, a few shoots sticking up out of its top
                            The hedge is drawn as a prop, not turned along its neighbours (MapObjects.AddProps), so every variant meets all
                            four edges of its cell with the same straight edge at the same height: a row, a corner or a block of hedge
                            cells joins without a seam. No spin and no jitter for it in models.yaml. A few brown twigs at its foot.
    hay_bale                a round bale lying on its side, 0.55 across, golden straw rolled round, three bands of twine, loose straw on
                            dark earth under it
    hay_bale_square         a stack of small square bales tied with twine: six on the ground, three across them, one on top
    hay_bale_heap           a haystack: a rounded heap of hay round a pole, 0.68 tall, its top weathered
    well                    a round grey stone well, its coping to 0.39, on a ring of flagstones; dark water in it; a gabled wooden roof
                            with grey shingles on two posts beside it, a wooden winch between them with an iron crank, a chain and a
                            bucket hanging over the water
    farm_tractor            a red tractor 0.89 long: large rear wheels with lugged tyres under round fenders, small front wheels, a long
                            bonnet with a grille and lamps, the exhaust and the air intake on it, a seat under a grey four-post cab frame
                            with a roof and a guard at its back, the hitch behind; grey rims
    farm_tractor_blue       the same, blue
Nodes: hedge "body"; hay bale "body"; well "body" (the stone ring, its water and the flagstones), "roof" (the posts and the roof: it is
what falls), "winch" (its origin on the roller's axis); tractor "hull", "wheel_rear", "wheel_front" (both wheels of an axle in one node,
its origin on the axle, front to +Y in Blender, -Z in Godot, as the units). Budgets (the element documents): hedge 500 triangles,
texture 512; hay bale 1000, 1024; well 1500, 1024; tractor 4000, 1024. No text, no logos.

In Blender, after tools/blender is on sys.path:
    import families.farm_props as farm
    print(farm.run_all())     # or farm.run("well")
"""

import math
import random

import bmesh
from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "farm"  # game/assets/models/farm/<id>/, assets_src/models/farm/<id>/

HEDGES = {
    # dome: how much the top swells between the edges; top, side: how uneven the top and the sides are; shoots, twigs: how many.
    "hedge_section": dict(dome=0.0, side_bulge=0.0, top=0.03, side=0.022, shoots=0, twigs=3),
    "hedge_section_rounded": dict(dome=0.08, side_bulge=0.035, top=0.022, side=0.018, shoots=0, twigs=2),
    "hedge_section_ragged": dict(dome=0.025, side_bulge=0.012, top=0.05, side=0.035, shoots=4, twigs=4),
}
HAY = ("hay_bale", "hay_bale_square", "hay_bale_heap")
WELLS = ("well",)
TRACTORS = {
    "farm_tractor": (0.74, 0.12, 0.09),
    "farm_tractor_blue": (0.14, 0.33, 0.66),
}
VARIANTS = (*HEDGES, *HAY, *WELLS, *TRACTORS)
SIZES = {**{k: 512 for k in HEDGES}, **{k: 1024 for k in (*HAY, *WELLS, *TRACTORS)}}
NODES = {**{k: ("body",) for k in (*HEDGES, *HAY)}, "well": ("body", "roof", "winch"),
         **{k: ("hull", "wheel_rear", "wheel_front") for k in TRACTORS}}

# Their places in the file (rts_common.LAYOUT), a row of the farm. rts_common has no such row yet: it is added here for the run, and
# stays as it is when rts_common gets one.
ROW = "15. Ферма"
SLOTS = {v: 20 + i for i, v in enumerate(VARIANTS)}


def _register():
    if not any(r == ROW for r in rc.ROWS):
        rc.ROWS = rc.ROWS + (ROW,)
    row = rc.ROWS.index(ROW)
    for key, slot in SLOTS.items():
        rc.LAYOUT.setdefault(key, (row, slot))


# --- shared shapes and nodes -----------------------------------------------------------

def _paint(p, face, painted):
    for loop in face.loops:
        loop[p.layer] = painted


def _loft(p, rings, parts, caps=(None, None)):
    """Parts.loft with a part per segment: parts[i] paints the quads from ring i to ring i + 1; caps: the parts of the first and the
    last cap (None leaves that end open)."""
    verts = [[p.bm.verts.new(q) for q in ring] for ring in rings]
    n = len(rings[0])
    for k, (a, b) in enumerate(zip(verts, verts[1:])):
        for i in range(n):
            j = (i + 1) % n
            _paint(p, p.bm.faces.new((a[i], a[j], b[j], b[i])), parts[k])
    if caps[0] is not None:
        _paint(p, p.bm.faces.new(verts[0]), caps[0])
    if caps[1] is not None:
        _paint(p, p.bm.faces.new(list(reversed(verts[-1]))), caps[1])


def _ring_z(c, z, r, sides, jitter=0.0, rnd=None, phase=0.0):
    """A ring about the vertical through c at height z, its radius r (each point moved by up to jitter)."""
    out = []
    for i in range(sides):
        a = 2 * math.pi * i / sides + phase
        rr = r + (rnd.uniform(-jitter, jitter) if jitter else 0.0)
        out.append(Vector((c[0] + math.cos(a) * rr, c[1] + math.sin(a) * rr, z)))
    return out


def _ring_x(c, t, r, sides, phase=0.0):
    """A ring about the line along x through c, t along it, its radius r."""
    return [Vector((c[0] + t, c[1] + math.cos(2 * math.pi * i / sides + phase) * r, c[2] + math.sin(2 * math.pi * i / sides + phase) * r))
            for i in range(sides)]


def _chamfered(c, half_a, half_b, ch, plane):
    """A rectangle with its corners cut by ch, 8 points: in the plane 'XZ' (a along x, b along z, c its centre) or 'YZ'."""
    pts = [(half_a, half_b - ch), (half_a - ch, half_b), (-half_a + ch, half_b), (-half_a, half_b - ch),
           (-half_a, -half_b + ch), (-half_a + ch, -half_b), (half_a - ch, -half_b), (half_a, -half_b + ch)]
    if plane == 'XZ':
        return [Vector((c[0] + a, c[1], c[2] + b)) for a, b in pts]
    return [Vector((c[0], c[1] + a, c[2] + b)) for a, b in pts]


def _ground_patch(p, rnd, rx, ry, painted, sides=14):
    """A low patch of earth and loose straw on the ground, rx by ry, its edge ragged."""
    outer, inner = [], []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        k = rnd.uniform(0.86, 1.0)
        outer.append(Vector((math.cos(a) * rx * k, math.sin(a) * ry * k, 0.003)))
        inner.append(Vector((math.cos(a) * rx * k * 0.8, math.sin(a) * ry * k * 0.8, 0.014)))
    _loft(p, [outer, inner], [painted], (None, painted))


def _noise(g, vec, scale, detail):
    node = g.add('ShaderNodeTexNoise')
    g.put(node, 'Scale', scale)
    g.put(node, 'Detail', detail)
    g.link(vec, node, 'Vector')
    return rc.sock(node, 'Fac', outputs=True)


def _stretch(g, coord, sx, sy, sz):
    """The object coordinate scaled along each axis: noise of it is streaked along the axes scaled least."""
    return g.join(g.math('MULTIPLY', g.axis(coord, 'X'), sx), g.math('MULTIPLY', g.axis(coord, 'Y'), sy),
                  g.math('MULTIPLY', g.axis(coord, 'Z'), sz))


def _step(g, v, edge, sharp=60.0):
    """0 below edge, 1 above, soft over 1 / sharp."""
    return g.clamp01(g.math('MULTIPLY_ADD', v, sharp, -sharp * edge))


def _pivot(obj, point):
    """Puts the object's origin at point, its mesh where it was: the node turns about it."""
    obj.data.transform(Matrix.Translation(-Vector(point)))
    obj.location = Vector(point)


# --- hedge -----------------------------------------------------------------------------

(LEAF, LEAF_TIP, TWIG) = (cc.part(i) for i in range(3))
HEDGE_PARTS = 3
HEDGE_H = 0.56  # its height at every edge of the cell: neighbours meet there
HEDGE_N, HEDGE_NZ = 8, 3  # quads along an edge, quads up a side


def _fade(t):
    """0 at both ends with no slope, 1 in the middle: what moves the surface leaves the cell's edges where they are."""
    return math.sin(math.pi * t) ** 2


def _hedge(p, rnd, look):
    """A box of the cell open at the bottom, its top a grid of HEDGE_N by HEDGE_N and its sides HEDGE_N by HEDGE_NZ; inside the edges
    the top rises and falls and the sides swell, the edges stay straight at HEDGE_H."""
    n, nz = HEDGE_N, HEDGE_NZ
    bumps = {}

    def bump(key, amount):
        if key not in bumps:
            bumps[key] = rnd.uniform(-1.0, 1.0) * amount
        return bumps[key]

    verts = {}

    def vert(i, j, k):
        key = (i, j, k)
        if key in verts:
            return verts[key]
        u, w = i / n, j / n
        x, y = -0.5 + u, -0.5 + w
        if k == nz:  # on the top
            z = HEDGE_H + _fade(u) * _fade(w) * (look["dome"] + bump(key, look["top"]))
            co = Vector((x, y, z))
        else:
            v = k / nz
            co = Vector((x, y, HEDGE_H * v))
            # Out of the side it is on, by how far in from the side's corners and from its foot and top.
            out = Vector((0.0, 0.0, 0.0))
            along = None
            if j == 0 or j == n:
                out, along = Vector((0.0, -1.0 if j == 0 else 1.0, 0.0)), u
            if i == 0 or i == n:
                out, along = (Vector((-1.0 if i == 0 else 1.0, 0.0, 0.0)), w) if along is None else (Vector(), 0.0)
            amount = _fade(along) * math.sin(math.pi * v) ** 2 * (look["side_bulge"] + bump(key, look["side"]))
            co = co + out * amount
        verts[key] = p.bm.verts.new(co)
        return verts[key]

    def quad(a, b, c, d):
        _paint(p, p.bm.faces.new((vert(*a), vert(*b), vert(*c), vert(*d))), LEAF)

    for i in range(n):
        for j in range(n):
            quad((i, j, nz), (i + 1, j, nz), (i + 1, j + 1, nz), (i, j + 1, nz))
    for t in range(n):
        for k in range(nz):
            quad((t, 0, k), (t + 1, 0, k), (t + 1, 0, k + 1), (t, 0, k + 1))  # the side at -y
            quad((t + 1, n, k), (t, n, k), (t, n, k + 1), (t + 1, n, k + 1))  # +y
            quad((0, t + 1, k), (0, t, k), (0, t, k + 1), (0, t + 1, k + 1))  # -x
            quad((n, t, k), (n, t + 1, k), (n, t + 1, k + 1), (n, t, k + 1))  # +x
    # Shoots sticking up out of an untrimmed top, away from the edges.
    for _ in range(look["shoots"]):
        x, y = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        base = HEDGE_H + look["dome"] * _fade(x + 0.5) * _fade(y + 0.5) - 0.02
        h, s = rnd.uniform(0.08, 0.13), rnd.uniform(0.05, 0.07)
        lean = Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03), 0.0))
        p.loft([[Vector((x + dx * s, y + dy * s, base)) for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1))],
                [Vector((x + dx * s * 0.5, y + dy * s * 0.5, base + h * 0.6)) + lean * 0.6 for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1))],
                [Vector((x, y, base + h)) + lean + Vector((dx * 0.004, dy * 0.004, 0.0)) for dx, dy in ((1, 0), (0, 1), (-1, 0), (0, -1))]],
               LEAF_TIP)
    # Twigs at its foot, poking out of the long sides.
    for k in range(look["twigs"]):
        side = 1 if k % 2 else -1
        x = rnd.uniform(-0.38, 0.38)
        a = Vector((x, side * 0.44, rnd.uniform(0.02, 0.06)))
        b = Vector((x + rnd.uniform(-0.06, 0.06), side * rnd.uniform(0.53, 0.56), rnd.uniform(0.005, 0.03)))
        p.bar(a, b, 0.006, TWIG)


def _voronoi(g, scale, vec=None):
    """The distance to the nearest of random points, scale of them to a unit: 0 at a clump's heart, about 1 between clumps."""
    node = g.add('ShaderNodeTexVoronoi')
    g.put(node, 'Scale', scale)
    if vec is not None:
        g.link(vec, node, 'Vector')
    return rc.sock(node, 'Distance', outputs=True)


def _hedge_material(name):
    """Dark green leaves in clumps: each clump lighter at its heart and where the sun falls on the top, dark in the gaps between them,
    leaf tips picked out lighter; brown twigs and earth with fallen leaves at the foot, ragged."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, HEDGE_PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    big = _voronoi(g, 9.0, coord)  # clumps of leaves
    small = _voronoi(g, 34.0, coord)  # sprays within a clump
    leaves = g.noise(70.0, 2.0)
    tone = g.noise(4.0, 2.0)
    leaf = g.mix(tone, lin((0.20, 0.36, 0.13)), lin((0.27, 0.44, 0.16)))
    leaf = g.mix(g.math('MULTIPLY', _step(g, leaves, 0.56, 7.0), 0.8), leaf, lin((0.42, 0.58, 0.22)))  # light leaf tips
    leaf = g.mix(g.math('MULTIPLY', _step(g, up, 0.5, 3.0), 0.35), leaf, lin((0.40, 0.56, 0.20)))  # sun on the top
    gaps = g.math('MAXIMUM', _step(g, big, 0.62, 5.0), g.math('MULTIPLY', _step(g, small, 0.7, 6.0), 0.6))
    leaf = g.mix(g.math('MULTIPLY', gaps, 0.85), leaf, lin((0.07, 0.14, 0.05)))  # dark between the clumps
    ragged = g.math('ADD', z, g.math('MULTIPLY', g.noise(14.0, 3.0), 0.05))
    foot = _step(g, ragged, 0.055, -60.0)
    litter = g.mix(g.noise(30.0, 3.0), lin((0.26, 0.20, 0.13)), lin((0.44, 0.34, 0.19)))
    leaf = g.mix(foot, leaf, litter)
    tip = g.mix(leaves, lin((0.30, 0.48, 0.18)), lin((0.44, 0.60, 0.26)))
    tip = g.mix(g.math('MULTIPLY', gaps, 0.6), tip, lin((0.10, 0.20, 0.07)))
    twig = g.mix(g.noise(40.0, 2.0), lin((0.30, 0.22, 0.14)), lin((0.42, 0.32, 0.20)))
    albedo = cc.paint(g, mask, (leaf, tip, twig))
    rough = cc.weighted(g, mask, (0.85, 0.8, 0.9))
    height = g.math('ADD', g.math('MULTIPLY', big, -1.0), g.math('ADD', g.math('MULTIPLY', small, -0.5), g.math('MULTIPLY', leaves, 0.3)))
    g.finish(albedo, g.bump(height, 0.7, 0.03), g.combine(ao, rough, 0.0))
    return mat, g


# --- hay bale ----------------------------------------------------------------------------

(STRAW, TWINE, GROUND, POLE) = (cc.part(i) for i in range(4))
HAY_PARTS = 4
BALE_R, BALE_HALF = 0.275, 0.29
BALE_Z = 0.25  # its axis: it settles under its weight, its bottom flatter, on the ground


def _round_bale(p, rnd):
    """Lying along x: the rolled body with rounded shoulders, its flat ends; on loose straw."""
    c = (0.0, 0.0, BALE_Z)
    stations = [(-BALE_HALF, 0.205), (-BALE_HALF + 0.006, 0.245), (-BALE_HALF + 0.02, 0.268), (-BALE_HALF + 0.045, BALE_R),
                (BALE_HALF - 0.045, BALE_R), (BALE_HALF - 0.02, 0.268), (BALE_HALF - 0.006, 0.245), (BALE_HALF, 0.205)]
    rings = [_ring_x(c, t, r, 22) for t, r in stations]
    for ring in rings:  # it settles a little under its weight: the bottom flatter
        for v in ring:
            if v.z < c[2]:
                v.z = c[2] + (v.z - c[2]) * 0.93
    _loft(p, rings, [STRAW] * 7, (STRAW, STRAW))
    _ground_patch(p, rnd, 0.46, 0.42, GROUND)


def _square_bale(p, rnd, centre, along_x, turn):
    """A small square bale 0.34 long, 0.17 wide, 0.135 tall, its edges rounded; two bands of twine round it."""
    length, half_w, half_h, ch = 0.34, 0.085, 0.0675, 0.022
    plane = 'YZ' if along_x else 'XZ'
    c = Vector(centre)
    stations = (-0.5, -0.27, -0.24, 0.24, 0.27, 0.5)
    rings = []
    for s in stations:
        k = 1.0 if abs(s) > 0.45 else 1.03  # the belly fuller than its cut ends
        shift = Vector((s * length, 0.0, 0.0)) if along_x else Vector((0.0, s * length, 0.0))
        rings.append(_chamfered(c + shift, half_w * k, half_h * k, ch, plane))
    m = Matrix.Rotation(turn, 3, 'Z')
    rings = [[c + m @ (v - c) for v in ring] for ring in rings]
    _loft(p, rings, [STRAW, TWINE, STRAW, TWINE, STRAW], (STRAW, STRAW))


def _square_stack(p, rnd):
    h = 0.135
    for x in (-0.175, 0.175):  # six on the ground, along x, three abreast
        for y in (-0.175, 0.0, 0.175):
            _square_bale(p, rnd, (x + rnd.uniform(-0.01, 0.01), y + rnd.uniform(-0.006, 0.006), h / 2), True, rnd.uniform(-0.03, 0.03))
    for x in (-0.175, 0.0, 0.175):  # three across them
        _square_bale(p, rnd, (x, -0.04 + rnd.uniform(-0.01, 0.01), h * 1.5), False, rnd.uniform(-0.04, 0.04))
    _square_bale(p, rnd, (0.03, -0.04, h * 2.5), True, 0.12)
    _ground_patch(p, rnd, 0.48, 0.42, GROUND)


def _heap(p, rnd):
    """A haystack round a pole: a rounded heap, lumpy, its top drawn in to the pole."""
    stations = [(0.0, 0.39), (0.05, 0.41), (0.15, 0.40), (0.27, 0.355), (0.38, 0.29), (0.48, 0.21), (0.56, 0.13), (0.62, 0.06), (0.645, 0.02)]
    sides = 18
    rings = []
    for k, (z, r) in enumerate(stations):
        jitter = 0.022 if 0 < k < len(stations) - 1 else 0.008
        rings.append(_ring_z((0.0, 0.0), z, r, sides, jitter, rnd, phase=0.17 * k))
    _loft(p, rings, [STRAW] * (len(rings) - 1), (None, STRAW))
    p.bar((0.0, 0.0, 0.5), (0.015, -0.01, 0.76), 0.012, POLE)
    _ground_patch(p, rnd, 0.49, 0.47, GROUND, sides=16)


def _hay_material(name, kind):
    """Golden straw: fibres rolled round a round bale (rings on its ends), flakes along a square one, running down a heap; light brown
    twine; dark earth with loose straw under it; a grey pole."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, HAY_PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    normal = g.normal_world()
    if kind == "round":
        side = _noise(g, _stretch(g, coord, 70.0, 2.5, 2.5), 1.0, 3.0)  # fibres round the roll
        dz = g.math('SUBTRACT', z, BALE_Z)
        r = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', y, y), g.math('MULTIPLY', dz, dz)))
        ends = g.math('ADD', g.math('MULTIPLY', g.math('SINE', g.math('MULTIPLY', r, 160.0)), 0.25), g.math('MULTIPLY', g.noise(30.0, 3.0), 0.75))
        end = _step(g, g.math('ABSOLUTE', g.axis(normal, 'X')), 0.75, 8.0)
        fibre = g.math('ADD', g.math('MULTIPLY', side, g.math('SUBTRACT', 1.0, end)), g.math('MULTIPLY', ends, end))
        ax = g.math('ABSOLUTE', x)
        twine = g.math('MULTIPLY', g.math('SUBTRACT', 1.0, end),
                       g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', ax, -150.0, 1.2)),
                              g.math('MULTIPLY', _step(g, ax, 0.142, 300.0), _step(g, ax, 0.158, -300.0))))
    else:
        fibre = _noise(g, _stretch(g, coord, 45.0, 45.0, 5.0), 1.0, 3.0)
        twine = 0.0
    straw = g.mix(g.noise(6.0, 3.0), lin((0.80, 0.63, 0.31)), lin((0.90, 0.77, 0.44)))
    straw = g.mix(_step(g, fibre, 0.6, 6.0), straw, lin((0.96, 0.86, 0.56)))  # light fibres
    straw = g.mix(g.math('MULTIPLY', _step(g, fibre, 0.42, -6.0), 0.7), straw, lin((0.60, 0.44, 0.20)))  # dark ones
    if kind == "heap":  # an old heap: its top grey and weathered
        straw = g.mix(g.math('MULTIPLY', _step(g, z, 0.42, 6.0), 0.6), straw, lin((0.66, 0.58, 0.42)))
    straw = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -20.0, 1.2)), 0.6), straw, lin((0.48, 0.38, 0.22)))  # damp foot
    rope = lin((0.62, 0.46, 0.28))
    if kind == "round":
        straw = g.mix(twine, straw, rope)
    earth = g.mix(g.noise(9.0, 3.0), lin((0.26, 0.20, 0.14)), lin((0.36, 0.28, 0.19)))
    loose = _noise(g, _stretch(g, coord, 40.0, 40.0, 40.0), 1.0, 4.0)
    reach = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', x, x), g.math('MULTIPLY', y, y)))
    near = g.clamp01(g.math('MULTIPLY_ADD', reach, -3.0, 1.75))
    ground = g.mix(g.math('MULTIPLY', _step(g, g.math('ADD', loose, g.math('MULTIPLY', near, 0.25)), 0.62, 10.0), 0.9), earth,
                   lin((0.82, 0.68, 0.38)))
    pole = g.mix(g.noise(30.0, 3.0), lin((0.44, 0.40, 0.34)), lin((0.54, 0.50, 0.42)))
    albedo = cc.paint(g, mask, (straw, rope, ground, pole))
    rough = cc.weighted(g, mask, (0.9, 0.85, 0.95, 0.85))
    g.finish(albedo, g.bump(g.math('ADD', fibre, g.math('MULTIPLY', loose, 0.3)), 0.35, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


# --- well --------------------------------------------------------------------------------

(STONE, COPING, INNER, WATER, FLAG, WOOD, SHINGLE, IRON, BUCKET) = (cc.part(i) for i in range(9))
WELL_PARTS = 9
WELL_SIDES = 20
RIM = 0.385
WATER_Z = 0.27
POST_X = 0.355
AXLE = Vector((0.0, 0.0, 0.64))
EAVE, RIDGE, ROOF_X = (0.30, 0.85), 1.04, 0.43  # the eaves (y, z), the ridge's height, the roof's half length


def _well_body(p, rnd):
    """The stone ring, hollow, the water inside it; a ring of flagstones round its foot."""
    profile = [(0.0, 0.318, 0.006), (0.1, 0.308, 0.006), (0.2, 0.312, 0.006), (0.3, 0.306, 0.004), (0.3, 0.332, 0.0),
               (0.37, 0.332, 0.0), (RIM, 0.31, 0.0), (RIM, 0.25, 0.0), (0.37, 0.236, 0.0), (WATER_Z, 0.236, 0.0)]
    rings = [_ring_z((0.0, 0.0), z, r, WELL_SIDES, j, rnd) for z, r, j in profile]
    _loft(p, rings, [STONE, STONE, STONE, COPING, COPING, COPING, COPING, COPING, INNER], (None, WATER))
    flags = [(0.0, 0.43), (0.02, 0.425), (0.032, 0.405), (0.034, 0.3)]
    _loft(p, [_ring_z((0.0, 0.0), z, r, WELL_SIDES, 0.0, rnd, phase=0.08) for z, r in flags], [FLAG] * 3, (None, FLAG))


def _slab(p, a, b, x0, x1, n, thick, painted):
    """A board from a to b (points y, z) across x0..x1, thick out of the face along n (y, z)."""
    a, b, n = Vector((0.0, *a)), Vector((0.0, *b)), Vector((0.0, *n)).normalized() * thick
    corners = [a, b, b + n, a + n]
    p.loft([[Vector((x, q.y, q.z)) for q in corners] for x in (x0, x1)], painted)


def _well_roof(p, rnd):
    """Two posts beside the ring, a tie beam on them, braces, a gable roof of three courses of shingles a side, a ridge board, the gables."""
    for s in (-1, 1):
        p.bar((s * POST_X, 0.0, 0.025), (s * POST_X, 0.0, EAVE[1] + 0.02), 0.022, WOOD)
        p.bar((s * POST_X, 0.0, 0.72), (s * (POST_X - 0.1), 0.0, EAVE[1] - 0.005), 0.011, WOOD)  # the brace
    p.bar((-ROOF_X + 0.02, 0.0, EAVE[1]), (ROOF_X - 0.02, 0.0, EAVE[1]), 0.02, WOOD)
    ridge = Vector((0.0, 0.0, RIDGE))
    for s in (-1, 1):
        eave = Vector((0.0, s * EAVE[0], EAVE[1]))
        down = eave - ridge
        n = Vector((0.0, -down.z, down.y))
        if n.z < 0:
            n = -n
        for k in range(3):
            t0, t1 = k / 3 - 0.01, (k + 1) / 3 + 0.06
            a = ridge + down * t0 - n.normalized() * 0.006 * k
            b = ridge + down * min(t1, 1.06) - n.normalized() * 0.006 * k
            _slab(p, (a.y, a.z), (b.y, b.z), -ROOF_X - 0.004 * k, ROOF_X + 0.004 * k, (n.y, n.z), 0.02, SHINGLE)
    # The gable boards under the roof's ends.
    for x in (-ROOF_X + 0.03, ROOF_X - 0.03):
        p.profile_x([(-EAVE[0] + 0.03, EAVE[1] + 0.005), (EAVE[0] - 0.03, EAVE[1] + 0.005), (0.0, RIDGE - 0.02)], x - 0.008, x + 0.008, WOOD)
    p.bar((-ROOF_X - 0.01, 0.0, RIDGE + 0.012), (ROOF_X + 0.01, 0.0, RIDGE + 0.012), 0.02, SHINGLE)  # the ridge cap


def _well_winch(p):
    """The roller between the posts with the chain wound on it, an iron crank out of one post, the chain down to a bucket."""
    p.cylinder(AXLE, 'X', 0.036, POST_X - 0.022, WOOD, sides=12)
    p.cylinder(AXLE + Vector((-0.04, 0.0, 0.0)), 'X', 0.043, 0.06, IRON, sides=12)  # the chain wound on it
    p.bar(AXLE + Vector((POST_X - 0.03, 0.0, 0.0)), AXLE + Vector((POST_X + 0.07, 0.0, 0.0)), 0.008, IRON)
    crank = AXLE + Vector((POST_X + 0.065, 0.0, 0.0))
    p.bar(crank, crank + Vector((0.0, 0.03, -0.085)), 0.008, IRON)
    p.bar(crank + Vector((0.0, 0.03, -0.085)), crank + Vector((0.06, 0.03, -0.085)), 0.011, WOOD)  # the handle
    p.bar(AXLE + Vector((-0.06, -0.043, 0.0)), (-0.06, -0.043, 0.545), 0.004, IRON)  # the chain
    b = Vector((-0.06, -0.043, 0.435))
    rings = [_ring_z(b, b.z + dz, r, 10) for dz, r in ((0.0, 0.036), (0.012, 0.038), (0.02, 0.038), (0.065, 0.043), (0.075, 0.044),
                                                          (0.082, 0.044))]
    _loft(p, rings, [IRON, BUCKET, BUCKET, IRON, IRON], (BUCKET, WATER))
    for s in (-1, 1):  # the bail
        p.bar(b + Vector((s * 0.042, 0.0, 0.075)), b + Vector((s * 0.02, 0.0, 0.105)), 0.003, IRON)
    p.bar(b + Vector((-0.02, 0.0, 0.105)), b + Vector((0.02, 0.0, 0.11)), 0.003, IRON)


def _well_material(name):
    """Grey field stones in courses with moss low down and at the joints; a lighter coping; the inner wall dark and damp; dark water;
    flagstones; dark weathered wood; grey shingles in rows; black iron; the bucket's wood."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, WELL_PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    angle = g.math('ARCTAN2', y, x)
    rows = g.math('MULTIPLY', z, 15.0)
    shift = g.math('MULTIPLY', g.math('MODULO', g.math('FLOOR', rows), 2.0), 0.5)
    along = g.math('ADD', g.math('MULTIPLY', angle, 3.2), shift)

    def seam(t, width):
        f = g.math('FRACT', t)
        return g.clamp01(g.math('MULTIPLY_ADD', g.math('MINIMUM', f, g.math('SUBTRACT', 1.0, f)), -1.0 / width, 1.0))

    joints = g.math('MAXIMUM', seam(rows, 0.12), seam(along, 0.07))
    per_stone = _noise(g, g.join(g.math('FLOOR', along), g.math('FLOOR', rows), 0.0), 1.37, 0.0)
    grain = g.noise(26.0, 3.0)
    stone = g.mix(per_stone, lin((0.46, 0.45, 0.42)), lin((0.64, 0.62, 0.58)))
    stone = g.mix(g.math('MULTIPLY', grain, 0.3), stone, lin((0.52, 0.48, 0.42)))
    moss = g.math('MULTIPLY', g.clamp01(g.math('ADD', g.math('MULTIPLY', joints, 0.6), g.clamp01(g.math('MULTIPLY_ADD', z, -5.0, 0.9)))),
                  _step(g, g.noise(7.0, 3.0), 0.52, 8.0))
    stone_c = g.mix(joints, stone, lin((0.26, 0.25, 0.23)))
    stone_c = g.mix(moss, stone_c, lin((0.34, 0.42, 0.20)))
    coping = g.mix(g.math('MULTIPLY', grain, 0.5), lin((0.66, 0.64, 0.60)), lin((0.56, 0.54, 0.50)))
    coping = g.mix(seam(g.math('MULTIPLY', angle, 1.6), 0.05), coping, lin((0.32, 0.31, 0.29)))
    coping = g.mix(g.math('MULTIPLY', _step(g, g.noise(9.0, 3.0), 0.6, 8.0), 0.8), coping, lin((0.38, 0.46, 0.22)))
    inner = g.mix(joints, g.mix(per_stone, lin((0.30, 0.31, 0.28)), lin((0.38, 0.39, 0.35))), lin((0.16, 0.17, 0.15)))
    inner = g.mix(g.math('MULTIPLY', moss, 0.7), inner, lin((0.22, 0.30, 0.14)))
    water = g.mix(g.noise(12.0, 2.0), lin((0.07, 0.12, 0.13)), lin((0.12, 0.19, 0.20)))
    flag_joint = g.math('MAXIMUM', seam(g.math('MULTIPLY', angle, 1.9), 0.05),
                        seam(g.math('MULTIPLY', g.math('SQRT', g.math('ADD', g.math('MULTIPLY', x, x), g.math('MULTIPLY', y, y))), 14.0), 0.06))
    flag = g.mix(per_stone, lin((0.54, 0.52, 0.48)), lin((0.62, 0.60, 0.55)))
    flag = g.mix(flag_joint, flag, lin((0.30, 0.32, 0.22)))
    wood = g.mix(_noise(g, _stretch(g, coord, 6.0, 6.0, 40.0), 1.0, 3.0), lin((0.30, 0.22, 0.15)), lin((0.42, 0.31, 0.21)))
    wood = g.mix(g.math('MULTIPLY', grain, 0.4), wood, lin((0.36, 0.33, 0.28)))  # weathered grey
    up = g.axis(g.normal_world(), 'Z')
    tiles = g.math('MULTIPLY', z, 42.0)
    tile_shift = g.math('MULTIPLY', g.math('MODULO', g.math('FLOOR', tiles), 2.0), 0.5)
    tile_joint = g.math('MAXIMUM', seam(tiles, 0.14), seam(g.math('ADD', g.math('MULTIPLY', x, 22.0), tile_shift), 0.08))
    shingle = g.mix(_noise(g, g.join(g.math('FLOOR', g.math('ADD', g.math('MULTIPLY', x, 22.0), tile_shift)), g.math('FLOOR', tiles), 0.0), 1.37, 0.0),
                    lin((0.46, 0.47, 0.48)), lin((0.60, 0.61, 0.62)))
    shingle = g.mix(tile_joint, shingle, lin((0.24, 0.25, 0.26)))
    shingle = g.mix(g.math('MULTIPLY', _step(g, g.noise(6.0, 3.0), 0.6, 8.0), 0.6), shingle, lin((0.40, 0.46, 0.26)))  # lichen
    shingle = g.mix(_step(g, up, 0.2, -6.0), shingle, wood)  # its underside is the boards
    iron = g.mix(grain, lin((0.17, 0.17, 0.18)), lin((0.26, 0.25, 0.24)))
    bucket = g.mix(_noise(g, _stretch(g, coord, 60.0, 60.0, 4.0), 1.0, 2.0), lin((0.44, 0.32, 0.20)), lin((0.54, 0.40, 0.25)))
    albedo = cc.paint(g, mask, (stone_c, coping, inner, water, flag, wood, shingle, iron, bucket))
    rough = cc.weighted(g, mask, (0.9, 0.85, 0.8, 0.08, 0.9, 0.85, 0.75, 0.55, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.7, 0.0))
    height = g.math('ADD', g.math('MULTIPLY', joints, -0.6), g.math('ADD', g.math('MULTIPLY', tile_joint, -0.4), grain))
    height = g.math('MULTIPLY', height, g.math('SUBTRACT', 1.0, mask[3]))  # the water flat
    g.finish(albedo, g.bump(height, 0.4, 0.01), g.combine(ao, rough, metallic))
    return mat, g


# --- tractor -----------------------------------------------------------------------------

(PAINT, GREY, DARK, TYRE, RIMS, METAL, LAMP, GRILLE) = (cc.part(i) for i in range(8))
TRACTOR_PARTS = 8
REAR = Vector((0.0, -0.19, 0.19))  # the rear axle; the wheels' radius is its height
REAR_X, REAR_HW = 0.225, 0.055  # where a rear wheel's middle is, its half width
FRONT = Vector((0.0, 0.255, 0.115))
FRONT_X, FRONT_HW = 0.165, 0.036
CAB_Y, CAB_TOP = (-0.31, 0.05), 0.74  # the cab frame's back and front posts, its roof


def _wheel(p, centre, side, r, hw, sides, lugs):
    """A tyre on its rim about the line along x through centre, side: +1 or -1, which way is out. lugs: how many pairs of tread lugs,
    each slanted from a shoulder towards the middle, the pairs staggered."""
    c = Vector(centre)
    prof = [(-hw, 0.72 * r), (-hw, 0.9 * r), (-0.8 * hw, r), (0.8 * hw, r), (hw, 0.9 * r), (hw, 0.72 * r), (0.45 * hw, 0.68 * r)]
    rings = [_ring_x(c, side * t, rr, sides) for t, rr in prof]
    _loft(p, rings, [TYRE] * 6, (TYRE, RIMS))
    # The hub out of the rim's dish, the axle's cap on it.
    hub = [(0.45 * hw, 0.3 * r), (0.8 * hw, 0.27 * r), (0.95 * hw, 0.14 * r)]
    _loft(p, [_ring_x(c, side * t, rr, 10) for t, rr in hub], [RIMS, RIMS], (RIMS, RIMS))
    if not lugs:
        return
    top = r + 0.002
    for k in range(lugs):
        for half, extra in ((-1, 0.0), (1, math.pi / lugs)):
            a0 = 2 * math.pi * k / lugs + extra
            a = c + Vector((half * 0.92 * hw, math.cos(a0 + 0.2) * top, math.sin(a0 + 0.2) * top))
            b = c + Vector((-half * 0.08 * hw, math.cos(a0) * top, math.sin(a0) * top))
            p.bar(a, b, 0.0115, TYRE)


def _fender(p, x0, x1):
    """A round fender over a rear wheel, from just in front of its top to behind it, across x0..x1."""
    inner, outer = REAR.z + 0.035, REAR.z + 0.05
    arc = [math.radians(a) for a in range(8, 181, 19)]
    yz = [(REAR.y + math.cos(a) * outer, REAR.z + math.sin(a) * outer) for a in arc]
    yz += [(REAR.y + math.cos(a) * inner, REAR.z + math.sin(a) * inner) for a in reversed(arc)]
    p.loft([[Vector((x, y, z)) for y, z in yz] for x in (x0, x1)], PAINT)


def _tractor_hull(p):
    # The bonnet over the engine, its nose drawn in, the grille on its front.
    rings = [_chamfered((0.0, y, zc), hw, hh, 0.035, 'XZ') for y, hw, zc, hh in
             ((0.02, 0.098, 0.255, 0.1), (0.36, 0.092, 0.255, 0.092), (0.425, 0.08, 0.25, 0.075))]
    _loft(p, rings, [PAINT, PAINT], (PAINT, GRILLE))
    p.box((-0.068, 0.04, 0.08), (0.068, 0.41, 0.17), DARK)  # the engine and the sump under it
    p.box((-0.045, 0.2, 0.09), (0.045, 0.31, 0.15), DARK)
    p.bar((-FRONT_X + 0.02, FRONT.y, FRONT.z), (FRONT_X - 0.02, FRONT.y, FRONT.z), 0.017, DARK)  # the front axle
    p.box((-0.1, 0.425, 0.09), (0.1, 0.47, 0.19), DARK)  # the front weight
    for s in (-1, 1):
        p.cylinder((s * 0.058, 0.432, 0.3), 'Y', 0.02, 0.006, GREY, sides=8)  # the lamp's bezel
        p.cylinder((s * 0.058, 0.437, 0.3), 'Y', 0.015, 0.004, LAMP, sides=8)
    # The transmission under the seat, and the fenders on it.
    p.box((-0.125, -0.32, 0.11), (0.125, 0.03, 0.29), PAINT)
    for s in (-1, 1):
        x0, x1 = (0.16, 0.295) if s > 0 else (-0.295, -0.16)
        _fender(p, x0, x1)
        p.box((min(s * 0.125, s * 0.165), -0.3, 0.27), (max(s * 0.125, s * 0.165), -0.04, 0.29), PAINT)  # the deck to the fender
        p.box((min(s * 0.13, s * 0.2), 0.06, 0.135), (max(s * 0.13, s * 0.2), 0.12, 0.15), DARK)  # the step
    p.box((-0.13, -0.06, 0.23), (0.13, 0.04, 0.25), DARK)  # the floor plate
    # The seat, the console, the steering wheel.
    p.box((-0.07, -0.22, 0.29), (0.07, -0.1, 0.335), DARK)
    p.box((-0.07, -0.25, 0.32), (0.07, -0.215, 0.46), DARK)
    p.box((-0.075, -0.01, 0.25), (0.075, 0.05, 0.4), PAINT)
    p.bar((0.0, 0.02, 0.38), (0.0, -0.035, 0.45), 0.009, DARK)
    p.cylinder((0.0, -0.04, 0.455), 'Z', 0.048, 0.006, DARK, sides=10)
    # The exhaust, its silencer, and the air intake on the bonnet.
    p.cylinder((0.055, 0.3, 0.42), 'Z', 0.022, 0.06, METAL, sides=8)
    p.bar((0.055, 0.3, 0.34), (0.055, 0.3, 0.62), 0.012, METAL)
    p.bar((0.055, 0.3, 0.62), (0.055, 0.32, 0.65), 0.012, METAL)
    p.cylinder((-0.055, 0.28, 0.39), 'Z', 0.02, 0.05, GREY, sides=8)
    p.cylinder((-0.055, 0.28, 0.45), 'Z', 0.026, 0.012, DARK, sides=8)
    # The cab frame: four posts, the rails round its top, a roof, a guard of bars at its back.
    for s in (-1, 1):
        for y in CAB_Y:
            p.bar((s * 0.15, y, 0.28), (s * 0.15, y, CAB_TOP), 0.012, GREY)
        p.bar((s * 0.15, CAB_Y[0], CAB_TOP), (s * 0.15, CAB_Y[1], CAB_TOP), 0.012, GREY)
    for y in CAB_Y:
        p.bar((-0.15, y, CAB_TOP), (0.15, y, CAB_TOP), 0.012, GREY)
    for z in (0.5, 0.58, 0.66):
        p.bar((-0.15, CAB_Y[0], z), (0.15, CAB_Y[0], z), 0.006, GREY)
    rings = [_chamfered((0.0, y, CAB_TOP + 0.022), 0.185, 0.012, 0.008, 'XZ') for y in (CAB_Y[0] - 0.035, CAB_Y[1] + 0.035)]
    for ring in rings:
        for v in ring:
            v.z += 0.012 * (1.0 - (v.x / 0.185) ** 2)  # crowned
    _loft(p, rings, [GREY], (GREY, GREY))
    # The hitch: two lower links and the top link, the drawbar.
    for s in (-1, 1):
        p.bar((s * 0.08, -0.3, 0.14), (s * 0.07, -0.41, 0.1), 0.011, DARK)
    p.bar((0.0, -0.31, 0.27), (0.0, -0.39, 0.2), 0.01, DARK)
    p.box((-0.03, -0.42, 0.1), (0.03, -0.3, 0.125), DARK)


def _tractor_material(name, paint):
    """Bright enamel with mud low down; grey frame and rims; dark steel; black tyres caked with earth; a dark grille of slats; lamps."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, TRACTOR_PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 3.0)
    mud = _step(g, g.math('ADD', g.math('MULTIPLY', z, -2.5), g.noise(9.0, 3.0)), 0.25, 8.0)  # more of it the lower
    enamel = g.mix(g.math('MULTIPLY', g.noise(5.0, 2.0), 0.3), lin(paint), lin(tuple(v * 0.82 for v in paint)))
    enamel = g.mix(g.math('MULTIPLY', mud, 0.8), enamel, lin((0.40, 0.32, 0.23)))
    grey = g.mix(grain, lin((0.56, 0.57, 0.58)), lin((0.64, 0.65, 0.66)))
    dark = g.mix(grain, lin((0.20, 0.20, 0.21)), lin((0.27, 0.27, 0.28)))
    dark = g.mix(g.math('MULTIPLY', mud, 0.6), dark, lin((0.36, 0.29, 0.21)))
    # The tyres: the wheels' own coordinates, about their axle; earth caked in the tread.
    tyre = g.mix(g.noise(14.0, 3.0), lin((0.12, 0.12, 0.12)), lin((0.17, 0.17, 0.16)))
    tyre = g.mix(g.math('MULTIPLY', _step(g, g.noise(11.0, 4.0), 0.55, 6.0), 0.85), tyre, lin((0.38, 0.30, 0.21)))
    rims = g.mix(grain, lin((0.58, 0.58, 0.56)), lin((0.66, 0.66, 0.64)))
    metal = g.mix(grain, lin((0.30, 0.30, 0.30)), lin((0.42, 0.40, 0.38)))
    lamp = lin((0.95, 0.92, 0.78))
    slats = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', z, 260.0)), 4.0, -1.0))
    grille = g.mix(slats, lin((0.10, 0.10, 0.11)), lin((0.36, 0.36, 0.37)))
    albedo = cc.paint(g, mask, (enamel, grey, dark, tyre, rims, metal, lamp, grille))
    rough = cc.weighted(g, mask, (0.35, 0.45, 0.6, 0.9, 0.45, 0.4, 0.15, 0.5))
    metallic = cc.weighted(g, mask, (0.0, 0.5, 0.5, 0.0, 0.4, 0.8, 0.0, 0.5))
    g.finish(albedo, g.bump(g.math('ADD', grain, slats), 0.08, 0.004), g.combine(ao, rough, metallic))
    return mat, g


# --- build and run -----------------------------------------------------------------------

def build(variant_id):
    ic.clear(variant_id, NODES[variant_id])
    rnd = random.Random(variant_id)
    if variant_id in HEDGES:
        body = Parts()
        _hedge(body, rnd, HEDGES[variant_id])
        return cc.finish(variant_id, [(body, "body")], 0.0, _hedge_material(variant_id + "_mat"))
    if variant_id in HAY:
        body = Parts()
        kind = {"hay_bale": "round", "hay_bale_square": "square", "hay_bale_heap": "heap"}[variant_id]
        {"round": _round_bale, "square": _square_stack, "heap": _heap}[kind](body, rnd)
        return cc.finish(variant_id, [(body, "body")], 0.0, _hay_material(variant_id + "_mat", kind))
    if variant_id in WELLS:
        body, roof, winch = Parts(), Parts(), Parts()
        _well_body(body, rnd)
        _well_roof(roof, rnd)
        _well_winch(winch)
        meshes, mat, graph = cc.finish(variant_id, [(body, "body"), (roof, "roof"), (winch, "winch")], 0.0, _well_material(variant_id + "_mat"))
        _pivot(meshes[2], AXLE)
        return meshes, mat, graph
    hull, rear, front = Parts(), Parts(), Parts()
    _tractor_hull(hull)
    for s in (-1, 1):
        _wheel(rear, (s * REAR_X, REAR.y, REAR.z), s, REAR.z, REAR_HW, 24, 11)
        _wheel(front, (s * FRONT_X, FRONT.y, FRONT.z), s, FRONT.z, FRONT_HW, 18, 0)
    meshes, mat, graph = cc.finish(variant_id, [(hull, "hull"), (rear, "wheel_rear"), (front, "wheel_front")], 0.0,
                                   _tractor_material(variant_id + "_mat", TRACTORS[variant_id]))
    _pivot(meshes[1], REAR)
    _pivot(meshes[2], FRONT)
    return meshes, mat, graph


def run(variant_id):
    _register()
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZES[variant_id], folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
