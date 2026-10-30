"""City mailbox: element 12 of the city set (docs/city_elements/12_mailbox) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A map object (mailbox, game/data/map/objects.yaml): no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, its slot to -Y. Made at the size the game draws it, as the bench
(the game does not squash map objects): about half a cell high, as the documents have it, 0.55 to the top of the box. The parts:
    pedestal  a short grey concrete post on a wider foot
    box       a blue steel box with a rounded top, a white label without text and a small dark lock on its front
    flap      the hinged flap over the slot, a node of its own (prompts.md, 4)
No emblems, no text, no house number (prompts.md, 4).
Nodes: "body" (the post, the box, and the pieces of a state), "flap". One GLB per state:
    city_mailbox            whole
    city_mailbox_damaged    damaged: the flap bent and twisted, the lock knocked out, dents and scratches in the paint
    city_mailbox_ruins      destroyed: the box on the ground, its flap off, the post broken to a stump, pieces of concrete
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 500 triangles, texture 512 (docs/city_elements/12_mailbox/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_mailbox as cmb
    print(cmb.run("city_mailbox"))   # or cmb.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 512
VARIANTS = {
    "city_mailbox": "intact",
    "city_mailbox_damaged": "damaged",
    "city_mailbox_ruins": "ruins",
}

BLUE, CONCRETE, WHITE, DARK = (cc.part(i) for i in range(4))
PARTS = 4

POST_TOP = 0.28
BOX = (0.12, 0.09, POST_TOP, 0.47)  # half width, half depth, bottom, top of the straight sides
ROUND = 6  # segments of the rounded top


def _post(p, stump=False):
    p.box((-0.09, -0.09, 0.0), (0.09, 0.09, 0.04), CONCRETE)
    p.box((-0.065, -0.065, 0.04), (0.065, 0.065, 0.14 if stump else POST_TOP), CONCRETE)


def _box_outline():
    """The box's outline across x: straight sides and a round top."""
    hw, _, z0, z1 = BOX
    return [(-hw, z0), (hw, z0)] + [(hw * math.cos(math.pi * k / ROUND), z1 + 0.6 * hw * math.sin(math.pi * k / ROUND)) for k in range(ROUND + 1)]


def _box(p, damaged):
    """The blue box with its rounded top, the label and the lock on its front."""
    _, hd, z0, z1 = BOX
    p.loft([[Vector((x, y, z)) for x, z in _box_outline()] for y in (-hd, hd)], BLUE)
    p.box((-0.07, -hd - 0.002, z1 - 0.055), (0.07, -hd + 0.002, z1 - 0.045), DARK)  # the slot, under the flap
    p.box((-0.07, -hd - 0.006, z0 + 0.04), (0.07, -hd + 0.002, z0 + 0.09), WHITE)  # the label, no text
    if damaged:
        p.box((0.04, -hd - 0.002, z0 + 0.015), (0.075, -hd + 0.002, z0 + 0.035), DARK)  # the hole where the lock was
    else:
        p.box((0.045, -hd - 0.012, z0 + 0.012), (0.07, -hd + 0.002, z0 + 0.035), DARK)  # the lock


def _flap(p, bent=False):
    """The flap over the slot near the top of the front, hinged along its upper edge; bent, it is pushed out and twisted."""
    _, hd, _, z1 = BOX
    hinge_z = z1 + 0.02
    rings = [[Vector((x, -hd - 0.004, hinge_z)), Vector((x, -hd - 0.016, hinge_z)), Vector((x, -hd - 0.03, hinge_z - 0.07)),
              Vector((x, -hd - 0.018, hinge_z - 0.07))] for x in (-0.08, 0.08)]
    if bent:
        pivot = Vector((0.0, -hd, hinge_z))
        m = Matrix.Rotation(math.radians(-40), 3, 'X') @ Matrix.Rotation(math.radians(18), 3, 'Y')
        rings = [[pivot + m @ (v - pivot) for v in ring] for ring in rings]
    p.loft(rings, BLUE)


def _material(name, variant):
    """Blue painted steel, grey concrete, a white label, a dark lock and slot; dents and scratches when damaged."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    blue = g.mix(grain, lin((0.12, 0.24, 0.52)), lin((0.16, 0.30, 0.60)))
    if variant != "intact":
        blue = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 4.0), 6.0, -3.2)), blue, lin((0.50, 0.50, 0.52)))  # bare metal
    concrete = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.64, 0.63, 0.60)))
    white = g.mix(grain, lin((0.90, 0.90, 0.88)), lin((0.95, 0.95, 0.93)))
    dark = lin((0.06, 0.06, 0.07))
    albedo = cc.paint(g, mask, (blue, concrete, white, dark))
    rough = cc.weighted(g, mask, (0.4, 0.9, 0.5, 0.6))
    metallic = cc.weighted(g, mask, (0.5, 0.0, 0.0, 0.5))
    normal = g.bump(g.noise(30.0, 3.0), 0.08 if variant != "intact" else 0.03, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def _turn_all(p, angle, axis, pivot, shift):
    m = Matrix.Rotation(angle, 3, axis)
    for v in p.bm.verts:
        v.co = m @ (v.co - pivot) + pivot + shift


def build(variant_id):
    cc.clear(variant_id, ("body", "flap"))
    variant = VARIANTS[variant_id]
    body = Parts()
    flap = Parts()
    if variant == "ruins":
        box = Parts()
        _box(box, damaged=True)
        _turn_all(box, math.radians(100), 'Y', Vector((0.0, 0.0, POST_TOP)), Vector((0.22, -0.12, -POST_TOP + BOX[0] + 0.005)))
        body.bm.free()
        body = box
        _post(body, stump=True)
        for c, s, t in (((-0.18, 0.1, 0.03), (0.1, 0.08, 0.06), (0.3, 0.2, 0.5)), ((-0.05, 0.22, 0.025), (0.08, 0.07, 0.05), (0.1, 0.4, 1.4)),
                        ((-0.22, -0.12, 0.02), (0.06, 0.05, 0.04), (0.2, 0.1, 2.2))):
            cc.block(body, c, s, t, CONCRETE)
        _flap(flap, bent=True)
        _turn_all(flap, math.radians(80), 'X', Vector((0.0, -BOX[1], BOX[3])), Vector((-0.1, -0.2, -BOX[3] + 0.02)))
    else:
        _post(body)
        _box(body, damaged=variant == "damaged")
        _flap(flap, bent=variant == "damaged")
    return cc.finish(variant_id, [(body, "body"), (flap, "flap")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body and flap."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
