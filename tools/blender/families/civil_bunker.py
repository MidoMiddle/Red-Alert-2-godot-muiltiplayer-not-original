"""Civilian bunker: the garrisoned concrete bunker of the map (neutral_civilian_bunker, game/data/neutral/civilian.yaml), its states
(docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside.

Footprint 2x2 cells, origin at the centre of the footprint on the ground. Made a storey to a cell, as the houses (the game squashes it
upright, game/data/visuals/models.yaml): walls 0.98 high, the roof at 1.13, the masts to about 2.1. The composition follows the owner's
references (RA2's bunker and a fan model of it); the parts are our own:
    pad       a low eight-sided concrete pad, a little wider than the bunker (the base node)
    glacis    a steep concrete skirt round its foot, its top the floor inside
    walls     eight walls, hollow inside; a firing slit with a dark back in each long wall; on the front a porch with a steel door
    roof      a thick slab over the walls, its sloped rim painted with hazard stripes front and back, a hatch, a vent and two masts
    crates    ammunition crates stacked against the front right corner
No emblems, no text. Eight soldiers inside: eight barrels out of the slits when occupied.
Nodes: "body" (the glacis, walls, porch, door, crates, and the barrels, chips or rubble of a state), "roof", "team_mask" (the band above
the door that the game paints in the owner's colour), "base" (the pad). The abandoned one also has the empty nodes fire_1..3. One GLB
per state:
    building_civilian_bunker                    whole, empty
    building_civilian_bunker_occupied           whole, occupied: rifle barrels out of the slits, sandbags along the edge of the roof
    building_civilian_bunker_damaged            damaged, empty: cracks, soot over the slits, a mast snapped, chips of concrete at the foot
    building_civilian_bunker_damaged_occupied   damaged, occupied: the same, with the barrels and the sandbags
    building_civilian_bunker_abandoned          critically damaged, its soldiers out: the front right corner blown in, a slab fallen
                                                inside, bars out of the broken edges, rubble
    building_civilian_bunker_ruins              destroyed: stumps of the walls on the skirt, broken slabs and rubble; the pad stays
Texture 1024, as the house: a 2x2 building is not a small prop (generation_requirements.md, section 4).

In Blender, after tools/blender is on sys.path:
    import families.civil_bunker as cb
    print(cb.run("building_civilian_bunker"))   # or cb.run_all()
"""

import math
import os

import bmesh
import bpy
from mathutils import Euler, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

SIZE = 1024
SQ2 = math.sqrt(2.0)

VARIANTS = {
    "building_civilian_bunker": "intact",
    "building_civilian_bunker_occupied": "occupied",
    "building_civilian_bunker_damaged": "damaged",
    "building_civilian_bunker_damaged_occupied": "damaged_occupied",
    "building_civilian_bunker_abandoned": "abandoned",
    "building_civilian_bunker_ruins": "ruins",
}


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back. A face made by a cut has no part
    written, so it reads 0: concrete."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, PAD, DARK, STEEL, RIM, TEAM, CRATE, WOOD, SAND = (_part(i) for i in range(9))
PARTS = 9

# Eight-sided plans: (half the width, the cut of a corner). The pad, the foot of the glacis, the walls, the roof slab, the top of the roof.
PAD_PLAN = (0.99, 0.40)
GLACIS_PLAN = (0.93, 0.36)
WALL_PLAN = (0.78, 0.27)
SLAB_PLAN = (0.84, 0.31)
TOP_PLAN = (0.76, 0.26)
WALL_T = 0.12
PAD_H = 0.06
FLOOR = 0.40  # the top of the glacis: the floor inside, where the walls stand
SLIT = (0.58, 0.70)
WALL_TOP = 0.98
SLAB_TOP = 1.04
ROOF_TOP = 1.13
PORCH_TOP = 0.76

# The slits of the long walls, (start, end) along the wall. The walls run anticlockwise from the front end of the right one: 0 right,
# 2 back, 4 left, 6 front; the odd ones are the cut corners. The front slit leaves room for the porch on the left.
SLITS = {0: (0.15, 0.85), 2: (0.15, 0.85), 4: (0.15, 0.85), 6: (0.56, 0.9)}

# Which walls stand, as (wall, start, end, top). Whole: all eight to the roof.
WHOLE = tuple((i, 0.0, 1.0, WALL_TOP) for i in range(8))
# Abandoned: the front right corner blown in. The corner wall is a stump; the right and the front wall broken towards it.
BROKEN = ((1, 0.0, 1.0, WALL_TOP), (2, 0.0, 1.0, WALL_TOP), (3, 0.0, 1.0, WALL_TOP), (4, 0.0, 1.0, WALL_TOP),
          (5, 0.0, 1.0, WALL_TOP),
          (0, 0.0, 0.3, 0.5), (0, 0.3, 0.6, 0.66), (0, 0.6, 1.0, WALL_TOP),
          (6, 0.0, 0.5, WALL_TOP), (6, 0.5, 0.75, 0.62), (6, 0.75, 1.0, 0.48),
          (7, 0.0, 1.0, 0.46))
# Ruins: stumps of different heights round the back and the left, little left at the front.
RUINS = ((1, 0.0, 1.0, 0.55), (2, 0.0, 0.7, 0.8), (2, 0.7, 1.0, 0.6), (3, 0.0, 1.0, 0.68), (4, 0.3, 1.0, 0.6),
         (5, 0.0, 1.0, 0.5), (0, 0.6, 1.0, 0.5), (6, 0.0, 0.35, 0.52))

# The masts on the roof: where they stand, how high above the roof.
MASTS = (((-0.42, 0.42), 0.95), ((-0.24, 0.53), 0.75))
# The crates against the front right corner: x, y of the middle, the z of the foot, the side.
CRATES = ((0.80, -0.80, PAD_H, 0.2), (0.86, -0.56, PAD_H, 0.18), (0.79, -0.79, PAD_H + 0.17, 0.16))
# The flames of the abandoned one: in the hole, on the top of the broken right wall, on the roof that is left.
FIRE_POINTS = ((0.45, -0.35, 0.55), (0.66, 0.35, 1.0), (-0.2, -0.3, ROOF_TOP))

# Blocks of concrete: middle, size, turn (radians about x, y, z).
CHIPS = (((-0.93, 0.3, PAD_H + 0.02), (0.08, 0.06, 0.05), (0.2, 0.0, 0.6)),
         ((-0.2, 0.95, PAD_H + 0.02), (0.07, 0.07, 0.04), (0.0, 0.3, 1.1)),
         ((0.95, 0.15, PAD_H + 0.02), (0.06, 0.09, 0.05), (0.3, 0.1, 0.2)),
         ((0.3, -0.96, PAD_H + 0.02), (0.08, 0.05, 0.04), (0.0, 0.2, 0.4)))
RUBBLE_ABANDONED = (((0.5, -0.55, FLOOR + 0.05), (0.2, 0.16, 0.12), (0.3, 0.2, 0.5)),
                    ((0.3, -0.2, FLOOR + 0.04), (0.16, 0.14, 0.1), (0.1, 0.4, 1.0)),
                    ((0.82, -0.72, PAD_H + 0.06), (0.22, 0.18, 0.14), (0.4, 0.2, 0.3)),
                    ((0.92, -0.35, PAD_H + 0.05), (0.14, 0.18, 0.1), (0.2, 0.5, 0.8)),
                    ((0.6, -0.9, PAD_H + 0.04), (0.16, 0.1, 0.08), (0.1, 0.1, 0.2)),
                    ((0.97, -0.85, PAD_H + 0.03), (0.1, 0.08, 0.07), (0.3, 0.0, 0.9)))
RUBBLE_RUINS = (((0.1, -0.1, FLOOR + 0.06), (0.3, 0.24, 0.14), (0.2, 0.1, 0.4)),
                ((-0.3, 0.2, FLOOR + 0.05), (0.22, 0.2, 0.12), (0.3, 0.2, 1.2)),
                ((0.4, 0.3, FLOOR + 0.05), (0.2, 0.16, 0.1), (0.1, 0.3, 0.3)),
                ((-0.1, -0.5, FLOOR + 0.04), (0.18, 0.14, 0.1), (0.2, 0.0, 0.9)),
                ((0.6, -0.4, FLOOR + 0.03), (0.16, 0.12, 0.08), (0.4, 0.2, 0.1)),
                ((0.85, -0.7, PAD_H + 0.06), (0.2, 0.18, 0.12), (0.3, 0.3, 0.5)),
                ((-0.75, -0.85, PAD_H + 0.04), (0.14, 0.12, 0.08), (0.2, 0.1, 0.2)),
                ((0.9, 0.5, PAD_H + 0.04), (0.12, 0.14, 0.08), (0.1, 0.3, 0.7)))
# Broken slabs of the roof: middle, size, turn.
SLAB_ABANDONED = (((0.42, -0.4, 0.66), (0.5, 0.36, 0.06), (0.55, -0.45, 0.8)),)
SLABS_RUINS = (((-0.15, 0.1, 0.62), (0.7, 0.5, 0.06), (0.35, -0.2, 0.3)),
               ((0.35, -0.3, 0.55), (0.5, 0.4, 0.06), (-0.4, 0.3, 1.0)),
               ((0.7, 0.75, 0.2), (0.36, 0.3, 0.06), (0.2, 0.6, 0.4)))
# Bars out of the broken concrete: from, to.
BARS_ABANDONED = (((0.3, -0.06, 1.02), (0.42, -0.2, 0.9)), ((0.18, -0.25, 1.03), (0.3, -0.42, 0.95)),
                  ((0.78, -0.2, 0.62), (0.9, -0.3, 0.75)), ((0.2, -0.78, 0.58), (0.28, -0.92, 0.7)))
BARS_RUINS = (((-0.3, 0.6, 0.78), (-0.2, 0.5, 0.95)), ((0.5, 0.6, 0.6), (0.6, 0.5, 0.75)), ((-0.6, -0.1, 0.6), (-0.7, -0.2, 0.74)),
              ((0.2, -0.3, 0.58), (0.35, -0.4, 0.72)))


# --- plans --------------------------------------------------------------------------

def _plan(half, cut):
    """The corners of an eight-sided plan, anticlockwise from the front end of the right side."""
    s = half - cut
    return [Vector(p) for p in ((half, -s), (half, s), (s, half), (-s, half), (-half, s), (-half, -s), (-s, -half), (s, -half))]


def _ring(points, z):
    return [Vector((q.x, q.y, z)) for q in points]


OUTER = _plan(*WALL_PLAN)
# The inner face of the walls, parallel to the outer one: the cut corners move in by the thickness too.
INNER = _plan(WALL_PLAN[0] - WALL_T, WALL_PLAN[1] - WALL_T * (2.0 - SQ2))


def _at(i, t, depth):
    """A point of wall i: t along it (0 at its start, 1 at its end), depth into it (0 the outer face, 1 the inner one)."""
    j = (i + 1) % 8
    return OUTER[i].lerp(OUTER[j], t).lerp(INNER[i].lerp(INNER[j], t), depth)


def _out(i):
    """The outward direction of wall i."""
    d = OUTER[(i + 1) % 8] - OUTER[i]
    return Vector((d.y, -d.x, 0.0)).normalized()


# --- geometry -------------------------------------------------------------------------

def _wall(p, i, t0, t1, z0, z1, part=CONCRETE, d0=0.0, d1=1.0):
    quad = [_at(i, t0, d0), _at(i, t1, d0), _at(i, t1, d1), _at(i, t0, d1)]
    p.loft([_ring(quad, z0), _ring(quad, z1)], part)


def _piece(p, i, t0, t1, top):
    """Wall i from t0 to t1 along it, up to top, with its slit where the piece crosses it: the dark back of the slit sits in the
    inner half of the wall, so the slit reads as a deep opening."""
    slit = SLITS.get(i)
    if slit is None or top <= SLIT[0] or slit[1] <= t0 or t1 <= slit[0]:
        _wall(p, i, t0, t1, FLOOR, top)
        return
    _wall(p, i, t0, t1, FLOOR, SLIT[0])
    if top > SLIT[1]:
        _wall(p, i, t0, t1, SLIT[1], top)
    hi = min(top, SLIT[1])
    a, b = max(t0, slit[0]), min(t1, slit[1])
    if t0 < a:
        _wall(p, i, t0, a, SLIT[0], hi)
    if b < t1:
        _wall(p, i, b, t1, SLIT[0], hi)
    _wall(p, i, a, b, SLIT[0], hi, DARK, 0.45, 1.0)


def _walls(p, plan):
    for i, t0, t1, top in plan:
        _piece(p, i, t0, t1, top)


def _glacis(p):
    p.loft([_ring(_plan(*GLACIS_PLAN), PAD_H), _ring(OUTER, FLOOR)], CONCRETE)


def _porch(p):
    """The porch in front of the left part of the front wall, lower than the walls: a concrete box with a steel door, a slab over it
    and a step."""
    p.box((-0.50, -0.95, PAD_H), (-0.06, -0.66, PORCH_TOP), CONCRETE)
    p.box((-0.54, -0.99, PORCH_TOP), (-0.02, -0.62, PORCH_TOP + 0.06), CONCRETE)
    p.box((-0.42, -0.962, PAD_H), (-0.14, -0.94, 0.56), STEEL)
    p.box((-0.46, -0.99, PAD_H), (-0.10, -0.95, 0.09), CONCRETE)


def _block(p, centre, size, turn, part):
    """A box of the given size turned about its middle: a fallen slab or a block of rubble."""
    sx, sy, sz = (v / 2 for v in size)
    c = Vector(centre)
    m = Euler(turn).to_matrix()
    p.loft([[c + m @ Vector((u * sx, v * sy, w * sz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w in (-1, 1)], part)


def _crates(p):
    """Ammunition crates, each with two boards crossed over its front and its right side."""
    for x, y, z, s in CRATES:
        h = s * 0.85
        x0, x1, y0, y1 = x - s / 2, x + s / 2, y - s / 2, y + s / 2
        p.box((x0, y0, z), (x1, y1, z + h), CRATE)
        e = 0.02
        for a, b in (((x0 + e, y0 - 0.006, z + e), (x1 - e, y0 - 0.006, z + h - e)),
                     ((x0 + e, y0 - 0.006, z + h - e), (x1 - e, y0 - 0.006, z + e)),
                     ((x1 + 0.006, y0 + e, z + e), (x1 + 0.006, y1 - e, z + h - e)),
                     ((x1 + 0.006, y0 + e, z + h - e), (x1 + 0.006, y1 - e, z + e))):
            p.bar(a, b, 0.009, WOOD)


def _barrels(p):
    """Eight rifle barrels out of the slits, two in each: one for each soldier inside."""
    for i, (t0, t1) in SLITS.items():
        for k in (0.3, 0.7):
            root = _at(i, t0 + k * (t1 - t0), 0.45)
            start = Vector((root.x, root.y, (SLIT[0] + SLIT[1]) / 2))
            p.bar(start, start + _out(i) * 0.36 + Vector((0.0, 0.0, -0.02)), 0.014, STEEL)


def _bag(p, centre, turn, length, width, height):
    """A sandbag: three rings, its belly fuller than its foot and its top."""
    m = Euler((0.0, 0.0, turn)).to_matrix()

    def ring(sl, sw, dz):
        return [centre + m @ Vector((u * sl / 2, v * sw / 2, dz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))]

    p.loft([ring(length, width, 0.0), ring(length * 1.05, width * 1.1, height * 0.5), ring(length * 0.9, width * 0.9, height)], SAND)


def _roof_bags(p):
    """Sandbags along the edge of the roof: three on each long side, one on each cut corner."""
    plan = _plan(0.66, 0.23)
    for i in range(8):
        a, b = plan[i], plan[(i + 1) % 8]
        turn = math.atan2(b.y - a.y, b.x - a.x)
        n = 3 if i % 2 == 0 else 1
        for k in range(n):
            q = a.lerp(b, (k + 0.5) / n)
            _bag(p, Vector((q.x, q.y, ROOF_TOP)), turn, 0.24, 0.13, 0.09)


def _roof(p):
    """The slab over the walls and its sloped rim; the top of the rim is the roof."""
    p.loft([_ring(_plan(*SLAB_PLAN), WALL_TOP), _ring(_plan(*SLAB_PLAN), SLAB_TOP)], CONCRETE)
    p.loft([_ring(_plan(*SLAB_PLAN), SLAB_TOP), _ring(_plan(*TOP_PLAN), ROOF_TOP)], RIM)


def _blow_in(p):
    """Cuts the front right corner off the roof along two planes and closes the cuts: the broken edge of the slab."""
    for point, normal in (((0.15, -0.05, 0.0), (1.0, -1.0, 0.15)), ((0.1, -0.5, 0.0), (0.5, -1.0, 0.0))):
        geom = list(p.bm.verts) + list(p.bm.edges) + list(p.bm.faces)
        bmesh.ops.bisect_plane(p.bm, geom=geom, plane_co=point, plane_no=Vector(normal).normalized(), clear_outer=True)
        bmesh.ops.holes_fill(p.bm, edges=[e for e in p.bm.edges if e.is_boundary], sides=0)


def _fixtures(p, variant):
    """The hatch, the vent and the masts on the roof. The damaged one has a mast snapped, its top lying on the roof; the abandoned
    one has lost it."""
    p.box((-0.12, 0.05, ROOF_TOP), (0.14, 0.31, ROOF_TOP + 0.04), STEEL)
    p.turned((0.38, 0.3, ROOF_TOP), 'Z', [(0.0, 0.06), (0.1, 0.06), (0.1, 0.11), (0.14, 0.1), (0.15, 0.01)], STEEL, sides=12)
    for k, ((x, y), h) in enumerate(MASTS):
        if k == 1 and variant == "abandoned":
            continue
        p.box((x - 0.05, y - 0.05, ROOF_TOP), (x + 0.05, y + 0.05, ROOF_TOP + 0.06), STEEL)
        if k == 1 and variant in ("damaged", "damaged_occupied"):
            p.bar((x, y, ROOF_TOP + 0.06), (x + 0.02, y, ROOF_TOP + 0.26), 0.012, STEEL)
            p.bar((x + 0.06, y - 0.06, ROOF_TOP + 0.015), (x + 0.5, y - 0.4, ROOF_TOP + 0.015), 0.012, STEEL)
        else:
            p.bar((x, y, ROOF_TOP + 0.06), (x, y, ROOF_TOP + h), 0.012, STEEL)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Grey concrete, a darker pad, dark slits, painted steel, hazard stripes on the rim front and back, a light team band, olive
    crates with pale boards, sandbags; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(14.0, 4.0)

    concrete = g.mix(grain, lin((0.56, 0.56, 0.54)), lin((0.67, 0.67, 0.64)))
    pad = g.mix(grain, lin((0.42, 0.41, 0.39)), lin((0.50, 0.49, 0.46)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.09, 0.09, 0.10)))
    steel = g.mix(grain, lin((0.30, 0.33, 0.31)), lin((0.38, 0.41, 0.38)))
    # The rim: yellow and black stripes across its front and back slopes, plain concrete on its corners and on top.
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, z), 7.0)), 40.0, -20.0))
    hazard = g.mix(stripe, lin((0.86, 0.66, 0.12)), lin((0.09, 0.09, 0.09)))
    front_back = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', y), 20.0, -10.0))
    sloped = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), -20.0, 19.0))
    rim = g.mix(g.math('MULTIPLY', front_back, sloped), concrete, hazard)
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    crate = g.mix(grain, lin((0.29, 0.31, 0.19)), lin((0.35, 0.37, 0.23)))
    wood = g.mix(grain, lin((0.58, 0.52, 0.34)), lin((0.66, 0.60, 0.40)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))

    albedo = concrete
    for m, col in zip(mask[1:], (pad, dark, steel, rim, team, crate, wood, sand)):
        albedo = g.mix(m, albedo, col)
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        # Cracks over everything, and soot over the slits: the band just above them.
        crack = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -100.0, 1.0))
        albedo = g.mix(crack, albedo, lin((0.14, 0.13, 0.12)))
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 30.0, -21.0)),
                      g.clamp01(g.math('MULTIPLY_ADD', z, -30.0, 27.0)))
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 6.0, -3.0)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.95, 0.9, 0.5, 0.8, 0.4, 0.85, 0.85, 0.95))
    metallic = weighted((0.0, 0.0, 0.0, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(40.0, 3.0), 0.05, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _drop_bottoms(objs):
    """Deletes the faces that look down onto the pad or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and f.calc_center_median().z < PAD_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def _pad(p):
    p.loft([_ring(_plan(*PAD_PLAN), 0.0), _ring(_plan(PAD_PLAN[0] - 0.02, PAD_PLAN[1]), PAD_H)], PAD)


def _finish(objs, mat_name, variant):
    """Shared steps: drop the hidden bottoms, smooth by angle, one UV layout over all, the material."""
    _drop_bottoms(objs)
    rc.select_only(objs)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(objs)
    mat, graph = rc.ruins_material(mat_name) if variant == "ruins" else _material(mat_name, variant)
    for obj in objs:
        obj.data.materials.append(mat)
    return objs, mat, graph


def build(variant_id):
    variant = VARIANTS[variant_id]
    occupied = variant in ("occupied", "damaged_occupied")
    abandoned = variant == "abandoned"
    body = Parts()
    _glacis(body)
    _walls(body, BROKEN if abandoned else WHOLE)
    _porch(body)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED + SLAB_ABANDONED:
            _block(body, centre, size, turn, CONCRETE)
        for a, b in BARS_ABANDONED:
            body.bar(a, b, 0.008, STEEL)
    else:
        _crates(body)
    if occupied:
        _barrels(body)
    if variant in ("damaged", "damaged_occupied"):
        for centre, size, turn in CHIPS:
            _block(body, centre, size, turn, CONCRETE)
    roof = Parts()
    _roof(roof)
    if abandoned:
        _blow_in(roof)
    _fixtures(roof, variant)
    if occupied:
        _roof_bags(roof)
    team = Parts()
    team.box((-0.42, -0.962, 0.61), (-0.14, -0.945, 0.71), TEAM)
    base = Parts()
    _pad(base)
    objs = [body.finish("bunker_body"), roof.finish("bunker_roof"), team.finish("bunker_team"), base.finish("bunker_base")]
    return _finish(objs, variant_id + "_mat", variant)


def build_ruins(variant_id):
    """The ruins: the skirt, stumps of the walls, broken slabs, rubble and bars, on the pad; no roof and no band."""
    body = Parts()
    _glacis(body)
    _walls(body, RUINS)
    for centre, size, turn in RUBBLE_RUINS + SLABS_RUINS:
        _block(body, centre, size, turn, CONCRETE)
    for a, b in BARS_RUINS:
        body.bar(a, b, 0.008, STEEL)
    base = Parts()
    _pad(base)
    return _finish([body.finish("bunker_body"), base.finish("bunker_base")], variant_id + "_mat", "ruins")


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    ruins = VARIANTS[variant_id] == "ruins"
    objs, mat, graph = build_ruins(variant_id) if ruins else build(variant_id)
    nodes = rc.fire_nodes(FIRE_POINTS) if VARIANTS[variant_id] == "abandoned" else []
    rc.arrange(objs + nodes, variant_id)
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in objs)}, "
              f"size {tuple(round(v, 3) for v in objs[0].dimensions)}"]
    imgs = rc.bake_material(objs, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    names = ["body", "base"] if ruins else ["body", "roof", "team_mask", "base"]
    with rc.node_names(objs + nodes, names + rc.fire_names(nodes)):
        rc.save_blend(objs + nodes, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs + nodes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
