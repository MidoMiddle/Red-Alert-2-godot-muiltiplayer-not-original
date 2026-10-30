"""City billboard: element 15 of the city set (docs/city_elements/15_billboard) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A map object (billboard, game/data/map/objects.yaml): no soldiers, no team zone.

Origin at the centre of the cell on the ground, the panel along x, its face to -Y. A map object stands on one cell, so the billboard
fits one cell (0.98 wide), not the prompt's two; made at the size the game draws it, as the bus stop (the game does not squash map
objects): the panel from 0.5 to 0.98, its bottom over a soldier's head (appearance.md, 1). The parts:
    posts      two grey steel posts with a cross brace, the grey frame round the panel and its back with stiffeners
    sign face  the panel's face with a poster for a made-up brand: a red ground, a broad yellow sash across it, a white disc with a
               blue square in it; no text, no real mark (prompts.md, 3.2). Drawn by the material, so no picture file is needed
No emblems, no text.
Nodes: "posts", "sign_face" (prompts.md, 4: a poster can be laid on it). One GLB per state:
    city_billboard              whole
    city_billboard_damaged      damaged: the panel's right top corner torn off and hanging, the poster cracked and stained
    city_billboard_ruins        destroyed: the panel down on the ground face up, the posts bent over
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 3000 triangles, texture 1024 (docs/city_elements/15_billboard/prompts.md, 1).

In Blender, after tools/blender is on sys.path:
    import families.city_billboard as cbb
    print(cbb.run("city_billboard"))   # or cbb.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_billboard": "intact",
    "city_billboard_damaged": "damaged",
    "city_billboard_ruins": "ruins",
}

STEEL, FRAME, POSTER = (cc.part(i) for i in range(3))
PARTS = 3

HW = 0.49  # half the width of the panel
PANEL_Z = (0.5, 0.98)
FACE_Y = -0.04  # the poster's face
BACK_Y = 0.0
POSTS_X = (-0.3, 0.3)
POST_Y = 0.06


def _posts(p):
    """The two posts, a cross brace between them, the stiffeners behind the panel."""
    for x in POSTS_X:
        p.box((x - 0.025, POST_Y - 0.025, 0.0), (x + 0.025, POST_Y + 0.025, PANEL_Z[1] - 0.04), STEEL)
    p.bar((POSTS_X[0], POST_Y, 0.12), (POSTS_X[1], POST_Y, PANEL_Z[0] - 0.04), 0.012, STEEL)
    p.bar((POSTS_X[1], POST_Y, 0.12), (POSTS_X[0], POST_Y, PANEL_Z[0] - 0.04), 0.012, STEEL)
    for z in (PANEL_Z[0] + 0.1, PANEL_Z[1] - 0.1):
        p.box((-HW + 0.03, BACK_Y, z - 0.012), (HW - 0.03, POST_Y - 0.02, z + 0.012), STEEL)


def _panel(frame, face, torn=False):
    """The panel: its back and its frame in the posts' node, its face with the poster in its own. Torn: the right top corner is cut
    out of the face and the frame and hangs below on its last bolt."""
    z0, z1 = PANEL_Z
    cut = (0.22, 0.2) if torn else None  # the width and the height of the torn corner
    def rect(p, x0, x1, za, zb, y0, y1, part):
        p.box((x0, y0, za), (x1, y1, zb), part)
    if cut:
        cw, ch = cut
        rect(face, -HW + 0.02, HW - 0.02 - cw, z0 + 0.02, z1 - 0.02, FACE_Y, FACE_Y + 0.02, POSTER)
        rect(face, HW - 0.02 - cw, HW - 0.02, z0 + 0.02, z1 - 0.02 - ch, FACE_Y, FACE_Y + 0.02, POSTER)
        hinge = Vector((HW - 0.02 - cw, FACE_Y, z1 - 0.02 - ch))
        m = Matrix.Rotation(math.radians(-115), 3, 'Y')
        corners = [Vector(c) for c in ((0.0, 0.0, 0.0), (cw, 0.0, 0.0), (cw, 0.0, ch), (0.0, 0.0, ch))]
        face.loft([[hinge + m @ c + Vector((0.0, dy, 0.0)) for c in corners] for dy in (0.0, 0.02)], POSTER)
        rect(frame, -HW, HW - cw, z1 - 0.02, z1, FACE_Y - 0.01, BACK_Y, FRAME)
        rect(frame, HW - 0.02, HW, z0, z1 - ch, FACE_Y - 0.01, BACK_Y, FRAME)
    else:
        rect(face, -HW + 0.02, HW - 0.02, z0 + 0.02, z1 - 0.02, FACE_Y, FACE_Y + 0.02, POSTER)
        rect(frame, -HW, HW, z1 - 0.02, z1, FACE_Y - 0.01, BACK_Y, FRAME)
        rect(frame, HW - 0.02, HW, z0, z1, FACE_Y - 0.01, BACK_Y, FRAME)
    rect(frame, -HW, HW, z0, z0 + 0.02, FACE_Y - 0.01, BACK_Y, FRAME)
    rect(frame, -HW, -HW + 0.02, z0, z1, FACE_Y - 0.01, BACK_Y, FRAME)
    rect(frame, -HW + 0.02, HW - 0.02, z0 + 0.02, z1 - 0.02, FACE_Y + 0.02, BACK_Y, FRAME)  # the back



# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Grey steel and frame; the poster by the panel's own coordinates: a red ground, a yellow sash, a white disc with a blue square.
    Damaged: cracked and stained."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    z = g.axis(coord, 'Z')
    grain = g.noise(40.0, 4.0)
    steel = g.mix(grain, lin((0.44, 0.45, 0.47)), lin((0.52, 0.53, 0.55)))
    frame = g.mix(grain, lin((0.56, 0.57, 0.59)), lin((0.62, 0.63, 0.65)))
    # u across the panel 0..1, v up it 0..1.
    u = g.math('MULTIPLY_ADD', x, 1.0 / (2 * HW), 0.5)
    v = g.math('MULTIPLY_ADD', z, 1.0 / (PANEL_Z[1] - PANEL_Z[0]), -PANEL_Z[0] / (PANEL_Z[1] - PANEL_Z[0]))
    red = lin((0.74, 0.14, 0.11))
    yellow = lin((0.96, 0.78, 0.16))
    sash = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('SUBTRACT', u, g.math('MULTIPLY', v, 0.5)), 0.42)),
                            -60.0, 7.5))
    poster = g.mix(sash, red, yellow)
    # The disc on the left: in the panel's own units, so it is round on the panel that is twice as wide as it is high.
    du = g.math('MULTIPLY', g.math('SUBTRACT', u, 0.24), 2.0)
    dv = g.math('SUBTRACT', v, 0.5)
    r = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', du, du), g.math('MULTIPLY', dv, dv)))
    disc = g.clamp01(g.math('MULTIPLY_ADD', r, -80.0, 30.0))  # within 0.37
    poster = g.mix(disc, poster, lin((0.95, 0.95, 0.93)))
    square = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', du), -80.0, 14.0)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', dv), -80.0, 14.0)))
    poster = g.mix(square, poster, lin((0.14, 0.28, 0.62)))
    if variant != "intact":
        poster = g.mix(cc.cracks(g, 10.0), poster, lin((0.20, 0.18, 0.16)))
        poster = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, -2.8)), poster, lin((0.30, 0.26, 0.22)))
    albedo = cc.paint(g, mask, (steel, frame, poster))
    rough = cc.weighted(g, mask, (0.55, 0.5, 0.6))
    metallic = cc.weighted(g, mask, (0.3, 0.25, 0.0))
    normal = g.bump(g.noise(40.0, 3.0), 0.02, 0.003)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("posts", "sign_face"))
    variant = VARIANTS[variant_id]
    posts = Parts()
    face = Parts()
    _posts(posts)
    if variant == "ruins":
        # The posts bent over backwards; the panel off them, down on the ground face up in front.
        for vtx in posts.bm.verts:
            if vtx.co.z > 0.2:
                vtx.co = Matrix.Rotation(math.radians(55), 3, 'X') @ (vtx.co - Vector((0.0, POST_Y, 0.2))) + Vector((0.0, POST_Y, 0.2))
        frame = cc.made(posts, lambda: _panel(posts, face))
        fallen = {v for f in frame for v in f.verts} | set(face.bm.verts)
        m = Matrix.Rotation(math.radians(-88), 3, 'X')
        pivot, shift = Vector((0.0, FACE_Y, PANEL_Z[0])), Vector((0.05, -0.62, -PANEL_Z[0] + 0.04))
        for vtx in fallen:
            vtx.co = m @ (vtx.co - pivot) + pivot + shift
    else:
        _panel(posts, face, torn=variant == "damaged")
    return cc.finish(variant_id, [(posts, "posts"), (face, "sign_face")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes posts and sign_face."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
