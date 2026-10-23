"""Procedural placeholder textures that follow the palettes of the docs prompts.

Stdlib only. Every texture is periodic, so it tiles seamlessly.
Not the final look: real textures come from the image generator.
"""
import math
import os
import random
import struct
import sys
import zlib

N = 1024


def clamp(v):
    if v < 0:
        return 0
    if v > 255:
        return 255
    return int(v + 0.5)


def smooth(t):
    return t * t * (3.0 - 2.0 * t)


def value_noise(gx_n, gy_n, rnd):
    """Periodic value noise on an N x N grid, values in [0, 1)."""
    lattice = [[rnd.random() for _ in range(gx_n)] for _ in range(gy_n)]
    xi0, xi1, xw = [], [], []
    for x in range(N):
        g = x * gx_n / N
        i0 = int(g)
        xi0.append(i0)
        xi1.append((i0 + 1) % gx_n)
        xw.append(smooth(g - i0))
    rows = []
    for j in range(gy_n):
        lj = lattice[j]
        rows.append([lj[xi0[x]] * (1.0 - xw[x]) + lj[xi1[x]] * xw[x] for x in range(N)])
    field = []
    for y in range(N):
        g = y * gy_n / N
        j0 = int(g)
        t = smooth(g - j0)
        j1 = (j0 + 1) % gy_n
        a = rows[j0]
        b = rows[j1]
        field.append([p * (1.0 - t) + q * t for p, q in zip(a, b)])
    return field


def fbm(base, octaves, rnd):
    total = None
    amp = 1.0
    norm = 0.0
    for k in range(octaves):
        size = base * (2 ** k)
        f = value_noise(size, size, rnd)
        if total is None:
            total = [[amp * v for v in row] for row in f]
        else:
            total = [[t + amp * v for t, v in zip(tr, row)] for tr, row in zip(total, f)]
        norm += amp
        amp *= 0.5
    return [[v / norm for v in row] for row in total]


def voronoi(cells, rnd):
    """Periodic cellular noise: F1, F2 distances (in cell units) and a cell id per pixel."""
    pts = [[(rnd.random(), rnd.random()) for _ in range(cells)] for _ in range(cells)]
    cid = [[rnd.random() for _ in range(cells)] for _ in range(cells)]
    scale = cells / N
    f1, f2, ids = [], [], []
    for y in range(N):
        py = y * scale
        cy = int(py)
        r1, r2, rid = [], [], []
        for x in range(N):
            px = x * scale
            cx = int(px)
            best = second = 1e9
            bid = 0.0
            for dy in (-1, 0, 1):
                wy = cy + dy
                sy = 0.0
                if wy < 0:
                    wy += cells
                    sy = -cells
                elif wy >= cells:
                    wy -= cells
                    sy = cells
                for dx in (-1, 0, 1):
                    wx = cx + dx
                    sx = 0.0
                    if wx < 0:
                        wx += cells
                        sx = -cells
                    elif wx >= cells:
                        wx -= cells
                        sx = cells
                    qx, qy = pts[wy][wx]
                    ddx = wx + qx + sx - px
                    ddy = wy + qy + sy - py
                    d = ddx * ddx + ddy * ddy
                    if d < best:
                        second = best
                        best = d
                        bid = cid[wy][wx]
                    elif d < second:
                        second = d
            r1.append(math.sqrt(best))
            r2.append(math.sqrt(second))
            rid.append(bid)
        f1.append(r1)
        f2.append(r2)
        ids.append(rid)
    return f1, f2, ids


def build(row_fn):
    rgb = bytearray(N * N * 3)
    for y in range(N):
        i = y * N * 3
        for r, g, b in row_fn(y):
            rgb[i] = clamp(r)
            rgb[i + 1] = clamp(g)
            rgb[i + 2] = clamp(b)
            i += 3
    return rgb


def write_png(path, rgb):
    raw = bytearray()
    stride = N * 3
    for y in range(N):
        raw.append(0)
        raw += rgb[y * stride:(y + 1) * stride]

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    header = struct.pack(">IIBBBBB", N, N, 8, 2, 0, 0, 0)
    blob = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(blob)


def seam_report(name, rgb):
    """Mean |difference| of neighbouring pixels inside the image vs across the wrap edge."""
    inner, seam = [], []
    for y in range(0, N, 32):
        base = y * N * 3
        for x in range(0, N - 1, 16):
            inner.append(abs(rgb[base + 3 * x] - rgb[base + 3 * (x + 1)]))
        seam.append(abs(rgb[base + 3 * (N - 1)] - rgb[base]))
    for x in range(0, N, 32):
        for y in range(0, N - 1, 16):
            a = (y * N + x) * 3
            b = ((y + 1) * N + x) * 3
            inner.append(abs(rgb[a] - rgb[b]))
        a = ((N - 1) * N + x) * 3
        seam.append(abs(rgb[a] - rgb[x * 3]))
    print("%-14s inner %6.2f  seam %6.2f" % (name, sum(inner) / len(inner), sum(seam) / len(seam)))


def snow(rnd):
    low = fbm(4, 3, rnd)
    grain = value_noise(256, 256, rnd)

    def row(y):
        out = []
        for t, g in zip(low[y], grain[y]):
            s = 0.6 * t
            k = (g - 0.5) * 6
            out.append((236 - 24 * s + k, 241 - 20 * s + k, 246 - 8 * s + k))
        return out

    return row


def grass(rnd):
    low = fbm(12, 3, rnd)
    streak = value_noise(160, 40, rnd)
    frost = value_noise(256, 256, rnd)

    def row(y):
        out = []
        for t, s, f in zip(low[y], streak[y], frost[y]):
            m = 0.9 + 0.16 * s
            r = (92 + 50 * t) * m
            g = (92 + 46 * t) * m
            b = (70 + 34 * t) * m
            if f > 0.93:
                w = 0.7
                r = r * (1 - w) + 222 * w
                g = g * (1 - w) + 232 * w
                b = b * (1 - w) + 236 * w
            out.append((r, g, b))
        return out

    return row


def mud(rnd):
    low = fbm(12, 3, rnd)
    crack = fbm(20, 2, rnd)
    puddle = fbm(5, 2, rnd)

    def row(y):
        out = []
        for t, c, p in zip(low[y], crack[y], puddle[y]):
            r = 66 + 26 * t
            g = 50 + 20 * t
            b = 38 + 16 * t
            if abs(c - 0.5) < 0.012:
                r *= 0.5
                g *= 0.5
                b *= 0.5
            if p > 0.7:
                w = 0.6
                r = r * (1 - w) + 150 * w
                g = g * (1 - w) + 172 * w
                b = b * (1 - w) + 184 * w
            out.append((r, g, b))
        return out

    return row


def gravel(rnd):
    f1, f2, cid = voronoi(44, rnd)
    crust = fbm(6, 2, rnd)

    def row(y):
        out = []
        for d1, d2, c, k in zip(f1[y], f2[y], cid[y], crust[y]):
            base = 120 + 60 * c
            r, g, b = base, base - 6, base - 14
            if d2 - d1 < 0.12:
                r *= 0.55
                g *= 0.55
                b *= 0.55
            if k > 0.68:
                w = 0.35
                r = r * (1 - w) + 196 * w
                g = g * (1 - w) + 218 * w
                b = b * (1 - w) + 228 * w
            out.append((r, g, b))
        return out

    return row


def rock(rnd):
    f1, f2, cid = voronoi(9, rnd)
    grain = value_noise(256, 256, rnd)

    def row(y):
        out = []
        for d1, d2, c, g in zip(f1[y], f2[y], cid[y], grain[y]):
            base = 52 + 30 * c
            k = (g - 0.5) * 10
            if d2 - d1 < 0.05:
                out.append((28 + k, 28 + k, 30 + k))
            else:
                out.append((base + k, base + 1 + k, base + 3 + k))
        return out

    return row


def ice(rnd):
    low = fbm(10, 3, rnd)
    cr = fbm(24, 2, rnd)
    dust = value_noise(256, 256, rnd)

    def row(y):
        out = []
        for t, c, d in zip(low[y], cr[y], dust[y]):
            r = 190 + 20 * t
            g = 216 + 16 * t
            b = 232 + 10 * t
            if abs(c - 0.5) < 0.01:
                w = 0.8
                r = r * (1 - w) + 245 * w
                g = g * (1 - w) + 252 * w
                b = b * (1 - w) + 255 * w
            if d > 0.8:
                w = 0.3
                r = r * (1 - w) + 240 * w
                g = g * (1 - w) + 246 * w
                b = b * (1 - w) + 250 * w
            out.append((r, g, b))
        return out

    return row


def height_field():
    """Periodic ripples: integer wave counts per tile, so the edge matches."""
    H = []
    for y in range(N):
        row = []
        for x in range(N):
            v = (128
                 + 40 * math.sin(2 * math.pi * (3 * x + 2 * y) / N)
                 + 30 * math.sin(2 * math.pi * (-2 * x + 5 * y) / N)
                 + 20 * math.sin(2 * math.pi * (7 * x + 1 * y) / N))
            row.append(v)
        H.append(row)
    return H


def normal_rows(H, k):
    """OpenGL-style normal map (Y up) from a height field."""
    def row(y):
        ym = (y - 1) % N
        yp = (y + 1) % N
        hy, hm, hp = H[y], H[ym], H[yp]
        out = []
        for x in range(N):
            xm = (x - 1) % N
            xp = (x + 1) % N
            dx = (hy[xp] - hy[xm]) * 0.5
            dy = (hp[x] - hm[x]) * 0.5
            nx = -dx * k
            ny = dy * k
            ln = math.sqrt(nx * nx + ny * ny + 1.0)
            nx /= ln
            ny /= ln
            nz = 1.0 / ln
            out.append((127.5 * (nx + 1.0), 127.5 * (ny + 1.0), 127.5 * (nz + 1.0)))
        return out

    return row


def main(out_dir):
    rnd = random.Random(20261023)
    land = os.path.join(out_dir, "land")
    water = os.path.join(out_dir, "water")
    os.makedirs(land, exist_ok=True)
    os.makedirs(water, exist_ok=True)
    jobs = [
        (land, "snow_clean", snow),
        (land, "frozen_grass", grass),
        (land, "mud", mud),
        (land, "beach_gravel", gravel),
        (land, "rock_field", rock),
        (water, "frozen_water", ice),
    ]
    for folder, name, factory in jobs:
        rgb = build(factory(rnd))
        write_png(os.path.join(folder, name + ".png"), rgb)
        seam_report(name, rgb)
    H = height_field()
    rgb = build(lambda y: [(v, v, v) for v in H[y]])
    write_png(os.path.join(water, "water_height.png"), rgb)
    seam_report("water_height", rgb)
    rgb = build(normal_rows(H, 1.5))
    write_png(os.path.join(water, "water_normal.png"), rgb)
    seam_report("water_normal", rgb)


if __name__ == "__main__":
    main(sys.argv[1])
