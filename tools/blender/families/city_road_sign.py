"""City road signs: element 16 of the city set (docs/city_elements/16_road_sign), three kinds and their states (appearance.md, 4). City
climate: dry daylight, no snow. A map object (road_sign, game/data/map/objects.yaml): the game picks one of the kinds for each cell.

Origin at the centre of the cell on the ground, the sign's face to -Y. Made at the size the game draws it, as the phone booth (the game
does not squash map objects): a thin post with the sign's top at 0.74, not the prompt's 1.5. The kinds, by shape and colour only, no
text, no letters, no figures of speed (prompts.md, 3 and 4):
    stop          a red octagon in a thin white border
    yield         a white triangle point down in a thick red border
    pedestrians   a blue square in a thin white border with a white walking figure
Each sign has a grey back plate, so its back reads as a sign's back. No emblems.
Nodes: "post" (the post, the clamps and the back plate), "sign_face" (the face, prompts.md, 4). One GLB per kind and state:
    city_road_sign_<kind>           whole
    city_road_sign_<kind>_damaged   bent: the post bent over, the sign turned round on it (appearance.md, 4: «погнут»)
    city_road_sign_<kind>_ruins     fallen: lying on the ground (appearance.md, 4: «упал»)
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the bent and the fallen ones wait for the client
to show them.
Budget 500 triangles a sign, texture 512 (docs/city_elements/16_road_sign/prompts.md, 1).

In Blender, after tools/blender is on sys.path:
    import families.city_road_sign as crs
    print(crs.run("city_road_sign_stop"))   # or crs.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 512
KINDS = ("stop", "yield", "pedestrians")
VARIANTS = {f"city_road_sign_{kind}{suffix}": (kind, state) for kind in KINDS
            for suffix, state in (("", "intact"), ("_damaged", "damaged"), ("_ruins", "ruins"))}

GREY, RED, WHITE, BLUE = (cc.part(i) for i in range(4))
PARTS = 4

POST_TOP = 0.66
SIGN_C = Vector((0.0, -0.025, 0.6))  # the middle of the sign
R = 0.11  # the sign's size: the radius of the octagon, half the square's side


def _outline(kind, size):
    """The sign's outline in its own plane (x across, z up), round its middle."""
    if kind == "stop":
        return [(size * math.cos(math.radians(22.5 + 45 * k)), size * math.sin(math.radians(22.5 + 45 * k))) for k in range(8)]
    if kind == "yield":
        s = size * 1.25
        return [(-s, s * 0.58), (0.0, -s * 1.15), (s, s * 0.58)]
    return [(-size, -size), (size, -size), (size, size), (-size, size)]


def _plate(p, kind, size, y0, y1, painted, centre=SIGN_C):
    p.loft([[Vector((centre.x + x, centre.y + y, centre.z + z)) for x, z in _outline(kind, size)] for y in (y0, y1)], painted)


def _post(p):
    p.box((-0.012, -0.012, 0.0), (0.012, 0.012, POST_TOP), GREY)
    for z in (SIGN_C.z - 0.06, SIGN_C.z + 0.06):
        p.box((-0.02, -0.02, z - 0.01), (0.02, 0.012, z + 0.01), GREY)  # the clamps


def _back(p, kind):
    _plate(p, kind, R + 0.012, -0.016, -0.01, GREY)


def _face(p, kind):
    """The face: a border plate and a smaller inner plate in front of it; the walking figure on the pedestrians' sign."""
    border, inner = {"stop": (WHITE, RED), "yield": (RED, WHITE), "pedestrians": (WHITE, BLUE)}[kind]
    shrink = {"stop": 0.012, "yield": 0.03, "pedestrians": 0.012}[kind]
    _plate(p, kind, R + 0.012, -0.022, -0.016, border)
    _plate(p, kind, R + 0.012 - shrink, -0.026, -0.022, inner)
    if kind == "pedestrians":
        c, y = SIGN_C, SIGN_C.y - 0.028
        p.cylinder((c.x + 0.005, y, c.z + 0.055), 'Y', 0.016, 0.002, WHITE, sides=8)  # the head
        for a, b in (((0.005, 0.035), (-0.005, -0.015)),   # the body
                     ((-0.005, -0.015), (-0.035, -0.07)), ((-0.005, -0.015), (0.03, -0.07)),   # the legs, mid-stride
                     ((0.002, 0.02), (-0.035, -0.005)), ((0.002, 0.02), (0.035, 0.0))):    # the arms
            p.bar((c.x + a[0], y, c.z + a[1]), (c.x + b[0], y, c.z + b[1]), 0.006, WHITE)


def _transform(parts, m, pivot, shift=Vector()):
    for p in parts:
        for v in p.bm.verts:
            v.co = m @ (v.co - pivot) + pivot + shift


def _material(name):
    """Grey galvanised steel, flat sign colours with a little wear."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    grey = g.mix(grain, lin((0.54, 0.55, 0.57)), lin((0.62, 0.63, 0.65)))
    red = g.mix(grain, lin((0.74, 0.12, 0.10)), lin((0.80, 0.16, 0.13)))
    white = g.mix(grain, lin((0.92, 0.92, 0.90)), lin((0.96, 0.96, 0.94)))
    blue = g.mix(grain, lin((0.10, 0.30, 0.66)), lin((0.14, 0.36, 0.72)))
    albedo = cc.paint(g, mask, (grey, red, white, blue))
    rough = cc.weighted(g, mask, (0.5, 0.45, 0.45, 0.45))
    metallic = cc.weighted(g, mask, (0.3, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(40.0, 3.0), 0.01, 0.002)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    cc.clear(variant_id, ("post", "sign_face"))
    kind, state = VARIANTS[variant_id]
    post = Parts()
    face = Parts()
    _post(post)
    _back(post, kind)
    _face(face, kind)
    if state == "damaged":
        # The sign turned round on the post, then the post bent over above its foot.
        _transform([face], Matrix.Rotation(math.radians(55), 3, 'Z'), Vector((0.0, 0.0, 0.0)))
        for v in post.bm.verts:
            if v.co.z > SIGN_C.z - 0.1:
                v.co = Matrix.Rotation(math.radians(55), 3, 'Z') @ v.co
        bend, pivot = Matrix.Rotation(math.radians(24), 3, 'X'), Vector((0.0, 0.0, 0.08))
        for v in list(post.bm.verts) + list(face.bm.verts):
            if v.co.z > pivot.z:  # the foot stays where it stands
                v.co = bend @ (v.co - pivot) + pivot
    elif state == "ruins":
        # Fallen: the post on the ground, the sign face up.
        _transform([post, face], Matrix.Rotation(math.radians(-88), 3, 'X'), Vector((0.0, 0.0, 0.0)), Vector((0.0, -0.3, 0.035)))
    return cc.finish(variant_id, [(post, "post"), (face, "sign_face")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes post and sign_face."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
