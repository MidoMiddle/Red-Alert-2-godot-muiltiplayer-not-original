"""Alliance naval shipyard: the Alliance's harbor on deep water (docs/ra2_buildings/allies/naval_shipyard).

Footprint 4x4 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the water: the water
is the ground's surface there (TerrainBuilder), so the model stands on z 0 and has no concrete plinth. The composition follows the
owner's references; the parts are our own:
    pods      four round pods on the diagonals, standing in the water: a dark leg, a grey drum with white coils, a blue dome
    hub       a round deck between them on spoked fins, a railing round it, a green column in its middle
    crane     on the column a turning crane: a housing with a curved shield, a yellow lattice jib out to a drum, a cable and a claw;
              it swings left and right and lets the claw down and up while the harbor builds or repairs a ship
No emblems, no text. The domes of the pods and the shield of the crane take the owner's colour, where the references have the blue.
Height 2.05 to the top of the housing. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the pods, the deck, the fins, the railing and the column
    crane           the housing and the jib: turns about its Y at its origin, on the column's axis
    team_mask_crane under crane: the shield, in the owner's colour
    cable, hook     under crane: the cable and the claw, their origins at the jib's end; the cable hangs down along -Y
    team_mask       the domes of the pods: the game paints them in the owner's colour

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_naval_shipyard as ans
    print(ans.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_naval_shipyard"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


GREY, WHITE, DARK, GREEN, YELLOW, TEAM = (_part(i) for i in range(6))
PARTS = 6

PODS = [Vector((sx * 0.95, sy * 0.95, 0.0)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))]
POD_R = 0.38
POD_TOP = 0.9
DECK_Z = (0.42, 0.52)
COLUMN_TOP = 1.45
PIVOT = Vector((0.0, 0.0, COLUMN_TOP))
JIB = 1.75  # the jib's reach from the column's axis
JIB_Z = 0.4  # the jib's height over the pivot
HOOK_Z = 0.95  # where the claw hangs at rest


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


# --- body -------------------------------------------------------------------------

def _pod(p, c):
    p.turned(c, 'Z', [(-0.05, 0.3), (0.25, 0.3), (0.3, 0.36), (0.36, POD_R), (POD_TOP - 0.06, POD_R), (POD_TOP, POD_R - 0.04),
                      (POD_TOP, 0.3)], GREY, sides=24)
    out = Vector((c.x, c.y, 0.0)).normalized()
    side = Vector((-out.y, out.x, 0.0))
    for s in (-1, 1):  # white coils on its sides
        q = c + side * s * (POD_R + 0.01)
        for i in range(4):
            z = 0.48 + i * 0.07
            p.turned(q + Vector((0.0, 0.0, z)), 'Z', [(0.0, 0.05), (0.035, 0.05)], WHITE, sides=10)


def _hub(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_Z[0], 0.95), (DECK_Z[1], 0.95), (DECK_Z[1], 0.3)], GREY, sides=40)  # the deck
    for i in range(32):  # its railing
        a = 2 * math.pi * i / 32
        p.box((0.92 * math.cos(a) - 0.01, 0.92 * math.sin(a) - 0.01, DECK_Z[1]), (0.92 * math.cos(a) + 0.01, 0.92 * math.sin(a) + 0.01, DECK_Z[1] + 0.12), DARK)
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_Z[1] + 0.11, 0.93), (DECK_Z[1] + 0.13, 0.93), (DECK_Z[1] + 0.13, 0.91), (DECK_Z[1] + 0.11, 0.91)], DARK, sides=40)
    for i in range(12):  # the spoked fins under the deck, down to the water
        a = 2 * math.pi * (i + 0.5) / 12
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0)) * 0.025
        prof = [(0.2, 0.0), (0.55, 0.0), (0.55, DECK_Z[0]), (0.2, 0.75)]
        p.loft([[out * r + Vector((0.0, 0.0, z)) + side * s for r, z in prof] for s in (-1, 1)], GREEN)
    # The green column, flared at the foot, banded.
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_Z[1], 0.34), (0.7, 0.24), (0.9, 0.2), (COLUMN_TOP - 0.1, 0.2), (COLUMN_TOP - 0.05, 0.26),
                                     (COLUMN_TOP, 0.26)], GREEN, sides=24)
    for z in (0.78, 0.92):
        p.turned((0.0, 0.0, 0.0), 'Z', [(z - 0.025, 0.225), (z + 0.025, 0.225)], DARK, sides=24)


# --- the crane --------------------------------------------------------------------

def _crane_nodes():
    """The crane, its shield, the cable and the claw; built along +X (east) from the pivot, at rest so."""
    c = PIVOT
    crane = Parts()
    crane.turned(c, 'Z', [(0.0, 0.26), (0.06, 0.26)], DARK, sides=24)  # the slewing ring
    crane.box((c.x - 0.3, c.y - 0.2, c.z + 0.06), (c.x + 0.12, c.y + 0.2, c.z + 0.5), GREY)  # the housing
    crane.box((c.x - 0.22, c.y - 0.06, c.z + 0.5), (c.x - 0.06, c.y + 0.06, c.z + 0.56), WHITE)  # the cab on top
    crane.sphere((c.x - 0.14, c.y, c.z + 0.56), 'Z', 0.06, WHITE, sides=12, rings=6)
    root = c + Vector((0.1, 0.0, JIB_Z))
    tip = c + Vector((JIB, 0.0, JIB_Z))
    for y, z in ((-0.08, -0.06), (0.08, -0.06), (-0.06, 0.08), (0.06, 0.08)):  # the jib's four chords
        crane.bar(root + Vector((0.0, y, z)), tip + Vector((-0.06, y * 0.6, z * 0.7)), 0.018, YELLOW)
    n = 8
    for i in range(n + 1):
        t = i / n
        q = root.lerp(tip + Vector((-0.06, 0.0, 0.0)), t)
        k = 1.0 - 0.4 * t
        for (y0, z0), (y1, z1) in ((((-0.08, -0.06)), (0.08, -0.06)), ((-0.06, 0.08), (0.06, 0.08)), ((-0.08, -0.06), (-0.06, 0.08)),
                                   ((0.08, -0.06), (0.06, 0.08))):
            crane.bar(q + Vector((0.0, y0 * k, z0 * (1 - 0.3 * t))), q + Vector((0.0, y1 * k, z1 * (1 - 0.3 * t))), 0.01, YELLOW)
        if i < n:
            q2 = root.lerp(tip + Vector((-0.06, 0.0, 0.0)), (i + 1) / n)
            crane.bar(q + Vector((0.0, -0.08 * k, -0.06)), q2 + Vector((0.0, 0.0, 0.08 * (1 - 0.3 * t))), 0.009, YELLOW)
            crane.bar(q + Vector((0.0, 0.08 * k, -0.06)), q2 + Vector((0.0, 0.0, 0.08 * (1 - 0.3 * t))), 0.009, YELLOW)
    crane.turned(tip, 'Y', [(-0.07, 0.09), (0.07, 0.09)], DARK, sides=16)  # the drum at the end
    crane = crane.finish(PREFIX + "crane")

    shield = Parts()  # a curved shield round the back of the housing
    steps = 10
    outer = [Vector((c.x - 0.3 - 0.12 * math.sin(math.pi * i / steps), c.y + 0.24 * math.cos(math.pi * i / steps), 0.0)) for i in range(steps + 1)]
    inner = [Vector((q.x + 0.05, q.y * 0.85, 0.0)) for q in reversed(outer)]
    shield.loft([[q + Vector((0.0, 0.0, c.z + z)) for q in outer + inner] for z in (0.08, 0.62)], TEAM)
    shield = shield.finish(PREFIX + "team_mask_crane")

    end = tip - Vector((0.0, 0.0, 0.09))
    drop = end.z - (HOOK_Z + 0.2)
    cable = Parts()
    cable.turned((0.0, 0.0, 0.0), 'Z', [(-drop, 0.012), (0.0, 0.012)], WHITE, sides=6)
    cable = cable.finish(PREFIX + "cable")
    hook = Parts()
    hook.sphere((0.0, 0.0, -drop - 0.06), 'Z', 0.07, YELLOW, sides=12, rings=6)  # the claw's ball
    for k in range(3):
        a = 2 * math.pi * k / 3
        out = Vector((math.cos(a), math.sin(a), 0.0))
        hook.bar(Vector((0, 0, -drop - 0.1)) + out * 0.04, Vector((0, 0, -drop - 0.16)) + out * 0.11, 0.016, YELLOW)
        hook.bar(Vector((0, 0, -drop - 0.16)) + out * 0.11, Vector((0, 0, -drop - 0.24)) + out * 0.06, 0.016, YELLOW)
    hook = hook.finish(PREFIX + "hook")

    crane.data.transform(Matrix.Translation(-c))
    crane.location = c
    shield.data.transform(Matrix.Translation(-c))
    shield.parent = crane
    for node in (cable, hook):
        node.parent = crane
        node.location = end - c
    bpy.context.view_layer.update()
    return crane, shield, cable, hook


def _team(p):
    for c in PODS:
        p.turned(c, 'Z', [(POD_TOP, 0.32), (POD_TOP + 0.08, 0.3), (POD_TOP + 0.15, 0.22), (POD_TOP + 0.19, 0.12), (POD_TOP + 0.2, 0.02)],
                 TEAM, sides=24)


# --- material -----------------------------------------------------------------------

def _material():
    """Grey steel in panels, white, dark, a sea-green column, yellow, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(30.0, 4.0)
    z = g.axis(g.object_coord(), 'Z')
    seams = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 8.0)), -25.0, 1.0))
    # The pods' legs and the fins' feet are dark under 0.25, wet from the water.
    wet = g.clamp01(g.math('MULTIPLY_ADD', z, -12.0, 3.0))

    grey = g.mix(seams, g.mix(grain, lin((0.50, 0.52, 0.55)), lin((0.58, 0.60, 0.63))), lin((0.32, 0.33, 0.36)))
    white = g.mix(grain, lin((0.82, 0.83, 0.85)), lin((0.90, 0.91, 0.93)))
    dark = g.mix(grain, lin((0.10, 0.11, 0.13)), lin((0.15, 0.16, 0.18)))
    green = g.mix(grain, lin((0.40, 0.50, 0.42)), lin((0.48, 0.58, 0.50)))
    yellow = g.mix(grain, lin((0.92, 0.80, 0.16)), lin((0.98, 0.88, 0.24)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = grey
    for m, col in zip(mask[1:], (white, dark, green, yellow, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(g.math('MULTIPLY', wet, g.math('ADD', mask[0], mask[3])), albedo, lin((0.16, 0.18, 0.20)))

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.45, 0.4, 0.6, 0.5, 0.4, 0.35))
    metallic = weighted((0.7, 0.3, 0.5, 0.4, 0.3, 0.4))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', seams, mask[0]), 0.6)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _pack(objs):
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    """The meshes (each parent before its children) and the material."""
    _clear()
    body = Parts()
    for c in PODS:
        _pod(body, c)
    _hub(body)
    body = body.finish(PREFIX + "body")
    crane, shield, cable, hook = _crane_nodes()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    meshes = [body, crane, shield, cable, hook, team]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    # The root takes the parts with no parent of their own; the shield, the cable and the claw stay under the crane.
    top = [obj for obj in meshes if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in meshes:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(meshes, [o.name[len(PREFIX):] for o in meshes]):
        rc.save_blend(meshes, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(meshes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
