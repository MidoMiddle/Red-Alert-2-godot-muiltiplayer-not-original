"""Silo, element 10 of the temperate set (docs/temperate_elements/10_silo): a tall concrete cylinder for grain with a dome on top and a
rusty ladder up its side, a landmark of the farm.

The map building neutral_silo: 1x1, 1500 hp, no soldiers go in, it does not burn (concrete). Footprint 1x1 cell, origin at the centre of
the cell on the ground, the front to -Y. Made at the size the game draws it, not squashed (as the lighthouse, families/
beach_lighthouse.py): a cylinder squashed upright would be a drum, and the silo should stand over the farm — the wall to 2.0, the dome
to about 2.4, as its placeholder (game/scripts/Terrain/BuildingLooks.cs). The parts:
    base     a round concrete plinth under the wall, a shade lighter than it
    body     the wall of concrete staves, grey, its dark steel hoops round it, the discharge hatch at its foot to the front with a chute
             over a trough, the galvanised filling pipe up the back left and over into the dome
    dome     the dark grey steel dome on its eave ring, the roof hatch and the vent on its top
    ladder   the rusty ladder up the front right in its safety cage, on over the dome to the hatch
Nodes: "body", "dome", "ladder", "base"; the abandoned one also fire_1..2 (the grain smouldering in the breach and on the heap). One GLB
per state:
    temperate_silo            whole
    temperate_silo_damaged    cracked: a crack down the front of the wall, grain running out of it in a stream to a heap at its foot,
                              a hoop snapped, a little soot
    temperate_silo_abandoned  critically damaged: the silo leaning back to the right (as if onto its neighbour), the crack torn open into
                              a breach, the dome knocked askew, a big heap of grain spilled over the plinth, soot; fire_1..2
    temperate_silo_ruins      destroyed: a jagged stump of staves on the plinth full of grain, the dome lying on the ground beside it,
                              lumps of concrete, the ladder fallen, grain all round
Budget 3000 triangles, texture 1024 (docs/temperate_elements/10_silo/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.farm_silo as silo
    print(silo.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "farm"
SIZE = 1024
VARIANTS = {
    "temperate_silo": "intact",
    "temperate_silo_damaged": "damaged",
    "temperate_silo_abandoned": "abandoned",
    "temperate_silo_ruins": "ruins",
}

CONCRETE, HOOP, DOME, RUST, PAD, GRAIN, DARK, PIPE = (cc.part(i) for i in range(8))
PARTS = 8

PLINTH = 0.1  # the top of the base
R = 0.36  # the wall's radius
WALL_TOP = 2.0
DOME_H = 0.34
SIDES = 20
LADDER = math.radians(-58)  # where the ladder stands round the silo: front right, to the camera
CRACK = math.radians(-15)  # where the crack runs: the right of the front, to the camera
PIPE_AT = math.radians(140)  # the filling pipe: back left
HOOPS = (0.32, 0.6, 0.88, 1.16, 1.44, 1.72, 1.94)
LEAN = Matrix.Translation((0.0, 0.0, PLINTH)) @ Matrix.Rotation(0.11, 4, Vector((-1.0, 1.0, 0.0)).normalized()) @ \
    Matrix.Translation((0.0, 0.0, -PLINTH))  # the abandoned one leans back to the right, about its foot


def _dir(a):
    return Vector((math.cos(a), math.sin(a), 0.0))


def _on_wall(a, z, out=0.0):
    return _dir(a) * (R + out) + Vector((0.0, 0.0, z))


def _dome_point(t, out=0.0):
    """The point of the dome's profile at t (0 the eave, 1 the top): (z, radius)."""
    th = t * math.pi / 2
    return WALL_TOP + 0.03 + DOME_H * math.sin(th), (R + 0.012) * math.cos(th) + out


# --- parts ---------------------------------------------------------------------------

def _base(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.47), (PLINTH - 0.02, 0.47), (PLINTH, 0.45)], PAD, sides=SIDES)


def _wall(p, top=WALL_TOP, snapped=None):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH, R + 0.01), (PLINTH + 0.06, R + 0.01), (PLINTH + 0.06, R), (top, R)], CONCRETE, sides=SIDES)
    for z in HOOPS:
        if z > top - 0.02:
            break
        if snapped is not None and abs(z - snapped) < 1e-6:
            # The hoop snapped over the crack: most of the ring, its two ends standing off the wall.
            ring = [(a, z) for a in [CRACK + 0.35 + (2 * math.pi - 0.7) * k / 16 for k in range(17)]]
            for (a0, _), (a1, _) in zip(ring, ring[1:]):
                p.bar(_on_wall(a0, z, 0.008), _on_wall(a1, z, 0.008), 0.009, HOOP)
            p.bar(_on_wall(CRACK + 0.35, z, 0.008), _on_wall(CRACK + 0.2, z - 0.05, 0.07), 0.008, HOOP)
            p.bar(_on_wall(CRACK - 0.35, z, 0.008), _on_wall(CRACK - 0.22, z + 0.04, 0.06), 0.008, HOOP)
            continue
        p.turned((0.0, 0.0, z), 'Z', [(-0.012, R + 0.012), (0.012, R + 0.012)], HOOP, sides=SIDES)


def _hatch(p):
    """The discharge hatch at the foot to the front, a chute out of it over a trough."""
    a = math.radians(-90)
    out, side = _dir(a), _dir(a + math.pi / 2)
    c = out * (R - 0.01)
    p.loft([[c + side * u + Vector((0.0, 0.0, z)) + out * d for u, z in ((-0.1, PLINTH + 0.1), (0.1, PLINTH + 0.1), (0.1, PLINTH + 0.36),
                                                                          (-0.1, PLINTH + 0.36))] for d in (0.0, 0.03)], HOOP)
    p.loft([[c + side * u + Vector((0.0, 0.0, z)) + out * d for u, z in ((-0.075, PLINTH + 0.13), (0.075, PLINTH + 0.13),
                                                                          (0.075, PLINTH + 0.33), (-0.075, PLINTH + 0.33))] for d in (0.03, 0.036)], DARK)
    # The chute: a sloping tray from under the hatch.
    top = out * (R + 0.02) + Vector((0.0, 0.0, PLINTH + 0.14))
    end = out * (R + 0.16) + Vector((0.0, 0.0, PLINTH + 0.05))
    p.loft([[q + side * u + Vector((0.0, 0.0, h)) for u, h in ((-0.05, 0.0), (0.05, 0.0), (0.05, 0.03), (-0.05, 0.03))] for q in (top, end)], PIPE)


def _pipe(p):
    """The filling pipe up the back left, its bend over the eave and into the dome."""
    out = _dir(PIPE_AT)
    foot = out * (R + 0.07) + Vector((0.0, 0.0, PLINTH + 0.18))
    top = out * (R + 0.07) + Vector((0.0, 0.0, WALL_TOP + 0.08))
    p.turned(foot, 'Z', [(0.0, 0.03), (top.z - foot.z, 0.03)], PIPE, sides=8)
    z1, r1 = _dome_point(0.45)
    bend = out * (r1 + 0.04) + Vector((0.0, 0.0, z1 + 0.05))
    p.bar(top, bend, 0.026, PIPE)
    p.bar(foot, foot + out * 0.12 + Vector((0.0, 0.0, -0.08)), 0.026, PIPE)  # its inlet, where the blower is coupled
    for z in (0.6, 1.3, 1.9):  # brackets to the wall
        p.bar(_on_wall(PIPE_AT, z, 0.0), _on_wall(PIPE_AT, z, 0.07), 0.01, HOOP)


def _dome(p, variant):
    stations = [(WALL_TOP - 0.02, R + 0.03), (WALL_TOP + 0.03, R + 0.03)]  # the eave ring
    stations += [_dome_point(t) for t in (0.0, 0.25, 0.5, 0.72, 0.9)]
    p.turned((0.0, 0.0, 0.0), 'Z', stations[:2], HOOP, sides=SIDES)
    z, r = _dome_point(0.9)
    p.turned((0.0, 0.0, 0.0), 'Z', stations[2:] + [(z + 0.005, 0.07)], DOME, sides=SIDES)
    # The vent on the top: a short stack under a little cap.
    p.turned((0.0, 0.0, z), 'Z', [(0.0, 0.065), (0.08, 0.065), (0.08, 0.1), (0.1, 0.1), (0.14, 0.0)], HOOP, sides=10)
    # The roof hatch where the ladder ends.
    zh, rh = _dome_point(0.62)
    out = _dir(LADDER)
    c = out * (rh - 0.04) + Vector((0.0, 0.0, zh))
    cc.block(p, tuple(c), (0.12, 0.14, 0.05), (0.0, -0.75, LADDER), DARK if variant != "intact" else HOOP)


def _ladder(p, top_t=0.62):
    out, side = _dir(LADDER), _dir(LADDER + math.pi / 2)
    rail = R + 0.05
    for s in (-0.05, 0.05):
        a = out * rail + side * s
        p.bar((a.x, a.y, PLINTH), (a.x, a.y, WALL_TOP + 0.12), 0.008, RUST)
    for z in cc.steps(PLINTH + 0.1, WALL_TOP, 0.1):
        a, b = out * rail - side * 0.05, out * rail + side * 0.05
        p.bar((a.x, a.y, z), (b.x, b.y, z), 0.006, RUST)
    for k in range(3):  # brackets to the wall
        z = 0.4 + 0.7 * k
        p.bar(out * R + Vector((0.0, 0.0, z)), out * rail + Vector((0.0, 0.0, z)), 0.007, RUST)
    for z in cc.steps(0.75, WALL_TOP - 0.05, 0.24):  # the cage
        pts = [out * rail + side * (-0.07), out * (rail + 0.13) + side * (-0.07), out * (rail + 0.13) + side * 0.07, out * rail + side * 0.07]
        for a, b in zip(pts, pts[1:]):
            p.bar((a.x, a.y, z), (b.x, b.y, z), 0.005, RUST)
    for s in (-0.07, 0.07):  # the cage's uprights
        a = out * (rail + 0.13) + side * s
        p.bar((a.x, a.y, 0.75), (a.x, a.y, WALL_TOP - 0.05), 0.005, RUST)
    # Over the dome to the hatch: rails following its curve, a few rungs.
    ts = [0.0, 0.2, 0.4, top_t]
    for s in (-0.04, 0.04):
        pts = [out * (_dome_point(t)[1] + 0.025) + side * s + Vector((0.0, 0.0, _dome_point(t)[0])) for t in ts]
        pts[0] = out * rail + side * (s * 1.25) + Vector((0.0, 0.0, WALL_TOP + 0.12))
        for a, b in zip(pts, pts[1:]):
            p.bar(a, b, 0.007, RUST)
    for t in (0.12, 0.3, 0.5):
        z, r = _dome_point(t, 0.025)
        p.bar(out * r - side * 0.04 + Vector((0.0, 0.0, z)), out * r + side * 0.04 + Vector((0.0, 0.0, z)), 0.005, RUST)


def _crack(p, wide):
    """A jagged crack down the front of the wall; torn open into a breach when wide."""
    pts = [(0.0, 1.42), (0.05, 1.3), (-0.03, 1.18), (0.04, 1.02), (-0.04, 0.88), (0.02, 0.72), (-0.02, 0.6)]
    if wide:
        pts = [(0.0, 1.62)] + pts + [(0.03, 0.48)]
    line = [_on_wall(CRACK + u / R, z, 0.004) for u, z in pts]
    for a, b in zip(line, line[1:]):
        p.bar(a, b, 0.014 if not wide else 0.02, DARK)
    if wide:
        # The breach: a dark gap with broken staves standing out round it.
        out, side = _dir(CRACK), _dir(CRACK + math.pi / 2)
        c = _on_wall(CRACK, 0.95, -0.01)
        p.loft([[c + side * u + Vector((0.0, 0.0, z)) + out * d for u, z in ((-0.06, -0.36), (0.07, -0.3), (0.08, 0.32), (-0.05, 0.4))]
                for d in (0.0, 0.03)], DARK)
        for k, (u, z, turn) in enumerate(((-0.09, 0.75, 0.4), (0.1, 0.9, -0.5), (-0.08, 1.2, 0.3), (0.09, 1.25, -0.3))):
            q = _on_wall(CRACK + u / R, z, 0.03)
            cc.block(p, tuple(q), (0.07, 0.03, 0.22), (0.0, turn, CRACK + math.pi / 2), CONCRETE)
    return line[-1]


def _spill(p, start, heap_at, heap_r, heap_h):
    """Grain running out of start in a stream to a heap at heap_at."""
    start = Vector(start)
    mid = start + _dir(CRACK) * 0.08 + Vector((0.0, 0.0, -0.12))
    top = Vector((heap_at[0], heap_at[1], heap_at[2] + heap_h * 0.8))
    p.bar(start, mid, 0.022, GRAIN)
    p.bar(mid, top, 0.028, GRAIN)
    p.turned(Vector(heap_at), 'Z', [(0.0, heap_r), (heap_h * 0.35, heap_r * 0.75), (heap_h * 0.75, heap_r * 0.35), (heap_h, 0.02)],
             GRAIN, sides=12)


def _moved(p, build, m):
    """Builds into p, the new vertices moved by the matrix m."""
    before = set(p.bm.verts)
    build()
    for v in p.bm.verts:
        if v not in before:
            v.co = m @ v.co


def _ruins(body, rnd):
    # The stump: a low ring of wall, jagged staves standing on it, grain filling it.
    body.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH, R + 0.01), (PLINTH + 0.06, R + 0.01), (PLINTH + 0.06, R), (0.3, R)], CONCRETE, sides=SIDES)
    ring = [(math.cos(2 * math.pi * k / 14) * (R - 0.03), math.sin(2 * math.pi * k / 14) * (R - 0.03)) for k in range(15)]
    ic.stumps(body, rnd, list(zip(ring, ring[1:])), 0.29, 0.1, 0.7, CONCRETE, step=0.17, gap=0.3, thick=0.06)
    body.turned((0.0, 0.0, 0.0), 'Z', [(0.28, R - 0.01), (0.33, R - 0.05), (0.4, 0.15), (0.44, 0.0)], GRAIN, sides=12)
    body.turned((0.0, 0.0, 0.3), 'Z', [(-0.012, R + 0.012), (0.012, R + 0.012)], HOOP, sides=SIDES)
    # The dome fallen into the stump, tipped over on the grain.
    m = Matrix.Translation((0.06, 0.1, 0.26)) @ Matrix.Rotation(0.9, 4, Vector((1.0, 0.5, 0.0)).normalized()) @ \
        Matrix.Translation((0.0, 0.0, -WALL_TOP - 0.02))
    _moved(body, lambda: body.turned((0.0, 0.0, 0.0), 'Z', [_dome_point(t) for t in (0.0, 0.3, 0.6, 0.9)], DOME, sides=14), m)
    # Lumps of the wall round it, grain spilled all over the plinth and off it, the ladder lying at the front.
    for k in range(7):
        a = rnd.uniform(-2.6, 0.6)
        c = _dir(a) * rnd.uniform(0.42, 0.5) + Vector((0.0, 0.0, 0.05))
        cc.block(body, tuple(c), (0.16, 0.08, 0.1), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), a), CONCRETE)
    for (x, y, r, h) in ((-0.2, -0.38, 0.2, 0.1), (0.25, -0.32, 0.16, 0.08), (-0.38, 0.12, 0.13, 0.06)):
        body.turned((x, y, PLINTH - 0.01 if math.hypot(x, y) < 0.45 else 0.0), 'Z', [(0.0, r), (h * 0.5, r * 0.6), (h, 0.02)], GRAIN, sides=10)
    ic.rubble(body, rnd, -0.45, 0.45, -0.45, 0.45, PLINTH, 8, 0.06, CONCRETE)
    out, side = _dir(LADDER), _dir(LADDER + math.pi / 2)
    a = Vector((0.08, -0.47, 0.03))
    d = Vector((-0.75, 0.15, 0.0)).normalized()
    for s in (-0.05, 0.05):
        q = a + Vector((-d.y, d.x, 0.0)) * s
        body.bar(q, q + d * 0.6, 0.008, RUST)
    for t in cc.steps(0.05, 0.55, 0.1):
        q = a + d * t
        body.bar(q + Vector((d.y, -d.x, 0.0)) * 0.05, q + Vector((-d.y, d.x, 0.0)) * 0.05, 0.006, RUST)


# --- material -----------------------------------------------------------------------

def _staves(g, coord):
    """Concrete staves: courses to a unit of height, staggered joints round the wall. Returns the joints mask."""
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    rows = g.math('MULTIPLY', z, 11.0)
    shift = g.math('MULTIPLY', g.math('MODULO', g.math('FLOOR', rows), 2.0), 0.5)
    round_ = g.math('ADD', g.math('MULTIPLY', g.math('ARCTAN2', y, x), 24.0 / (2 * math.pi)), shift)

    def seam(t, width):
        f = g.math('FRACT', t)
        edge = g.math('MINIMUM', f, g.math('SUBTRACT', 1.0, f))
        return g.clamp01(g.math('MULTIPLY_ADD', edge, -1.0 / width, 1.0))

    return g.math('MAXIMUM', seam(rows, 0.08), seam(round_, 0.05))


def _streaks(g, coord):
    """Dark streaks running down the wall from the hoops: a noise stretched upright."""
    vm = g.add('ShaderNodeVectorMath', operation='MULTIPLY')
    g.link(coord, vm, 'Vector')
    vm.inputs[1].default_value = (14.0, 14.0, 0.8)
    tex = g.add('ShaderNodeTexNoise')
    g.put(tex, 'Scale', 1.0)
    g.put(tex, 'Detail', 3.0)
    g.link(rc.sock(vm, 'Vector', outputs=True), tex, 'Vector')
    return g.clamp01(g.math('MULTIPLY_ADD', rc.sock(tex, 'Fac', outputs=True), 4.0, -2.0))


def _material(name, variant):
    """Light grey concrete staves streaked from the hoops, dark steel hoops, a dark grey dome, rust, a lighter plinth, golden grain,
    near-black, galvanised pipe; cracks and soot per state."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    joints = _staves(g, coord)
    concrete = g.mix(g.noise(6.0, 4.0), lin((0.58, 0.57, 0.54)), lin((0.67, 0.66, 0.63)))
    concrete = g.mix(g.math('MULTIPLY', _streaks(g, coord), 0.35), concrete, lin((0.45, 0.43, 0.40)))
    low = g.clamp01(g.math('MULTIPLY_ADD', z, -2.5, 1.0))  # earth splashed on the foot of the wall
    concrete = g.mix(g.math('MULTIPLY', low, 0.5), concrete, lin((0.48, 0.42, 0.34)))
    concrete = g.mix(g.math('MULTIPLY', joints, 0.35), concrete, lin((0.40, 0.39, 0.37)))
    hoop = g.mix(grain, lin((0.16, 0.16, 0.17)), lin((0.24, 0.23, 0.22)))
    hoop = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(9.0, 3.0), 4.0, -2.2)), 0.7), hoop, lin((0.38, 0.20, 0.10)))
    dome = g.mix(g.noise(5.0, 3.0), lin((0.26, 0.28, 0.30)), lin((0.34, 0.36, 0.38)))
    rust = g.mix(g.noise(12.0, 3.0), lin((0.38, 0.20, 0.10)), lin((0.52, 0.30, 0.16)))
    pad = g.mix(grain, lin((0.66, 0.65, 0.62)), lin((0.73, 0.72, 0.69)))
    pad = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 3.0), 3.0, -1.6)), 0.5), pad, lin((0.50, 0.45, 0.38)))
    corn = g.mix(g.noise(40.0, 2.0), lin((0.78, 0.62, 0.30)), lin((0.88, 0.74, 0.40)))
    dark = lin((0.05, 0.05, 0.05))
    pipe = g.mix(grain, lin((0.56, 0.58, 0.60)), lin((0.64, 0.66, 0.68)))
    albedo = cc.paint(g, mask, (concrete, hoop, dome, rust, pad, corn, dark, pipe))
    if variant != "intact":
        patches = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 2.0), 6.0, {"damaged": -3.4, "abandoned": -2.8, "ruins": -2.8}[variant]))
        crack = g.math('MULTIPLY', g.math('MULTIPLY', g.math('ADD', mask[0], mask[4]), patches), cc.cracks(g, 4.0))
        albedo = g.mix(g.math('MULTIPLY', crack, 0.8), albedo, lin((0.24, 0.23, 0.22)))
        amount = {"damaged": 0.15, "abandoned": 0.55, "ruins": 0.7}[variant]
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.75), albedo, lin((0.12, 0.11, 0.10)))
    rough = cc.weighted(g, mask, (0.9, 0.55, 0.5, 0.75, 0.9, 0.95, 0.9, 0.4))
    metallic = cc.weighted(g, mask, (0.0, 0.6, 0.6, 0.3, 0.0, 0.0, 0.0, 0.7))
    height = g.math('ADD', g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[0]), -1.0), g.math('MULTIPLY', g.noise(60.0, 3.0), 0.4))
    g.finish(albedo, g.bump(height, 0.06, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build and run -------------------------------------------------------------------

NODES = ("body", "dome", "ladder", "base")


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, NODES)
    rnd = ic.seed(variant_id)
    body, dome, ladder, base = Parts(), Parts(), Parts(), Parts()
    _base(base)
    if variant == "ruins":
        _ruins(body, rnd)
        parts = [(body, "body"), (base, "base")]
        return cc.finish(variant_id, parts, PLINTH - 0.001, _material(variant_id + "_mat", variant))
    lean = LEAN if variant == "abandoned" else Matrix.Identity(4)
    end = [None]

    def tower():
        _wall(body, snapped=HOOPS[3] if variant in ("damaged", "abandoned") else None)
        _hatch(body)
        _pipe(body)
        if variant != "intact":
            end[0] = _crack(body, variant == "abandoned")

    _moved(body, tower, lean)
    if variant == "abandoned":
        # The dome knocked askew on its eave, tipped to the front left.
        hinge = Matrix.Translation(_on_wall(math.radians(45), WALL_TOP))
        tip = hinge @ Matrix.Rotation(0.14, 4, _dir(math.radians(135))) @ hinge.inverted()
        _moved(dome, lambda: _dome(dome, variant), lean @ tip)
    else:
        _dome(dome, variant)
    _moved(ladder, lambda: _ladder(ladder), lean)
    if variant == "damaged":
        _spill(body, end[0], tuple(_dir(CRACK) * 0.41 + Vector((0.0, 0.0, PLINTH - 0.01))), 0.1, 0.08)
    elif variant == "abandoned":
        _spill(body, end[0], tuple(_dir(CRACK) * 0.38 + Vector((0.0, 0.0, PLINTH - 0.01))), 0.15, 0.15)
    parts = [(body, "body"), (dome, "dome"), (ladder, "ladder"), (base, "base")]
    return cc.finish(variant_id, parts, PLINTH - 0.001, _material(variant_id + "_mat", variant))


def _fire_points():
    breach = LEAN @ _on_wall(CRACK, 0.95, 0.04)
    heap = _dir(CRACK) * 0.38 + Vector((0.0, 0.0, PLINTH + 0.15))
    return (tuple(breach), tuple(heap))


def _layout():
    """The places of the farm's models in the file: rts_common's LAYOUT has no farm row yet, so this script adds its own for the run."""
    row = "15. Ферма"
    if row not in rc.ROWS:
        rc.ROWS = rc.ROWS + (row,)
    for k, key in enumerate(VARIANTS):
        rc.LAYOUT.setdefault(key, (rc.ROWS.index(row), 10 + k))


def run(variant_id):
    _layout()
    meshes, mat, graph = build(variant_id)
    fire = _fire_points() if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
