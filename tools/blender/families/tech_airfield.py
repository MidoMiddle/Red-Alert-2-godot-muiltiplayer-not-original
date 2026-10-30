"""Tech airfield, tech_airfield (docs/map_elements/12_tech_buildings): the neutral airfield an engineer takes; its owner gets a paradrop
of infantry every four minutes (game/data/neutral/tech.yaml). The kinds of parts follow the owner's pictures of RA2's tech airport
(2026-10-30): a pad with landing marks, a hangar under an arched roof, a control tower, a flag; the shapes and the layout are ours
(generation_requirements.md, section 8).

Footprint 3x3 cells, origin at the centre of the footprint on the ground, the front to -Y; made at the size the game draws it, as the
oil derrick (not squashed). The parts:
    pad         a concrete pad in slabs over the footprint (the base node), two landing circles on its front half, yellow rings with a
                white four-pointed star in each, a row of small lights along its front edge
    hangar      at the back right: low walls of grey sheet under an arched roof of corrugated metal, ribs along it, big dark doors
                in its front gable with a white frame, a small door at the side
    tower       at the back left: a square shaft of light concrete with a dark band of narrow windows up each side, a glass cabin
                on top under a flat roof, an aerial with a red light
    yard        crates and drums by the hangar, a windsock on a pole at the front right, a small helicopter on the right pad
    team mask   the flag on its pole by the hangar and the rim of the tower's roof: the owner's colour
Nodes: "body", "team_mask", "base". One GLB per state:
    tech_airfield            whole
    tech_airfield_abandoned  critically damaged: holes in the hangar's roof and its doors torn, the cabin's glass out and the aerial
                             bent, crates thrown about, the helicopter burnt out on its side, soot; fire on the hangar, the tower and
                             the crates (fire_1..3)
    tech_airfield_ruins      destroyed: the stump of the tower, the bare ribs of the hangar half down, heaps, the pad
Budget 8000 triangles, texture 2048 (docs/map_elements/12_tech_buildings/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.tech_airfield as ta
    print(ta.run_all())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 2048
VARIANTS = {
    "tech_airfield": "intact",
    "tech_airfield_abandoned": "abandoned",
    "tech_airfield_ruins": "ruins",
}

CONCRETE, SHEET, ROOF, GLASS, DARK, WHITE, YELLOW, RED, WOOD, DRUM, TEAM, TOWER, ORANGE, OLIVE = (cc.part(i) for i in range(14))
PARTS = 14

PAD = 0.06
HANGAR = (-0.1, 1.38, -0.05, 1.35)  # x0, x1, y0, y1
WALL = PAD + 0.32
ARCH = 0.42  # the rise of the roof over the walls
TOWER = (-1.3, -0.92, 0.85, 1.23)  # x0, x1, y0, y1 of the shaft
TOWER_TOP = 1.55
CABIN = 0.28
CIRCLES = ((-0.7, -0.75), (0.65, -0.8))
FIRE_POINTS = ((0.6, 0.6, WALL + ARCH), (-1.11, 1.04, TOWER_TOP + CABIN), (-0.35, 0.05, PAD + 0.2))


def _arch(t):
    """The roof over the hangar's span: t from -1 to 1 across it, its height over the walls."""
    return ARCH * math.sqrt(max(0.0, 1.0 - t * t))


def _hangar(p, variant):
    x0, x1, y0, y1 = HANGAR
    cx, half = (x0 + x1) / 2, (x1 - x0) / 2
    if variant == "ruins":
        # Bare ribs, half of them down; a heap of sheets inside.
        for k, y in enumerate(cc.steps(y0 + 0.05, y1 - 0.05, 0.32)):
            if k % 2:
                cc.block(p, (cx, y, PAD + 0.05), (x1 - x0, 0.04, 0.04), (0.0, 0.0, 0.2 * k), DARK)
                continue
            pts = [Vector((cx + half * t, y, WALL + _arch(t))) for t in (-1.0, -0.6, -0.2, 0.2, 0.6, 1.0)]
            for a, b in zip(pts, pts[1:]):
                p.bar(a, b, 0.02, DARK)
            for t in (-1.0, 1.0):
                p.bar((cx + half * t, y, PAD), (cx + half * t, y, WALL), 0.02, DARK)
        for c, s, t in (((0.6, 0.6, PAD + 0.08), (1.0, 0.7, 0.16), (0.1, 0.05, 0.3)), ((0.3, 0.2, PAD + 0.06), (0.6, 0.4, 0.12), (0.2, 0.1, 1.2))):
            cc.block(p, c, s, t, ROOF)
        return
    # Walls of sheet, closed, and the roof: a loft of the arch along y, its underside the walls' top.
    p.box((x0, y0, PAD), (x1, y1, WALL), SHEET)
    n = 10
    rings = []
    for y in (y0 - 0.04, y1 + 0.04):
        ring = [Vector((cx + (half + 0.04) * (2 * i / n - 1), y, WALL - 0.02 + _arch(2 * i / n - 1) * 1.02)) for i in range(n + 1)]
        ring += [Vector((cx + (half - 0.02) * (1 - 2 * i / n), y, WALL - 0.02)) for i in range(n + 1)]
        rings.append(ring)
    p.loft(rings, ROOF)
    # Ribs over the roof, along its arch.
    for y in cc.steps(y0 + 0.1, y1 - 0.1, 0.3):
        pts = [Vector((cx + (half + 0.05) * t, y, WALL + _arch(t) * 1.02 + 0.005)) for t in (-1.0, -0.6, -0.2, 0.2, 0.6, 1.0)]
        for a, b in zip(pts, pts[1:]):
            p.bar(a, b, 0.012, WHITE)
    # The front gable over the doors, and the doors.
    arch = [(cx + half * t, WALL + _arch(t)) for t in (-1.0, -0.6, -0.2, 0.2, 0.6, 1.0)]
    p.loft([[Vector((gx, y, gz)) for gx, gz in arch] for y in (y0 - 0.015, y0)], SHEET)
    p.box((cx - 0.5, y0 - 0.02, PAD), (cx + 0.5, y0 + 0.005, WALL + 0.12), DARK)
    p.box((cx - 0.54, y0 - 0.03, WALL + 0.12), (cx + 0.54, y0, WALL + 0.16), WHITE)
    for x in (cx - 0.54, cx + 0.5):
        p.box((x, y0 - 0.03, PAD), (x + 0.04, y0, WALL + 0.12), WHITE)
    if variant == "abandoned":
        # Holes in the roof: dark patches on it, the doors' edge torn.
        for t, y in ((-0.3, 0.45), (0.4, 0.9)):
            c = Vector((cx + half * t, y, WALL + _arch(t) * 1.02 + 0.01))
            cc.block(p, c, (0.32, 0.28, 0.02), (0.0, math.atan2(-t * ARCH, 1.0) * 0.8, 0.0), DARK)
        cc.block(p, (cx + 0.2, y0 - 0.25, PAD + 0.03), (0.5, 0.04, 0.3), (1.4, 0.0, 0.3), SHEET)  # a door leaf down
    # The side door.
    p.box((x1 - 0.005, 0.85, PAD), (x1 + 0.015, 1.05, WALL - 0.08), RED)


def _tower(p, team, variant):
    x0, x1, y0, y1 = TOWER
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    if variant == "ruins":
        p.box((x0, y0, PAD), (x1, y1, 0.55), TOWER)
        cc.block(p, (cx + 0.35, cy - 0.3, PAD + 0.12), (0.8, 0.34, 0.24), (0.0, 0.1, 0.7), TOWER)
        cc.block(p, (cx + 0.2, cy - 0.7, PAD + 0.06), (0.4, 0.3, 0.12), (0.2, 0.1, 1.5), GLASS)
        return
    p.loft([[Vector(v) for v in ((x0, y0, PAD), (x1, y0, PAD), (x1, y1, PAD), (x0, y1, PAD))],
            [Vector(v) for v in ((x0 + 0.03, y0 + 0.03, TOWER_TOP), (x1 - 0.03, y0 + 0.03, TOWER_TOP), (x1 - 0.03, y1 - 0.03, TOWER_TOP),
                                 (x0 + 0.03, y1 - 0.03, TOWER_TOP))]], TOWER)
    # A dark band of narrow windows up the front and the right side.
    for z in cc.steps(PAD + 0.2, TOWER_TOP - 0.15, 0.2):
        p.box((cx - 0.04, y0 + 0.015, z), (cx + 0.04, y0 + 0.03, z + 0.12), GLASS)
        p.box((x1 - 0.03, cy - 0.04, z), (x1 - 0.015, cy + 0.04, z + 0.12), GLASS)
    p.box((cx - 0.07, y0 + 0.01, PAD), (cx + 0.07, y0 + 0.03, PAD + 0.2), DARK)  # its door
    # The cabin: glass under a flat roof that stands out, the roof's rim the owner's colour.
    glass = DARK if variant == "abandoned" else GLASS
    p.box((cx - 0.25, cy - 0.25, TOWER_TOP), (cx + 0.25, cy + 0.25, TOWER_TOP + CABIN), glass)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((cx + sx * 0.25 - 0.015, cy + sy * 0.25 - 0.015, TOWER_TOP), (cx + sx * 0.25 + 0.015, cy + sy * 0.25 + 0.015,
                                                                                TOWER_TOP + CABIN), WHITE)
    p.box((cx - 0.3, cy - 0.3, TOWER_TOP + CABIN), (cx + 0.3, cy + 0.3, TOWER_TOP + CABIN + 0.04), WHITE)
    team.box((cx - 0.31, cy - 0.31, TOWER_TOP + CABIN + 0.04), (cx + 0.31, cy + 0.31, TOWER_TOP + CABIN + 0.09), TEAM)
    top = TOWER_TOP + CABIN + 0.09
    tip = Vector((cx, cy, top + 0.45)) if variant == "intact" else Vector((cx + 0.2, cy - 0.15, top + 0.3))
    p.bar((cx, cy, top), tip, 0.012, DARK)
    p.sphere(tip, 'Z', 0.025, RED, sides=8, rings=4)


def _flag(team, variant):
    x, y = HANGAR[1] - 0.1, HANGAR[2] - 0.25
    if variant == "ruins":
        return
    top = 1.55
    team.cylinder((x, y, (PAD + top) / 2), 'Z', 0.015, (top - PAD) / 2, WHITE, sides=6)
    if variant == "abandoned":
        cc.block(team, (x - 0.12, y, top - 0.12), (0.22, 0.01, 0.16), (0.0, 0.25, 0.0), TEAM)  # torn, hanging
    else:
        team.loft([[Vector((x - 0.02, y + d, top - 0.02)), Vector((x - 0.42, y + d + 0.03, top - 0.04)), Vector((x - 0.42, y + d + 0.03, top - 0.28)),
                    Vector((x - 0.02, y + d, top - 0.26))] for d in (-0.006, 0.006)], TEAM)


def _pad(base, body, variant):
    base.box((-1.48, -1.48, 0.0), (1.48, 1.48, PAD), CONCRETE)
    if variant == "ruins":
        return
    for cx, cy in CIRCLES:
        body.turned((cx, cy, PAD), 'Z', [(0.0, 0.42), (0.004, 0.42)], YELLOW, sides=24)
        body.turned((cx, cy, PAD + 0.004), 'Z', [(0.0, 0.38), (0.003, 0.38)], CONCRETE, sides=24)
        # A white star of four points.
        star = []
        for k in range(8):
            a = math.pi / 4 * k
            r = 0.32 if k % 2 == 0 else 0.07
            star.append(Vector((cx + r * math.cos(a), cy + r * math.sin(a), PAD + 0.007)))
        body.loft([star, [q + Vector((0, 0, 0.003)) for q in star]], WHITE)
    for x in cc.steps(-1.35, 1.35, 0.3):
        body.box((x - 0.02, -1.42, PAD), (x + 0.02, -1.38, PAD + 0.03), YELLOW)  # the lights along the front edge


def _yard(p, variant):
    crates = ((-0.3, 0.2), (-0.15, 0.25), (-0.27, 0.36), (-0.22, 0.28))
    if variant == "intact":
        for k, (x, y) in enumerate(crates[:3]):
            p.box((x - 0.06, y - 0.06, PAD), (x + 0.06, y + 0.06, PAD + 0.12), WOOD if k != 1 else RED)
        cc.block(p, (-0.22, 0.28, PAD + 0.18), (0.12, 0.12, 0.12), (0.0, 0.0, 0.3), WOOD)
        for x in (-0.05, 0.02):
            p.turned((x, 0.55, PAD), 'Z', [(0.0, 0.035), (0.11, 0.035)], DRUM, sides=8)
    else:
        for k, (x, y) in enumerate(crates):
            cc.block(p, (x + 0.1 * k, y - 0.15 * k, PAD + 0.05), (0.12, 0.12, 0.1), (0.2 * k, 0.1, 0.7 * k), WOOD if k % 2 else RED)
    if variant != "ruins":
        # The windsock at the front right.
        p.cylinder((1.3, -1.2, PAD + 0.35), 'Z', 0.012, 0.35, WHITE, sides=6)
        if variant == "intact":
            p.turned((1.3, -1.2, PAD + 0.66), 'X', [(0.0, 0.05), (0.28, 0.025)], ORANGE, sides=8)
        else:
            cc.block(p, (1.3, -1.25, PAD + 0.55), (0.06, 0.06, 0.22), (0.3, 0.0, 0.0), ORANGE)  # hanging limp


def _helicopter(p, variant):
    """A small helicopter on the right pad: a rounded cabin with its glass, a tail boom with its fin and rotor, skids, the main rotor;
    abandoned, burnt out and down on its side."""
    if variant == "ruins":
        return
    cx, cy = CIRCLES[1]
    burnt = variant == "abandoned"
    hull = DARK if burnt else OLIVE
    h = Parts()
    h.turned((0.0, 0.0, 0.12), 'X', [(-0.16, 0.02), (-0.12, 0.07), (0.0, 0.085), (0.1, 0.075), (0.15, 0.03)], hull, sides=10)
    h.turned((0.07, 0.0, 0.14), 'X', [(0.0, 0.06), (0.06, 0.04), (0.09, 0.01)], DARK if burnt else GLASS, sides=10)
    h.bar((-0.15, 0.0, 0.14), (-0.48, 0.0, 0.17), 0.015, hull)  # the tail boom
    h.box((-0.5, -0.004, 0.15), (-0.45, 0.004, 0.25), hull)  # its fin
    for y in (-0.07, 0.07):
        h.bar((-0.1, y, 0.02), (0.12, y, 0.02), 0.008, DARK)  # the skids
        for x in (-0.06, 0.06):
            h.bar((x, y, 0.02), (x, y * 0.6, 0.06), 0.006, DARK)
    h.cylinder((0.0, 0.0, 0.215), 'Z', 0.015, 0.012, DARK, sides=6)
    for a in (0.3, 0.3 + math.pi / 2):
        d = Vector((math.cos(a), math.sin(a), 0.0)) * 0.3
        h.bar(Vector((0, 0, 0.23)) - d, Vector((0, 0, 0.23)) + d, 0.01, DARK)
    turn = Matrix.Rotation(0.6, 4, 'Z')
    if burnt:
        turn = Matrix.Translation((0.0, 0.0, 0.06)) @ Matrix.Rotation(0.6, 4, 'Z') @ Matrix.Rotation(1.2, 4, 'X')
    bmesh.ops.transform(h.bm, matrix=Matrix.Translation((cx, cy, PAD + 0.008)) @ turn, verts=list(h.bm.verts))
    # Copy the helicopter's faces into the body, part by part.
    layer = h.layer
    for face in h.bm.faces:
        verts = [p.bm.verts.new(v.co) for v in face.verts]
        new = p.bm.faces.new(verts)
        for loop, old in zip(new.loops, face.loops):
            loop[p.layer] = old[layer]


def _rubble(p, variant):
    spots = (((0.1, -0.3, PAD + 0.05), (0.2, 0.15, 0.1), 0.4), ((-0.6, 0.4, PAD + 0.04), (0.18, 0.14, 0.08), 1.1))
    if variant == "ruins":
        spots += (((0.8, -0.1, PAD + 0.06), (0.25, 0.2, 0.12), 0.3), ((-0.9, 0.5, PAD + 0.06), (0.22, 0.2, 0.12), 0.9))
    for c, s, t in spots:
        cc.block(p, c, s, (0.2, 0.1, t), CONCRETE)


def _material(name, variant):
    """Concrete in slabs, grey sheet, corrugated roof metal, blue-green glass, dark doors, white trim, yellow and white marks, red,
    crates of wood, olive drums, the light team zone, the tower's light concrete, an orange sock; soot when abandoned, in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.0)), -40.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 2.0)), -40.0, 1.5)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.7))
    concrete = g.mix(slabs, g.mix(grain, lin((0.62, 0.61, 0.58)), lin((0.70, 0.69, 0.66))), lin((0.44, 0.43, 0.41)))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.5), concrete, lin((0.36, 0.34, 0.31)))
    sheet = g.mix(grain, lin((0.62, 0.64, 0.64)), lin((0.70, 0.72, 0.72)))
    corrugation = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', y, 30.0)), 0.5)), 4.0, -0.6))
    roof = g.mix(corrugation, lin((0.80, 0.81, 0.80)), lin((0.64, 0.65, 0.64)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.5)), lin((0.14, 0.26, 0.30)), lin((0.36, 0.52, 0.56)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    white = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    yellow = g.mix(grain, lin((0.86, 0.70, 0.14)), lin((0.92, 0.78, 0.20)))
    red = g.mix(grain, lin((0.66, 0.12, 0.08)), lin((0.74, 0.16, 0.10)))
    wood = g.mix(g.noise(5.0, 5.0), lin((0.52, 0.38, 0.22)), lin((0.62, 0.46, 0.28)))
    drum = g.mix(grain, lin((0.30, 0.34, 0.20)), lin((0.36, 0.40, 0.24)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    tower = g.mix(g.noise(8.0, 3.0), lin((0.74, 0.73, 0.70)), lin((0.82, 0.81, 0.78)))
    orange = lin((0.95, 0.45, 0.10))
    olive = g.mix(grain, lin((0.30, 0.36, 0.26)), lin((0.36, 0.42, 0.30)))
    albedo = cc.paint(g, mask, (concrete, sheet, roof, glass, dark, white, yellow, red, wood, drum, team, tower, orange, olive))
    if variant != "intact":
        soot = g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -2.9 if variant == "abandoned" else -2.4))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.9, 0.5, 0.4, 0.1, 0.6, 0.6, 0.6, 0.6, 0.85, 0.5, 0.45, 0.85, 0.8, 0.5))
    metallic = cc.weighted(g, mask, (0.0, 0.5, 0.6, 0.0, 0.3, 0.1, 0.0, 0.1, 0.0, 0.4, 0.0, 0.0, 0.0, 0.3))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[2], corrugation, g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', slabs, -1.0), g.noise(60.0, 3.0))), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    cc.clear(variant_id, ("body", "team_mask", "base"))
    body, team, base = Parts(), Parts(), Parts()
    _pad(base, body, variant)
    _hangar(body, variant)
    _tower(body, team, variant)
    _flag(team, variant)
    _yard(body, variant)
    _helicopter(body, variant)
    if variant != "intact":
        _rubble(body, variant)
    parts = [(body, "body"), (base, "base")] if variant == "ruins" else [(body, "body"), (team, "team_mask"), (base, "base")]
    meshes = [p.finish(f"{variant_id}_{name}") for p, name in parts]
    cc.drop_bottoms(meshes, PAD - 0.001)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    cc.repack(meshes)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    nodes = rc.fire_nodes(FIRE_POINTS) if variant == "abandoned" else []
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes]
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}"]
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + nodes
    rc.arrange(objs, variant_id)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names(objs, names + rc.fire_names(nodes)):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
