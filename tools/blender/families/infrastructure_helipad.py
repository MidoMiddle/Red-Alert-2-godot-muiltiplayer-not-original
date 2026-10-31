"""Helipad, element 02 of the infrastructure set (docs/infrastructure_elements/02_helipad): a neutral tech building an engineer takes; the
owner's helicopters land on it and are repaired.

Footprint 3x3 cells, origin at the centre of the footprint on the ground, the front to -Y; made at the size the game draws it, as the tech
airfield (families/tech_airfield.py, not squashed). The parts:
    pad        a flat concrete pad over the footprint with bevelled edges, a little over the ground, in slabs; a big white H painted in its
               middle (in the texture, not geometry), soot of the rotors round it, cracks at the edges; the windsock's pole at the front
               right corner
    lights     small yellow lights in ribbed dark housings round the edge
    hut        the control hut in the back left corner: blue-grey walls on a concrete plinth, a band of windows round its top in white
               frames, a door with a step, a flat roof with a parapet (the owner's colour), an air conditioner, a mast with a red light;
               two drums and a red fire point by it
    windsock   an orange and white striped sock on a swivel at the top of the pole, pointing in over the pad
Nodes: "pad", "lights", "hut", "windsock", "team_mask" (the parapet of the hut's roof). The windsock's origin is the axis of its pole: the
game turns it about the vertical there with the wind. One GLB per state:
    helipad          whole
    helipad_damaged  damaged: a hole in the hut's side, its front windows out, the mast bent, a third of the lights out and two knocked
                     over, the pad's front edge broken, the sock limp, cracks and soot
    helipad_ruins    destroyed: the pad broken in slabs, some tilted, a crater in the middle; stumps of the hut's walls, its roof down on
                     the rubble, the pole bent down, a few lights left; nodes "pad", "body"
Budget 8000 triangles, texture 2048 (docs/infrastructure_elements/02_helipad/prompts.md). The helipad does not burn: no fire nodes.

In Blender, after tools/blender is on sys.path:
    import families.infrastructure_helipad as hp
    print(hp.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from families.infrastructure_gate import FOLDER, transformed
from units.parts import Parts, lin
import rts_common as rc

SIZE = 2048
VARIANTS = {
    "helipad": "intact",
    "helipad_damaged": "damaged",
    "helipad_ruins": "ruins",
}

(CONCRETE, HUT, GLASS, WHITE, DARK, YELLOW, ORANGE, RED, TEAM, DRUM) = (cc.part(i) for i in range(10))
PARTS = 10

PAD = 0.05  # the pad's top
EDGE = 1.47
HUT_X = (-1.42, -0.88)
HUT_Y = (0.88, 1.42)
PLINTH = PAD + 0.05
EAVE = 0.6  # the walls' top
ROOF = EAVE + 0.04
BAND = (0.36, 0.54)  # the windows
SOCK = (1.3, -1.3)  # the pole
POLE_TOP = 0.8


def _slab(p, x0, x1, y0, y1, z0, z1, bevel=0.03):
    """A slab with bevelled sides: its top bevel narrower than its foot."""
    p.loft([[Vector(v) for v in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0))],
            [Vector(v) for v in ((x0 + bevel, y0 + bevel, z1), (x1 - bevel, y0 + bevel, z1), (x1 - bevel, y1 - bevel, z1),
                                 (x0 + bevel, y1 - bevel, z1))]], CONCRETE)


# --- pad, lights ----------------------------------------------------------------------

def _pad(p, variant, rnd):
    if variant == "ruins":
        # The pad in nine slabs, broken along its joints; the middle one gone into a crater.
        for i in range(3):
            for j in range(3):
                x0, y0 = -EDGE + i * 0.98, -EDGE + j * 0.98
                if (i, j) == (1, 1):
                    continue
                tilt = Matrix.Identity(4)
                if rnd.random() < 0.5:
                    c = Vector((x0 + 0.49, y0 + 0.49, 0.0))
                    tilt = (Matrix.Translation(c) @ Matrix.Rotation(rnd.uniform(-0.08, 0.08), 4, 'X')
                            @ Matrix.Rotation(rnd.uniform(-0.08, 0.08), 4, 'Y') @ Matrix.Translation(-c))
                transformed(p, tilt, lambda x0=x0, y0=y0: _slab(p, x0 + 0.01, x0 + 0.96, y0 + 0.01, y0 + 0.96, 0.0, PAD, 0.015))
        for k in range(7):  # the broken pieces of the middle round the crater
            a = 2 * math.pi * k / 7 + rnd.uniform(-0.2, 0.2)
            c = Vector((math.cos(a) * 0.36, math.sin(a) * 0.36, 0.0))
            cc.block(p, c, (0.3, 0.22, 0.05), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), a), CONCRETE)
        p.box((-0.48, -0.48, 0.0), (0.48, 0.48, 0.005), DARK)  # the crater's floor
        return
    _slab(p, -EDGE, EDGE, -EDGE, EDGE, 0.0, PAD)
    if variant == "damaged":  # chunks broken out of the front edge, lying in front
        for x in (-0.6, -0.45, 0.3):
            cc.block(p, (x, -EDGE - 0.06, 0.02), (0.12, 0.09, 0.04), (0.3, 0.2, x * 3), CONCRETE)
    # The windsock's pole on a foot, a swivel at the top.
    sx, sy = SOCK
    p.box((sx - 0.04, sy - 0.04, PAD), (sx + 0.04, sy + 0.04, PAD + 0.03), CONCRETE)
    p.cylinder((sx, sy, (PAD + POLE_TOP) / 2), 'Z', 0.012, (POLE_TOP - PAD) / 2, WHITE, sides=6)
    p.cylinder((sx, sy, POLE_TOP + 0.02), 'Z', 0.018, 0.02, DARK, sides=6)


def _light_points():
    """The edge lights, round the pad a little in from its edge; none where the hut and the pole stand."""
    pts = []
    for t in cc.steps(-1.38, 1.38, 0.46):
        for x, y in ((t, -1.38), (t, 1.38), (-1.38, t), (1.38, t)):
            key = (round(x, 3), round(y, 3))
            if key not in pts and not (x < -0.8 and y > 0.8 or x > 1.15 and y < -1.15):
                pts.append(key)
    return pts


def _lights(p, variant, rnd):
    for k, (x, y) in enumerate(_light_points()):
        out = variant != "intact" and k % 3 == 1
        if variant == "ruins" and k % 4:
            continue
        down = variant == "damaged" and k in (4, 11) or variant == "ruins"
        turn = Matrix.Identity(4)
        if down:  # knocked over, lying by its place
            turn = Matrix.Translation((x, y, PAD + 0.02)) @ Matrix.Rotation(rnd.uniform(0, math.pi), 4, 'Z') @ Matrix.Rotation(1.5, 4, 'X')
            turn = turn @ Matrix.Translation((-x, -y, -PAD))

        def light(x=x, y=y, out=out or down):
            p.cylinder((x, y, PAD + 0.012), 'Z', 0.024, 0.012, DARK, sides=8)
            p.sphere((x, y, PAD + 0.03), 'Z', 0.017, DARK if out else YELLOW, sides=8, rings=3)
            for a in (0.0, math.pi / 2):  # the ribs of the cage over the glass
                d = Vector((math.cos(a), math.sin(a), 0.0)) * 0.022
                c = Vector((x, y, PAD + 0.024))
                p.bar(c - d, c + d, 0.003, DARK)
                p.bar(c + d, c + d * 0.4 + Vector((0, 0, 0.026)), 0.003, DARK)
                p.bar(c - d, c - d * 0.4 + Vector((0, 0, 0.026)), 0.003, DARK)
        transformed(p, turn, light)


# --- hut ------------------------------------------------------------------------------

def _hut(p, team, variant, rnd):
    x0, x1 = HUT_X
    y0, y1 = HUT_Y
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if variant == "ruins":
        p.box((x0 - 0.02, y0 - 0.02, PAD), (x1 + 0.02, y1 + 0.02, PLINTH), CONCRETE)
        ic.stumps(p, rnd, [((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))],
                  PLINTH, 0.06, 0.28, HUT, step=0.1, gap=0.2, thick=0.04)
        cc.block(p, (cx + 0.12, cy - 0.08, PLINTH + 0.1), (0.62, 0.6, 0.04), (0.25, -0.2, 0.3), CONCRETE)  # the roof, down on the heap
        ic.rubble(p, rnd, x0, x1, y0, y1, PLINTH, 14, 0.07, HUT, height=0.06)
        ic.rubble(p, rnd, x0 - 0.1, x1 + 0.25, y0 - 0.3, y0, PAD, 8, 0.06, GLASS)
        p.bar((x1 + 0.05, y0 + 0.05, PAD + 0.02), (x1 + 0.5, y0 - 0.3, PAD + 0.05), 0.008, DARK)  # the mast, fallen
        return
    p.box((x0 - 0.02, y0 - 0.02, PAD), (x1 + 0.02, y1 + 0.02, PLINTH), CONCRETE)
    p.box((x0, y0, PLINTH), (x1, y1, EAVE), HUT)
    damaged = variant == "damaged"
    # The band of windows on each side, mullions in white; the front ones out when damaged.
    for wall, (a0, a1) in (("front", (x0, x1)), ("back", (x0, x1)), ("left", (y0, y1)), ("right", (y0, y1))):
        n = 4
        glass = DARK if damaged and wall == "front" else GLASS
        for k in range(n):
            u0 = a0 + 0.04 + (a1 - a0 - 0.08) * k / n + 0.008
            u1 = a0 + 0.04 + (a1 - a0 - 0.08) * (k + 1) / n - 0.008
            if wall == "front":
                p.box((u0, y0 - 0.006, BAND[0]), (u1, y0, BAND[1]), glass)
            elif wall == "back":
                p.box((u0, y1, BAND[0]), (u1, y1 + 0.006, BAND[1]), glass)
            elif wall == "left":
                p.box((x0 - 0.006, u0, BAND[0]), (x0, u1, BAND[1]), glass)
            else:
                p.box((x1, u0, BAND[0]), (x1 + 0.006, u1, BAND[1]), glass)
        for z in (BAND[0] - 0.015, BAND[1]):  # sill and lintel
            if wall in ("front", "back"):
                y = y0 - 0.012 if wall == "front" else y1
                p.box((a0, y, z), (a1, y + 0.012, z + 0.015), WHITE)
            else:
                x = x0 - 0.012 if wall == "left" else x1
                p.box((x, a0, z), (x + 0.012, a1, z + 0.015), WHITE)
    # The door in the front, under the windows, and its step.
    p.box((x1 - 0.2, y0 - 0.008, PLINTH), (x1 - 0.08, y0, BAND[0] - 0.03), DARK)
    p.box((x1 - 0.22, y0 - 0.08, PAD), (x1 - 0.06, y0 - 0.02, PLINTH), CONCRETE)
    # The roof, its parapet in the owner's colour, the air conditioner at the back, the mast with its light.
    p.box((x0 - 0.04, y0 - 0.04, EAVE), (x1 + 0.04, y1 + 0.04, ROOF), CONCRETE)
    for a, b in (((x0 - 0.04, y0 - 0.04), (x1 + 0.04, y0 - 0.01)), ((x0 - 0.04, y1 + 0.01), (x1 + 0.04, y1 + 0.04)),
                 ((x0 - 0.04, y0 - 0.01), (x0 - 0.01, y1 + 0.01)), ((x1 + 0.01, y0 - 0.01), (x1 + 0.04, y1 + 0.01))):
        team.box((a[0], a[1], ROOF), (b[0], b[1], ROOF + 0.05), TEAM)
    p.box((cx - 0.1, y1, PLINTH + 0.12), (cx + 0.1, y1 + 0.08, PLINTH + 0.26), WHITE)
    p.box((cx - 0.07, y1 + 0.081, PLINTH + 0.14), (cx + 0.07, y1 + 0.085, PLINTH + 0.24), DARK)  # its grille
    foot = Vector((cx - 0.1, cy + 0.1, ROOF))
    tip = foot + (Vector((0.18, -0.12, 0.42)) if damaged else Vector((0.0, 0.0, 0.55)))
    p.bar(foot, tip, 0.01, DARK)
    p.bar(foot + (tip - foot) * 0.7 - Vector((0.07, 0, 0)), foot + (tip - foot) * 0.7 + Vector((0.07, 0, 0)), 0.005, DARK)
    p.sphere(tip, 'Z', 0.022, DARK if damaged else RED, sides=8, rings=4)
    # By the hut: two drums and a red fire point on a post.
    for k, (x, y) in enumerate(((-1.36, 0.78), (-1.27, 0.74))):
        if damaged and k == 1:
            p.turned((x, y - 0.1, PAD + 0.035), 'X', [(-0.055, 0.035), (0.055, 0.035)], DRUM, sides=8)  # knocked over
        else:
            p.turned((x, y, PAD), 'Z', [(0.0, 0.035), (0.11, 0.035)], DRUM, sides=8)
    p.box((x1 + 0.12, y0 - 0.02, PAD), (x1 + 0.14, y0, PAD + 0.3), DARK)
    p.box((x1 + 0.1, y0 - 0.05, PAD + 0.18), (x1 + 0.16, y0 - 0.02, PAD + 0.28), RED)
    if damaged:
        # A hole through the right side: dark inside, its edge jagged, the pieces at the foot.
        p.box((x1 + 0.001, cy - 0.16, PLINTH + 0.06), (x1 + 0.004, cy - 0.0, BAND[0] + 0.06), DARK)
        for k in range(5):
            cc.block(p, (x1 + 0.01, cy - 0.17 + 0.04 * k, PLINTH + 0.05 + rnd.uniform(0.0, 0.3)), (0.03, 0.05, 0.05),
                     (rnd.uniform(-0.5, 0.5), 0.0, rnd.uniform(-0.5, 0.5)), HUT)
        ic.rubble(p, rnd, x1 + 0.03, x1 + 0.3, cy - 0.3, cy + 0.1, PAD, 10, 0.05, HUT)
        ic.rubble(p, rnd, x0, x1, y0 - 0.25, y0 - 0.05, PAD, 6, 0.03, GLASS)


# --- windsock -------------------------------------------------------------------------

def _windsock(p, variant):
    """The sock on its swivel in its own frame: the pole's axis at the origin, pointing along -x over the pad."""
    z = POLE_TOP + 0.03
    p.bar((0.0, 0.0, z), (-0.06, 0.0, z), 0.006, DARK)  # the arm to its ring
    droop = -1.2 if variant == "damaged" else -0.12
    stations = [(0.0, 0.055), (0.08, 0.048), (0.16, 0.04), (0.24, 0.032), (0.3, 0.026)]
    if variant == "damaged":
        stations = stations[:4]
    # Turned along +x, then hung from the ring and pointed back over the pad.
    transformed(p, Matrix.Translation((-0.06, 0.0, z)) @ Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Rotation(-droop, 4, 'Y'),
                lambda: p.turned((0.0, 0.0, 0.0), 'X', stations, ORANGE, sides=8))
    p.turned((-0.06, 0.0, z), 'X', [(-0.004, 0.058), (0.004, 0.058), (0.004, 0.05), (-0.004, 0.05)], DARK, sides=8)


# --- material -------------------------------------------------------------------------

def _inside(g, t, half, sharp=150.0):
    """1 where |t| < half, with an edge a few texels wide."""
    return g.clamp01(g.math('MULTIPLY', g.math('SUBTRACT', half, g.math('ABSOLUTE', t)), sharp))


def _material(name, variant):
    """Grey concrete in slabs with the white H on the pad's top, soot of the rotors round it and cracks at the edges; blue-grey walls,
    dark blue glass, white frames, dark metal, yellow lights, an orange and white striped sock, red, a light team zone, olive drums;
    cracks and soot when damaged, more in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    up = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), 10.0, -9.0))

    joint = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, EDGE), 1.0 / 0.98)), 0.5)), 60.0, -29.0)),
                      g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', y, EDGE), 1.0 / 0.98)), 0.5)), 60.0, -29.0)))
    concrete = g.mix(grain, lin((0.50, 0.50, 0.48)), lin((0.58, 0.58, 0.55)))
    concrete = g.mix(g.math('MULTIPLY', joint, 0.7), concrete, lin((0.30, 0.30, 0.29)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.6))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.4), concrete, lin((0.38, 0.37, 0.34)))
    # Soot of the rotors: darker in a ring round the middle.
    r = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', x, x), g.math('MULTIPLY', y, y)))
    wash = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', r, -1.2, 1.25)), g.noise(6.0, 3.0))
    concrete = g.mix(g.math('MULTIPLY', wash, 0.45), concrete, lin((0.24, 0.23, 0.22)))
    # The H: two bars and the bar across, on the pad's top; worn at its edges.
    bars = g.math('MULTIPLY', _inside(g, g.math('SUBTRACT', g.math('ABSOLUTE', x), 0.36), 0.09), _inside(g, y, 0.6))
    cross = g.math('MULTIPLY', _inside(g, x, 0.36), _inside(g, y, 0.07))
    letter = g.math('MULTIPLY', g.math('MULTIPLY', g.math('MAXIMUM', bars, cross), up), g.clamp01(g.math('MULTIPLY_ADD', g.noise(40.0, 3.0), 6.0, -1.6)))
    concrete = g.mix(letter, concrete, lin((0.92, 0.92, 0.90)))
    edge = g.clamp01(g.math('MULTIPLY_ADD', g.math('MAXIMUM', g.math('ABSOLUTE', x), g.math('ABSOLUTE', y)), 8.0, -10.4))
    concrete = g.mix(g.math('MULTIPLY', edge, cc.cracks(g, 6.0)), concrete, lin((0.18, 0.18, 0.17)))

    hut = g.mix(grain, lin((0.38, 0.45, 0.52)), lin((0.44, 0.51, 0.58)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 4.0, -1.5)), lin((0.10, 0.16, 0.22)), lin((0.30, 0.42, 0.50)))
    white = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.93, 0.93, 0.91)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    yellow = lin((1.0, 0.80, 0.25))
    stripe = g.math('GREATER_THAN', g.math('FRACT', g.math('MULTIPLY', g.math('ABSOLUTE', x), 16.0)), 0.5)  # the sock's origin is the pole
    orange = g.mix(stripe, lin((0.95, 0.42, 0.08)), lin((0.94, 0.93, 0.90)))
    red = g.mix(grain, lin((0.72, 0.10, 0.07)), lin((0.80, 0.14, 0.09)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    drum = g.mix(grain, lin((0.30, 0.36, 0.22)), lin((0.36, 0.42, 0.26)))
    albedo = cc.paint(g, mask, (concrete, hut, glass, white, dark, yellow, orange, red, team, drum))
    if variant != "intact":
        crack = g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]), cc.cracks(g, 5.0))
        if variant == "damaged":  # in patches: where it was hit
            crack = g.math('MULTIPLY', crack, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 2.0), 5.0, -2.4)))
        albedo = g.mix(crack, albedo, lin((0.14, 0.13, 0.12)))
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.15 if variant == "damaged" else 0.6), 0.8), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.9, 0.7, 0.1, 0.6, 0.6, 0.2, 0.85, 0.5, 0.6, 0.5))
    metallic = cc.weighted(g, mask, (0.0, 0.2, 0.0, 0.1, 0.5, 0.0, 0.0, 0.2, 0.0, 0.4))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', joint, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.04, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("pad", "lights", "hut", "windsock", "team_mask", "body"))
    rnd = ic.seed(variant_id)
    pad = Parts()
    _pad(pad, variant, rnd)
    if variant == "ruins":
        body = Parts()
        _hut(body, None, variant, rnd)
        _lights(body, variant, rnd)
        # The pole bent down at its foot, the sock under it.
        sx, sy = SOCK
        body.box((sx - 0.04, sy - 0.04, 0.0), (sx + 0.04, sy + 0.04, PAD + 0.03), CONCRETE)
        body.bar((sx, sy, PAD + 0.03), (sx - 0.12, sy + 0.05, PAD + 0.12), 0.012, WHITE)
        body.bar((sx - 0.12, sy + 0.05, PAD + 0.12), (sx - 0.7, sy + 0.25, PAD + 0.015), 0.012, WHITE)
        body.turned((sx - 0.78, sy + 0.3, PAD + 0.025), 'X', [(-0.1, 0.025), (0.1, 0.02)], ORANGE, sides=8)
        parts = [(pad, "pad"), (body, "body")]
        return cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant)), {}
    lights, hut, team, sock = Parts(), Parts(), Parts(), Parts()
    _lights(lights, variant, rnd)
    _hut(hut, team, variant, rnd)
    _windsock(sock, variant)
    parts = [(pad, "pad"), (lights, "lights"), (hut, "hut"), (sock, "windsock"), (team, "team_mask")]
    return cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant)), {"windsock": (*SOCK, 0.0)}


def run(variant_id):
    """Builds, bakes, saves the .blend and exports the GLB; the windsock stands at its pole, so the game turns it there."""
    (meshes, mat, graph), origins = build(variant_id)
    for obj in meshes:
        node = obj.name.split(".")[0][len(variant_id) + 1:]
        if node in origins:
            obj.location = origins[node]
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
