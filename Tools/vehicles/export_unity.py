#!/usr/bin/env python3
"""
Haritadaki 3B araçlar: bus_gen.py'nin az çokgenli sürümü -> Assets/_Project/Resources/BusTycoon/Models/vehicles.bytes

Biçim (little-endian): "BTV1", int32 model sayısı; her model: byte uzunluk + ASCII kimlik, int32 köşe, int32 indeks,
köşe başına 3 int16 konum (milimetre; Unity ekseni: +x sağ, +y yukarı, +z ileri; taban y=0, orta x=z=0),
3 int8 normal (×127), 1 bayt malzeme yuvası (SLOTS sırası); ardından uint16 üçgen indeksleri (Unity ön yüz: Cross(b-a, c-a) dışa bakar).
Gövde ("body") köşeleri her modelde en başta gelir (şehir haritasında hat rengine boyanır).
Kullanım: python3 Tools/vehicles/export_unity.py [çıktı]
"""
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE)) if os.path.basename(os.path.dirname(HERE)) == "Tools" else None
G = {"math": math}
exec(open(os.path.join(HERE, "bus_gen.py")).read(), G)
G["Q"]["low"] = True

SLOTS = ["body", "stripe", "glass", "darkglass", "frame", "tire", "hub", "trim", "arch", "headlight", "indicator", "taillight",
         "led", "roofunit"]
MAT_SLOT = {
    "paint": "body", "stripe": "stripe", "glass": "glass", "glass_tint": "darkglass", "frame": "frame", "seam": "frame",
    "lamp_surround": "frame", "rubber": "tire", "bellows": "tire", "rim": "hub", "rim_polished": "hub", "hub": "hub",
    "hubcap": "hub", "mirror_glass": "hub", "trim": "trim", "trim_light": "trim", "arch": "arch", "headlamp": "headlight",
    "indicator": "indicator", "taillamp": "taillight", "led": "led", "roof_unit": "roofunit", "battery": "roofunit", "sign": "indicator",
}
# Avrupa Paketi modelleri: en yakın gövdeden
ALIASES = {"NORDSTAR_E12": ("TOROS_46", {"roof": [(-2.6, 1.2, 0.95, 0.34, "battery")]}),
           "ALPINA_C50": ("ZIRVE_VIP", {})}


def to_unity(p):
    return (-p[1], p[2], p[0])


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def build(spec):
    M = G["build_vehicle"](spec)
    order = sorted(M["order"], key=lambda k: 0 if M["parts"][k]["mat"] == "paint" else 1)
    verts, idx = [], []
    for key in order:
        part = M["parts"][key]
        slot = SLOTS.index(MAT_SLOT[part["mat"]])
        for poly in part["polys"]:
            n = G["newell"](poly)
            ln = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2)
            nu = to_unity((n[0] / ln, n[1] / ln, n[2] / ln))
            base = len(verts)
            pts = [to_unity(q) for q in poly]
            for q in pts:
                verts.append((q, nu, slot))
            for i in range(1, len(pts) - 1):
                a, b, c = 0, i, i + 1
                if sum(x * y for x, y in zip(cross(sub(pts[b], pts[a]), sub(pts[c], pts[a])), nu)) < 0:
                    b, c = c, b
                idx.extend([base + a, base + b, base + c])
    return smooth(verts, idx)


def smooth(verts, idx, cos_limit=math.cos(math.radians(40))):
    """Aynı yerde, aynı yuvada ve yakın normalli köşeleri birleştirir (yuvarlak kenarlar yumuşak, köşeler keskin kalır)."""
    groups = {}
    remap = [0] * len(verts)
    out = []
    for i, (p, n, slot) in enumerate(verts):
        key = (round(p[0], 4), round(p[1], 4), round(p[2], 4), slot)
        found = None
        for j in groups.get(key, []):
            m = out[j][3]
            if n[0] * m[0] + n[1] * m[1] + n[2] * m[2] > cos_limit:
                found = j
                break
        if found is None:
            found = len(out)
            out.append([p, [n[0], n[1], n[2]], slot, n])
            groups.setdefault(key, []).append(found)
        else:
            acc = out[found][1]
            acc[0] += n[0]
            acc[1] += n[1]
            acc[2] += n[2]
        remap[i] = found
    result = []
    for p, acc, slot, _ in out:
        ln = math.sqrt(acc[0] ** 2 + acc[1] ** 2 + acc[2] ** 2) or 1.0
        result.append((p, (acc[0] / ln, acc[1] / ln, acc[2] / ln), slot))
    return result, [remap[i] for i in idx]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO or ".", "Assets", "_Project", "Resources", "BusTycoon", "Models", "vehicles.bytes")
    specs = G["vehicle_specs"]()
    models = []
    for vid in specs:
        models.append((vid, specs[vid]))
    for vid, (src, extra) in ALIASES.items():
        sp = dict(specs[src])
        sp.update(extra)
        models.append((vid, sp))
    blob = bytearray(b"BTV1")
    blob += struct.pack("<i", len(models))
    for vid, spec in models:
        verts, idx = build(spec)
        assert len(verts) < 65535
        blob += struct.pack("<B", len(vid)) + vid.encode("ascii")
        blob += struct.pack("<ii", len(verts), len(idx))
        for (p, n, slot) in verts:
            q = [int(round(c * 1000)) for c in p]           # milimetre
            m = [int(round(c * 127)) for c in n]
            blob += struct.pack("<3h3bB", q[0], q[1], q[2], m[0], m[1], m[2], slot)
        blob += struct.pack("<%dH" % len(idx), *idx)
        print(vid, "köşe", len(verts), "üçgen", len(idx) // 3)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "wb").write(bytes(blob))
    print("yazıldı", out, len(blob), "bayt")


if __name__ == "__main__":
    main()
