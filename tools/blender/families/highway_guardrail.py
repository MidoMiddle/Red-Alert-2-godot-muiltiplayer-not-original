"""Highway guardrail, highway_guardrail (docs/map_elements/04_roads, «Отбойник»): one section of the barrier along a highway.

One cell long, along x, origin at the centre of the cell on the ground; made at the size the game draws it, as the fences
(docs/map_elements/08_fences_walls: a section 0.25 high), not squashed. Neighbouring sections join end to end, so the beam runs the
whole cell and the posts stand every half cell. The game turns a section along z when its neighbours run that way. The parts:
    beam        a galvanised W-beam (two waves) at waist height of a soldier, on spacer blocks
    posts       two steel posts, half a cell apart, an amber reflector on the beam over each
Node "body". One GLB per state (README: целый, помятый, сломанный):
    highway_guardrail           whole
    highway_guardrail_damaged   dented: the beam pushed in and dropped in the middle, a post leaning, the reflector off
    highway_guardrail_ruins     broken: the beam torn in two on the ground, the posts bent down to stumps
Budget 600 triangles, texture 512 (docs/map_elements/04_roads/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.highway_guardrail as hg
    print(hg.run_all())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 512
VARIANTS = {
    "highway_guardrail": "intact",
    "highway_guardrail_damaged": "damaged",
    "highway_guardrail_ruins": "ruins",
}

STEEL, POST, REFLECT = (cc.part(i) for i in range(3))
PARTS = 3

POSTS = (-0.25, 0.25)
POST_TOP = 0.21
BEAM_Y = -0.035  # the face of the beam, on the side of the road (-y)
# The W-beam's section, y and z, from its bottom up: two waves facing the road.
WAVE = ((0.0, 0.12), (-0.012, 0.135), (0.0, 0.15), (-0.012, 0.165), (0.0, 0.18))
THICK = 0.006


def _section(dy=0.0, dz=0.0):
    """The closed outline of the beam's section, moved by dy, dz: its face, then its back the other way."""
    face = [(BEAM_Y + y + dy, z + dz) for y, z in WAVE]
    back = [(y + THICK, z) for y, z in reversed(face)]
    return face + back


def _beam(p, stations):
    """The beam along x through the stations (x, dy, dz): a loft of its section."""
    p.loft([[Vector((x, y, z)) for y, z in _section(dy, dz)] for x, dy, dz in stations], STEEL)


def _post(p, x, lean=0.0, top=POST_TOP):
    """A post behind the beam, with its spacer block; lean tips it back (radians)."""
    foot = Vector((x, 0.0, 0.0))
    head = foot + Vector((0.0, math.sin(lean) * top, math.cos(lean) * top))
    p.bar(foot, head, 0.014, POST)
    if lean == 0.0:
        p.box((x - 0.012, BEAM_Y + THICK, 0.13), (x + 0.012, -0.014, 0.17), POST)  # the spacer


def _reflector(p, x, dy=0.0, dz=0.0):
    p.box((x - 0.012, BEAM_Y - 0.016 + dy, 0.143 + dz), (x + 0.012, BEAM_Y - 0.01 + dy, 0.157 + dz), REFLECT)


def build(variant_id):
    variant = VARIANTS[variant_id]
    cc.clear(variant_id, ("body",))
    p = Parts()
    if variant == "intact":
        _beam(p, [(-0.5, 0.0, 0.0), (0.5, 0.0, 0.0)])
        for x in POSTS:
            _post(p, x)
            _reflector(p, x)
    elif variant == "damaged":
        # Pushed in and dropped where something hit it, between the posts.
        _beam(p, [(-0.5, 0.0, 0.0), (-0.1, 0.012, -0.004), (0.05, 0.05, -0.025), (0.2, 0.02, -0.01), (0.5, 0.0, 0.0)])
        _post(p, POSTS[0])
        _reflector(p, POSTS[0])
        _post(p, POSTS[1], lean=0.35)
    else:
        # Torn in two and down on the ground, the posts bent over.
        for c, turn in (((-0.24, -0.02, 0.012), 0.12), ((0.27, 0.05, 0.012), -0.3)):
            # A piece 0.4 long, laid face up: its section turned a quarter about x, then placed and turned about z.
            m = Matrix.Translation(c) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X')
            p.loft([[m @ Vector((x, y - BEAM_Y, z - 0.15)) for y, z in _section()] for x in (-0.2, 0.2)], STEEL)
        for x, lean in ((POSTS[0], 1.2), (POSTS[1], 0.9)):
            _post(p, x, lean=lean, top=0.12)
    mesh = p.finish(f"{variant_id}_body")
    cc.drop_bottoms([mesh], 0.001)
    rc.select_only([mesh])
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together([mesh])
    cc.repack([mesh])
    mat, graph = _material(variant_id + "_mat", variant)
    mesh.data.materials.append(mat)
    return mesh, mat, graph


def _material(name, variant):
    """Galvanised steel, lighter on the beam, darker on the posts, rust spots and streaks; amber reflectors; scuffs when hit."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    steel = g.mix(grain, lin((0.66, 0.68, 0.70)), lin((0.76, 0.78, 0.80)))
    post = g.mix(grain, lin((0.48, 0.50, 0.52)), lin((0.56, 0.58, 0.60)))
    reflect = lin((0.95, 0.55, 0.08))
    albedo = cc.paint(g, mask, (steel, post, reflect))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(9.0, 3.0), 5.0, -3.2 if variant == "intact" else -2.6))
    albedo = g.mix(g.math('MULTIPLY', rust, g.math('ADD', mask[0], mask[1])), albedo, lin((0.42, 0.24, 0.12)))
    if variant != "intact":
        scuff = g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 4.0, -2.2))
        albedo = g.mix(g.math('MULTIPLY', scuff, mask[0]), albedo, lin((0.30, 0.30, 0.31)))
    rough = cc.weighted(g, mask, (0.4, 0.5, 0.3))
    rough = g.math('MULTIPLY_ADD', rust, 0.45, rough)
    metallic = cc.weighted(g, mask, (0.6, 0.5, 0.0))
    metallic = g.math('MULTIPLY_ADD', rust, -0.5, metallic)
    normal = g.bump(g.noise(60.0, 3.0), 0.03, 0.003)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the node body."""
    mesh, mat, graph = build(variant_id)
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {rc.tri_count(mesh)}, size {tuple(round(v, 3) for v in mesh.dimensions)}"]
    cc.repack([mesh], cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material([mesh], graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    rc.arrange([mesh], variant_id)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names([mesh], ["body"]):
        rc.save_blend([mesh], mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb([mesh], glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
