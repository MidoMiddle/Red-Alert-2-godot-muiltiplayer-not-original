"""Chrono miner alliance_harvester: the Alliance's ore harvester (docs/ra2_units/allies/chrono_miner).

Size of the placeholder it replaces (UnitView.VehicleSize): 0.75 wide, 1 long, 0.42 high; the load strip of
HarvesterMarks floats at 0.46, so nothing rises above 0.44. Origin at the centre on the ground, front to +Y in
Blender (−Z in Godot). Tracked, no turret (game/data/factions/alliance/units.yaml). Budget 8000 triangles,
texture 1024.

Nodes (docs/art/model_standards.md, section 4):
    hull           tracks, chassis, the ore bin with its handle, the ring housing of the chrono core
      team_mask    band round the housing: the owner's colour
      spin_core    the chrono core, a sphere in the housing; its origin on its axis
      spin_drum    the crushing drum with its teeth; its origin on the axle
The game does not turn spin_* yet (M1, 4.9: working animations); the origins are ready for it.

The parts follow the reference picture given by the owner (a fan model of the mod Condition Red): a short
tracked chassis, a big core in a ring in front, a toothed drum on two arms ahead of it, a three-part bin with a
pipe handle behind. Proportions and details are our own, no emblems or text (docs/15_legal.md, section 3).

In Blender, after rts_common is on sys.path:
    import units.alliance_harvester as miner
    print(miner.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, attach, lin, save_blend, uv_together

ID = "alliance_harvester"
SIZE = 1024


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


ENAMEL, METAL, RUBBER, BIN, CORE = (_part(i) for i in range(5))

CORE_AT = Vector((0.0, 0.17, 0.235))    # centre of the chrono core
RING_Y = (-0.01, 0.13)                  # the housing along Y, round the axis through CORE_AT
DRUM_AT = Vector((0.0, 0.40, 0.09))     # axle of the drum


# --- geometry -------------------------------------------------------------------

def _hull():
    p = Parts()
    track = [(-0.42, 0.0), (0.12, 0.0), (0.18, 0.05), (0.18, 0.10), (0.14, 0.15),
             (-0.44, 0.15), (-0.49, 0.10), (-0.49, 0.05)]
    for s in (-1, 1):
        lo, hi = sorted((s * 0.24, s * 0.36))
        p.profile_x(track, lo, hi, RUBBER)                                     # track belt
        for y in (-0.33, -0.21, -0.09, 0.03):
            p.cylinder((s * 0.365, y, 0.055), 'X', 0.05, 0.008, METAL)         # road wheel
        p.cylinder((s * 0.365, -0.44, 0.075), 'X', 0.055, 0.008, METAL)        # drive sprocket
        p.cylinder((s * 0.365, 0.13, 0.075), 'X', 0.045, 0.008, METAL)         # idler
        lo, hi = sorted((s * 0.23, s * 0.37))
        p.box((lo, -0.5, 0.15), (hi, 0.19, 0.165), ENAMEL)                     # fender over the track
        # Arm from the chassis to the drum, and its bearing on the drum's end.
        p.bar((s * 0.3, 0.15, 0.14), (s * 0.3, DRUM_AT.y, DRUM_AT.z), 0.022, METAL)
        p.cylinder((s * 0.3, DRUM_AT.y, DRUM_AT.z), 'X', 0.05, 0.025, METAL)
    p.box((-0.24, -0.47, 0.05), (0.24, 0.2, 0.18), ENAMEL)                     # chassis between the tracks

    # The ore bin in three sections, its top edges chamfered.
    for x0, x1 in ((-0.3, -0.104), (-0.096, 0.096), (0.104, 0.3)):
        rings = []
        for z, inset in ((0.18, 0.0), (0.34, 0.0), (0.375, 0.03)):
            rings.append([Vector((x0 + inset * 0.4, -0.5 + inset, z)), Vector((x1 - inset * 0.4, -0.5 + inset, z)),
                          Vector((x1 - inset * 0.4, -0.09 - inset, z)), Vector((x0 + inset * 0.4, -0.09 - inset, z))])
        p.loft(rings, BIN)
    p.box((-0.14, -0.1, 0.18), (0.14, 0.0, 0.33), METAL)                       # neck from the bin to the housing
    for s in (-1, 1):
        p.cylinder((s * 0.17, -0.29, 0.405), 'Z', 0.018, 0.03, METAL, sides=10)  # handle post
    p.cylinder((0.0, -0.29, 0.432), 'X', 0.02, 0.19, METAL, sides=10)         # handle bar

    # Ring housing round the core, bevelled at both ends.
    y0, y1 = RING_Y
    p.turned((CORE_AT.x, 0.0, CORE_AT.z), 'Y', [(y0, 0.185), (y0 + 0.02, 0.205), (y1 - 0.02, 0.205), (y1, 0.185)],
             ENAMEL, sides=24)
    return p.finish("hull")


def _team():
    p = Parts()
    p.turned((CORE_AT.x, 0.0, CORE_AT.z), 'Y', [(RING_Y[0] + 0.025, 0.209), (RING_Y[0] + 0.075, 0.209)],
             ENAMEL, sides=24)                                                 # band on the rear of the housing
    return p.finish("team_mask")


def _core():
    p = Parts()
    p.sphere((0.0, 0.0, 0.0), 'Y', 0.17, CORE, sides=20, rings=10)
    p.cylinder((0.0, 0.17, 0.0), 'Y', 0.03, 0.01, METAL, sides=10)            # cap on the front pole
    return p.finish("spin_core")


def _drum():
    p = Parts()
    p.cylinder((0.0, 0.0, 0.0), 'X', 0.085, 0.27, METAL, sides=16)
    for x in (-0.27, 0.0, 0.27):
        p.cylinder((x, 0.0, 0.0), 'X', 0.095, 0.012, METAL, sides=16)        # rims
    for i in range(6):
        a = 2 * math.pi * (i + 0.5 * (i % 2)) / 6
        out = Vector((0.0, math.cos(a), math.sin(a)))
        for x in (-0.2, -0.1, 0.1, 0.2):
            p.bar(Vector((x, 0, 0)) + out * 0.075, Vector((x, 0, 0)) + out * 0.112, 0.012, METAL)  # tooth
    return p.finish("spin_drum")


# --- material -------------------------------------------------------------------

def _material():
    """Light enamel, grey metal, black rubber, an ochre ore bin and a steel core with pale blue seams."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(5)]

    grain = g.noise(40.0, 4.0)
    coord = g.object_coord()

    def lines(axis_name, per_cell, width):
        t = g.math('FRACT', g.math('MULTIPLY', g.axis(coord, axis_name), per_cell))
        return g.clamp01(g.math('MULTIPLY_ADD', t, -1.0 / width, 1.0))

    # Seams of the core: a grid over the sphere; panel lines on the bin.
    seams = g.math('MAXIMUM', lines('X', 14.0, 0.08), lines('Z', 14.0, 0.08))
    bin_lines = g.math('MULTIPLY', lines('Z', 25.0, 0.06), 0.6)

    enamel_col = g.mix(grain, lin((0.74, 0.76, 0.80)), lin((0.83, 0.85, 0.89)))
    metal_col = g.mix(grain, lin((0.36, 0.37, 0.40)), lin((0.47, 0.48, 0.51)))
    rubber_col = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.17, 0.17, 0.18)))
    bin_col = g.mix(bin_lines, g.mix(grain, lin((0.50, 0.37, 0.19)), lin((0.60, 0.46, 0.26))), lin((0.30, 0.22, 0.12)))
    core_col = g.mix(seams, g.mix(grain, lin((0.58, 0.62, 0.68)), lin((0.66, 0.70, 0.76))), lin((0.50, 0.78, 0.96)))

    albedo = enamel_col
    for m, col in zip(mask[1:], (metal_col, rubber_col, bin_col, core_col)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.45, 0.4, 0.88, 0.6, 0.3))
    metallic = weighted((0.1, 0.85, 0.0, 0.3, 0.9))
    normal = g.bump(g.noise(80.0, 3.0), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------

def build():
    hull = _hull()
    team = _team()
    core = _core()
    drum = _drum()
    attach(team, hull, (0.0, 0.0, 0.0))
    attach(core, hull, CORE_AT)
    attach(drum, hull, DRUM_AT)
    bpy.context.view_layer.update()

    meshes = [hull, team, core, drum]
    uv_together(meshes)
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
    base = os.path.join(rc.SRC_ROOT, "unit", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "unit", ID, ID + ".glb")
    save_blend(meshes, mat, imgs, os.path.join(base, ID + ".blend"))
    rc.export_glb(meshes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
