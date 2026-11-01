"""Container stacks, element 06 of the naval set (docs/naval_elements/06_container_stack): steel shipping containers stacked on the quay;
they block the ground.

Footprint 2x1 cells (2 along x), origin at the centre of the footprint on the ground. Made at the size the game draws it, against the
tank (a unit is about five metres): a forty-foot container is 1.9 long, 0.44 wide, 0.48 high; two rows side by side across the cell,
two and three high, up to 1.45. The parts:
    containers  ribbed steel walls (the ribs in the texture), a frame of corner posts and rails a shade darker, the doors at the +x end
                with their locking rods; each its own colour of red, blue, green, grey and orange; rust at the corners and the feet;
                no writing and no logos
Nodes: "box_bottom" (all but the top of the tall row), "box_top" (the top of the tall row: the one that falls). One GLB per look:
    container_stack          the tall row red, blue and grey, the low one green and orange
    container_stack_b        the other colours and order: the tall row grey, green and red, the low one blue and red, shifted
    container_stack_damaged  the first, dented: dark dents and scratches to bare steel, more rust, a door of the low row hanging open,
                             the top container pushed askew
    container_stack_fallen   the first with its top container fallen: down off the stack onto the ground at its side, tilted on its
                             edge, its doors burst; nodes "box_bottom", "box_top" (the fallen one)
Budget 3000 triangles, texture 1024 (docs/naval_elements/06_container_stack/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_containers as ct
    print(ct.run_all())
"""

import math

from mathutils import Euler, Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from families.infrastructure_gate import transformed
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "container_stack": "a",
    "container_stack_b": "b",
    "container_stack_damaged": "damaged",
    "container_stack_fallen": "fallen",
}

COLOURS = ("red", "blue", "green", "grey", "orange")
WALL = {c: cc.part(i) for i, c in enumerate(COLOURS)}
FRAME = {c: cc.part(5 + i) for i, c in enumerate(COLOURS)}
DARK = cc.part(10)
PARTS = 11

L, W, H = 1.9, 0.44, 0.48
ROWS = (-0.235, 0.235)  # the rows' y
# Per look: each row, bottom up, (colour, shift along x, turn about z).
STACKS = {
    "a": ((("red", 0.0, 0.0), ("blue", 0.03, 0.012), ("grey", -0.02, -0.015)), (("green", 0.02, -0.01), ("orange", -0.03, 0.015))),
    "b": ((("grey", -0.03, 0.01), ("green", 0.02, -0.012), ("red", 0.05, 0.02)), (("blue", 0.0, 0.0), ("red", -0.04, -0.015))),
}


def container(p, colour, door=None):
    """One container in its own frame: centred on x and y, its floor at z 0, the doors at +x. door: how far the near door leaf of the
    two stands open (radians), when it does."""
    wall, frame = WALL[colour], FRAME[colour]
    hx, hy = L / 2, W / 2
    p.box((-hx + 0.02, -hy + 0.01, 0.02), (hx - 0.02, hy - 0.01, H - 0.02), wall)
    t = 0.03
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (hx - t / 2), sy * (hy - t / 2)
            p.box((x - t / 2, y - t / 2, 0.0), (x + t / 2, y + t / 2, H), frame)  # the corner posts
        p.box((sx * hx - t if sx > 0 else -hx, -hy, 0.0), (hx if sx > 0 else -hx + t, hy, 0.03), frame)  # the end sills
        p.box((sx * hx - t if sx > 0 else -hx, -hy, H - 0.03), (hx if sx > 0 else -hx + t, hy, H), frame)  # the end headers
    for sy in (-1, 1):
        y0, y1 = (hy - 0.025, hy) if sy > 0 else (-hy, -hy + 0.025)
        p.box((-hx, y0, 0.0), (hx, y1, 0.035), frame)  # the side rails
        p.box((-hx, y0, H - 0.03), (hx, y1, H), frame)
    # The doors at +x: two leaves, their locking rods; the near leaf swung open when door is given.
    for k, sy in enumerate((-1, 1)):
        def leaf(sy=sy):
            y0, y1 = (0.0, hy - 0.03) if sy > 0 else (-hy + 0.03, 0.0)
            p.box((hx - 0.02, y0 + 0.004, 0.035), (hx - 0.005, y1 - 0.004, H - 0.035), wall)
            for f in (0.3, 0.7):
                y = y0 + (y1 - y0) * f
                p.bar((hx - 0.002, y, 0.04), (hx - 0.002, y, H - 0.04), 0.004, DARK)
        if door is not None and k == 0:
            hinge = Vector((hx - 0.01, -hy + 0.03, 0.0))
            transformed(p, Matrix.Translation(hinge) @ Matrix.Rotation(-door, 4, 'Z') @ Matrix.Translation(-hinge), leaf)
        else:
            leaf()


def placed(p, at, turn, build):
    """build() made in a container's frame, then put at at, turned by turn (Euler, radians)."""
    transformed(p, Matrix.Translation(at) @ Euler(turn).to_matrix().to_4x4(), build)


def _stack(bottom, top, look):
    stack = STACKS["a" if look in ("damaged", "fallen") else look]
    tall = max(range(2), key=lambda r: len(stack[r]))
    for r, row in enumerate(stack):
        for k, (colour, shift, turn) in enumerate(row):
            is_top = r == tall and k == len(row) - 1
            if is_top and look == "fallen":
                # Down off the stack at the side of the tall row, on its edge, its doors burst.
                placed(top, (0.1, ROWS[r] * 2.2, 0.21), (1.25, 0.06, 0.2), lambda c=colour: container(top, c, door=1.2))
                continue
            if is_top and look == "damaged":
                shift, turn = shift + 0.12, turn + 0.12
            door = 1.0 if look == "damaged" and r != tall and k == 0 else None
            owner = top if is_top else bottom
            placed(owner, (shift, ROWS[r], k * H), (0.0, 0.0, turn), lambda c=colour, d=door, o=owner: container(o, c, d))


def _material(name, look):
    """The containers' colours, their walls ribbed (dark bands across each side, every few centimetres), their frames a shade darker;
    rust at the corners and the feet; dark iron rods; dents and scratches when damaged."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    rib = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 22.0)), 0.5)), 4.0, -1.2))
    base = {"red": (0.62, 0.14, 0.10), "blue": (0.14, 0.30, 0.56), "green": (0.20, 0.42, 0.26), "grey": (0.56, 0.57, 0.58),
            "orange": (0.84, 0.44, 0.10)}
    walls, frames = [], []
    for c in COLOURS:
        r, gr, b = base[c]
        colour = g.mix(grain, lin((r * 0.92, gr * 0.92, b * 0.92)), lin((r * 1.05, gr * 1.05, b * 1.05)))
        walls.append(g.mix(g.math('MULTIPLY', rib, 0.35), colour, lin((r * 0.55, gr * 0.55, b * 0.55))))
        frames.append(lin((r * 0.7, gr * 0.7, b * 0.7)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    albedo = cc.paint(g, mask, walls + frames + [dark])
    # Rust in patches, more at the feet of each tier (the floors are every H up).
    amount = 0.35 if look in ("damaged", "fallen") else 0.0
    feet = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 1.0 / H)), -8.0, 1.0))
    rusty = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(14.0, 3.0), 5.0, -3.0 + 2.0 * amount)), g.math('ADD', 0.4, feet))
    rust = g.mix(g.noise(25.0, 3.0), lin((0.38, 0.20, 0.10)), lin((0.50, 0.28, 0.14)))
    albedo = g.mix(g.math('MULTIPLY', rusty, g.math('SUBTRACT', 1.0, mask[10])), albedo, rust)
    if look in ("damaged", "fallen"):
        dents = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 2.0), 7.0, -4.4))
        albedo = g.mix(g.math('MULTIPLY', dents, 0.5), albedo, lin((0.10, 0.09, 0.09)))
        scratch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(70.0, 2.0), 12.0, -7.8))
        albedo = g.mix(scratch, albedo, lin((0.44, 0.44, 0.42)))
    rough = g.math('ADD', 0.55, g.math('MULTIPLY', rusty, 0.3))
    height = g.math('MULTIPLY_ADD', rib, -1.0, g.math('MULTIPLY', g.noise(60.0, 2.0), 0.2))
    g.finish(albedo, g.bump(height, 0.3, 0.004), g.combine(ao, rough, 0.4))
    return mat, g


def build(variant_id):
    look = VARIANTS[variant_id]
    ic.clear(variant_id, ("box_bottom", "box_top"))
    bottom, top = Parts(), Parts()
    _stack(bottom, top, look)
    return cc.finish(variant_id, [(bottom, "box_bottom"), (top, "box_top")], 0.0, _material(variant_id + "_mat", look))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
