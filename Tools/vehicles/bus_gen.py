# Otobus Imparatoru arac modelleri (ozgun tasarim). Ayni kod hem yerelde hem SketchUp build_model icinde calisir:
# import yok (math disaridan gelir), sinif yok, str.format yok, any/all/next yok.
# Eksenler (metre): X ileri (on +X), Y genislik (0 ortada, sag taraf -Y: kapilar), Z yukari, Z=0 zemin.

# Q["low"]: haritadaki kucuk araclar icin az cokgenli surum (ayrinti parcalari atlanir, yaylar kaba)
Q = {"low": False}


def v_sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def v_dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def newell(pts):
    nx = 0.0
    ny = 0.0
    nz = 0.0
    n = len(pts)
    for i in range(n):
        a = pts[i]
        b = pts[(i + 1) % n]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    return (nx, ny, nz)


def centroid(pts):
    n = float(len(pts))
    return (sum([p[0] for p in pts]) / n, sum([p[1] for p in pts]) / n, sum([p[2] for p in pts]) / n)


def same_pt(a, b):
    return abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2]) < 1e-5


def clean_loop(pts):
    out = []
    for p in pts:
        if len(out) > 0 and same_pt(out[-1], p):
            continue
        out.append(p)
    while len(out) > 2 and same_pt(out[0], out[-1]):
        out.pop()
    return out


def new_mesh():
    return {"parts": {}, "order": []}


def add_poly(M, part, mat, pts, out):
    """out = ("p", nokta): yuz bu noktadan uzaga bakar; ("d", yon): yuz bu yone bakar."""
    pts = clean_loop(pts)
    if len(pts) < 3:
        return
    n = newell(pts)
    if abs(n[0]) + abs(n[1]) + abs(n[2]) < 1e-7:
        return
    if out[0] == "p":
        ref = v_sub(centroid(pts), out[1])
    else:
        ref = out[1]
    if v_dot(n, ref) < 0:
        pts = list(reversed(pts))
    key = part + "_" + mat
    if key not in M["parts"]:
        M["parts"][key] = {"mat": mat, "polys": []}
        M["order"].append(key)
    M["parts"][key]["polys"].append(pts)


# ---------------------------------------------------------------- kesitler ve govde

def arc2(cy, cz, r, a0, a1, n):
    pts = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / float(n))
        pts.append((cy + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def profile(w, zb, zt, rt, rb, levels, nt, nb):
    """Yuvarlak koseli kesit (y, z); simetrik: nokta i ile N-1-i ayni z'de, y'si ters."""
    h = w / 2.0
    rt = min(rt, h * 0.95, (zt - zb) * 0.45)
    rb = min(rb, h * 0.5, (zt - zb) * 0.2)
    lo = zb + rb
    hi = zt - rt
    side = [min(max(z, lo), hi) for z in sorted(levels)]
    pts = []
    pts += arc2(h - rb, zb + rb, rb, -90.0, 0.0, nb)
    for z in side:
        pts.append((h, z))
    pts += arc2(h - rt, zt - rt, rt, 0.0, 90.0, nt)
    pts += arc2(-h + rt, zt - rt, rt, 90.0, 180.0, nt)
    for z in reversed(side):
        pts.append((-h, z))
    pts += arc2(-h + rb, zb + rb, rb, 180.0, 270.0, nb)
    return pts


def interp(table, z):
    """table: [(z, x)...] artan z; disinda uclara sabitlenir."""
    if z <= table[0][0]:
        return table[0][1]
    for i in range(len(table) - 1):
        z0, x0 = table[i]
        z1, x1 = table[i + 1]
        if z <= z1:
            if z1 - z0 < 1e-9:
                return x1
            return x0 + (x1 - x0) * (z - z0) / (z1 - z0)
    return table[-1][1]


def loft(M, part, mat, rings):
    for k in range(len(rings) - 1):
        a = rings[k]
        b = rings[k + 1]
        ref = centroid([centroid(a), centroid(b)])
        n = len(a)
        for i in range(n):
            j = (i + 1) % n
            add_poly(M, part, mat, [a[i], a[j], b[j], b[i]], ("p", ref))


def cap_pairs(M, part, mat, ring, direction):
    """Simetrik halkayi ust-alt serit dortgenleriyle kapatir (her dortgen duzlemsel ikizkenar yamuk)."""
    n = len(ring)
    half = n // 2
    pairs = []
    for j in range(half):
        pairs.append((ring[half - 1 - j], ring[half + j]))
    for j in range(len(pairs) - 1):
        r0, l0 = pairs[j]
        r1, l1 = pairs[j + 1]
        add_poly(M, part, mat, [r0, r1, l1, l0], ("d", direction))


def shell(M, part, mat, S, front, rear, levels):
    """Arac govdesi. front/rear: {"t": [(z, x)], "c": kose yaricapi}; ucta yuvarlatilmis kenarlar."""
    w = S["w"]
    zb = S["zb"]
    zt = S["zt"]
    rt = S["rt"]
    rb = S["rb"]
    nt = 2 if Q["low"] else 5
    nb = 1 if Q["low"] else 2
    lv = list(levels)
    for p in front["t"]:
        lv.append(p[0])
    for p in rear["t"]:
        lv.append(p[0])
    cf = front["c"]
    cr = rear["c"]
    xf_body = min([p[1] for p in front["t"]]) - cf
    xr_body = max([p[1] for p in rear["t"]]) + cr
    base = profile(w, zb, zt, rt, rb, lv, nt, nb)

    def ring_at(prof, xfn):
        return [(xfn(z), y, z) for (y, z) in prof]

    steps = 2 if Q["low"] else 4
    rear_rings = []
    for k in range(steps, -1, -1):
        a = math.radians(90.0 * k / steps)
        shrink = cr * (1.0 - math.cos(a))
        prof = profile(w - 2 * shrink, zb, zt - shrink * 0.6, max(rt - shrink * 0.5, 0.05), rb, lv, nt, nb)
        off = cr - cr * math.sin(a)
        rear_rings.append(ring_at(prof, (lambda o: (lambda z: interp(rear["t"], z) + o))(off)))
    front_rings = []
    for k in range(0, steps + 1):
        a = math.radians(90.0 * k / steps)
        shrink = cf * (1.0 - math.cos(a))
        prof = profile(w - 2 * shrink, zb, zt - shrink * 0.6, max(rt - shrink * 0.5, 0.05), rb, lv, nt, nb)
        off = -cf + cf * math.sin(a)
        front_rings.append(ring_at(prof, (lambda o: (lambda z: interp(front["t"], z) + o))(off)))
    body_r = ring_at(base, lambda z: xr_body)
    body_f = ring_at(base, lambda z: xf_body)
    rings = rear_rings + [body_r, body_f] + front_rings
    loft(M, part, mat, rings)
    cap_pairs(M, part, mat, front_rings[-1], (1.0, 0.0, 0.0))
    cap_pairs(M, part, mat, rear_rings[0], (-1.0, 0.0, 0.0))
    return {"xf_body": xf_body, "xr_body": xr_body}


def rbox(M, part, mat, x0, x1, y0, y1, z0, z1, r):
    """Uzun kenarlari X boyunca, yuvarlak koseli kutu."""
    w = y1 - y0
    yc = (y0 + y1) / 2.0
    seg = 1 if Q["low"] else 2
    prof = profile(w, z0, z1, r, r, [], seg, seg)
    a = [(x0, yc + y, z) for (y, z) in prof]
    b = [(x1, yc + y, z) for (y, z) in prof]
    ref = ((x0 + x1) / 2.0, yc, (z0 + z1) / 2.0)
    loft(M, part, mat, [a, b])
    add_poly(M, part, mat, a, ("d", (-1.0, 0.0, 0.0)))
    add_poly(M, part, mat, b, ("d", (1.0, 0.0, 0.0)))


def box(M, part, mat, x0, x1, y0, y1, z0, z1):
    c = ((x0 + x1) / 2.0, (y0 + y1) / 2.0, (z0 + z1) / 2.0)
    P = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    for f in [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
        add_poly(M, part, mat, [P[f[0]], P[f[1]], P[f[2]], P[f[3]]], ("p", c))


def cyl_y(M, part, mat, cx, cz, r, y0, y1, n):
    a = []
    b = []
    for i in range(n):
        t = 2.0 * math.pi * i / n
        a.append((cx + r * math.cos(t), y0, cz + r * math.sin(t)))
        b.append((cx + r * math.cos(t), y1, cz + r * math.sin(t)))
    ref = (cx, (y0 + y1) / 2.0, cz)
    for i in range(n):
        j = (i + 1) % n
        add_poly(M, part, mat, [a[i], a[j], b[j], b[i]], ("p", ref))
    add_poly(M, part, mat, a, ("p", ref))
    add_poly(M, part, mat, b, ("p", ref))


# ---------------------------------------------------------------- yan yuzey kaplamalari

def rrect_xz(x0, x1, z0, z1, r, y, n):
    r = min(r, (x1 - x0) * 0.45, (z1 - z0) * 0.45)
    pts = []
    for (cx, cz, a0) in [(x1 - r, z0 + r, -90.0), (x1 - r, z1 - r, 0.0), (x0 + r, z1 - r, 90.0), (x0 + r, z0 + r, 180.0)]:
        for i in range(n + 1):
            a = math.radians(a0 + 90.0 * i / n)
            pts.append((cx + r * math.cos(a), y, cz + r * math.sin(a)))
    return pts


def side_quad(M, part, mat, s, h, off, x0, x1, z0, z1):
    if x1 - x0 < 0.01 or z1 - z0 < 0.005:
        return
    y = s * (h + off)
    add_poly(M, part, mat, [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], ("d", (0.0, s, 0.0)))


def side_trap(M, part, mat, s, h, off, x0, lim, z0, z1):
    """Arka kenari x0, on kenari egik (lim(z)) dortgen: on cam direkleri boyunca biten pencereler."""
    y = s * (h + off)
    xa = lim(z0)
    xb = lim(z1)
    if min(xa, xb) - x0 < 0.02:
        return
    add_poly(M, part, mat, [(x0, y, z0), (xa, y, z0), (xb, y, z1), (x0, y, z1)], ("d", (0.0, s, 0.0)))


def cut_ranges(x0, x1, holes):
    segs = [(x0, x1)]
    for (h0, h1) in holes:
        nxt = []
        for (a, b) in segs:
            if h1 <= a or h0 >= b:
                nxt.append((a, b))
                continue
            if h0 > a:
                nxt.append((a, h0))
            if h1 < b:
                nxt.append((h1, b))
        segs = nxt
    return [sg for sg in segs if sg[1] - sg[0] > 0.05]


def window_row(M, s, h, x0, x1, z0, z1, pane, holes, lim, glass):
    """Siyah cam bandi + yuvarlak koseli camlar. lim(z): on kenar siniri (egik on direk)."""
    e = 0.05
    x1 = min(x1, max(lim(z0 - e), lim(z1 + e)))
    for (a, b) in cut_ranges(x0, x1, holes):
        front_cut = b > min(lim(z0 - e), lim(z1 + e)) - 0.02
        if front_cut:
            side_trap(M, "glazing", "frame", s, h, 0.008, a, lambda z: min(b, lim(z)), z0 - e, z1 + e)
        else:
            side_quad(M, "glazing", "frame", s, h, 0.008, a, b, z0 - e, z1 + e)
        length = b - a
        count = max(1, int(round(length / pane)))
        step = length / count
        for k in range(count):
            pa = a + k * step + 0.03
            pb = a + (k + 1) * step - 0.03
            if pb > min(lim(z0), lim(z1)) - 0.03:
                side_trap(M, "glass", glass, s, h, 0.012, pa, lambda z: min(pb, lim(z) - 0.03), z0, z1)
            else:
                add_poly(M, "glass", glass, rrect_xz(pa, pb, z0, z1, 0.07, s * (h + 0.012), 1 if Q["low"] else 3), ("d", (0.0, s, 0.0)))


def door(M, s, h, x0, x1, z0, z1, glass, split, bar_z):
    """Cift kanatli cam kapi (sehir otobusu/otokar) ya da tek kanat; kapali haliyle."""
    side_quad(M, "glazing", "frame", s, h, 0.009, x0, x1, z0, z1)
    leaves = [(x0 + 0.05, x1 - 0.05)]
    if split:
        m = (x0 + x1) / 2.0
        leaves = [(x0 + 0.05, m - 0.02), (m + 0.02, x1 - 0.05)]
    for (a, b) in leaves:
        add_poly(M, "door_glass", glass, rrect_xz(a, b, z0 + 0.08, z1 - 0.06, 0.06, s * (h + 0.013), 1 if Q["low"] else 3), ("d", (0.0, s, 0.0)))
        if bar_z > 0 and not Q["low"]:
            side_quad(M, "trim", "trim", s, h, 0.016, a, b, bar_z, bar_z + 0.05)


def seam_rect(M, s, h, x0, x1, z0, z1, t):
    """Ince kapi / bagaj kapagi derzi."""
    if Q["low"]:
        return
    side_quad(M, "seams", "seam", s, h, 0.007, x0, x1, z0, z0 + t)
    side_quad(M, "seams", "seam", s, h, 0.007, x0, x1, z1 - t, z1)
    side_quad(M, "seams", "seam", s, h, 0.007, x0, x0 + t, z0, z1)
    side_quad(M, "seams", "seam", s, h, 0.007, x1 - t, x1, z0, z1)


def arch(M, s, h, ax, R, zb):
    """Teker yuvasi: govde yanina koyu daire parcasi (kesik yerine)."""
    ra = R + 0.07
    sa = (zb - R) / ra
    sa = max(-0.99, min(0.99, sa))
    a0 = math.degrees(math.asin(sa))
    pts = []
    n = 8 if Q["low"] else 18
    for i in range(n + 1):
        a = math.radians(a0 + (180.0 - 2 * a0) * i / n)
        pts.append((ax + ra * math.cos(a), s * (h + 0.010), R + ra * math.sin(a)))
    add_poly(M, "arches", "arch", pts, ("d", (0.0, s, 0.0)))


def wheel(M, s, h, ax, R, rim):
    y_out = s * (h + 0.03)
    y_in = s * (h - 0.27)
    if Q["low"]:
        ring = []
        disc = []
        for i in range(8):
            t = 2.0 * math.pi * (i + 0.5) / 8
            ring.append((ax + R * math.cos(t), R + R * math.sin(t)))
            disc.append((ax + R * 0.6 * math.cos(t), s * (h + 0.036), R + R * 0.6 * math.sin(t)))
        ref = (ax, (y_in + y_out) / 2.0, R)
        for i in range(8):
            a = ring[i]
            b = ring[(i + 1) % 8]
            add_poly(M, "tires", "rubber", [(a[0], y_in, a[1]), (b[0], y_in, b[1]), (b[0], y_out, b[1]), (a[0], y_out, a[1])], ("p", ref))
        add_poly(M, "tires", "rubber", [(q[0], y_out, q[1]) for q in ring], ("d", (0.0, s, 0.0)))
        add_poly(M, "rims", rim, disc, ("d", (0.0, s, 0.0)))
        return
    cyl_y(M, "tires", "rubber", ax, R, R, y_in, y_out, 28)
    cyl_y(M, "rims", rim, ax, R, R * 0.64, y_out, s * (h + 0.036), 24)
    cyl_y(M, "hubs", "hub", ax, R, R * 0.42, s * (h + 0.036), s * (h + 0.05), 20)
    cyl_y(M, "hubs", "hubcap", ax, R, R * 0.16, s * (h + 0.05), s * (h + 0.065), 12)


# ---------------------------------------------------------------- on ve arka yuzey kaplamalari

def surf_panels(M, part, mat, table, sign, z0, z1, y0, y1, off):
    """On (sign=+1) ya da arka (sign=-1) yuzeye, z kirilimlarinda bolunmus duzlemsel dortgenler."""
    zs = [z0]
    for p in table:
        if z0 < p[0] < z1:
            zs.append(p[0])
    zs.append(z1)
    for i in range(len(zs) - 1):
        za = zs[i]
        zb_ = zs[i + 1]
        xa = interp(table, za) + sign * off
        xb = interp(table, zb_) + sign * off
        add_poly(M, part, mat, [(xa, y0, za), (xa, y1, za), (xb, y1, zb_), (xb, y0, zb_)], ("d", (sign * 1.0, 0.0, 0.0)))


def surf_rrect(M, part, mat, table, sign, z0, z1, y0, y1, r, off):
    """Kucuk duz parca (far, stop): yuvarlak koseli, yuzeyin o yuksekligindeki egime uyar."""
    pts2 = []
    r = min(r, (y1 - y0) * 0.45, (z1 - z0) * 0.45)
    for (cy, cz, a0) in [(y1 - r, z0 + r, -90.0), (y1 - r, z1 - r, 0.0), (y0 + r, z1 - r, 90.0), (y0 + r, z0 + r, 180.0)]:
        k = 1 if Q["low"] else 3
        for i in range(k + 1):
            a = math.radians(a0 + 90.0 * i / k)
            pts2.append((cy + r * math.cos(a), cz + r * math.sin(a)))
    za = interp(table, z0)
    zb_ = interp(table, z1)
    pts = []
    for (y, z) in pts2:
        t = (z - z0) / (z1 - z0)
        pts.append((za + (zb_ - za) * t + sign * off, y, z))
    add_poly(M, part, mat, pts, ("d", (sign * 1.0, 0.0, 0.0)))


# ---------------------------------------------------------------- araclar

def build_vehicle(spec):
    M = new_mesh()
    L = spec["L"]
    w = spec["w"]
    h = w / 2.0
    zb = spec["zb"]
    zt = spec["zt"]
    R = spec["R"]
    xf = L / 2.0
    xr = -L / 2.0
    ftab = [(z, xf + dx) for (z, dx) in spec["front"]]
    rtab = [(z, xr + dx) for (z, dx) in spec["rear"]]
    S = {"w": w, "zb": zb, "zt": zt, "rt": spec["rt"], "rb": 0.06}
    levels = list(spec.get("levels", []))
    cf = spec["cf"]
    cr = spec.get("cr", 0.1)

    def lim(z):
        return interp(ftab, z) - cf - 0.06

    def lim_rear_free(z):
        return 1e9

    sections = spec.get("sections")
    if sections is None:
        shell(M, "body", "paint", S, {"t": ftab, "c": cf}, {"t": rtab, "c": cr}, levels)
    else:
        # Korukli: on bolum + koruk + arka bolum
        j0, j1 = sections
        shell(M, "body", "paint", S, {"t": ftab, "c": cf}, {"t": [(zb, j0), (zt, j0)], "c": 0.06}, levels)
        shell(M, "body_rear", "paint", S, {"t": [(zb, j1), (zt, j1)], "c": 0.06}, {"t": rtab, "c": cr}, levels)
        ribs = 7
        for k in range(ribs):
            a = j1 + (j0 - j1) * k / float(ribs)
            b = j1 + (j0 - j1) * (k + 1) / float(ribs)
            inset = 0.05 if k % 2 == 0 else 0.11
            rbox(M, "bellows", "bellows", a, b, -h + inset, h - inset, zb + 0.12, zt - 0.06 - inset * 0.5, 0.12)

    paint2 = spec.get("stripe_mat", "stripe")
    for s in [-1.0, 1.0]:
        holes = spec.get("doors", []) if s < 0 else []
        hole_ranges = [(d[0] - 0.04, d[1] + 0.04) for d in holes]
        if sections is not None:
            hole_ranges = hole_ranges + [(sections[1] - 0.02, sections[0] + 0.02)]
        # alt etek
        if spec.get("skirt", 0) > 0:
            for (a, b) in cut_ranges(xr + cr + 0.02, lim(zb + 0.1) + 0.02, hole_ranges):
                side_quad(M, "skirt", "trim", s, h, 0.004, a, b, zb + 0.02, zb + spec["skirt"])
        # seritler
        for st in spec.get("stripes", []):
            x0 = xr + cr + 0.02 if st[0] is None else st[0]
            x1 = None if st[1] is None else st[1]
            for (a, b) in cut_ranges(x0, 1e9, hole_ranges):
                bb = b
                if x1 is not None:
                    bb = min(bb, x1)
                if bb - a < 0.05:
                    continue
                side_trap(M, "stripes", st[4] if len(st) > 4 else paint2, s, h, 0.006, a,
                          (lambda B: (lambda z: min(B, interp(ftab, z) - cf - 0.01)))(bb), st[2], st[3])
        # egik serit (arka ceyrekte yukselen)
        sw = spec.get("swoosh")
        if sw is not None:
            xa, xb, za, zb2, thick = sw
            y = s * (h + 0.006)
            add_poly(M, "stripes", paint2, [(xa, y, za), (xa + thick, y, za), (xb + thick, y, zb2), (xb, y, zb2)],
                     ("d", (0.0, s, 0.0)))
        # pencereler
        for row in spec["windows"]:
            x0, x1, z0, z1, pane = row
            window_row(M, s, h, xr + cr + x0, x1 if x1 is not None else 1e9, z0, z1, pane, hole_ranges, lim,
                       spec.get("glass", "glass"))
        # kapilar (sag taraf)
        if s < 0:
            for d in spec.get("doors", []):
                door(M, s, h, d[0], d[1], d[2], d[3], spec.get("door_glass", "glass"), d[4], d[5])
        # bagaj kapaklari
        for bay in spec.get("bays", []):
            seam_rect(M, s, h, bay[0], bay[1], bay[2], bay[3], 0.018)
        for sm in spec.get("seams", []):
            if sm[4] == 0 or sm[4] == s:
                seam_rect(M, s, h, sm[0], sm[1], sm[2], sm[3], 0.015)
        # tekerlekler
        for ax in spec["axles"]:
            arch(M, s, h, ax, R, zb)
            wheel(M, s, h, ax, R, spec.get("rim", "rim"))
        # aynalar
        mir = spec.get("mirror")
        if Q["low"]:
            mir = None
        if mir == "ears":
            mx = interp(ftab, zt - 0.3)
            box(M, "mirrors", "trim", mx - 0.25, mx + 0.32, s * (h - 0.25), s * (h - 0.13), zt - 0.34, zt - 0.26)
            box(M, "mirrors", "trim", mx + 0.22, mx + 0.34, s * (h - 0.26), s * (h - 0.04), zt - 0.95, zt - 0.3)
            box(M, "mirrors", "mirror_glass", mx + 0.34, mx + 0.345, s * (h - 0.25), s * (h - 0.05), zt - 0.92, zt - 0.33)
        elif mir == "car":
            mz = spec.get("mirror_z", 1.1)
            mx = interp(ftab, mz + 0.06) - cf - 0.02
            box(M, "mirrors", "paint", mx - 0.16, mx - 0.02, s * h, s * (h + 0.05), mz + 0.02, mz + 0.08)
            rbox(M, "mirrors", "paint", mx - 0.2, mx - 0.06, s * (h + 0.04) if s > 0 else s * (h + 0.22), s * (h + 0.22) if s > 0 else s * (h + 0.04),
                 mz, mz + 0.15, 0.04)
        elif mir == "van":
            mz = spec.get("mirror_z", 1.35)
            mx = interp(ftab, mz) - cf - 0.05
            box(M, "mirrors", "trim", mx - 0.06, mx, s * h, s * (h + 0.16), mz, mz + 0.05)
            box(M, "mirrors", "trim", mx - 0.08, mx + 0.02, s * (h + 0.12), s * (h + 0.26), mz - 0.05, mz + 0.2)

    # on yuz
    hf = h - cf
    ws = spec["windshield"]
    for (z0, z1) in ws:
        surf_panels(M, "glazing", "frame", ftab, 1.0, z0 - 0.04, z1 + 0.04, -hf + 0.04, hf - 0.04, 0.008)
        surf_panels(M, "glass", spec.get("glass_front", "glass"), ftab, 1.0, z0, z1, -hf + 0.09, hf - 0.09, 0.014)
    disp = spec.get("display")
    if disp is not None:
        z0, z1 = disp
        surf_panels(M, "glazing", "frame", ftab, 1.0, z0, z1, -hf + 0.12, hf - 0.12, 0.010)
        surf_panels(M, "lights", "led", ftab, 1.0, z0 + 0.06, z1 - 0.06, -hf * 0.55, hf * 0.55, 0.016)
    hl = spec["headlights"]
    z0, z1, yin, yout = hl
    for s in [-1.0, 1.0]:
        ya = s * (hf - yout)
        yb = s * (hf - yin)
        if not Q["low"]:
            surf_rrect(M, "lights", "lamp_surround", ftab, 1.0, z0 - 0.03, z1 + 0.03, min(ya, yb) - 0.03, max(ya, yb) + 0.03, 0.05, 0.010)
        surf_rrect(M, "lights", spec.get("lamp_mat", "headlamp"), ftab, 1.0, z0, z1, min(ya, yb), max(ya, yb), 0.04, 0.016)
        if spec.get("lamp_mat") is not None:
            surf_panels(M, "lights", "headlamp", ftab, 1.0, z1 - 0.035, z1 - 0.012, min(ya, yb) + 0.02, max(ya, yb) - 0.01, 0.019)
        if not Q["low"] and spec.get("indicators", True):
            surf_rrect(M, "lights", "indicator", ftab, 1.0, z0 - 0.1, z0 - 0.04, min(ya, yb), max(ya, yb), 0.02, 0.014)
    gr = spec.get("grille")
    if gr is not None:
        gf = spec.get("grille_frame")
        if gf is not None:
            surf_panels(M, "grille", gf, ftab, 1.0, gr[0] - 0.03, gr[1] + 0.03, -gr[2] - 0.03, gr[2] + 0.03, 0.008)
        surf_panels(M, "grille", "trim", ftab, 1.0, gr[0], gr[1], -gr[2], gr[2], 0.010)
        k = gr[0] + 0.03
        gstep = spec.get("grille_step", 0.05)
        gbar = 0.015 if gstep <= 0.05 else 0.03
        k = gr[0] + gstep * 0.6
        while k + 0.03 < gr[1] and not Q["low"]:
            surf_panels(M, "grille", spec.get("grille_bar", "trim_light"), ftab, 1.0, k, k + gbar, -gr[2] + 0.04, gr[2] - 0.04, 0.013)
            k += gstep
    hs = spec.get("head_side")
    if hs is not None:
        # far on yuzden kosenin cevresinden dolasip yana dogru geriye uzanir (premium van gibi)
        hz0 = hs[1]
        hz1 = hs[2]
        zs = [hz0]
        for p_ in ftab:
            if hz0 < p_[0] < hz1:
                zs.append(p_[0])
        zs.append(hz1)
        nseg = 2 if Q["low"] else 5
        e = 0.014
        for s in [-1.0, 1.0]:
            for zi in range(len(zs) - 1):
                za_ = zs[zi]
                zb2_ = zs[zi + 1]
                for k in range(nseg):
                    a0_ = math.radians(90.0 * k / nseg)
                    a1_ = math.radians(90.0 * (k + 1) / nseg)
                    pts = []
                    for (zz, aa) in [(za_, a0_), (za_, a1_), (zb2_, a1_), (zb2_, a0_)]:
                        pts.append((interp(ftab, zz) - cf + (cf + e) * math.sin(aa), s * (h - cf + (cf + e) * math.cos(aa)), zz))
                    out = (math.sin((a0_ + a1_) / 2.0), s * math.cos((a0_ + a1_) / 2.0), 0.0)
                    add_poly(M, "lights", spec.get("lamp_mat", "headlamp"), pts, ("d", out))
            hx0 = interp(ftab, hz1) - cf
            hx1 = interp(ftab, hz0) - cf
            y = s * (h + e)
            add_poly(M, "lights", spec.get("lamp_mat", "headlamp"), [(hx1, y, hz0), (hx0, y, hz1), (hx0 - hs[0], y, hz1),
                                                                     (hx1 - hs[0] * 0.35, y, hz0 + (hz1 - hz0) * 0.55)], ("d", (0.0, s, 0.0)))
    rl = spec.get("rails")
    if rl is not None and not Q["low"]:
        for s in [-1.0, 1.0]:
            rbox(M, "roof", "rim_polished", rl[0], rl[1], s * rl[2] - 0.022, s * rl[2] + 0.022, zt - 0.03, zt + 0.035, 0.015)
    it = spec.get("intake")
    if it is not None:
        surf_panels(M, "grille", "trim", ftab, 1.0, it[0], it[1], -it[2], it[2], 0.012)
    # tamponlar
    bx = interp(ftab, zb + 0.1)
    bd = spec.get("bumper_d", 0.05)
    bi = spec.get("bumper_inset", cf * 0.4)
    if spec.get("front_bumper", True):
        rbox(M, "bumpers", "trim", bx - 0.12, bx + bd, -h + bi, h - bi, zb - 0.04, zb + spec.get("bumper_h", 0.26), 0.05)
    rx = interp(rtab, zb + 0.1)
    rbox(M, "bumpers", "trim", rx - 0.05, rx + 0.12, -h + cr * 0.4, h - cr * 0.4, zb - 0.04, zb + 0.24, 0.05)
    # arka
    hr = h - cr
    for s in [-1.0, 1.0]:
        ya = s * (hr - 0.04)
        yb = s * (hr - 0.2)
        tl = spec.get("taillights", (0.7, 1.3))
        surf_rrect(M, "lights", "taillamp", rtab, -1.0, tl[0], tl[1], min(ya, yb), max(ya, yb), 0.04, 0.012)
    rw = spec.get("rear_window")
    if rw is not None:
        surf_panels(M, "glazing", "frame", rtab, -1.0, rw[0] - 0.04, rw[1] + 0.04, -hr + 0.1, hr - 0.1, 0.008)
        surf_panels(M, "glass", spec.get("glass", "glass"), rtab, -1.0, rw[0], rw[1], -hr + 0.15, hr - 0.15, 0.012)
    if spec.get("rear_split") and not Q["low"]:
        # cift kanatli arka kapi: ortada dikey derz
        surf_panels(M, "seams", "seam", rtab, -1.0, zb + 0.1, zt - 0.14, -0.009, 0.009, 0.016)
    fb = spec.get("front_bar")
    if fb is not None:
        # elektrikli: kapali on yuz, farlari birlestiren isik cubugu
        surf_panels(M, "lights", "headlamp", ftab, 1.0, fb[0], fb[1], -hf + yout, hf - yout, 0.014)
        surf_panels(M, "grille", "trim", ftab, 1.0, 0.56, 0.7, -0.5, 0.5, 0.010)
    eg = spec.get("engine_grille")
    if eg is not None and not Q["low"]:
        k = eg[0]
        while k + 0.03 < eg[1]:
            surf_panels(M, "grille", "trim", rtab, -1.0, k, k + 0.03, -hr * 0.6, hr * 0.6, 0.010)
            k += 0.07
    # tavan ekipmani
    for rb_ in spec.get("roof", []):
        x0, x1, half, hgt, mat = rb_
        rbox(M, "roof", mat, x0, x1, -half, half, zt - 0.04, zt + hgt, min(0.1, hgt * 0.45))
    return M


# Govde renkleri oyundaki segment renkleriyle ayni (haritadaki 3B otobusler): minibus beyaz/turuncu, midibus turkuaz,
# sehirlerarasi mavi, konfor mor, VIP altin, cift katli kirmizi, sehir otobusu beyaz/turkuaz.

def coach_spec(L, color_key, axles, extra):
    s = {
        "L": L, "w": 2.55, "zb": 0.42, "zt": 3.62, "rt": 0.24, "R": 0.52, "cf": 0.2, "cr": 0.14,
        "front": [(0.42, 0.0), (1.08, -0.02), (3.36, -0.44), (3.62, -0.58)],
        "rear": [(0.42, 0.0), (3.62, 0.06)],
        "levels": [1.5, 2.95],
        "windows": [(0.25, None, 1.6, 2.92, 1.55)],
        "doors": [(L / 2 - 1.42, L / 2 - 0.42, 0.5, 3.05, False, -1), (-1.55, -0.7, 0.5, 3.0, False, -1)],
        "windshield": [(1.12, 3.3)],
        "display": None,
        "headlights": (0.62, 0.8, 0.06, 0.5),
        "grille": (0.48, 0.58, 0.55),
        "skirt": 0.1,
        "bays": [],
        "mirror": "ears",
        "rear_window": (2.2, 3.2),
        "engine_grille": (0.75, 1.25),
        "taillights": (0.62, 1.5),
        "axles": axles,
        "rim": "rim_polished",
        "roof": [(-2.6, -0.4, 0.85, 0.24, "roof_unit")],
        "color": color_key,
    }
    x = -L / 2 + 0.5
    ends = [a for a in axles]
    while x + 1.3 < L / 2 - 1.6:
        clash = False
        for ax in ends:
            if abs((x + x + 1.2) / 2 - ax) < 1.25:
                clash = True
        for d in s["doors"]:
            if x + 1.2 > d[0] - 0.05 and x < d[1] + 0.05:
                clash = True
        if not clash:
            s["bays"].append((x, x + 1.2, 0.52, 1.36))
        x += 1.32
    for k in extra:
        s[k] = extra[k]
    return s


def city_spec(L, color_key, axles, doors, extra):
    s = {
        "L": L, "w": 2.55, "zb": 0.32, "zt": 3.02, "rt": 0.16, "R": 0.5, "cf": 0.18, "cr": 0.12,
        "front": [(0.32, 0.0), (0.92, -0.02), (2.62, -0.1), (3.02, -0.2)],
        "rear": [(0.32, 0.0), (3.02, 0.04)],
        "levels": [1.0, 2.62],
        "windows": [(0.15, None, 1.08, 2.62, 1.42)],
        "doors": doors,
        "windshield": [(0.98, 2.56)],
        "display": (2.62, 2.92),
        "headlights": (0.56, 0.74, 0.06, 0.46),
        "grille": None,
        "skirt": 0.16,
        "mirror": "ears",
        "rear_window": (2.0, 2.62),
        "engine_grille": (0.7, 1.4),
        "taillights": (0.6, 1.6),
        "axles": axles,
        "rim": "rim",
        "stripes": [(None, None, 0.5, 0.72)],
        "roof": [(L / 2 - 4.6, L / 2 - 2.2, 0.9, 0.26, "roof_unit"), (-L / 2 + 0.4, -L / 2 + 2.0, 0.95, 0.42, "roof_unit")],
        "color": color_key,
    }
    for k in extra:
        s[k] = extra[k]
    return s


def van_spec(L, zt, ac, color_key):
    xf = L / 2.0
    s = {
        "L": L, "w": 2.02, "zb": 0.4, "zt": zt, "rt": 0.16, "R": 0.36, "cf": 0.14, "cr": 0.1,
        "front": [(0.4, 0.0), (0.94, -0.07), (1.1, -0.66), (zt - 0.18, -1.28), (zt, -1.36)],
        "rear": [(0.4, 0.0), (zt, 0.06)],
        "levels": [1.3, zt - 0.4],
        "windows": [(0.25, None, 1.32, zt - 0.42, 1.05)],
        "doors": [],
        "seams": [(xf - 2.62, xf - 1.62, 0.46, zt - 0.32, -1.0), (xf - 1.6, xf - 0.86, 0.5, zt - 0.6, 0.0)],
        "windshield": [(1.16, zt - 0.24)],
        "display": None,
        "headlights": (0.74, 0.88, 0.05, 0.34),
        "grille": (0.62, 0.86, 0.46),
        "skirt": 0.0,
        "mirror": "van",
        "mirror_z": 1.2,
        "rear_window": (1.35, zt - 0.5),
        "taillights": (0.65, 1.25),
        "axles": [xf - 0.98, -L / 2 + 1.3],
        "rim": "rim",
        "stripes": [(None, None, 0.98, 1.1), (None, None, 1.13, 1.17)],
        "bumper_h": 0.3,
        "roof": [],
        "color": color_key,
    }
    if ac:
        s["roof"] = [(-1.6, 0.2, 0.7, 0.2, "roof_unit")]
    return s


def servis_spec(L, zt, kind, color_key):
    """Ihale isleri icin servis araci (Serce ailesi): kisa yuvarlak burun, dik on cam, yuksek tavanli modellerde
    camin ustunde govde rengi tavan kapagi, sagda surgulu kapi, arkada cift kanat, tavanda servis tabelasi."""
    xf = L / 2.0
    high = zt > 2.4
    if high:
        front = [(0.4, 0.0), (0.78, -0.02), (1.0, -0.16), (1.14, -0.6), (1.98, -1.1), (2.1, -1.16), (zt - 0.07, -1.2),
                 (zt, -1.28)]
        shield = (1.18, 1.94)
        wtop = 1.98
    else:
        front = [(0.4, 0.0), (0.78, -0.02), (1.0, -0.16), (1.14, -0.6), (zt - 0.16, -1.14), (zt, -1.24)]
        shield = (1.18, zt - 0.22)
        wtop = zt - 0.42
    s = {
        "L": L, "w": 2.0, "zb": 0.38, "zt": zt, "rt": 0.18, "R": 0.35, "cf": 0.16, "cr": 0.1,
        "front": front,
        "rear": [(0.38, 0.0), (zt, 0.04)],
        "levels": [1.28, wtop + 0.06],
        "windows": [(0.22, None, 1.3, wtop, 0.98)],
        "doors": [],
        "seams": [(xf - 2.5, xf - 1.56, 0.46, wtop + 0.06, -1.0), (xf - 1.5, xf - 0.88, 0.5, 1.26, 0.0)],
        "windshield": [shield],
        "display": None,
        "headlights": (0.8, 0.97, 0.04, 0.38),
        "grille": (0.52, 0.76, 0.44),
        "skirt": 0.0,
        "mirror": "van",
        "mirror_z": 1.24,
        "rear_window": (1.32, wtop - 0.04),
        "rear_split": True,
        "taillights": (0.62, 1.22),
        "axles": [xf - 0.92, -L / 2 + (1.05 if L < 6.0 else 1.3)],
        "rim": "rim",
        "stripes": [(None, None, 0.9, 1.1), (None, None, 1.16, 1.2)],
        "bumper_h": 0.32,
        "roof": [(xf - 1.98, xf - 1.48, 0.46, 0.15, "sign")],
        "color": color_key,
    }
    if kind == "compact_vip":
        # kucuk VIP yolcu vani (7 deri koltuk), premium orta boy van oranlari: kisa egimli kaput, kaputla ayni cizgide
        # yatik on cam, genis krom citali izgara, ince yana uzanan farlar, altta hava girisi, buyuk jant
        s.update({
            "w": 1.92, "zb": 0.3, "R": 0.34, "rt": 0.22, "cf": 0.34,
            "front": [(0.3, -0.04), (0.52, 0.0), (0.74, -0.04), (0.86, -0.1), (0.93, -0.22), (1.0, -0.46), (1.08, -0.76),
                      (zt - 0.08, -1.7), (zt, -1.82)],
            "rear": [(0.3, 0.0), (zt - 0.12, 0.05), (zt, 0.12)],
            "levels": [0.9, 1.04, zt - 0.14],
            "windows": [(0.14, None, 1.08, zt - 0.2, 0.86)],
            "seams": [(xf - 2.3, xf - 1.5, 0.4, zt - 0.16, -1.0), (xf - 1.48, xf - 0.98, 0.42, 1.0, 0.0)],
            "windshield": [(1.12, zt - 0.14)],
            "headlights": (0.77, 0.9, 0.0, 0.3),
            "head_side": (0.34, 0.77, 0.9),
            "lamp_mat": "mirror_glass",
            "indicators": False,
            "grille": (0.54, 0.86, 0.4),
            "grille_step": 0.08,
            "grille_bar": "rim_polished",
            "grille_frame": "rim_polished",
            "front_bumper": False,
            "intake": (0.34, 0.46, 0.56),
            "rails": (-L / 2 + 0.35, L / 2 - 2.05, 0.66),
            "mirror": "car",
            "mirror_z": 1.08,
            "rear_window": (1.1, zt - 0.22),
            "rear_split": False,
            "taillights": (0.66, 1.1),
            "axles": [xf - 0.98, -L / 2 + 0.98],
            "glass": "glass_tint",
            "door_glass": "glass_tint",
            "rim": "rim_polished",
            # krom cam cercevesi ve bel cizgisi: siyah govdede camlar ve cizgi okunsun
            "stripes": [(None, None, 1.02, 1.04, "rim_polished")],
            "bumper_h": 0.12,
            "roof": [],
        })
    if kind in ("ac", "vip"):
        s["roof"].append((-1.5, 0.1, 0.62, 0.2, "roof_unit"))
    if kind == "vip":
        s["roof"] = [(-1.5, 0.1, 0.62, 0.18, "roof_unit")]
        s["glass"] = "glass_tint"
        s["door_glass"] = "glass_tint"
        s["rim"] = "rim_polished"
        # krom cam cercevesi (koyu govdede camlar okunsun) + ince altin serit
        s["stripes"] = [(None, None, 1.2, 1.235, "rim_polished"), (None, None, wtop + 0.07, wtop + 0.1, "rim_polished"),
                        (None, None, 1.1, 1.13), (None, None, 0.5, 0.56, "trim_light")]
        s["grille"] = (0.5, 0.78, 0.48)
    if kind == "electric":
        s["grille"] = None
        s["front_bar"] = (0.99, 1.03)
        s["stripes"] = [(None, None, 0.9, 1.1), (None, None, 1.16, 1.2)]
        s["roof"].append((-1.2, 0.0, 0.5, 0.1, "battery"))
    return s


def vehicle_specs():
    V = {}
    V["KARTAL_M16"] = van_spec(6.2, 2.55, False, "minibus")
    V["KARTAL_M19"] = van_spec(7.0, 2.78, True, "minibus")
    # Servis araclari (yalniz ihale isleri): Serce ailesi
    V["SERCE_9"] = servis_spec(5.15, 2.25, "", "servis")
    V["SERCE_14"] = servis_spec(5.95, 2.55, "", "servis")
    V["SERCE_17"] = servis_spec(6.95, 2.6, "ac", "servis")
    V["SERCE_19"] = servis_spec(7.4, 2.86, "ac", "servis")
    V["SERCE_VIP12"] = servis_spec(6.95, 2.7, "vip", "servis_vip")
    V["SERCE_14E"] = servis_spec(5.95, 2.55, "electric", "servis_e")
    V["SERCE_K7"] = servis_spec(4.9, 1.9, "compact_vip", "servis_k")
    for (vid, L, two) in [("ANKA_27", 8.1, False), ("ANKA_31", 9.0, True)]:
        xf = L / 2.0
        doors = [(xf - 1.32, xf - 0.34, 0.42, 2.72, True, 1.2)]
        if two:
            doors.append((-0.9, 0.1, 0.42, 2.72, True, 1.2))
        V[vid] = {
            "L": L, "w": 2.33, "zb": 0.36, "zt": 3.0, "rt": 0.2, "R": 0.43, "cf": 0.17, "cr": 0.12,
            "front": [(0.36, 0.0), (0.98, -0.03), (2.74, -0.3), (3.0, -0.4)],
            "rear": [(0.36, 0.0), (3.0, 0.05)],
            "levels": [1.12, 2.4],
            "windows": [(0.2, None, 1.16, 2.38, 1.3)],
            "doors": doors,
            "windshield": [(1.02, 2.66)],
            "display": None,
            "headlights": (0.6, 0.76, 0.05, 0.42),
            "grille": (0.46, 0.56, 0.45),
            "skirt": 0.1,
            "mirror": "ears",
            "rear_window": (1.7, 2.45),
            "engine_grille": (0.65, 1.1),
            "taillights": (0.6, 1.35),
            "axles": [xf - 1.85, -L / 2 + 2.05],
            "rim": "rim",
            "stripes": [(None, None, 0.86, 1.0), (None, None, 1.03, 1.07)],
            "roof": [(-1.4, 0.6, 0.8, 0.22, "roof_unit")],
            "color": "midibus",
        }
    V["TOROS_46"] = coach_spec(12.2, "intercity", [6.1 - 2.75, -6.1 + 3.4], {
        "stripes": [(None, None, 1.4, 1.5)]})
    V["TOROS_42C"] = coach_spec(12.2, "comfort", [6.1 - 2.75, -6.1 + 3.4], {
        "stripes": [(None, None, 1.38, 1.45), (None, None, 1.49, 1.52)], "stripe_mat": "stripe"})
    V["ZIRVE_VIP"] = coach_spec(13.0, "vip", [6.5 - 2.8, -6.5 + 3.05, -6.5 + 4.45], {
        "glass": "glass_tint", "glass_front": "glass", "door_glass": "glass_tint",
        "stripes": [(None, None, 1.4, 1.48)], "rim": "rim_polished"})
    dd = coach_spec(13.7, "double", [6.85 - 2.8, -6.85 + 3.1, -6.85 + 4.5], {})
    dd.update({
        "zt": 4.0, "rt": 0.26,
        "front": [(0.4, 0.0), (1.02, -0.02), (3.72, -0.26), (4.0, -0.4)],
        "rear": [(0.4, 0.0), (4.0, 0.06)],
        "zb": 0.4,
        "levels": [1.0, 2.05, 2.36, 3.7],
        "windows": [(0.3, None, 1.06, 1.96, 1.4), (0.3, None, 2.42, 3.66, 1.6)],
        "doors": [(6.85 - 1.42, 6.85 - 0.42, 0.48, 2.0, False, -1), (-1.55, -0.7, 0.48, 2.0, False, -1)],
        "windshield": [(1.06, 2.0), (2.42, 3.72)],
        "display": (2.04, 2.36),
        "stripes": [(None, None, 2.02, 2.38, "stripe")],
        "rear_window": (2.6, 3.6),
        "bays": [],
        "roof": [(-3.0, -1.0, 0.85, 0.2, "roof_unit")],
    })
    V["ZIRVE_DD"] = dd
    xf = 6.0
    V["KENT_12"] = city_spec(12.0, "city", [xf - 2.7, -6.0 + 3.45],
                             [(xf - 1.38, xf - 0.28, 0.36, 2.66, True, 1.15), (-0.95, 0.35, 0.36, 2.66, True, 1.15)], {})
    V["KENT_12E"] = city_spec(12.0, "city_e", [xf - 2.7, -6.0 + 3.45],
                              [(xf - 1.38, xf - 0.28, 0.36, 2.66, True, 1.15), (-0.95, 0.35, 0.36, 2.66, True, 1.15)], {
        "stripes": [(None, None, 0.5, 0.72), (None, None, 2.68, 2.74, "stripe")],
        "roof": [(xf - 5.2, xf - 2.3, 0.95, 0.34, "battery"), (xf - 8.5, xf - 5.6, 0.95, 0.34, "battery")],
        "engine_grille": None})
    V["KENT_18"] = city_spec(18.0, "city", [9.0 - 2.7, -0.35, -9.0 + 2.6],
                             [(9.0 - 1.38, 9.0 - 0.28, 0.36, 2.66, True, 1.15), (1.15, 2.4, 0.36, 2.66, True, 1.15),
                              (-4.7, -3.45, 0.36, 2.66, True, 1.15)], {
        "sections": (-1.85, -2.55),
        "roof": [(9.0 - 4.6, 9.0 - 2.2, 0.9, 0.26, "roof_unit"), (-9.0 + 0.4, -9.0 + 2.0, 0.95, 0.42, "roof_unit")]})
    return V


# Malzemeler: (r, g, b) ve yuzey turu. Govde ve serit rengi araca gore degisir.
BODY_COLORS = {
    "minibus": ((244, 243, 238), (255, 140, 20)),
    "midibus": ((64, 196, 200), (250, 252, 252)),
    "intercity": ((62, 128, 240), (250, 252, 255)),
    "comfort": ((150, 96, 230), (255, 196, 40)),
    "vip": ((232, 178, 52), (40, 40, 52)),
    "double": ((226, 72, 62), (250, 250, 250)),
    "city": ((238, 241, 238), (0, 160, 145)),
    "city_e": ((238, 241, 238), (0, 196, 128)),
    "servis": ((246, 246, 242), (34, 72, 138)),
    "servis_vip": ((40, 52, 78), (212, 174, 92)),
    "servis_e": ((246, 246, 242), (0, 186, 118)),
    "servis_k": ((34, 36, 42), (214, 218, 224)),
}

MATERIALS = {
    "frame": ((22, 24, 28), "matte"),
    "glass": ((34, 52, 72), "glass"),
    "glass_tint": ((18, 22, 30), "glass"),
    "rubber": ((28, 29, 32), "rubber"),
    "rim": ((176, 182, 190), "metal"),
    "rim_polished": ((214, 218, 224), "chrome"),
    "hub": ((120, 126, 134), "metal"),
    "hubcap": ((60, 64, 70), "metal"),
    "trim": ((44, 48, 54), "plastic"),
    "trim_light": ((88, 94, 102), "plastic"),
    "seam": ((30, 32, 36), "matte"),
    "arch": ((14, 15, 17), "matte"),
    "headlamp": ((236, 242, 250), "lamp"),
    "lamp_surround": ((26, 28, 32), "plastic"),
    "indicator": ((255, 166, 40), "lamp"),
    "taillamp": ((214, 30, 36), "lamp"),
    "led": ((255, 150, 30), "led"),
    "mirror_glass": ((150, 170, 190), "chrome"),
    "roof_unit": ((222, 226, 230), "plastic"),
    "battery": ((206, 212, 218), "plastic"),
    "bellows": ((38, 40, 44), "rubber"),
    "sign": ((250, 196, 36), "plastic"),
}


def material_table(color_key):
    t = dict(MATERIALS)
    body, stripe = BODY_COLORS[color_key]
    t["paint"] = (body, "metallic" if color_key in ("vip", "servis_vip", "servis_k") else "paint")
    t["stripe"] = (stripe, "paint")
    return t
