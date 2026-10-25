"""Unpacks the packed sections of a Red Alert 2 map (.mpr/.yrm): IsoMapPack5 (LZO1X), OverlayPack and OverlayDataPack
(Westwood format 80), PreviewPack (LZO1X). Usage: ra2_unpack.py <map> <out_dir>. Writes iso.json, overlay.bin, overlaydata.bin,
preview.ppm and sections.json (the plain INI sections)."""
import sys, re, base64, struct, json, os

def sections(text):
    out = {}; name = None
    for line in text.splitlines():
        line = line.split(';', 1)[0].rstrip() if not line.startswith(';') else ''
        m = re.match(r'^\[(.+)\]\s*$', line)
        if m:
            name = m.group(1); out.setdefault(name, []); continue
        if name is not None and '=' in line:
            k, v = line.split('=', 1); out[name].append((k.strip(), v.strip()))
    return out

def packed(sec):
    return base64.b64decode(''.join(v for k, v in sorted(sec, key=lambda kv: int(kv[0]))))

def chunks(data, decode):
    out = bytearray(); i = 0
    while i + 4 <= len(data):
        size_in, size_out = struct.unpack_from('<HH', data, i); i += 4
        out += decode(data[i:i + size_in], size_out); i += size_in
    return bytes(out)

def lzo1x(src, out_len):
    out = bytearray(); ip = 0
    def copy_match(m_pos, n):
        for _ in range(n):
            out.append(out[m_pos]); m_pos += 1
    state = 'start'
    t = 0
    while True:
        if state == 'start':
            t = src[ip]
            if t > 17:
                ip += 1; t -= 17
                if t < 4:
                    state = 'match_next'; continue
                out += src[ip:ip + t]; ip += t
                state = 'first_literal_run'; continue
            state = 'loop'; continue
        if state == 'loop':
            t = src[ip]; ip += 1
            if t >= 16:
                state = 'match'; continue
            if t == 0:
                while src[ip] == 0:
                    t += 255; ip += 1
                t += 15 + src[ip]; ip += 1
            out += src[ip:ip + t + 3]; ip += t + 3
            state = 'first_literal_run'; continue
        if state == 'first_literal_run':
            t = src[ip]; ip += 1
            if t >= 16:
                state = 'match'; continue
            m_pos = len(out) - (1 + 0x0800) - (t >> 2) - (src[ip] << 2); ip += 1
            copy_match(m_pos, 3)
            state = 'match_done'; continue
        if state == 'match':
            if t >= 64:
                m_pos = len(out) - 1 - ((t >> 2) & 7) - (src[ip] << 3); ip += 1
                t = (t >> 5) - 1
                copy_match(m_pos, t + 2)
                state = 'match_done'; continue
            elif t >= 32:
                t &= 31
                if t == 0:
                    while src[ip] == 0:
                        t += 255; ip += 1
                    t += 31 + src[ip]; ip += 1
                m_pos = len(out) - 1 - ((src[ip] | (src[ip + 1] << 8)) >> 2); ip += 2
            elif t >= 16:
                m_pos = len(out) - ((t & 8) << 11)
                t &= 7
                if t == 0:
                    while src[ip] == 0:
                        t += 255; ip += 1
                    t += 7 + src[ip]; ip += 1
                m_pos -= (src[ip] | (src[ip + 1] << 8)) >> 2; ip += 2
                if m_pos == len(out):
                    break
                m_pos -= 0x4000
            else:
                m_pos = len(out) - 1 - (t >> 2) - (src[ip] << 2); ip += 1
                copy_match(m_pos, 2)
                state = 'match_done'; continue
            copy_match(m_pos, t + 2)
            state = 'match_done'; continue
        if state == 'match_done':
            t = src[ip - 2] & 3
            if t == 0:
                state = 'loop'; continue
            state = 'match_next'; continue
        if state == 'match_next':
            out += src[ip:ip + t]; ip += t
            t = src[ip]; ip += 1
            state = 'match'; continue
    return bytes(out[:out_len])

def format80(src, out_len):
    out = bytearray(); i = 0
    while i < len(src):
        c = src[i]; i += 1
        if c & 0x80 == 0:
            count = (c >> 4) + 3; pos = ((c & 0x0f) << 8) + src[i]; i += 1
            start = len(out) - pos
            for k in range(count):
                out.append(out[start + k])
        elif c & 0x40 == 0:
            count = c & 0x3f
            if count == 0:
                break
            out += src[i:i + count]; i += count
        else:
            if c == 0xFE:
                count, value = struct.unpack_from('<HB', src, i); i += 3
                out += bytes([value]) * count
            else:
                if c == 0xFF:
                    count, pos = struct.unpack_from('<HH', src, i); i += 4
                else:
                    count = (c & 0x3f) + 3; pos, = struct.unpack_from('<H', src, i); i += 2
                for k in range(count):
                    out.append(out[pos + k])
    return bytes(out[:out_len])

def main(path, out_dir):
    text = open(path, encoding='latin-1').read()
    secs = sections(text)
    os.makedirs(out_dir, exist_ok=True)
    iso = chunks(packed(secs['IsoMapPack5']), lzo1x)
    tiles = []
    for i in range(0, len(iso) - 10, 11):
        x, y, tile, sub, level, ice = struct.unpack_from('<hhiBBB', iso, i)
        if x == 0 and y == 0 and tile == 0:
            continue
        tiles.append([x, y, tile if tile != -1 else 0, sub, level])
    json.dump(tiles, open(os.path.join(out_dir, 'iso.json'), 'w'))
    for name, file in [('OverlayPack', 'overlay.bin'), ('OverlayDataPack', 'overlaydata.bin')]:
        open(os.path.join(out_dir, file), 'wb').write(chunks(packed(secs[name]), format80))
    if 'PreviewPack' in secs:
        w, h = map(int, dict(secs['Preview'])['Size'].split(',')[2:4])
        rgb = chunks(packed(secs['PreviewPack']), lzo1x)
        open(os.path.join(out_dir, 'preview.ppm'), 'wb').write(b'P6 %d %d 255\n' % (w, h) + rgb[:w * h * 3])
    plain = {k: v for k, v in secs.items() if not k.endswith('Pack') and k not in ('IsoMapPack5',)}
    json.dump(plain, open(os.path.join(out_dir, 'sections.json'), 'w'), ensure_ascii=False)
    print(f'{len(tiles)} iso tiles, overlay {os.path.getsize(os.path.join(out_dir, "overlay.bin"))} bytes')

main(sys.argv[1], sys.argv[2])
