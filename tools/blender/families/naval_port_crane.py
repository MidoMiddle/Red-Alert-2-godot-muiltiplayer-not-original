"""Port crane, element 05 of the naval set (docs/naval_elements/05_port_crane): a tower crane on the quay; it blocks the cell of its base,
its boom over the quay blocks nothing.

Footprint 1x1 cell, origin at the centre of the cell on the ground. Made at the size the game draws it: a landmark of the port as tall as
the tall things of the map (the smokestack stands 2.3 in the game, the prism tower 1.9): the tower to 2.2, the boom's apex to 2.75, the boom
1.6 out. A hammerhead crane:
    base       a grey concrete footing with four steel feet bolted to it
    tower      a yellow lattice tower, its top the slewing ring
    boom       all that turns on the ring (its origin on the tower's axis at the ring, the game turns it about the vertical): the cab of
               dark glass in a grey frame, the A-frame apex, the jib of three chords in a yellow lattice with white bands out to 1.6, the
               counter-jib with its concrete counterweights, the tie bars from the apex, the trolley on the jib and the hook on its cable,
               red lights at the apex and the jib's tip
Nodes: "base", "tower", "boom". One GLB per state:
    port_crane          whole: the cable taut, the hook at half height
    port_crane_damaged  damaged: the cable snapped and the hook down on the footing, the cab's glass out, the jib's tip bent down,
                        soot and rust, the lights out
    port_crane_ruins    fallen: the tower broken at 0.9 with its members bent out, the top of it and the boom lying across the quay,
                        the counterweights thrown about; nodes "base", "body"
Budget 4000 triangles, texture 1024 (docs/naval_elements/05_port_crane/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_port_crane as crane
    print(crane.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from families.infrastructure_gate import transformed
from families.naval_platform import lattice
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "port_crane": "intact",
    "port_crane_damaged": "damaged",
    "port_crane_ruins": "ruins",
}

(YELLOW, JIB, CONCRETE, DARK, GLASS, RED, STEEL) = (cc.part(i) for i in range(7))
PARTS = 7

FOOT = 0.15  # the footing's top
TOWER_HALF = 0.2
RING = 2.2  # the slewing ring: the boom's origin
JIB_LEN = 1.6
COUNTER = 0.7
APEX = 0.55  # over the ring
TROLLEY = 1.05
BREAK = 0.9  # where the tower broke


def _base(p):
    p.box((-0.4, -0.4, 0.0), (0.4, 0.4, FOOT - 0.04), CONCRETE)
    p.loft([[Vector((sx * s, sy * s, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))] for s, z in ((0.4, FOOT - 0.04), (0.34, FOOT))],
           CONCRETE)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * TOWER_HALF, sy * TOWER_HALF
            p.box((x - 0.05, y - 0.05, FOOT), (x + 0.05, y + 0.05, FOOT + 0.03), DARK)


def _jib_lattice(p, x0, x1, segments, painted, sag=None):
    """A jib of three chords from x0 to x1 along x, in the boom's frame: two at the bottom at y = ±0.08, one on top; diagonals between.
    sag(x) lowers it (a bent tip)."""
    def at(x, y, z):
        dz = sag(x) if sag else 0.0
        return Vector((x, y, z + dz))

    xs = [x0 + (x1 - x0) * k / segments for k in range(segments + 1)]
    for a, b in zip(xs, xs[1:]):
        for y, z in ((-0.08, 0.0), (0.08, 0.0), (0.0, 0.16)):
            p.bar(at(a, y, z), at(b, y, z), 0.015, painted)
        p.bar(at(a, -0.08, 0.0), at(b, 0.0, 0.16), 0.009, painted)
        p.bar(at(a, 0.08, 0.0), at(b, 0.0, 0.16), 0.009, painted)
        p.bar(at(a, -0.08, 0.0), at(b, 0.08, 0.0), 0.009, painted)
    return at


def _boom(p, variant):
    """All that turns, in the frame of the ring: x out along the jib, z up from the ring. variant: intact, damaged, or fallen (the
    ruins': as damaged, the glass whole)."""
    damaged = variant == "damaged"
    lit = variant == "intact"
    p.cylinder((0.0, 0.0, 0.03), 'Z', TOWER_HALF + 0.04, 0.03, DARK, sides=16)  # the slewing ring
    p.box((-0.22, -0.2, 0.06), (0.22, 0.2, 0.14), YELLOW)  # the turntable
    # The cab at the front right of the turntable, out over the side.
    p.box((0.02, -0.38, 0.06), (0.3, -0.18, 0.3), STEEL)
    p.box((0.3, -0.37, 0.12), (0.306, -0.19, 0.28), DARK if damaged else GLASS)
    p.box((0.04, -0.386, 0.12), (0.28, -0.38, 0.28), DARK if damaged else GLASS)
    p.box((0.0, -0.4, 0.3), (0.32, -0.16, 0.33), STEEL)
    # The apex: an A-frame of four legs up to its top.
    top = Vector((0.0, 0.0, APEX))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.bar((sx * 0.14, sy * 0.12, 0.14), top + Vector((sx * 0.02, sy * 0.02, 0.0)), 0.014, YELLOW)
    p.bar((-0.14, 0.0, 0.3), (0.14, 0.0, 0.3), 0.01, YELLOW)
    p.sphere(top + Vector((0.0, 0.0, 0.04)), 'Z', 0.022, RED if lit else DARK, sides=8, rings=4)
    # The jib out to JIB_LEN, its tip bent down when damaged; the counter-jib back with its weights.
    sag = (lambda x: -0.22 * max(0.0, (x - 1.15) / (JIB_LEN - 1.15)) ** 2) if damaged else None
    at = _jib_lattice(p, 0.18, JIB_LEN, 9, JIB, sag)
    _jib_lattice(p, -COUNTER, -0.18, 3, YELLOW)
    for k in range(3):
        x = -COUNTER + 0.04 + 0.13 * k
        p.box((x, -0.11, -0.16), (x + 0.12, 0.11, 0.0), CONCRETE)
    p.box((-0.24, -0.08, 0.14), (-0.12, 0.08, 0.24), DARK)  # the hoist winch
    # The ties from the apex to the jib and the counter-jib.
    for x in (0.9, JIB_LEN - 0.05):
        p.bar(top, at(x, 0.0, 0.15), 0.005, STEEL)
    p.bar(top, Vector((-COUNTER + 0.05, 0.0, 0.15)), 0.005, STEEL)
    p.sphere(at(JIB_LEN, 0.0, 0.17), 'Z', 0.018, RED if lit else DARK, sides=8, rings=4)
    # The trolley and the hook on its cable; snapped when damaged (the hook lies on the footing, the tower's).
    trolley = at(TROLLEY, 0.0, -0.03)
    p.box(trolley - Vector((0.06, 0.08, 0.03)), trolley + Vector((0.06, 0.08, 0.02)), DARK)
    if not lit:
        p.bar(trolley, trolley - Vector((0.03, 0.0, 0.35)), 0.003, STEEL)
        return
    hook = trolley - Vector((0.0, 0.0, 1.2))
    for dy in (-0.03, 0.03):
        p.bar(trolley + Vector((0.0, dy, 0.0)), hook + Vector((0.0, dy * 0.6, 0.08)), 0.006, DARK)
    p.box(hook + Vector((-0.05, -0.04, 0.03)), hook + Vector((0.05, 0.04, 0.09)), YELLOW)  # the hook block
    p.bar(hook + Vector((0.0, 0.0, 0.03)), hook + Vector((0.0, 0.0, -0.02)), 0.008, DARK)
    p.bar(hook + Vector((0.0, 0.0, -0.02)), hook + Vector((0.03, 0.0, 0.0)), 0.007, DARK)


def _tower(p, top):
    lattice(p, (0.0, 0.0), FOOT + 0.03, top, TOWER_HALF, TOWER_HALF, max(1, round((top - FOOT) / 0.26)), YELLOW, bar=0.014)
    # A ladder up the middle of the front face.
    p.bar((-0.03, -TOWER_HALF - 0.02, FOOT + 0.05), (-0.03, -TOWER_HALF - 0.02, top), 0.004, STEEL)
    p.bar((0.03, -TOWER_HALF - 0.02, FOOT + 0.05), (0.03, -TOWER_HALF - 0.02, top), 0.004, STEEL)


def _ruins(p, rnd):
    _tower(p, BREAK)
    # The members bent out of the break.
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        a = Vector((sx * TOWER_HALF, sy * TOWER_HALF, BREAK))
        p.bar(a, a + Vector((sx * rnd.uniform(0.05, 0.15), sy * rnd.uniform(0.05, 0.15), rnd.uniform(0.05, 0.18))), 0.011, YELLOW)
    # The rest of the tower and the boom, lying across the quay: built standing, then laid down along +x.
    def fallen():
        lattice(p, (0.0, 0.0), BREAK, RING, TOWER_HALF, TOWER_HALF, 5, YELLOW, bar=0.014)
        before = set(p.bm.verts)
        _boom(p, "fallen")
        for v in p.bm.verts:
            if v not in before:
                v.co = Matrix.Translation((0.0, 0.0, RING)) @ v.co
    lie = (Matrix.Translation((BREAK * 0.4, 0.15, TOWER_HALF + 0.02)) @ Matrix.Rotation(-0.35, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'Y')
           @ Matrix.Rotation(math.pi / 2, 4, 'Z') @ Matrix.Translation((0.0, 0.0, -BREAK)))
    transformed(p, lie, fallen)
    for k in range(3):  # the counterweights thrown about
        cc.block(p, (rnd.uniform(-0.6, -0.3), rnd.uniform(-0.4, 0.4), 0.06), (0.12, 0.22, 0.16), (rnd.uniform(-0.4, 0.4), 0.3, rnd.uniform(0, math.pi)), CONCRETE)


def _material(name, variant):
    """Safety yellow steel, the jib's white bands, grey concrete, dark iron, dark blue glass, red lights, grey steel of the cab and the
    cables; rust and soot when damaged and fallen."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, z = g.axis(coord, 'X'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    yellow = g.mix(grain, lin((0.86, 0.66, 0.08)), lin((0.94, 0.75, 0.14)))
    band = g.math('GREATER_THAN', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), 0.75)
    jib = g.mix(band, yellow, lin((0.92, 0.92, 0.90)))
    concrete = g.mix(grain, lin((0.54, 0.53, 0.50)), lin((0.62, 0.61, 0.58)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 4.0, -0.5)), lin((0.10, 0.16, 0.22)), lin((0.32, 0.44, 0.52)))
    red = lin((0.85, 0.10, 0.08))
    steel = g.mix(grain, lin((0.44, 0.45, 0.47)), lin((0.54, 0.55, 0.57)))
    albedo = cc.paint(g, mask, (yellow, jib, concrete, dark, glass, red, steel))
    if variant != "intact":
        rust = g.mix(g.noise(25.0, 3.0), lin((0.40, 0.22, 0.10)), lin((0.52, 0.30, 0.15)))
        rusty = g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]), g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 3.0), 5.0, -2.8)))
        albedo = g.mix(rusty, albedo, rust)
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.3 if variant == "damaged" else 0.5), 0.8), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.5, 0.5, 0.9, 0.55, 0.1, 0.4, 0.45))
    metallic = cc.weighted(g, mask, (0.2, 0.2, 0.0, 0.6, 0.0, 0.1, 0.6))
    g.finish(albedo, g.bump(g.noise(60.0, 3.0), 0.04, 0.004), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("base", "tower", "boom", "body"))
    rnd = ic.seed(variant_id)
    base = Parts()
    _base(base)
    if variant == "ruins":
        body = Parts()
        _ruins(body, rnd)
        return cc.finish(variant_id, [(base, "base"), (body, "body")], 0.0, _material(variant_id + "_mat", variant)), {}
    tower, boom = Parts(), Parts()
    _tower(tower, RING)
    _boom(boom, variant)
    if variant == "damaged":  # the hook fallen on the footing
        tower.box((0.18, -0.3, FOOT - 0.04), (0.28, -0.22, FOOT + 0.02), YELLOW)
        tower.bar((0.23, -0.26, FOOT + 0.02), (0.45, -0.1, 0.01), 0.003, STEEL)
    finished = cc.finish(variant_id, [(base, "base"), (tower, "tower"), (boom, "boom")], 0.0, _material(variant_id + "_mat", variant))
    return finished, {"boom": (0.0, 0.0, RING)}


def run(variant_id):
    """Builds, bakes, saves the .blend and exports the GLB; the boom stands at the ring, so the game turns it there."""
    (meshes, mat, graph), origins = build(variant_id)
    for obj in meshes:
        node = obj.name.split(".")[0][len(variant_id) + 1:]
        if node in origins:
            obj.location = origins[node]
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
