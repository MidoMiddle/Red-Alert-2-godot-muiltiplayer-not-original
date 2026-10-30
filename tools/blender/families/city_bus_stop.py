"""City bus stop: element 14 of the city set (docs/city_elements/14_bus_stop) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A map object (bus_stop, game/data/map/objects.yaml): no soldiers, no team zone.

Origin at the centre of the cell on the ground, the shelter along x, open to -Y. A map object stands on one cell, so the shelter fits
one cell (0.94 long), not the prompt's two; made at the size the game draws it, as the phone booth (the game does not squash map
objects): the roof at 0.84, not the prompt's 2.2. The parts:
    frame     three grey metal posts along the back, rails along the top, a frame round the glass, a bench of wooden slats inside on
              metal legs, a route board without text on its own post at the left end
    roof      a flat grey metal canopy over it, standing out over the front, sloping a little to the back
    glass     the panels of the back wall and of the right end: a node of its own with a clear material, not baked
No emblems, no text, nobody waiting (prompts.md, 4).
Nodes: "body" (the frame, the bench, the board), "roof", "glass". One GLB per state:
    city_bus_stop           whole
    city_bus_stop_damaged   damaged: one back panel and the end panel gone, the rest cracked, the board hanging from its post
    city_bus_stop_ruins     destroyed: the frame bent over, the roof fallen behind it, the glass in shards on the ground
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 2000 triangles, texture 1024 (docs/city_elements/14_bus_stop/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_bus_stop as cbs
    print(cbs.run("city_bus_stop"))   # or cbs.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_bus_stop": "intact",
    "city_bus_stop_damaged": "damaged",
    "city_bus_stop_ruins": "ruins",
}

METAL, ROOF, WOOD, WHITE, DARK, GLASS_PART = (cc.part(i) for i in range(6))
PARTS = 6

HX = 0.42  # the end posts
BACK_Y = 0.14
POSTS_X = (-HX, 0.0, HX)
TOP = 0.76  # the top of the posts
ROOF_Y = (-0.26, 0.2)
ROOF_Z = (0.8, 0.77)  # the roof's top at the front and at the back
GLASS_Z = (0.1, 0.68)
END_Y = (-0.12, BACK_Y)  # the right end's panel
BOARD = Vector((-0.47, -0.22, 0.0))  # the route board's post


def _frame(p, variant):
    """The posts, the rails, the frame round the glass, the bench and the route board."""
    for x in POSTS_X:
        p.box((x - 0.018, BACK_Y - 0.018, 0.0), (x + 0.018, BACK_Y + 0.018, TOP), METAL)
    p.box((HX - 0.018, END_Y[0] - 0.018, 0.0), (HX + 0.018, END_Y[0] + 0.018, TOP), METAL)  # the front post of the right end
    for z in (GLASS_Z[0] - 0.015, TOP - 0.02):
        p.box((-HX, BACK_Y - 0.012, z), (HX, BACK_Y + 0.012, z + 0.02), METAL)
        p.box((HX - 0.012, END_Y[0], z), (HX + 0.012, BACK_Y, z + 0.02), METAL)
    # The bench: three slats on two legs at the back wall.
    for k in range(3):
        y = BACK_Y - 0.03 - 0.045 * (k + 1)
        p.box((-0.34, y - 0.018, 0.2), (0.34, y + 0.018, 0.22), WOOD)
    for x in (-0.28, 0.28):
        p.box((x - 0.012, BACK_Y - 0.17, 0.0), (x + 0.012, BACK_Y - 0.04, 0.2), METAL)
    # The route board on its own post at the left end; hanging from it when damaged.
    p.box((BOARD.x - 0.012, BOARD.y - 0.012, 0.0), (BOARD.x + 0.012, BOARD.y + 0.012, 0.86), METAL)
    if variant == "damaged":
        cc.block(p, (BOARD.x + 0.02, BOARD.y - 0.02, 0.62), (0.015, 0.12, 0.16), (0.0, 0.0, 0.0), WHITE)
        cc.block(p, (BOARD.x + 0.02, BOARD.y - 0.02, 0.62), (0.008, 0.12, 0.16), (0.5, 0.0, 0.2), WHITE)
    else:
        p.box((BOARD.x - 0.008, BOARD.y - 0.07, 0.68), (BOARD.x + 0.008, BOARD.y + 0.07, 0.86), WHITE)
        p.box((BOARD.x - 0.01, BOARD.y - 0.05, 0.73), (BOARD.x + 0.01, BOARD.y + 0.05, 0.75), DARK)  # a plain dark stripe, no text


def _roof(p):
    """The flat canopy, standing out over the front, sloping a little to the back."""
    (y0, y1), (z0, z1) = ROOF_Y, ROOF_Z
    p.profile_x([(y0, z0 - 0.03), (y1, z1 - 0.03), (y1, z1), (y0, z0)], -HX - 0.05, HX + 0.05, ROOF)


def _glass(p, variant):
    """The back wall's two panels and the right end's one; damaged, the left back panel and the end one are gone; destroyed, only
    shards on the ground."""
    if variant == "ruins":
        for k, (x, y) in enumerate(((-0.3, -0.3), (0.0, -0.4), (0.3, -0.35), (0.42, 0.3), (-0.4, 0.32), (0.15, 0.4), (-0.15, -0.2))):
            cc.block(p, (x, y, 0.004), (0.08, 0.05, 0.006), (0.0, 0.0, 1.3 * k), GLASS_PART)
        return
    z0, z1 = GLASS_Z
    for x0, x1 in ((-HX + 0.02, -0.02), (0.02, HX - 0.02)):
        if variant == "damaged" and x0 < 0:
            continue
        p.box((x0, BACK_Y - 0.005, z0), (x1, BACK_Y + 0.005, z1), GLASS_PART)
    if variant != "damaged":
        p.box((HX - 0.005, END_Y[0] + 0.02, z0), (HX + 0.005, END_Y[1] - 0.02, z1), GLASS_PART)


def _bend(objs, angle, pivot):
    """Bends the objects over towards the front about a line along x at the pivot's height: the frame knocked over."""
    m = Matrix.Rotation(angle, 3, 'X')
    for p in objs:
        for v in p.bm.verts:
            if v.co.z > pivot.z:
                v.co = m @ (v.co - pivot) + pivot


def _material(name, variant):
    """Grey metal, a grey canopy, warm wood, a white board with a dark stripe."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    metal = g.mix(grain, lin((0.42, 0.44, 0.46)), lin((0.50, 0.52, 0.54)))
    roof = g.mix(grain, lin((0.30, 0.32, 0.34)), lin((0.36, 0.38, 0.40)))
    wood = g.mix(g.noise(4.0, 8.0), lin((0.50, 0.33, 0.18)), lin((0.60, 0.41, 0.24)))
    if variant != "intact":
        metal = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 4.0), 6.0, -3.4)), metal, lin((0.36, 0.24, 0.16)))  # rust
    white = lin((0.92, 0.92, 0.90))
    dark = lin((0.10, 0.12, 0.16))
    glass = lin((0.60, 0.72, 0.78))
    albedo = cc.paint(g, mask, (metal, roof, wood, white, dark, glass))
    rough = cc.weighted(g, mask, (0.55, 0.6, 0.75, 0.5, 0.5, 0.1))
    metallic = cc.weighted(g, mask, (0.25, 0.2, 0.0, 0.0, 0.0, 0.0))  # painted metal: dull, or the flat light of a map draws it black
    normal = g.bump(g.noise(40.0, 3.0), 0.02, 0.003)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "glass"))
    variant = VARIANTS[variant_id]
    body = Parts()
    roof = Parts()
    glass = Parts()
    _frame(body, variant)
    _roof(roof)
    _glass(glass, variant)
    if variant == "ruins":
        _bend([body], math.radians(40), Vector((0.0, BACK_Y, 0.25)))
        for v in roof.bm.verts:  # the roof fallen behind the shelter, tilted
            v.co = Matrix.Rotation(math.radians(-25), 3, 'X') @ (v.co - Vector((0.0, 0.0, ROOF_Z[0]))) + Vector((0.05, -0.05, 0.28))
    meshes, mat, graph = cc.finish(variant_id, [(body, "body"), (roof, "roof")], 0.0, _material(variant_id + "_mat", variant))
    pane = glass.finish(f"{variant_id}_glass")
    pane.data.materials.append(cc.glass_material(variant_id + "_glass_mat", cracked=variant != "intact"))
    return meshes, mat, graph, [pane]


def run(variant_id):
    """Builds, bakes the frame and the roof, saves the .blend, exports the GLB with the nodes body, roof and glass."""
    meshes, mat, graph, extra = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE, extra=extra)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
