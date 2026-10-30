"""Shared steps of the industrial set (docs/industrial_elements): the bake into game/assets/models/industrial/<id>/, brick and concrete in
the material, rubble, the jagged stumps of walls and the soldiers of a garrison.

The set follows the city's (families/city_common.py): faces painted with surface parts, a storey to a cell upright, and the game squashes
a building upright (height_scale 0.35, game/data/visuals/models.yaml), so a model is about three times as tall here as it stands in the
game; the pipeline, a map object, is made at its own size.
"""

import math
import os
import random

import bpy
from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import lin

FOLDER = "industrial"  # game/assets/models/industrial/<id>/, assets_src/models/industrial/<id>/


def run(variant_id, meshes, mat, graph, size, fire_points=(), anchors=(), folder=FOLDER):
    """Bakes, puts the model in its place in the file, saves the .blend and exports the GLB into folder (industrial, or map for the
    civilian buildings of the map); returns a report.

    The GLB nodes are the meshes' names without the state's id, fire_1, fire_2… at fire_points and the empty nodes anchors: (name,
    point), where the game may stand smoke or steam."""
    nodes = rc.fire_nodes(fire_points) if fire_points else []
    empties = []
    for name, point in anchors:
        node = bpy.data.objects.new(f"{variant_id}_{name}", None)
        node.empty_display_type = 'PLAIN_AXES'
        node.empty_display_size = 0.2
        node.location = Vector(point)
        bpy.context.scene.collection.objects.link(node)
        empties.append(node)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}, size {tuple(round(v, 3) for v in meshes[0].dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, folder, variant_id)
    cc.repack(meshes, cc.ISLAND_GAP / size)
    imgs = rc.bake_material(meshes, graph, size, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + empties + nodes
    rc.arrange(objs, variant_id)
    glb = os.path.join(rc.GLB_ROOT, folder, variant_id, variant_id + ".glb")
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes + empties] + rc.fire_names(nodes)
    with rc.node_names(objs, names):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def clear(variant_id, nodes):
    """cc.clear, and the empty anchors of an earlier build."""
    cc.clear(variant_id, tuple(nodes) + ("smoke_anchor", "steam_anchor"))


# --- shapes -------------------------------------------------------------------------

def rubble(p, rnd, x0, x1, y0, y1, z, count, size, painted, height=0.0):
    """Count lumps of rubble over a rectangle at z, up to height higher, each about size across."""
    for _ in range(count):
        c = (rnd.uniform(x0, x1), rnd.uniform(y0, y1), z + rnd.uniform(0.0, height) + size * 0.2)
        s = (size * rnd.uniform(0.6, 1.4), size * rnd.uniform(0.5, 1.1), size * rnd.uniform(0.3, 0.7))
        cc.block(p, c, s, (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), rnd.uniform(0, math.pi)), painted)


def stumps(p, rnd, walls, z0, low, high, painted, step=0.22, gap=0.25, thick=0.1):
    """Jagged stumps of walls: walls are (a, b) segments in the plane, each cut in pieces about step long standing from z0 up to between
    low and high over it, a share gap of them gone."""
    for a, b in walls:
        a, b = Vector((a[0], a[1], 0.0)), Vector((b[0], b[1], 0.0))
        length = (b - a).length
        turn = math.atan2(b.y - a.y, b.x - a.x)
        n = max(1, int(length / step))
        for k in range(n):
            if rnd.random() < gap:
                continue
            h = rnd.uniform(low, high)
            c = a + (b - a) * ((k + 0.5) / n)
            cc.block(p, (c.x, c.y, z0 + h / 2), (length / n * 1.02, thick, h), (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04), turn), painted)


def barrels(p, points, out, painted, length=0.42):
    """A rifle barrel out of each point, along out and a little down: a soldier of the garrison at a window."""
    for a in points:
        a = Vector(a)
        p.bar(a, a + Vector(out) * length + Vector((0.0, 0.0, -0.04)), 0.017, painted)


def sandbags(p, xs, y, z, painted, rows=2):
    """Rows of sandbags along x at y, from z up."""
    for k in range(rows):
        for x in xs:
            cc.bag(p, (x + 0.12 * (k % 2), y, z + 0.08 * k + 0.04), 0.0, painted, length=0.22, width=0.14, height=0.08)


# --- material -----------------------------------------------------------------------

def brick(g, coord, colour_a, colour_b, mortar, course=10.0, length=7.0):
    """Brickwork in courses: course rows to a unit of height, length bricks to a unit along a wall (x + y, so every wall has them),
    each other course shifted by half a brick. Returns (albedo, the mortar mask)."""
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    rows = g.math('MULTIPLY', z, course)
    shift = g.math('MULTIPLY', g.math('MODULO', g.math('FLOOR', rows), 2.0), 0.5)
    along = g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), length), shift)

    def seam(t, width):
        f = g.math('FRACT', t)
        edge = g.math('MINIMUM', f, g.math('SUBTRACT', 1.0, f))
        return g.clamp01(g.math('MULTIPLY_ADD', edge, -1.0 / width, 1.0))

    joints = g.math('MAXIMUM', seam(rows, 0.1), seam(along, 0.06))
    tone = g.noise(14.0, 2.0)
    face = g.mix(tone, lin(colour_a), lin(colour_b))
    return g.mix(joints, face, lin(mortar)), joints


def soot(g, amount, scale=1.8):
    """A mask of soot patches: more of the surface under them the larger amount (0…1)."""
    return g.clamp01(g.math('MULTIPLY_ADD', g.noise(scale, 2.0), 6.0, -3.2 + 1.4 * amount))


def seed(variant_id):
    return random.Random(sum(map(ord, variant_id)))
