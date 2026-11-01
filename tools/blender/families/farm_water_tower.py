"""Water tower, element 07 of the temperate set (docs/temperate_elements/07_water_tower): a steel tank on four tall braced legs with a
ladder, a landmark at the edge of the farm.

The map building neutral_water_tower: 1x1, no soldiers go in, it does not burn (steel); only its base blocks. Footprint 1x1 cell, origin
at the centre of the cell on the ground, the front to -Y. Made at the size the game draws it, not squashed (as the lighthouse, families/
beach_lighthouse.py): squashed upright the legs would be stubs under a disc — the legs to 1.42, the tank to 2.0, its roof to about 2.3,
as its placeholder (game/scripts/Terrain/BuildingLooks.cs). The parts:
    base     a concrete slab under the legs, inside the cell, a footing under each leg
    legs     four round steel legs leaning in, horizontal struts at two heights and a ring girder at the top, rods crossed in every
             panel; the riser pipe down the middle; the darkened ladder up the front right, outside its leg, to the catwalk
    tank     the round tank of grey-blue steel courses with a shallow bowl under it, a low conical roof with a vent; the catwalk round
             its foot with its railing, a ladder up the tank's wall to the roof
Nodes: "legs", "tank", "base"; the abandoned one also fire_1..2. One GLB per state:
    water_tower            whole, a little rust at its seams
    water_tower_rusty      whole, rusting: rust streaks running down from the seams, rust patches over the tank and the roof
    water_tower_damaged    holed: a shell hole in the front right of the tank, water running out of it down the front right leg
                           into a puddle on the slab; rust streaks, a length of railing bent down
    water_tower_abandoned  critically damaged: the front left leg buckled, the tower leaning over it; the tank holed and running,
                           the railing torn; fire_1..2 (the buckled leg, its footing)
    water_tower_ruins      destroyed: the tank fallen on its side across the slab, crumpled and torn open, its roof off at the back,
                           stumps of the legs on the footings, two legs lying bent, rods and struts about, a puddle
Budget 4000 triangles, texture 1024 (docs/temperate_elements/07_water_tower/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.farm_water_tower as water_tower
    print(water_tower.run_all())
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
    "water_tower": "intact",
    "water_tower_rusty": "rusty",
    "water_tower_damaged": "damaged",
    "water_tower_abandoned": "abandoned",
    "water_tower_ruins": "ruins",
}

STEEL, LEG, RAIL, CONCRETE, LADDER, WATER, DARK, ROOF = (cc.part(i) for i in range(8))
PARTS = 8

PAD = 0.05
FOOT = 0.13  # the top of the footings
FOOT_XY, TOP_XY = 0.34, 0.26  # the legs' corners at their feet and at the top
LEG_TOP = 1.42
TANK_LOW, TANK_TOP, R = 1.47, 2.0, 0.42
LEVELS = (FOOT, 0.6, 1.02, LEG_TOP)
SIDES = 24
CORNERS = ((1, -1), (1, 1), (-1, 1), (-1, -1))  # front right, back right, back left, front left
HOLE = math.radians(-30)  # the shell hole: front right
LEAN = Vector((-1.0, -1.0, 0.0)).normalized() * math.tan(0.1)  # the abandoned one leans over its front left leg


def _corner(k, z):
    sx, sy = CORNERS[k]
    t = (z - FOOT) / (LEG_TOP - FOOT)
    c = FOOT_XY + (TOP_XY - FOOT_XY) * t
    return Vector((sx * c, sy * c, z))


def _dir(a):
    return Vector((math.cos(a), math.sin(a), 0.0))


def _tube(p, a, b, r, painted, sides=8):
    """A round tube from a to b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = d.cross(Vector((0.0, 0.0, 1.0)))
    if u.length < 1e-6:
        u = Vector((1.0, 0.0, 0.0))
    u = u.normalized()
    v = d.cross(u).normalized()
    circle = [u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides) for i in range(sides)]
    p.loft([[q + c * r for c in circle] for q in (a, b)], painted)


def _moved(p, build, m):
    """Builds into p, the new vertices moved by m: a matrix, or a function of a point."""
    before = set(p.bm.verts)
    build()
    for v in p.bm.verts:
        if v not in before:
            v.co = m(v.co) if callable(m) else m @ v.co


def _lean(co):
    """The abandoned one's lean: a shear growing with height over the footings, so the feet stay on them."""
    return co + LEAN * max(0.0, co.z - FOOT)


# --- parts ---------------------------------------------------------------------------

def _base(p):
    p.box((-0.46, -0.46, 0.0), (0.46, 0.46, PAD), CONCRETE)
    for k in range(4):
        c = _corner(k, FOOT)
        p.box((c.x - 0.07, c.y - 0.07, PAD), (c.x + 0.07, c.y + 0.07, FOOT), CONCRETE)


def _legs(p, variant):
    buckled = 3 if variant == "abandoned" else None
    for k in range(4):
        a, b = _corner(k, FOOT), _corner(k, LEG_TOP)
        if k == buckled:
            # Buckled two fifths up: bent out, its lower half kinked.
            kink = a + (b - a) * 0.4 + Vector((-0.07, -0.07, -0.05))
            _tube(p, a, kink, 0.028, LEG)
            _tube(p, kink, b, 0.028, LEG)
            continue
        _tube(p, a, b, 0.028, LEG)
        p.box((a.x - 0.04, a.y - 0.04, FOOT), (a.x + 0.04, a.y + 0.04, FOOT + 0.015), LEG)  # its base plate
    for i, z in enumerate(LEVELS[1:]):
        for k in range(4):
            if k == buckled and i == 0:
                continue  # the strut torn off the buckled leg
            p.bar(_corner(k, z), _corner((k + 1) % 4, z), 0.014 if z < LEG_TOP else 0.022, LEG)
    for z0, z1 in zip(LEVELS, LEVELS[1:]):
        for k in range(4):
            n = (k + 1) % 4
            if buckled in (k, n) and z0 == FOOT:
                continue
            p.bar(_corner(k, z0 + 0.03), _corner(n, z1 - 0.03), 0.006, RAIL)
            p.bar(_corner(n, z0 + 0.03), _corner(k, z1 - 0.03), 0.006, RAIL)
    p.turned((0.0, 0.0, 0.0), 'Z', [(PAD, 0.055), (TANK_LOW - 0.12, 0.055)], LEG, sides=10)  # the riser
    p.turned((0.0, 0.0, 0.0), 'Z', [(PAD, 0.085), (PAD + 0.06, 0.085)], LEG, sides=10)


def _leg_ladder(p):
    """Up the front right, outside its leg, from the slab to the catwalk."""
    out, side = _dir(math.radians(-45)), _dir(math.radians(45))
    at = out * 0.55
    for s in (-0.045, 0.045):
        q = at + side * s
        p.bar((q.x, q.y, PAD), (q.x, q.y, LEG_TOP + 0.18), 0.007, LADDER)
    for z in cc.steps(PAD + 0.1, LEG_TOP, 0.1):
        a, b = at - side * 0.045, at + side * 0.045
        p.bar((a.x, a.y, z), (b.x, b.y, z), 0.005, LADDER)
    for z in (0.6, 1.02):  # brackets to the leg
        c = _corner(0, z)
        p.bar(c, at + Vector((0.0, 0.0, z)), 0.006, LADDER)


def _tank(p, variant):
    p.turned((0.0, 0.0, 0.0), 'Z', [(TANK_LOW - 0.16, 0.05), (TANK_LOW - 0.12, 0.16), (TANK_LOW - 0.05, 0.33), (TANK_LOW, R),
                                    (TANK_TOP, R)], STEEL, sides=SIDES)
    for z in (TANK_LOW + 0.01, TANK_TOP - 0.01, (TANK_LOW + TANK_TOP) / 2):  # bands at the seams
        p.turned((0.0, 0.0, z), 'Z', [(-0.012, R + 0.01), (0.012, R + 0.01)], LEG, sides=SIDES)
    p.turned((0.0, 0.0, 0.0), 'Z', [(TANK_TOP - 0.01, R + 0.03), (TANK_TOP + 0.02, R + 0.03), (TANK_TOP + 0.2, 0.12),
                                    (TANK_TOP + 0.24, 0.07)], ROOF, sides=SIDES)
    p.turned((0.0, 0.0, TANK_TOP + 0.24), 'Z', [(0.0, 0.04), (0.06, 0.04), (0.06, 0.07), (0.08, 0.07), (0.11, 0.0)], LEG, sides=10)
    # The roof hatch where the ladder comes up.
    a = math.radians(-45)
    c = _dir(a) * 0.3 + Vector((0.0, 0.0, TANK_TOP + 0.1))
    cc.block(p, tuple(c), (0.12, 0.1, 0.04), (0.0, -0.42, a), LEG)
    # The ladder up the wall, from the catwalk to the eave.
    out, side = _dir(a), _dir(a + math.pi / 2)
    for s in (-0.04, 0.04):
        q = out * (R + 0.035) + side * s
        p.bar((q.x, q.y, LEG_TOP + 0.03), (q.x, q.y, TANK_TOP + 0.08), 0.006, LADDER)
    for z in cc.steps(LEG_TOP + 0.1, TANK_TOP + 0.02, 0.09):
        a0, b0 = out * (R + 0.035) - side * 0.04, out * (R + 0.035) + side * 0.04
        p.bar((a0.x, a0.y, z), (b0.x, b0.y, z), 0.005, LADDER)


def _catwalk(p, variant):
    z = LEG_TOP + 0.01
    p.turned((0.0, 0.0, 0.0), 'Z', [(z, 0.3), (z, 0.49), (z + 0.025, 0.49), (z + 0.025, 0.3)], RAIL, sides=SIDES)
    torn = {"damaged": (-1.0, -0.2), "abandoned": (-2.4, -1.3)}.get(variant)  # where the railing is bent or torn: an angle span
    posts = 16
    for k in range(posts):
        a = 2 * math.pi * k / posts - math.pi
        if torn and torn[0] < a < torn[1] and variant == "abandoned":
            continue
        q = _dir(a) * 0.475
        top = Vector((q.x, q.y, z + 0.17))
        if torn and torn[0] < a < torn[1]:
            top = Vector((q.x, q.y, z + 0.025)) + _dir(a) * 0.15 + Vector((0.0, 0.0, -0.05))  # bent down and out
        p.bar((q.x, q.y, z + 0.025), top, 0.006, RAIL)
    for h in (0.09, 0.17):
        pts = [2 * math.pi * k / 32 - math.pi for k in range(33)]
        for a0, a1 in zip(pts, pts[1:]):
            if torn and (torn[0] < a0 < torn[1] or torn[0] < a1 < torn[1]):
                continue
            p.bar(_dir(a0) * 0.475 + Vector((0.0, 0.0, z + h)), _dir(a1) * 0.475 + Vector((0.0, 0.0, z + h)), 0.006, RAIL)


def _hole(p, variant):
    """A shell hole in the front right of the tank, its edges bent in; water running out of it."""
    out, side = _dir(HOLE), _dir(HOLE + math.pi / 2)
    c = out * (R + 0.004) + Vector((0.0, 0.0, 1.72))
    ring = [c + side * (math.cos(t) * 0.075) + Vector((0.0, 0.0, math.sin(t) * 0.065)) for t in [2 * math.pi * k / 9 for k in range(9)]]
    p.loft([ring, [q + out * 0.008 for q in ring]], DARK)
    for k in range(5):
        t = 2 * math.pi * k / 5 + 0.3
        q = c + side * (math.cos(t) * 0.085) + Vector((0.0, 0.0, math.sin(t) * 0.075))
        cc.block(p, tuple(q), (0.05, 0.012, 0.035), (0.3 * math.sin(t), 0.4, HOLE + math.pi / 2 + 0.3 * math.cos(t)), STEEL)


def _stream(legs, tank):
    """Water out of the hole down the front right leg to a puddle on the slab: the stream in the tank's node to the catwalk, the rest in
    the legs'."""
    out = _dir(HOLE)
    start = out * (R + 0.02) + Vector((0.0, 0.0, 1.68))
    lip = out * (R + 0.09) + Vector((0.0, 0.0, 1.6))
    tank.bar(start, lip, 0.014, WATER)
    tank.bar(lip, out * (R + 0.1) + Vector((0.0, 0.0, LEG_TOP + 0.04)), 0.012, WATER)
    leg_top = _corner(0, LEG_TOP - 0.05) + Vector((0.0, 0.0, 0.0))
    legs.bar(out * (R + 0.1) + Vector((0.0, 0.0, LEG_TOP)), leg_top + Vector((0.01, -0.01, 0.0)), 0.01, WATER)
    legs.bar(leg_top + Vector((0.03, -0.03, 0.0)), _corner(0, FOOT + 0.05) + Vector((0.03, -0.03, 0.0)), 0.009, WATER)


def _puddle(p, at, r):
    p.turned(Vector(at), 'Z', [(0.0, r), (0.004, r)], WATER, sides=14)


def _ruins(legs, tank, rnd):
    # Stumps of the legs on their footings, their tops bent.
    for k in range(4):
        a = _corner(k, FOOT)
        h = (0.38, 0.22, 0.5, 0.15)[k]
        b = _corner(k, FOOT + h)
        _tube(legs, a, b, 0.028, LEG)
        sx, sy = CORNERS[k]
        _tube(legs, b, b + Vector((sx * 0.05, sy * 0.02, 0.06)), 0.026, LEG)
    # Two legs lying bent across the slab and off it, a strut, rods.
    for (a, b, c) in (((-0.44, 0.3, 0.04), (0.0, 0.1, 0.05), (0.38, 0.32, 0.07)), ((0.42, -0.4, 0.04), (0.3, 0.05, 0.05), (0.44, 0.42, 0.04))):
        _tube(legs, a, b, 0.028, LEG)
        _tube(legs, b, c, 0.028, LEG)
    legs.bar((-0.3, -0.42, PAD + 0.02), (0.2, -0.2, PAD + 0.02), 0.014, LEG)
    for k in range(5):
        x, y = rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4)
        t = rnd.uniform(0, math.pi)
        legs.bar((x, y, PAD + 0.01), (x + 0.3 * math.cos(t), y + 0.3 * math.sin(t), PAD + 0.02), 0.006, RAIL)
    # The tank on its side across the slab, crumpled flatter, torn open to the front; its roof at the back.
    m = Matrix.Translation((-0.05, -0.08, PAD + 0.28)) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(math.pi / 2, 4, 'Y') @ \
        Matrix.Scale(0.68, 4, Vector((1.0, 0.0, 0.0))) @ Matrix.Translation((0.0, 0.0, -(TANK_LOW + TANK_TOP) / 2))
    _moved(tank, lambda: tank.turned((0.0, 0.0, 0.0), 'Z', [(TANK_LOW - 0.12, 0.16), (TANK_LOW - 0.05, 0.33), (TANK_LOW, R), (TANK_TOP, R)],
                                       STEEL, sides=16), m)
    _moved(tank, lambda: [tank.turned((0.0, 0.0, z), 'Z', [(-0.012, R + 0.01), (0.012, R + 0.01)], LEG, sides=16)
                          for z in (TANK_LOW + 0.01, (TANK_LOW + TANK_TOP) / 2)], m)
    c = Vector((0.02, -0.38, PAD + 0.3))
    tank.loft([[c + Vector((u, 0.0, w)) for u, w in ((-0.16, -0.12), (0.12, -0.15), (0.17, 0.1), (-0.1, 0.14))],
               [c + Vector((u, -0.03, w)) for u, w in ((-0.16, -0.12), (0.12, -0.15), (0.17, 0.1), (-0.1, 0.14))]], DARK)
    for k in range(5):
        t = 2 * math.pi * k / 5
        q = c + Vector((math.cos(t) * 0.16, -0.05, math.sin(t) * 0.13))
        cc.block(tank, tuple(q), (0.09, 0.012, 0.07), (0.5 * math.sin(t), 0.6, 0.4 * math.cos(t)), STEEL)
    roof = Matrix.Translation((0.24, 0.3, PAD + 0.04)) @ Matrix.Rotation(0.35, 4, Vector((1.0, 1.0, 0.0)).normalized()) @ \
        Matrix.Translation((0.0, 0.0, -TANK_TOP))
    _moved(tank, lambda: tank.turned((0.0, 0.0, 0.0), 'Z', [(TANK_TOP, R * 0.6), (TANK_TOP + 0.02, R * 0.6), (TANK_TOP + 0.12, 0.08)],
                                       ROOF, sides=14), roof)
    _puddle(tank, (-0.1, -0.2, PAD), 0.36)


# --- material -----------------------------------------------------------------------

def _streaks(g, coord):
    """Streaks running down: a noise stretched upright."""
    vm = g.add('ShaderNodeVectorMath', operation='MULTIPLY')
    g.link(coord, vm, 'Vector')
    vm.inputs[1].default_value = (18.0, 18.0, 1.0)
    tex = g.add('ShaderNodeTexNoise')
    g.put(tex, 'Scale', 1.0)
    g.put(tex, 'Detail', 3.0)
    g.link(rc.sock(vm, 'Vector', outputs=True), tex, 'Vector')
    return rc.sock(tex, 'Fac', outputs=True)


def _material(name, variant):
    """Grey-blue steel in courses streaked with rust, dark grey legs and rails, concrete, the darkened ladder, water, near-black, the
    roof a shade darker than the tank; rust per state, soot when it is critical, more in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    rusty = {"intact": 0.1, "rusty": 0.75, "damaged": 0.45, "abandoned": 0.6, "ruins": 0.7}[variant]
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', z, 7.5)), 0.5)), -60.0, 29.5))
    streak = g.clamp01(g.math('MULTIPLY_ADD', _streaks(g, coord), 5.0, -3.6 + 2.0 * rusty))
    patch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 6.0, -4.4 + 2.6 * rusty))
    rust = g.mix(g.noise(14.0, 3.0), lin((0.40, 0.20, 0.09)), lin((0.56, 0.31, 0.15)))
    steel = g.mix(g.noise(3.0, 2.0), lin((0.48, 0.56, 0.62)), lin((0.56, 0.64, 0.70)))
    steel = g.mix(g.math('MULTIPLY', courses, 0.45), steel, lin((0.32, 0.37, 0.42)))
    steel = g.mix(g.math('MAXIMUM', g.math('MULTIPLY', streak, 0.8), patch), steel, rust)
    roof = g.mix(g.noise(4.0, 2.0), lin((0.36, 0.42, 0.48)), lin((0.42, 0.48, 0.54)))
    roof = g.mix(g.math('MULTIPLY', patch, 0.9), roof, rust)
    leg = g.mix(grain, lin((0.20, 0.21, 0.22)), lin((0.27, 0.28, 0.29)))
    leg = g.mix(g.math('MULTIPLY', g.math('ADD', patch, g.clamp01(g.math('MULTIPLY_ADD', z, -3.0, 0.9))), 0.6), leg, rust)  # rust at the feet
    rail = g.mix(grain, lin((0.16, 0.17, 0.18)), lin((0.22, 0.23, 0.24)))
    concrete = g.mix(grain, lin((0.60, 0.59, 0.56)), lin((0.68, 0.67, 0.64)))
    concrete = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 3.0), 3.0, -1.6)), 0.5), concrete, lin((0.40, 0.36, 0.30)))
    ladder = g.mix(grain, lin((0.14, 0.13, 0.12)), lin((0.24, 0.18, 0.14)))
    water = g.mix(g.noise(8.0, 2.0), lin((0.30, 0.40, 0.48)), lin((0.52, 0.64, 0.72)))
    dark = lin((0.04, 0.04, 0.05))
    albedo = cc.paint(g, mask, (steel, leg, rail, concrete, ladder, water, dark, roof))
    if variant in ("abandoned", "ruins"):
        amount = {"abandoned": 0.35, "ruins": 0.55}[variant]
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.7), albedo, lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.5, 0.6, 0.6, 0.9, 0.7, 0.08, 0.9, 0.55))
    metallic = cc.weighted(g, mask, (0.55, 0.5, 0.5, 0.0, 0.4, 0.0, 0.0, 0.55))
    height = g.math('ADD', g.math('MULTIPLY', g.math('MULTIPLY', courses, mask[0]), -1.0), g.math('MULTIPLY', g.noise(60.0, 3.0), 0.3))
    g.finish(albedo, g.bump(height, 0.04, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build and run -------------------------------------------------------------------

NODES = ("legs", "tank", "base")


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, NODES)
    rnd = ic.seed(variant_id)
    legs, tank, base = Parts(), Parts(), Parts()
    _base(base)
    if variant == "ruins":
        _ruins(legs, tank, rnd)
    else:
        lean = _lean if variant == "abandoned" else (lambda co: co)

        def upper():
            _legs(legs, variant)
            _leg_ladder(legs)

        _moved(legs, upper, lean)

        def top():
            _tank(tank, variant)
            _catwalk(tank, variant)
            if variant in ("damaged", "abandoned"):
                _hole(tank, variant)

        _moved(tank, top, lean)
        if variant in ("damaged", "abandoned"):
            _moved(tank, lambda: _moved(legs, lambda: _stream(legs, tank), lean), lean)
            _puddle(legs, (0.24, -0.24, PAD), 0.2)
    parts = [(legs, "legs"), (tank, "tank"), (base, "base")]
    return cc.finish(variant_id, parts, PAD - 0.001, _material(variant_id + "_mat", variant))


def _fire_points():
    kink = _lean(_corner(3, FOOT) + (_corner(3, LEG_TOP) - _corner(3, FOOT)) * 0.4 + Vector((-0.07, -0.07, -0.05)))
    return (tuple(kink), tuple(_corner(3, FOOT) + Vector((0.05, 0.05, 0.0))))


def _layout():
    """The places of the farm's models in the file: rts_common's LAYOUT has no farm row yet, so this script adds its own for the run."""
    row = "15. Ферма"
    if row not in rc.ROWS:
        rc.ROWS = rc.ROWS + (row,)
    for k, key in enumerate(VARIANTS):
        rc.LAYOUT.setdefault(key, (rc.ROWS.index(row), 18 + k))


def run(variant_id):
    _layout()
    meshes, mat, graph = build(variant_id)
    fire = _fire_points() if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
