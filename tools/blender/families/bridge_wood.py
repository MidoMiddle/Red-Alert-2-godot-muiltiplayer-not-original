"""The low wooden bridge, bridge_wood_low (docs/map_elements/05_bridges), in the pieces the game puts together along a bridge: a span
of it a cell long and three cells across (the bridge is three tanks wide), whole or broken. After RA2's small wooden bridge (the
owner's pictures, 2026-10-30): a deck of planks laid across, two heavy timbers along it where the wheels run and a curb along each
edge, on stringers and piles in the water; at the banks the deck bends down onto the shore.

Every piece is made along x (the length of the bridge), its middle cell on the origin at the water line, the deck's top at DECK; the
game turns a piece for a bridge along y, and the end pieces so their bank lies outward:
    bridge_wood_span        a span of the middle
    bridge_wood_end         the last span at a bank, its ramp going down onto the shore at +x
    bridge_wood_broken      a span standing by a fallen one: its edge at +x torn, planks short and splintered, one hanging down, a
                            timber and a stringer broken off; all of it inside its own cell
    bridge_wood_end_broken  what is left at a bank when its span fell: the ramp on the shore, its end ragged at the water
A fallen span leaves nothing on the water (the owner, 2026-10-30: no wood afloat in the way): no piece for it.
Node "body". Budget 1500 triangles a piece, texture 1024 (docs/map_elements/05_bridges/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.bridge_wood as bw
    print(bw.run_all())
"""

import math
import os
import random

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 1024
NAMES = ("bridge_wood_span", "bridge_wood_end", "bridge_wood_broken", "bridge_wood_end_broken")

PLANK, TIMBER, IRON, PILE = (cc.part(i) for i in range(4))
PARTS = 4

DECK = 0.06  # the top of the planks over the water line: units on the bridge stand at the ground's height, just under
PLANK_T = 0.03
HALF = 1.42  # the planks' half length across the bridge
PLANKS = 8  # planks along a cell
WHEEL_Y = 0.55  # the timbers where the wheels run
CURB_Y = 1.36
STRINGERS = (-1.1, 0.0, 1.1)
RAMP = 0.55  # how far the ramp of an end runs onto the bank, past the cell


def _plank(p, x, half_width, y0, y1, z, tilt=(0.0, 0.0, 0.0)):
    cc.block(p, (x, (y0 + y1) / 2, z - PLANK_T / 2), (2 * half_width, y1 - y0, PLANK_T), tilt, PLANK)


def _deck(p, rnd, x0, x1, torn=None):
    """Planks across from x0 to x1; torn: the x past which they are broken off, each at its own length."""
    step = 1.0 / PLANKS
    for k in range(int(round((x1 - x0) / step))):
        x = x0 + step * (k + 0.5)
        y0, y1 = -HALF + rnd.uniform(0.0, 0.04), HALF - rnd.uniform(0.0, 0.04)
        if torn is not None and x > torn:
            # Short and splintered: a piece from one side or the other, or nothing.
            if rnd.random() < 0.35:
                continue
            if rnd.random() < 0.5:
                y1 = y0 + rnd.uniform(0.4, 1.6)
            else:
                y0 = y1 - rnd.uniform(0.4, 1.6)
        _plank(p, x, step * 0.4, y0, y1, DECK)


def _frame(p, x0, x1, broken_at=None):
    """The stringers under the planks, the timbers and curbs on them, iron bands on the timbers."""
    for y in STRINGERS:
        end = x1 if broken_at is None or y != 0.0 else broken_at
        p.box((x0, y - 0.05, -0.05), (end, y + 0.05, DECK - PLANK_T), TIMBER)
    for y in (-WHEEL_Y, WHEEL_Y):
        end = x1 if broken_at is None or y > 0 else broken_at
        p.box((x0, y - 0.07, DECK), (end, y + 0.07, DECK + 0.04), TIMBER)
    for y in (-CURB_Y, CURB_Y):
        end = x1 if broken_at is None or y < 0 else broken_at + 0.15
        p.box((x0, y - 0.05, DECK), (end, y + 0.05, DECK + 0.08), TIMBER)
        p.box((x0 + 0.08, y - 0.055, DECK + 0.02), (x0 + 0.12, y + 0.055, DECK + 0.085), IRON)


def _piles(p, x, broken=False):
    for y in STRINGERS:
        top = -0.05 if not broken else -0.2
        p.cylinder((x, y, (top - 0.45) / 2), 'Z', 0.05, (top + 0.45) / 2, PILE, sides=8)
    if not broken:
        p.bar((x, -1.1, -0.35), (x, 1.1, -0.12), 0.02, PILE)  # a cross brace


def _ramp(p, rnd, x0, length, stub=False):
    """The deck bending down from the span onto the bank, from x0 out to x0 + length: planks, curbs and timbers following it."""
    steps = 5
    def z(t):
        return DECK * (1.0 - t) - 0.015 * t  # down to just under the ground at the bank

    for k in range(steps):
        t0, t1 = k / steps, (k + 1) / steps
        xa, xb = x0 + length * t0, x0 + length * t1
        angle = math.atan2(z(t0) - z(t1), xb - xa)
        _plank(p, (xa + xb) / 2, (xb - xa) * 0.42, -HALF + 0.02, HALF - 0.02, (z(t0) + z(t1)) / 2, (0.0, angle, 0.0))
    for y, h, half in ((-CURB_Y, 0.08, 0.05), (CURB_Y, 0.08, 0.05), (-WHEEL_Y, 0.04, 0.07), (WHEEL_Y, 0.04, 0.07)):
        rings = []
        for t in (0.0, 0.5, 1.0):
            x, base = x0 + length * t, z(t)
            rings.append([Vector((x, y - half, base)), Vector((x, y + half, base)), Vector((x, y + half, base + h * (1 - 0.6 * t))),
                          Vector((x, y - half, base + h * (1 - 0.6 * t)))])
        p.loft(rings, TIMBER)
    # The sill the ramp rests on at the bank.
    p.box((x0 + length - 0.08, -HALF, -0.06), (x0 + length + 0.04, HALF, 0.0), TIMBER)


def build(name):
    cc.clear(name, ("body",))
    rnd = random.Random(NAMES.index(name) + 7)
    p = Parts()
    if name == "bridge_wood_span":
        _deck(p, rnd, -0.5, 0.5)
        _frame(p, -0.5, 0.5)
        _piles(p, 0.0)
    elif name == "bridge_wood_end":
        _deck(p, rnd, -0.5, 0.5)
        _frame(p, -0.5, 0.5)
        _piles(p, 0.0)
        _ramp(p, rnd, 0.5, RAMP)
    elif name == "bridge_wood_broken":
        _deck(p, rnd, -0.5, 0.5, torn=0.1)
        _frame(p, -0.5, 0.45, broken_at=0.2)
        _piles(p, -0.1)
        # A plank and a timber hanging from the torn edge, inside the span's own cell: nothing reaches over the fallen one.
        cc.block(p, (0.36, 0.2, -0.02), (0.08, 1.3, PLANK_T), (0.6, 0.4, 0.3), PLANK)
        cc.block(p, (0.3, WHEEL_Y, 0.0), (0.36, 0.14, 0.04), (0.0, 0.45, 0.0), TIMBER)
        cc.block(p, (0.3, 0.0, -0.06), (0.32, 0.1, 0.1), (0.0, 0.5, 0.1), TIMBER)
    elif name == "bridge_wood_end_broken":
        # What stands at the bank: the ramp on the shore, nothing over the water; a few planks of its edge splintered short.
        _ramp(p, rnd, 0.5, RAMP)
        for y in (-0.9, 0.2, 1.0):
            cc.block(p, (0.56, y, DECK * 0.9), (0.1, rnd.uniform(0.3, 0.6), PLANK_T), (0.0, 0.1, rnd.uniform(-0.3, 0.3)), PLANK)
    mesh = p.finish(f"{name}_body")
    rc.select_only([mesh])
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together([mesh])
    cc.repack([mesh])
    mat, graph = _material(name + "_mat", name)
    mesh.data.materials.append(mat)
    return mesh, mat, graph


def _material(name, piece):
    """Weathered planks, darker timbers, black iron, wet dark piles; the broken pieces scorched and darker."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    y = g.axis(coord, 'Y')
    grain = g.noise(30.0, 4.0)
    streak = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', y, 24.0)), 0.5)), -6.0, 3.2))
    plank = g.mix(g.noise(6.0, 3.0), lin((0.42, 0.29, 0.17)), lin((0.52, 0.37, 0.22)))
    plank = g.mix(g.math('MULTIPLY', streak, 0.4), plank, lin((0.30, 0.20, 0.12)))
    timber = g.mix(grain, lin((0.26, 0.17, 0.10)), lin((0.33, 0.22, 0.13)))
    iron = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.16, 0.15, 0.15)))
    pile = g.mix(grain, lin((0.16, 0.13, 0.10)), lin((0.22, 0.18, 0.13)))
    albedo = cc.paint(g, mask, (plank, timber, iron, pile))
    if piece in ("bridge_wood_broken", "bridge_wood_end_broken"):
        soot = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 3.0), 4.0, -1.8))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.7), albedo, lin((0.08, 0.06, 0.05)))
    rough = cc.weighted(g, mask, (0.85, 0.8, 0.5, 0.7))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.6, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', streak, -1.0), g.noise(60.0, 3.0)), 0.06, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def run(name):
    mesh, mat, graph = build(name)
    base = os.path.join(rc.SRC_ROOT, "map", name)
    report = [f"{name}: triangles {rc.tri_count(mesh)}, size {tuple(round(v, 3) for v in mesh.dimensions)}"]
    cc.repack([mesh], cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material([mesh], graph, SIZE, name, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    rc.arrange([mesh], name)
    glb = os.path.join(rc.GLB_ROOT, "map", name, name + ".glb")
    with rc.node_names([mesh], ["body"]):
        rc.save_blend([mesh], mat, imgs, os.path.join(base, name + ".blend"))
        rc.export_glb([mesh], glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(name) for name in NAMES)
