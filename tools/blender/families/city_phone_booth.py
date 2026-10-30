"""City phone booth: element 13 of the city set (docs/city_elements/13_phone_booth) and its states (appearance.md, 4). City climate:
dry daylight, no snow. A map object (phone_booth, game/data/map/objects.yaml): no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, its door to -Y. Made at the size the game draws it, as the bench
(the game does not squash map objects): 0.8 to the top of the roof, not the prompt's 1.5, which would stand over the walls of the
squashed houses. The parts:
    frame     a dark red metal frame: four corner posts on a low plinth, rails at the middle, a frieze under a dark roof that stands
              out a little; the door's frame with a handle on the front
    inside    a grey phone on the back wall with its handset, a white plate without text, a warm lamp under the roof
    glass     the panels of the three walls and of the door: a node of its own with a clear material of its own, not baked
              (prompts.md, 4), so the game draws it see-through
No emblems, no text.
Nodes: "body" (the frame and what is inside), "glass". One GLB per state:
    city_phone_booth            whole
    city_phone_booth_damaged    damaged: two panels gone, the door's skewed on its hinges, the glass that is left cracked darker
    city_phone_booth_ruins      destroyed: the frame leaning, no glass in it, the roof slid off, shards on the ground
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 1000 triangles, texture 512 (docs/city_elements/13_phone_booth/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_phone_booth as cpb
    print(cpb.run("city_phone_booth"))   # or cpb.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 512
VARIANTS = {
    "city_phone_booth": "intact",
    "city_phone_booth_damaged": "damaged",
    "city_phone_booth_ruins": "ruins",
}

FRAME, ROOF, PHONE, WHITE, LAMP, GLASS_PART = (cc.part(i) for i in range(6))
PARTS = 6

H = 0.21  # half the side of the booth
PLINTH = 0.04
TOP = 0.7  # the top of the walls
RAIL = 0.33
ROOF_Z = (0.72, 0.8)  # the frieze and the roof's top
POST = 0.022
# The glass panels: (wall, from, to) along the wall. The door is the front panel.
PANELS = (("front", -H + POST, H - POST), ("back", -H + POST, H - POST), ("left", -H + POST, H - POST), ("right", -H + POST, H - POST))


def _panel_box(wall, u0, u1, z0, z1, t=0.006):
    """The corners of a thin panel in a wall of the booth."""
    if wall in ("front", "back"):
        y = -H if wall == "front" else H
        return (u0, y - t, z0), (u1, y + t, z1)
    x = -H if wall == "left" else H
    return (x - t, u0, z0), (x + t, u1, z1)


def _frame(p, variant):
    """The posts, the plinth, the rails, the frieze and the roof; the door's frame and handle; what is inside."""
    p.box((-H - 0.02, -H - 0.02, 0.0), (H + 0.02, H + 0.02, PLINTH), FRAME)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * H - POST, sy * H - POST, PLINTH), (sx * H + POST, sy * H + POST, TOP), FRAME)
    for wall, u0, u1 in PANELS:  # the rails at the middle and over the glass
        for z in (RAIL, TOP - 0.03):
            lo, hi = _panel_box(wall, u0, u1, z - 0.012, z + 0.012, t=0.014)
            p.box(lo, hi, FRAME)
    if variant != "ruins":
        p.box((-H - 0.01, -H - 0.01, TOP), (H + 0.01, H + 0.01, ROOF_Z[0]), FRAME)  # the frieze
        p.box((-H - 0.035, -H - 0.035, ROOF_Z[0]), (H + 0.035, H + 0.035, ROOF_Z[1]), ROOF)
        p.box((-0.05, -0.05, ROOF_Z[0] - 0.02), (0.05, 0.05, ROOF_Z[0]), LAMP)  # the lamp under the roof
    else:
        cc.block(p, (0.32, -0.1, 0.04), (0.5, 0.5, 0.08), (0.3, 0.1, 0.4), ROOF)  # the roof, slid off onto the ground
    # The door's handle; the phone and the plate on the back wall.
    p.box((H - 0.07, -H - 0.025, 0.36), (H - 0.05, -H - 0.01, 0.46), FRAME)
    p.box((-0.07, H - 0.06, 0.38), (0.07, H - 0.015, 0.56), PHONE)
    p.box((-0.075, H - 0.08, 0.5), (-0.035, H - 0.06, 0.58), PHONE)  # the handset
    p.box((-0.06, H - 0.02, 0.58), (0.06, H - 0.012, 0.64), WHITE)


def _glass(p, variant):
    """The glass panels, between the posts and the rails. Damaged: the left wall's panel and the back's lower one gone, the door
    skewed; destroyed: only shards on the ground."""
    if variant == "ruins":
        for k, (x, y) in enumerate(((-0.35, -0.3), (-0.1, -0.38), (0.25, -0.35), (-0.38, 0.1), (0.35, 0.3), (0.0, -0.45))):
            cc.block(p, (x, y, 0.004), (0.07, 0.05, 0.006), (0.0, 0.0, 1.1 * k), GLASS_PART)
        return
    for wall, u0, u1 in PANELS:
        for z0, z1 in ((PLINTH, RAIL - 0.012), (RAIL + 0.012, TOP - 0.042)):
            if variant == "damaged" and (wall == "left" or wall == "back" and z0 == PLINTH):
                continue
            lo, hi = _panel_box(wall, u0, u1, z0, z1)
            if variant == "damaged" and wall == "front":
                hinge = Vector((-H, -H, 0.0))
                corners = [Vector(c) for c in ((lo[0], lo[1], lo[2]), (hi[0], lo[1], lo[2]), (hi[0], hi[1], lo[2]), (lo[0], hi[1], lo[2]))]
                m = Matrix.Rotation(math.radians(-25), 3, 'Z')
                rings = [[hinge + m @ (c - hinge) + Vector((0, 0, dz)) for c in corners] for dz in (0.0, z1 - z0)]
                p.loft(rings, GLASS_PART)
            else:
                p.box(lo, hi, GLASS_PART)


def _lean(objs, angle):
    """Leans the objects over about the right edge of the foot."""
    m = Matrix.Rotation(angle, 3, 'Y')
    pivot = Vector((H + 0.02, 0.0, 0.0))
    for p in objs:
        for v in p.bm.verts:
            if v.co.z > 0.05:
                v.co = m @ (v.co - pivot) + pivot


# --- materials ------------------------------------------------------------------------

def _material(name, variant):
    """Dark red painted metal, a dark roof, a grey phone, a white plate, a warm lamp."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    frame = g.mix(grain, lin((0.46, 0.08, 0.07)), lin((0.54, 0.11, 0.09)))
    if variant != "intact":
        frame = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(10.0, 4.0), 6.0, -3.3)), frame, lin((0.30, 0.26, 0.24)))
    roof = g.mix(grain, lin((0.12, 0.12, 0.13)), lin((0.17, 0.17, 0.18)))
    phone = g.mix(grain, lin((0.38, 0.39, 0.41)), lin((0.46, 0.47, 0.49)))
    white = lin((0.92, 0.92, 0.90))
    lamp = lin((0.98, 0.86, 0.56))
    glass = lin((0.60, 0.72, 0.78))
    albedo = cc.paint(g, mask, (frame, roof, phone, white, lamp, glass))
    rough = cc.weighted(g, mask, (0.4, 0.6, 0.5, 0.5, 0.3, 0.1))
    metallic = cc.weighted(g, mask, (0.6, 0.3, 0.4, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(30.0, 3.0), 0.02, 0.003)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "glass"))
    variant = VARIANTS[variant_id]
    body = Parts()
    glass = Parts()
    _frame(body, variant)
    _glass(glass, variant)
    if variant == "ruins":
        _lean([body], math.radians(14))
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", variant))
    pane = glass.finish(f"{variant_id}_glass")
    pane.data.materials.append(cc.glass_material(variant_id + "_glass_mat", cracked=variant != "intact"))
    return meshes, mat, graph, [pane]


def run(variant_id):
    """Builds, bakes the frame, saves the .blend, exports the GLB with the nodes body and glass (the glass with its own material)."""
    meshes, mat, graph, extra = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE, extra=extra)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
