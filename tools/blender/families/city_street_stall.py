"""City street stall: element 08 of the city set (docs/city_elements/08_street_stall) and its states (appearance.md, 4). City climate:
dry daylight, no snow. A map object (street_stall, game/data/map/objects.yaml), not a building: no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, the cart along x. Made at the size the game draws it, not a storey
to a cell: the game does not squash map objects, and at the prompt's 1.2 it would stand over the houses (0.88 once squashed). The
counter is at 0.38, the umbrella's top at 0.84. The parts:
    cart      a dark brown wooden cart on two spoked wheels with two legs and two handles; on its counter three crates of goods in
              plain bright shapes (red, orange and green heaps), no products to recognise (prompts.md, 4)
    umbrella  a striped umbrella, red and white, on a pole over the cart
No emblems, no text.
Nodes: "cart" (the cart and its goods, and the goods spilled in a state), "umbrella" (a node of its own: it sways, prompts.md, 4).
One GLB per state:
    city_street_stall           whole
    city_street_stall_damaged   damaged: the umbrella torn, two of its panels gone and the rest leaning, goods spilled on the pavement
    city_street_stall_ruins     destroyed: the cart on its side, the umbrella on the ground, the goods all over the pavement
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one are made for the
documents' states and wait for the client to show them.
Budget 800 triangles, texture 1024 (docs/city_elements/08_street_stall/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_street_stall as css
    print(css.run("city_street_stall"))   # or css.run_all()
"""

import math

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_street_stall": "intact",
    "city_street_stall_damaged": "damaged",
    "city_street_stall_ruins": "ruins",
}

WOOD, METAL, CRATE, RED, ORANGE, GREEN, CLOTH_RED, CLOTH_WHITE = (cc.part(i) for i in range(8))
PARTS = 8

BODY = (0.3, 0.18, 0.12, 0.36)  # half length, half width, bottom, top
COUNTER = 0.38
WHEEL_R = 0.12
POLE_TOP = 0.78
CANOPY_R = 0.42
PANELS = 8


# --- the cart -------------------------------------------------------------------------

def _cart(p, goods=True):
    hx, hy, z0, z1 = BODY
    p.box((-hx, -hy, z0), (hx, hy, z1), WOOD)
    p.box((-hx - 0.02, -hy - 0.02, z1), (hx + 0.02, hy + 0.02, COUNTER), WOOD)  # the counter
    for y in (-hy - 0.03, hy + 0.03):  # the wheels, their axle under the middle
        p.cylinder((-0.04, y, WHEEL_R), 'Y', WHEEL_R, 0.018, METAL, sides=12)
    p.bar((-0.04, -hy - 0.03, WHEEL_R), (-0.04, hy + 0.03, WHEEL_R), 0.012, METAL)
    for y in (-hy + 0.03, hy - 0.03):  # the legs at the handle end, and the handles
        p.bar((hx - 0.04, y, z0), (hx - 0.02, y, 0.0), 0.014, WOOD)
        p.bar((hx, y, 0.3), (hx + 0.16, y, 0.33), 0.012, WOOD)
    if goods:
        for x, heap in ((-0.18, RED), (0.0, ORANGE), (0.18, GREEN)):
            _crate(p, x, 0.0, COUNTER, heap)


def _crate(p, x, y, z, heap):
    """A low crate of goods on the counter: the crate and a rounded heap of one bright colour on it."""
    p.box((x - 0.08, y - 0.12, z), (x + 0.08, y + 0.12, z + 0.05), CRATE)
    p.loft([[Vector((x + sx * a, y + sy * b, z + h)) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
            for a, b, h in ((0.07, 0.11, 0.05), (0.06, 0.1, 0.075), (0.03, 0.06, 0.09))], heap)


SPILLED = (((0.25, -0.33, 0.015), RED), ((0.33, -0.25, 0.015), RED), ((-0.1, -0.36, 0.015), ORANGE), ((0.05, -0.4, 0.015), GREEN))


def _spilled(p, points):
    """Goods on the pavement: small heaps of colour."""
    for (x, y, z), heap in points:
        cc.block(p, (x, y, z), (0.05, 0.04, 0.03), (0.0, 0.0, x * 7.0), heap)


# --- the umbrella -----------------------------------------------------------------------

def _umbrella(p, variant):
    """The pole and the canopy: eight thin panels from the rim to the top, red and white in turn. Damaged: two panels torn off and the
    umbrella leaning."""
    p.bar((0.0, 0.0, COUNTER), (0.0, 0.0, POLE_TOP + 0.06), 0.012, METAL)
    apex = Vector((0.0, 0.0, POLE_TOP + 0.06))
    rim_z = POLE_TOP - 0.06
    for k in range(PANELS):
        if variant == "damaged" and k in (2, 5):
            continue
        a0, a1 = 2 * math.pi * k / PANELS, 2 * math.pi * (k + 1) / PANELS
        rim = [Vector((CANOPY_R * math.cos(a), CANOPY_R * math.sin(a), rim_z)) for a in (a0, a1)]
        p.loft([[rim[0], rim[1], apex], [rim[0] + Vector((0, 0, 0.012)), rim[1] + Vector((0, 0, 0.012)), apex + Vector((0, 0, 0.012))]],
               CLOTH_RED if k % 2 == 0 else CLOTH_WHITE)


def _lean(p, angle, axis, pivot):
    bmesh.ops.rotate(p.bm, verts=list(p.bm.verts), cent=pivot, matrix=Matrix.Rotation(angle, 3, axis))


# --- material -----------------------------------------------------------------------

def _material(name):
    """Dark brown wood with its grain, metal, pale crates, red, orange and green goods, red and white cloth."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 4.0)
    wood = g.mix(g.noise(3.0, 6.0), lin((0.30, 0.20, 0.12)), lin((0.38, 0.26, 0.16)))
    metal = g.mix(grain, lin((0.26, 0.27, 0.29)), lin((0.34, 0.35, 0.37)))
    crate = g.mix(g.noise(3.0, 6.0), lin((0.70, 0.58, 0.40)), lin((0.78, 0.66, 0.46)))
    bumps = g.noise(60.0, 2.0)  # the heaps read as many small fruits
    red = g.mix(bumps, lin((0.62, 0.10, 0.08)), lin((0.80, 0.18, 0.12)))
    orange = g.mix(bumps, lin((0.84, 0.46, 0.08)), lin((0.94, 0.58, 0.14)))
    green = g.mix(bumps, lin((0.30, 0.52, 0.14)), lin((0.42, 0.64, 0.20)))
    cloth_red = g.mix(grain, lin((0.70, 0.16, 0.14)), lin((0.78, 0.20, 0.17)))
    cloth_white = g.mix(grain, lin((0.90, 0.88, 0.82)), lin((0.95, 0.93, 0.88)))
    albedo = cc.paint(g, mask, (wood, metal, crate, red, orange, green, cloth_red, cloth_white))
    rough = cc.weighted(g, mask, (0.8, 0.45, 0.8, 0.5, 0.5, 0.5, 0.9, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.7, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY', g.math('ADD', g.math('ADD', mask[3], mask[4]), mask[5]), bumps), 0.3, 0.01)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("cart", "umbrella"))
    variant = VARIANTS[variant_id]
    cart = Parts()
    umbrella = Parts()
    if variant == "ruins":
        _cart(cart, goods=False)
        _lean(cart, math.radians(-90), 'X', Vector((0.0, -BODY[1] - 0.05, 0.0)))  # on its side, its counter to the front
        for v in cart.bm.verts:
            v.co.z += BODY[1] + 0.07
        _umbrella(umbrella, variant)
        _lean(umbrella, math.radians(150), 'Y', Vector((0.0, 0.0, COUNTER)))  # fallen over, its canopy low on the ground
        low = min(v.co.z for v in umbrella.bm.verts)
        for v in umbrella.bm.verts:
            v.co += Vector((0.05, 0.25, -low))
        _spilled(cart, SPILLED + (((-0.3, -0.3, 0.015), GREEN), ((0.1, -0.15, 0.015), ORANGE), ((-0.35, 0.05, 0.015), RED),
                                  ((0.3, 0.3, 0.015), GREEN)))
    else:
        _cart(cart)
        _umbrella(umbrella, variant)
        if variant == "damaged":
            _lean(umbrella, math.radians(14), 'X', Vector((0.0, 0.0, COUNTER)))
            _spilled(cart, SPILLED)
    return cc.finish(variant_id, [(cart, "cart"), (umbrella, "umbrella")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes cart and umbrella."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
