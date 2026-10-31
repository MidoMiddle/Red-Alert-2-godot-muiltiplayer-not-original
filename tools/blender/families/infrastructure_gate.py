"""Gate, element 01 of the infrastructure set (docs/infrastructure_elements/01_gate): a section of a wall with two swinging leaves, open for
the owner's units.

Footprint 1x1 cell, origin at the centre of the cell on the ground; the wall runs along x, the front is -y. A map object made at its own
size, as the walls it stands among (game/assets/models/building/wall, map/wall_stone_section): the posts to 0.9, the leaves to the walls'
height (0.62). The parts:
    posts      two concrete posts on the ends of the cell (0.42 to 0.58 out: 0.84 between them, a tank's width) with formwork seams, a plinth and a cap, a caged amber lamp on each
    leaves     two steel leaves painted dark green: a frame, a sheet in the lower half with a brace, pointed bars above, a hazard-striped
               bottom rail, two hinges on the post's side; rust at the hinges and at the foot
    latch      a sliding bar across the leaves on the front, in keepers on both leaves; a drop bolt on the left leaf
    base       the footings of the posts and the steel sill between them
Nodes: "post_left", "post_right", "leaf_left", "leaf_right", "latch", "team_mask" (a band round the top of each post), "base". Each leaf's
origin is its hinge (x = -0.41 and 0.41, y = 0): the game swings it about the vertical there, to the back (+y here, -z in Godot), the left
leaf by +90 degrees, the right one by -90. The latch's origin is its middle; the game lifts it before the leaves move. One GLB per state:
    gate          whole, closed
    gate_damaged  damaged, still closed: the left leaf dented with two bars bent and one gone, the right leaf hanging askew from its lower
                  hinge, the right post's cap broken off and its lamp smashed, chips round it, cracks and scorch
    gate_ruins    destroyed, the way open for good: the posts broken to stumps with rebar, the leaves torn off and lying bent on the ground,
                  the sill bent, rubble; nodes "body" and "base"
Budget 3000 triangles, texture 1024 (docs/infrastructure_elements/01_gate/prompts.md). The gate does not burn: no fire nodes.

In Blender, after tools/blender is on sys.path:
    import families.infrastructure_gate as gate
    print(gate.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "infrastructure"  # game/assets/models/infrastructure/<id>/, assets_src/models/infrastructure/<id>/
SIZE = 1024
VARIANTS = {
    "gate": "intact",
    "gate_damaged": "damaged",
    "gate_ruins": "ruins",
}

(CONCRETE, GREEN, DARK, HAZARD, LAMP, STEEL, TEAM) = (cc.part(i) for i in range(7))
PARTS = 7

FOOT = 0.04  # the footings' top
POST_X = 0.42  # the inner face of a post
POST_OUT = 0.58  # its outer face: a post stands on the end of the cell, into the next wall, so a tank fits between the posts
POST_C = (POST_X + POST_OUT) / 2
POST_Y = 0.17  # as deep as the stone wall
POST_TOP = 0.86
CAP_TOP = 0.9
HINGE_X = 0.41
LEAF_W = 0.405  # from the hinge to the middle, short of it by half the gap
LEAF_Z0, LEAF_Z1 = 0.045, 0.62
MID = 0.33  # the middle rail: the sheet below, the bars above
LATCH = (0.0, -0.025, 0.31)
BAND = (0.72, 0.8)  # the owner's band round the top of a post


def transformed(p, matrix, build):
    """Builds with build() into p, then moves what it made by matrix: a leaf mirrored, leaning or lying on the ground."""
    before = set(p.bm.verts)
    build()
    for v in p.bm.verts:
        if v not in before:
            v.co = matrix @ v.co


# --- posts ----------------------------------------------------------------------------

def _post(p, side, variant):
    """A post at the end of the cell, side -1 the left one, 1 the right; the hinge plates on its inner face."""
    def xs(a, b):  # a, b measured from the middle outward
        return (min(side * a, side * b), max(side * a, side * b))

    x0, x1 = xs(POST_X - 0.01, POST_OUT + 0.01)
    p.box((x0, -POST_Y - 0.01, FOOT), (x1, POST_Y + 0.01, 0.12), CONCRETE)  # the plinth
    x0, x1 = xs(POST_X, POST_OUT)
    broken = variant == "damaged" and side > 0
    top = POST_TOP - 0.1 if broken else POST_TOP
    p.box((x0, -POST_Y, 0.12), (x1, POST_Y, top), CONCRETE)
    if broken:  # the cap broke off with a corner of the post: a jagged stub of the corner left standing
        cc.block(p, (side * (POST_C + 0.04), -0.08, top + 0.04), (0.1, 0.16, 0.08), (0.1, -0.15, 0.2), CONCRETE)
        cc.block(p, (side * (POST_C - 0.04), 0.07, top + 0.025), (0.08, 0.18, 0.05), (-0.12, 0.08, -0.1), CONCRETE)
    else:
        x0, x1 = xs(POST_X - 0.005, POST_OUT + 0.005)
        p.box((x0, -POST_Y - 0.01, POST_TOP), (x1, POST_Y + 0.01, CAP_TOP), CONCRETE)
    # The lamp: a dark housing, an amber glass in a cage; gone from the broken post (its housing lies at the foot).
    if not broken:
        c = side * POST_C
        p.box((c - 0.035, -0.035, CAP_TOP), (c + 0.035, 0.035, CAP_TOP + 0.025), DARK)
        p.sphere((c, 0.0, CAP_TOP + 0.055), 'Z', 0.03, LAMP, sides=8, rings=4)
        for dx, dy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            p.bar((c + dx * 0.03, dy * 0.03, CAP_TOP + 0.025), (c + dx * 0.022, dy * 0.022, CAP_TOP + 0.09), 0.004, DARK)
        p.box((c - 0.025, -0.025, CAP_TOP + 0.088), (c + 0.025, 0.025, CAP_TOP + 0.096), DARK)
    for hz in (0.14, 0.52):  # hinge plates
        x0, x1 = xs(POST_X - 0.015, POST_X)
        p.box((x0, -0.025, hz - 0.035), (x1, 0.025, hz + 0.035), DARK)


def _band(p, side):
    x0, x1 = sorted((side * (POST_X - 0.006), side * (POST_OUT + 0.006)))
    p.box((x0, -POST_Y - 0.006, BAND[0]), (x1, POST_Y + 0.006, BAND[1]), TEAM)


# --- leaves ---------------------------------------------------------------------------

def _leaf(p, latch_side, dented=False, upper_hinge=True):
    """A leaf in its own frame: the hinge at the origin, the leaf along +x to LEAF_W. latch_side: the leaf with the drop bolt (the left)."""
    w, t = LEAF_W, 0.014
    p.box((0.005, -t, LEAF_Z0), (0.03, t, LEAF_Z1), GREEN)  # the stile at the hinge
    p.box((w - 0.025, -t, LEAF_Z0), (w, t, LEAF_Z1), GREEN)  # the meeting stile
    p.box((0.03, -t, LEAF_Z0), (w - 0.025, t, LEAF_Z0 + 0.03), HAZARD)  # the bottom rail
    p.box((0.03, -t, MID), (w - 0.025, t, MID + 0.025), GREEN)
    p.box((0.03, -t, LEAF_Z1 - 0.025), (w - 0.025, t, LEAF_Z1), GREEN)
    # The sheet, with a brace on each face; folded in when dented.
    if dented:
        cc.block(p, (w * 0.3, 0.0, (LEAF_Z0 + MID) / 2 + 0.015), (w * 0.4, 0.012, MID - LEAF_Z0 - 0.03), (0.0, 0.0, -0.12), GREEN)
        cc.block(p, (w * 0.69, 0.012, (LEAF_Z0 + MID) / 2 + 0.015), (w * 0.42, 0.012, MID - LEAF_Z0 - 0.03), (0.06, 0.0, 0.18), GREEN)
    else:
        p.box((0.03, -0.006, LEAF_Z0 + 0.03), (w - 0.025, 0.006, MID), GREEN)
    for y in (-0.011, 0.011):
        p.bar((0.04, y, LEAF_Z0 + 0.045), (w - 0.035, y, MID - 0.015), 0.008, GREEN)
    # Pointed bars over the middle rail, through the top rail.
    xs = [0.03 + (w - 0.055) * (k + 0.5) / 7 for k in range(7)]
    for k, x in enumerate(xs):
        if dented and k == 1:
            continue  # shot away
        if dented and k in (3, 4):  # bent out of the leaf
            bow = Vector((x + 0.01 * (k - 3), -0.045, (MID + LEAF_Z1) / 2))
            p.bar((x, 0.0, MID + 0.02), bow, 0.006, GREEN)
            p.bar(bow, (x, 0.0, LEAF_Z1 - 0.02), 0.006, GREEN)
            continue
        p.bar((x, 0.0, MID + 0.02), (x, 0.0, LEAF_Z1 + 0.035), 0.006, GREEN)
        p.loft([[Vector((x + dx * 0.009, dy * 0.009, LEAF_Z1 + 0.03)) for dx, dy in ((1, 1), (-1, 1), (-1, -1), (1, -1))],
                [Vector((x + dx * 0.001, dy * 0.001, LEAF_Z1 + 0.065)) for dx, dy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]], GREEN)
    # The hinges: knuckles on the axis and straps along the front and the back.
    for hz in (0.14, 0.52) if upper_hinge else (0.14,):
        p.cylinder((0.0, 0.0, hz), 'Z', 0.018, 0.035, DARK, sides=8)
        for y0, y1 in ((-0.02, -t), (t, 0.02)):
            p.box((0.0, y0, hz - 0.012), (0.17, y1, hz + 0.012), DARK)
    # The keeper of the latch on the meeting stile, and the drop bolt into the sill.
    p.box((w - 0.05, -0.035, LATCH[2] - 0.025), (w - 0.01, -t, LATCH[2] + 0.025), DARK)
    if latch_side:
        p.bar((w - 0.04, -0.02, LEAF_Z0 - 0.02), (w - 0.04, -0.02, 0.2), 0.005, DARK)
        p.box((w - 0.05, -0.024, 0.17), (w - 0.03, -t, 0.18), DARK)


MIRROR = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))


def _latch(p):
    """The bar of the latch round its middle, a knob to slide it by."""
    p.box((-0.085, -0.007, -0.015), (0.085, 0.007, 0.015), DARK)
    p.box((0.03, -0.035, -0.01), (0.045, -0.007, 0.01), DARK)


# --- base, ruins ----------------------------------------------------------------------

def _base(p, variant):
    for side in (-1, 1):
        x0, x1 = sorted((side * (POST_X - 0.02), side * (POST_OUT + 0.02)))
        p.box((x0, -POST_Y - 0.03, 0.0), (x1, POST_Y + 0.03, FOOT), CONCRETE)
    if variant == "ruins":  # the sill bent up in the middle
        p.box((-HINGE_X, -0.04, 0.0), (-0.1, 0.04, 0.012), STEEL)
        cc.block(p, (0.0, -0.01, 0.03), (0.22, 0.08, 0.012), (0.0, 0.3, 0.1), STEEL)
        p.box((0.1, -0.04, 0.0), (HINGE_X, 0.04, 0.012), STEEL)
    else:
        p.box((-HINGE_X, -0.04, 0.0), (HINGE_X, 0.04, 0.012), STEEL)
        p.box((-HINGE_X + LEAF_W - 0.05, -0.03, 0.012), (-HINGE_X + LEAF_W - 0.03, -0.01, 0.016), DARK)  # the socket of the drop bolt


def _lying(origin, heading, rise):
    """Where a torn-off leaf lies: its height along heading (radians about the vertical) rising by rise, its width to the right of it,
    its front up; origin under its hinge and its foot."""
    h = Vector((math.cos(heading) * math.cos(rise), math.sin(heading) * math.cos(rise), math.sin(rise)))
    w = Vector((math.sin(heading), -math.cos(heading), 0.0))
    m = Matrix((w, h.cross(w), h)).transposed().to_4x4()
    m.translation = origin
    return m @ Matrix.Translation((0.0, 0.0, -LEAF_Z0))


def _ruins(p, rnd):
    # The left post broken low, the right one at half height, both jagged, rebar out of the breaks.
    for side, top in ((-1, 0.32), (1, 0.5)):
        x0, x1 = sorted((side * (POST_X - 0.01), side * (POST_OUT + 0.01)))
        p.box((x0, -POST_Y - 0.01, FOOT), (x1, POST_Y + 0.01, 0.12), CONCRETE)
        x0, x1 = sorted((side * POST_X, side * POST_OUT))
        p.box((x0, -POST_Y, 0.12), (x1, POST_Y, top), CONCRETE)
        for k in range(4):
            if rnd.random() < 0.25:
                continue
            c = (side * rnd.uniform(POST_X + 0.03, POST_OUT - 0.03), rnd.uniform(-0.12, 0.12), top + rnd.uniform(0.02, 0.06))
            cc.block(p, c, (rnd.uniform(0.06, 0.1), rnd.uniform(0.07, 0.12), rnd.uniform(0.05, 0.12)),
                     (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, math.pi)), CONCRETE)
        for dx, dy, lean in ((POST_C - 0.04, -0.1, 0.04), (POST_C + 0.04, 0.09, -0.05), (POST_C, 0.11, 0.07)):
            a = Vector((side * dx, dy, top - 0.02))
            p.bar(a, a + Vector((side * lean, lean * 0.6, 0.12)), 0.005, DARK)
    # The top of the right post lying in front, its cap and lamp housing at its end.
    def top_piece():
        p.box((-0.18, -POST_Y, -0.085), (0.18, POST_Y, 0.085), CONCRETE)
        p.box((0.18, -POST_Y - 0.01, -0.09), (0.22, POST_Y + 0.01, 0.09), CONCRETE)
        p.box((0.22, -0.035, -0.035), (0.245, 0.035, 0.035), DARK)
    transformed(p, Matrix.Translation((0.5, -0.36, 0.085)) @ Matrix.Rotation(math.radians(75), 4, 'Z'), top_piece)
    # The leaves torn off, lying bent: the left one in front, the right one behind with its end up on rubble.
    transformed(p, _lying((-0.38, -0.12, 0.035), math.radians(6), 0.0), lambda: _leaf(p, True, dented=True))
    transformed(p, _lying((0.38, 0.12, 0.035), math.radians(175), 0.1), lambda: _leaf(p, False, dented=True, upper_hinge=False))
    transformed(p, Matrix.Translation((-0.15, 0.06, 0.015)) @ Matrix.Rotation(0.5, 4, 'Z'), lambda: _latch(p))
    ic.rubble(p, rnd, -0.6, -0.35, -0.32, 0.32, 0.0, 10, 0.07, CONCRETE)
    ic.rubble(p, rnd, 0.35, 0.6, -0.32, 0.32, 0.0, 12, 0.08, CONCRETE)
    ic.rubble(p, rnd, -0.2, 0.2, -0.25, 0.2, 0.0, 5, 0.05, CONCRETE)


# --- material -------------------------------------------------------------------------

def _material(name, variant):
    """Grey concrete with dark formwork seams, dirtier at the foot; dark green paint on the leaves, rust at the hinges and the foot; dark
    iron, yellow and black hazard stripes, amber glass, bare steel, a light team band; cracks, scratches and scorch per state."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, z = g.axis(coord, 'X'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    seam = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', z, 5.0)), 0.5)), 30.0, -13.5))
    concrete = g.mix(grain, lin((0.55, 0.54, 0.51)), lin((0.63, 0.62, 0.59)))
    concrete = g.mix(g.math('MULTIPLY', seam, 0.6), concrete, lin((0.30, 0.30, 0.29)))
    concrete = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -6.0, 0.9)), 0.5), concrete, lin((0.33, 0.30, 0.26)))
    green = g.mix(grain, lin((0.17, 0.28, 0.19)), lin((0.21, 0.33, 0.23)))
    # Rust: near the hinge (a leaf's origin is its hinge) and at the foot, in patches.
    near = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), -12.0, 1.0)), g.clamp01(g.math('MULTIPLY_ADD', z, -9.0, 1.3)))
    rust_amount = {"intact": 0.0, "damaged": 0.15, "ruins": 0.35}[variant]
    rusty = g.clamp01(g.math('MULTIPLY_ADD', g.noise(18.0, 3.0), 5.0, -2.6 + 2.0 * rust_amount))
    rust = g.mix(g.noise(40.0, 2.0), lin((0.36, 0.18, 0.08)), lin((0.50, 0.27, 0.12)))
    green = g.mix(g.math('MULTIPLY', g.math('MAXIMUM', near, rust_amount), rusty), green, rust)
    dark = g.mix(grain, lin((0.09, 0.09, 0.09)), lin((0.15, 0.14, 0.13)))
    stripe = g.math('GREATER_THAN', g.math('FRACT', g.math('MULTIPLY', g.math('SUBTRACT', x, z), 16.0)), 0.5)
    hazard = g.mix(stripe, lin((0.08, 0.08, 0.07)), lin((0.88, 0.70, 0.12)))
    lamp = lin((1.0, 0.62, 0.18))
    steel = g.mix(grain, lin((0.34, 0.34, 0.34)), lin((0.44, 0.44, 0.43)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    albedo = cc.paint(g, mask, (concrete, green, dark, hazard, lamp, steel, team))

    if variant != "intact":
        crack = g.math('MULTIPLY', mask[0], cc.cracks(g))
        albedo = g.mix(crack, albedo, lin((0.18, 0.17, 0.16)))
        # Scratches and bullet marks through the paint to bare steel.
        scratch = g.math('MULTIPLY', g.math('ADD', mask[1], mask[3]), g.clamp01(g.math('MULTIPLY_ADD', g.noise(70.0, 2.0), 12.0, -7.6)))
        albedo = g.mix(scratch, albedo, lin((0.42, 0.42, 0.40)))
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.25 if variant == "damaged" else 0.6), 0.8), albedo, lin((0.06, 0.05, 0.05)))

    rough = cc.weighted(g, mask, (0.9, 0.55, 0.6, 0.6, 0.2, 0.45, 0.6))
    metallic = cc.weighted(g, mask, (0.0, 0.35, 0.6, 0.2, 0.0, 0.7, 0.0))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', seam, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "base", "post_left", "post_right", "leaf_left", "leaf_right", "latch", "team_mask"))
    rnd = ic.seed(variant_id)
    base = Parts()
    _base(base, variant)
    if variant == "ruins":
        body = Parts()
        _ruins(body, rnd)
        return cc.finish(variant_id, [(body, "body"), (base, "base")], 0.0, _material(variant_id + "_mat", variant)), {}
    damaged = variant == "damaged"
    post_left, post_right, leaf_left, leaf_right, latch, team = (Parts() for _ in range(6))
    _post(post_left, -1, variant)
    _post(post_right, 1, variant)
    if damaged:  # chips of the broken cap at the foot of the right post
        ic.rubble(post_right, rnd, 0.38, 0.6, -0.36, -0.22, 0.0, 6, 0.05, CONCRETE)
        cc.block(post_right, (0.48, 0.3, 0.03), (0.17, 0.2, 0.05), (0.15, -0.1, 0.4), CONCRETE)
        cc.block(post_right, (0.4, -0.3, 0.014), (0.07, 0.07, 0.025), (0.3, 0.1, 0.7), DARK)  # the lamp's housing
    _leaf(leaf_left, True, dented=damaged)
    # The right leaf, mirrored; hanging from its lower hinge when damaged, its meeting edge dropped to the sill.
    lean = Matrix.Translation((0, 0, 0.14)) @ Matrix.Rotation(math.radians(5), 4, 'Y') @ Matrix.Translation((0, 0, -0.14)) if damaged else Matrix()
    transformed(leaf_right, MIRROR @ lean, lambda: _leaf(leaf_right, False, upper_hinge=not damaged))
    _latch(latch)
    for side in (-1, 1):
        if not (damaged and side > 0):
            _band(team, side)
    if damaged:  # the band of the broken post: what is left of it below the break
        team.box((POST_X - 0.006, -POST_Y - 0.006, BAND[0]), (POST_OUT + 0.006, POST_Y + 0.006, min(BAND[1], POST_TOP - 0.1)), TEAM)
    parts = [(post_left, "post_left"), (post_right, "post_right"), (leaf_left, "leaf_left"), (leaf_right, "leaf_right"),
             (latch, "latch"), (team, "team_mask"), (base, "base")]
    meshes, mat, graph = cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant))
    origins = {"leaf_left": (-HINGE_X, 0.0, 0.0), "leaf_right": (HINGE_X, 0.0, 0.0), "latch": LATCH}
    return (meshes, mat, graph), origins


def run(variant_id):
    """Builds, bakes, saves the .blend and exports the GLB; the leaves and the latch stand at their origins (the hinges, the latch's
    middle), so the game turns them in place."""
    (meshes, mat, graph), origins = build(variant_id)
    for obj in meshes:
        node = obj.name.split(".")[0][len(variant_id) + 1:]
        if node in origins:
            obj.location = origins[node]
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
