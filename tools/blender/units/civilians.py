"""The civilians of the map that get a skeleton (docs/map_elements/13_civilians): the first people rigged through Mixamo (M1, 4.13).

Made at the size of the beachgoers (families/beach_people.py, 0.56 tall) and painted the same way, but standing in a T-pose: the arms
straight out to the sides, palms down, the feet a little apart, as the Mixamo auto-rigger wants. Origin at the centre on the ground,
the front to +Y in Blender (-Z in Godot). One GLB per look:
    civ_man      a light casual jacket over a white shirt, jeans, white sneakers
No text, no logos, no team colour (nobody's). Node: "body". Budget 1500 triangles, texture 512.

The way to the game (docs/art/mixamo.md):
    1. run(): the T-pose model, baked; its .blend in assets_src/models/unit/<id>/ and the upload file mixamo/<id>_upload.fbx there:
       one mesh at human size (1.75 m), facing -Y as Mixamo reads it, its texture inside.
    2. The owner rigs it on mixamo.com and downloads the rigged model and the clips into mixamo/ (the list in docs/art/mixamo.md).
    3. The rigged model and its clips come back here and go to the game as one GLB (the next step of the script).

In Blender, after tools/blender is on sys.path:
    import units.civilians as civilians
    print(civilians.run("civ_man"))
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, save_blend

FOLDER = "unit"
SIZE = 512
HUMAN_HEIGHT = 1.75  # metres: the upload file is scaled to a man's height, the size the auto-rigger is made for

(SKIN, HAIR, TOP, BOTTOM, SHOE, ACCENT, DARK, WHITE) = (cc.part(i) for i in range(8))
PARTS = 8

# The look of each: skin, hair, top (the jacket), bottom (the trousers), shoe and accent (the shirt) colours.
VARIANTS = {
    "civ_man": dict(skin=(0.82, 0.62, 0.48), hair=(0.25, 0.17, 0.11), top=(0.70, 0.62, 0.46), bottom=(0.22, 0.30, 0.48),
                    shoe=(0.86, 0.86, 0.84), accent=(0.93, 0.93, 0.91)),
}

KNEE, HIP, WAIST, CHEST, SHOULDER, NECK = 0.15, 0.27, 0.315, 0.39, 0.435, 0.455
HEAD, HEAD_R = 0.5, 0.042
ARM = SHOULDER - 0.006  # the height of the arms held out


def ring(centre, rx, ry, sides=8):
    c = Vector(centre)
    return [c + Vector((math.cos(2 * math.pi * k / sides) * rx, math.sin(2 * math.pi * k / sides) * ry, 0.0)) for k in range(sides)]


def limb(p, points, radii, parts, sides=6):
    """A tapered limb through the points; each length painted with its part (parts has one fewer than points)."""
    for (a, b), (ra, rb), part in zip(zip(points, points[1:]), zip(radii, radii[1:]), parts):
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        u = d.cross(Vector((0.0, 1.0, 0.0)))
        if u.length < 1e-6:
            u = d.cross(Vector((1.0, 0.0, 0.0)))
        u = u.normalized()
        v = d.cross(u).normalized()
        p.loft([[c + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)]
                for c, r in ((a, ra), (b, rb))], part)


def _figure(p):
    # Legs a little apart, as the auto-rigger wants them: thighs and shins in the jeans, the sneakers on a dark sole.
    for side in (-1, 1):
        limb(p, [(0.024 * side, 0.0, HIP), (0.027 * side, 0.004, (HIP + KNEE) / 2), (0.03 * side, 0.006, KNEE), (0.034 * side, 0.0, 0.025)],
             [0.024, 0.02, 0.016, 0.013], [BOTTOM, BOTTOM, BOTTOM])
        x = 0.034 * side
        p.box((x - 0.012, -0.01, 0.004), (x + 0.012, 0.036, 0.016), SHOE)
        p.box((x - 0.0125, -0.0105, 0.0), (x + 0.0125, 0.0365, 0.004), DARK)
    # The hips in the jeans, the jacket from its hem over the hips up to the shoulders, the neck.
    p.loft([ring((0.0, 0.0, HIP - 0.01), 0.044, 0.028), ring((0.0, 0.0, HIP + 0.012), 0.042, 0.027)], BOTTOM)
    stations = [(HIP + 0.008, 0.047, 0.030), (WAIST, 0.042, 0.028), (CHEST, 0.048, 0.030), (SHOULDER, 0.055, 0.027)]
    for (z0, rx0, ry0), (z1, rx1, ry1) in zip(stations, stations[1:]):
        p.loft([ring((0.0, 0.0, z0), rx0, ry0), ring((0.0, 0.0, z1), rx1, ry1)], TOP)
    p.box((-0.012, 0.024, CHEST - 0.035), (0.012, 0.032, SHOULDER - 0.004), ACCENT)  # the shirt between the open lapels
    p.loft([ring((0.0, 0.0, SHOULDER), 0.03, 0.022), ring((0.0, 0.0, NECK + 0.01), 0.014, 0.014)], SKIN)
    # Arms straight out to the sides, palms down: the sleeves of the jacket, the hands.
    for side in (-1, 1):
        limb(p, [(0.05 * side, 0.0, ARM), (0.128 * side, 0.0, ARM), (0.198 * side, 0.0, ARM), (0.232 * side, 0.0, ARM)],
             [0.016, 0.013, 0.011, 0.009], [TOP, TOP, SKIN])
    # The head and short hair.
    p.sphere((0.0, 0.0, HEAD), 'Z', HEAD_R, SKIN, sides=10, rings=6)
    p.sphere((0.0, -0.01, HEAD + 0.006), 'Z', HEAD_R * 1.06, HAIR, sides=10, rings=4)
    p.box((-0.004, HEAD_R - 0.002, HEAD - 0.004), (0.004, HEAD_R + 0.006, HEAD + 0.008), SKIN)  # the nose


def _material(name, look):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.03)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(60.0, 2.0)
    shade = lambda c, k=0.12: g.mix(g.math('MULTIPLY', grain, k), lin(c), lin(tuple(v * 0.8 for v in c)))
    albedo = cc.paint(g, mask, (shade(look["skin"], 0.08), shade(look["hair"]), shade(look["top"]), shade(look["bottom"]),
                               lin(look["shoe"]), shade(look["accent"]), lin((0.04, 0.04, 0.05)), lin((0.94, 0.94, 0.92))))
    rough = cc.weighted(g, mask, (0.6, 0.7, 0.85, 0.85, 0.7, 0.75, 0.6, 0.8))
    g.finish(albedo, g.bump(grain, 0.03, 0.002), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    look = VARIANTS[variant_id]
    cc.clear(variant_id, ("body",))
    body = Parts()
    _figure(body)
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", look))


def export_upload(variant_id, meshes, path):
    """The file for mixamo.com: a copy of the model joined into one mesh, scaled to a man's height and turned to face -Y (Mixamo
    reads the front of a Blender model there), its texture embedded. The copy is removed afterwards."""
    copies = []
    for obj in meshes:
        copy = obj.copy()
        copy.data = obj.data.copy()
        bpy.context.scene.collection.objects.link(copy)
        copies.append(copy)
    rc.select_only(copies)
    if len(copies) > 1:
        bpy.ops.object.join()
    upload = bpy.context.view_layer.objects.active
    upload.name = variant_id + "_upload"
    height = max(v.co.z for v in upload.data.vertices)
    upload.data.transform(Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(HUMAN_HEIGHT / height, 4))
    upload.matrix_world = Matrix.Identity(4)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rc.select_only(upload)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             add_leaf_bones=False, path_mode='COPY', embed_textures=True)
    data = upload.data
    bpy.data.objects.remove(upload)
    bpy.data.meshes.remove(data)
    return f"  upload {path}: {os.path.getsize(path)} bytes, {round(HUMAN_HEIGHT, 2)} m tall"


def run(variant_id):
    """Builds and bakes the T-pose model, saves its .blend and writes the upload file for Mixamo; returns a report."""
    meshes, mat, graph = build(variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}, size {tuple(round(v, 3) for v in meshes[0].dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, FOLDER, variant_id)
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    save_blend(meshes, mat, imgs, os.path.join(base, variant_id + ".blend"))
    report.append(export_upload(variant_id, meshes, os.path.join(base, "mixamo", variant_id + "_upload.fbx")))
    return "\n".join(report)
