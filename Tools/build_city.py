#!/usr/bin/env python3
"""
Şehir içi modun verisi: 10 büyük şehrin gerçek sokak haritası, yol ağı, mahalle durakları ve ünlü mekânlar.

Kaynak: OpenFreeMap vektör döşemeleri (OpenMapTiles şeması; veri © OpenStreetMap katkıcıları, ODbL;
https://openfreemap.org). Atıf oyunda şehir haritasının köşesinde ve README'de yazılıdır.

Her şehir için (Assets/StreamingAssets/Cities/<kimlik>/):
  map.jpg     Koyu temalı şehir haritası (uzun kenar 4096 px; yazısız: adlar oyunda çizilir)
  city.bytes  İkili veri (küçük-endian, sürüm 1), koordinatlar metre (x doğu, z kuzey; şehir merkezi 0,0):
                "BTCY" u16 sürüm
                str ad, f64 merkez enlem, f64 merkez boylam, f32 xmin zmin xmax zmax (haritanın kapladığı alan)
                u16 durak sayısı   × (str ad, f32 x, f32 z, f32 ağırlık 0..1, i32 en yakın yol düğümü)
                u16 mekân sayısı   × (str ad, u8 tür, f32 x, f32 z, f32 ün 0..1)
                i32 düğüm sayısı   × (f32 x, f32 z)
                i32 kenar sayısı   × (i32 a, i32 b, u8 yol sınıfı)
              str: u16 bayt uzunluğu + UTF-8
  Mekân türleri: 0 saray/kale, 1 cami, 2 stadyum, 3 müze, 4 kule/simge, 5 çarşı/AVM, 6 üniversite, 7 park, 8 kilise/sinagog
  Yol sınıfları: 0 ana yol (motorway/trunk/primary), 1 ara yol (secondary), 2 mahalle yolu (tertiary)

Kullanım: python3 Tools/build_city.py [şehir kimliği ...]   (boşsa hepsi)
Gereksinim: mapbox-vector-tile, cairosvg, Pillow (pip); internet (tiles.openfreemap.org)
Önbellek: Tools/.cache/tiles (git'e girmez)
"""
import concurrent.futures as cf
import io
import json
import math
import os
import struct
import sys
import urllib.request

import cairosvg
import mapbox_vector_tile as mvt
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "StreamingAssets", "Cities")
CACHE = os.path.join(REPO, "Tools", ".cache", "tiles")
TILEJSON = "https://tiles.openfreemap.org/planet"
Z = 14
IMAGE = 4096
USER_AGENT = "bus-tycoon-city-builder/1.0"

# Kimlik, ad, sınırlar (batı, güney, doğu, kuzey): şehrin dolmuş/belediye otobüsü çekirdeği
CITIES = [
    ("istanbul", "İstanbul", (28.78, 40.95, 29.26, 41.13)),
    ("ankara", "Ankara", (32.64, 39.82, 33.00, 40.01)),
    ("izmir", "İzmir", (26.96, 38.34, 27.28, 38.51)),
    ("bursa", "Bursa", (28.90, 40.15, 29.18, 40.25)),
    ("antalya", "Antalya", (30.56, 36.84, 30.86, 36.96)),
    ("adana", "Adana", (35.20, 36.95, 35.42, 37.07)),
    ("konya", "Konya", (32.38, 37.81, 32.60, 37.94)),
    ("gaziantep", "Gaziantep", (37.28, 37.00, 37.47, 37.12)),
    ("kayseri", "Kayseri", (35.39, 38.67, 35.61, 38.78)),
    ("samsun", "Samsun", (36.20, 41.22, 36.44, 41.34)),
]

# Koyu tema (arayüzle aynı aile)
BACKGROUND = "#1A2A2F"
STYLE_POLYGONS = [
    ("landcover", lambda p: p.get("class") in ("grass", "wood", "farmland", "wetland"), "#1C3530", 1.0),
    ("landuse", lambda p: p.get("class") in ("residential", "suburb", "neighbourhood"), "#1D2F35", 1.0),
    ("park", lambda p: True, "#1B3A31", 1.0),
    ("landuse", lambda p: p.get("class") in ("cemetery", "stadium", "pitch", "university", "school", "hospital"), "#1C3530", 1.0),
    ("water", lambda p: True, "#0B2230", 1.0),
]
ROAD_STYLE = [("minor", "#2A3D43", 0.7), ("service", "#26393F", 0.45), ("tertiary", "#34494F", 1.2), ("secondary", "#3F545A", 1.6),
              ("primary", "#506268", 2.1), ("trunk", "#5C696C", 2.4), ("motorway", "#71643F", 2.7)]
# Otoyol da ağda: boğaz köprüleri ve çevre yolları şehrin iki yakasını bağlar
# OSM'deki yazım hataları (Türkçe büyük İ eksik vb.)
NAME_FIXES = {"Inönü": "İnönü"}
GRAPH_CLASSES = {"motorway": 0, "trunk": 0, "primary": 0, "secondary": 1, "tertiary": 2}

POI_KINDS_UNUSED = [  # (eski otomatik seçim; ün sıralaması döşeme içi olduğu için elle seçime geçildi)
    (0, lambda c, s: c == "castle" or s in ("castle", "palace", "fort"), 5),
    (1, lambda c, s: c == "place_of_worship" and s == "muslim", 5),
    (2, lambda c, s: c == "stadium" or s == "stadium", 3),
    (3, lambda c, s: c == "museum" or s == "museum", 4),
    (4, lambda c, s: c in ("attraction", "monument") or s in ("attraction", "monument", "tower", "viewpoint"), 5),
    (5, lambda c, s: s in ("mall", "marketplace", "department_store") or c == "shop" and s == "mall", 3),
    (6, lambda c, s: c == "college" or s in ("university", "college"), 3),
    (8, lambda c, s: c == "place_of_worship" and s in ("christian", "jewish"), 2),
]


def tile_url_template():
    with urllib.request.urlopen(urllib.request.Request(TILEJSON, headers={"User-Agent": USER_AGENT}), timeout=60) as r:
        return json.load(r)["tiles"][0]


def tile_xy(lon, lat):
    n = 2 ** Z
    return (lon + 180) / 360 * n, (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n


def fetch_tiles(template, bbox):
    os.makedirs(CACHE, exist_ok=True)
    west, south, east, north = bbox
    x0, y1 = tile_xy(west, south)
    x1, y0 = tile_xy(east, north)
    jobs = [(x, y) for x in range(int(x0), int(x1) + 1) for y in range(int(y0), int(y1) + 1)]

    def get(xy):
        x, y = xy
        path = os.path.join(CACHE, f"{Z}_{x}_{y}.pbf")
        if not os.path.exists(path):
            req = urllib.request.Request(template.format(z=Z, x=x, y=y), headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(req, timeout=90) as r:
                data = r.read()
            with open(path, "wb") as f:
                f.write(data)
        return x, y, path

    with cf.ThreadPoolExecutor(8) as ex:
        return list(ex.map(get, jobs))


class City:
    def __init__(self, cid, name, bbox):
        self.cid, self.name, self.bbox = cid, name, bbox
        west, south, east, north = bbox
        self.lat0, self.lon0 = (south + north) / 2, (west + east) / 2
        n = 2 ** Z * 256
        self.n = n
        gx0, gy0 = self.gp(west, north)
        gx1, gy1 = self.gp(east, south)
        self.gx0, self.gy0, self.gx1, self.gy1 = gx0, gy0, gx1, gy1
        # Mercator pikselinden metreye: merkez enlemdeki ölçek (şehir ölçeğinde doğrusal)
        self.m_per_px = 40075016.686 * math.cos(math.radians(self.lat0)) / n
        cx, cy = self.gp(self.lon0, self.lat0)
        self.cx, self.cy = cx, cy
        w, h = gx1 - gx0, gy1 - gy0
        self.img_w, self.img_h = (IMAGE, round(IMAGE * h / w)) if w >= h else (round(IMAGE * w / h), IMAGE)
        self.s = self.img_w / w

    def gp(self, lon, lat):
        return (lon + 180) / 360 * self.n, (1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * self.n

    def px(self, g):  # küresel piksel → resim pikseli
        return (g[0] - self.gx0) * self.s, (g[1] - self.gy0) * self.s

    def meters(self, g):  # küresel piksel → metre (x doğu, z kuzey)
        return (g[0] - self.cx) * self.m_per_px, -(g[1] - self.cy) * self.m_per_px

    def bounds_m(self):
        x0, z1 = self.meters((self.gx0, self.gy0))
        x1, z0 = self.meters((self.gx1, self.gy1))
        return x0, z0, x1, z1


def load_layers(city, tiles):
    layers = {}
    for tx, ty, path in tiles:
        data = mvt.decode(open(path, "rb").read(), default_options={"y_coord_down": True})
        for lname, layer in data.items():
            ext = layer["extent"]
            for f in layer["features"]:
                def conv(c):
                    return tx * 256 + c[0] / ext * 256, ty * 256 + c[1] / ext * 256
                g = f["geometry"]
                t, cs = g["type"], g["coordinates"]
                if t == "LineString":
                    parts = [[conv(c) for c in cs]]
                elif t == "MultiLineString":
                    parts = [[conv(c) for c in line] for line in cs]
                elif t == "Polygon":
                    parts = [[[conv(c) for c in ring] for ring in cs]]
                elif t == "MultiPolygon":
                    parts = [[[conv(c) for c in ring] for ring in poly] for poly in cs]
                elif t == "Point":
                    parts = [conv(cs)]
                elif t == "MultiPoint":
                    parts = [conv(c) for c in cs]
                else:
                    continue
                layers.setdefault(lname, []).append((t, parts, f["properties"]))
    return layers


def render_map(city, layers, out_path):
    W, H = city.img_w, city.img_h

    def path_line(pts):
        return "M" + " L".join(f"{x:.1f} {y:.1f}" for x, y in (city.px(p) for p in pts))

    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
           f'<rect width="{W}" height="{H}" fill="{BACKGROUND}"/>']
    for lname, pred, fill, op in STYLE_POLYGONS:
        ds = []
        for t, parts, p in layers.get(lname, []):
            if not pred(p) or not t.endswith("Polygon"):
                continue
            for poly in parts:
                ds.append(" ".join(path_line(r) + "Z" for r in poly))
        if ds:
            # Her çokgen ayrı yol: tek yolda "evenodd" kuralı, komşu döşemelerin örtüşen kenar şeridini delik sayıyor
            # ve denizde ince bir ızgara çiziliyordu. Delikler (iç halkalar) yine kendi çokgeninde evenodd ile açılır.
            # Aynı renkli ince kenar, döşeme sınırındaki kenar yumuşatma çizgisini kapatır.
            edge = 1.5 if lname == "water" else 1.0
            svg.append(f'<g fill="{fill}" fill-opacity="{op}" fill-rule="evenodd" stroke="{fill}" stroke-width="{edge}" stroke-linejoin="round">')
            svg.extend(f'<path d="{d}"/>' for d in ds)
            svg.append('</g>')
    scale = max(0.8, IMAGE / 4096)
    for cls, col, w in ROAD_STYLE:
        ds = [path_line(line) for t, parts, p in layers.get("transportation", []) if p.get("class") == cls and t.endswith("LineString") for line in parts]
        if ds:
            svg.append(f'<path d="{" ".join(ds)}" stroke="{col}" stroke-width="{w * scale:.2f}" fill="none" stroke-linecap="round" stroke-linejoin="round"/>')
    ds = [path_line(line) for t, parts, p in layers.get("transportation", []) if p.get("class") in ("rail", "transit") and t.endswith("LineString") for line in parts]
    if ds:
        svg.append(f'<path d="{" ".join(ds)}" stroke="#3B4C52" stroke-width="0.9" stroke-dasharray="3 2" fill="none"/>')
    ds = [path_line(line) for t, parts, p in layers.get("transportation", []) if p.get("class") == "ferry" and t.endswith("LineString") for line in parts]
    if ds:
        svg.append(f'<path d="{" ".join(ds)}" stroke="#2F5C73" stroke-width="0.9" stroke-dasharray="4 3" fill="none" opacity=".5"/>')
    svg.append("</svg>")
    png = cairosvg.svg2png(bytestring="\n".join(svg).encode("utf-8"), output_width=W, output_height=H)
    Image.open(io.BytesIO(png)).convert("RGB").save(out_path, "JPEG", quality=86, optimize=True, progressive=False)


def build_graph(city, layers):
    """Ana, ara ve mahalle yolları: düğümler metre cinsinden, döşeme sınırlarındaki kopukluklar birleştirilir."""
    nodes, index, edges = [], {}, {}
    q = 0.5  # metre: aynı noktayı birleştirme hassasiyeti

    def node(g):
        x, z = city.meters(g)
        key = (round(x / q), round(z / q))
        i = index.get(key)
        if i is None:
            i = index[key] = len(nodes)
            nodes.append((x, z))
        return i

    west, south, east, north = city.bbox
    for t, parts, p in layers.get("transportation", []):
        cls = GRAPH_CLASSES.get(p.get("class"))
        if cls is None or not t.endswith("LineString"):
            continue
        for line in parts:
            ids = [node(g) for g in line]
            for a, b in zip(ids, ids[1:]):
                if a != b:
                    k = (min(a, b), max(a, b))
                    edges[k] = min(edges.get(k, 9), cls)
    # Kavşaklar: döşemedeki çizgiler kavşakta ortak nokta paylaşmayabilir (sadeleştirme). Açıkta kalan her uç
    # (tek bağlantılı düğüm) 15 m içindeki en yakın yol parçasına bağlanır; parça o noktadan bölünür.
    degree = [0] * len(nodes)
    for a, b in edges:
        degree[a] += 1
        degree[b] += 1
    cell = 60.0
    seg_grid = {}

    def seg_cells(a, b):
        (ax, az), (bx, bz) = nodes[a], nodes[b]
        for gx in range(int(min(ax, bx) // cell), int(max(ax, bx) // cell) + 1):
            for gz in range(int(min(az, bz) // cell), int(max(az, bz) // cell) + 1):
                yield gx, gz

    for key in list(edges):
        for c in seg_cells(*key):
            seg_grid.setdefault(c, set()).add(key)
    joined = 0
    for i in range(len(nodes)):
        if degree[i] != 1:
            continue
        x, z = nodes[i]
        best, bd, bt = None, 15.0, 0.0
        for dx in (-1, 0, 1):
            for dz in (-1, 0, 1):
                for key in seg_grid.get((int(x // cell) + dx, int(z // cell) + dz), ()):
                    a, b = key
                    if a == i or b == i or key not in edges:
                        continue
                    (ax, az), (bx, bz) = nodes[a], nodes[b]
                    vx, vz = bx - ax, bz - az
                    l2 = vx * vx + vz * vz
                    t = 0.0 if l2 < 1e-9 else max(0.0, min(1.0, ((x - ax) * vx + (z - az) * vz) / l2))
                    d = math.hypot(ax + t * vx - x, az + t * vz - z)
                    if d < bd:
                        best, bd, bt = key, d, t
        if best is None:
            continue
        a, b = best
        cls = edges.pop(best)
        if bt <= 0.02 or bt >= 0.98:
            j = a if bt <= 0.02 else b
            edges[best] = cls
        else:
            (ax, az), (bx, bz) = nodes[a], nodes[b]
            j = len(nodes)
            nodes.append((ax + bt * (bx - ax), az + bt * (bz - az)))
            degree.append(0)
            for k in ((min(a, j), max(a, j)), (min(j, b), max(j, b))):
                edges[k] = cls
                for c in seg_cells(*k):
                    seg_grid.setdefault(c, set()).add(k)
        if j != i:
            k = (min(i, j), max(i, j))
            edges[k] = min(edges.get(k, 9), cls)
            degree[i] += 1
            joined += 1
    print(f"  kavşak: {joined} uç bağlandı")
    # En büyük bağlı parça (adalar ve kopuk parçalar atılır)
    adj = [[] for _ in nodes]
    for a, b in edges:
        adj[a].append(b)
        adj[b].append(a)
    seen, best = [False] * len(nodes), []
    for s in range(len(nodes)):
        if seen[s] or not adj[s]:
            continue
        comp, stack = [], [s]
        seen[s] = True
        while stack:
            v = stack.pop()
            comp.append(v)
            for w in adj[v]:
                if not seen[w]:
                    seen[w] = True
                    stack.append(w)
        if len(comp) > len(best):
            best = comp
    keep = sorted(best)
    remap = {old: new for new, old in enumerate(keep)}
    nodes2 = [nodes[i] for i in keep]
    edges2 = [(remap[a], remap[b], c) for (a, b), c in edges.items() if a in remap and b in remap]
    return nodes2, edges2


def nearest_node(nodes, x, z):
    best, bd = 0, float("inf")
    for i, (nx, nz) in enumerate(nodes):
        d = (nx - x) ** 2 + (nz - z) ** 2
        if d < bd:
            best, bd = i, d
    return best, math.sqrt(bd)


def build_stops(city, layers, nodes):
    """
    Mahalle durakları: önce ilçeler (town), sonra semt ve mahalleler (suburb, quarter, neighbourhood); birbirine
    1 km'den yakın olanlardan önemlisi kalır. Bütün şehre yayılır (en çok 110 durak).
    """
    cands = []
    x0, z0, x1, z1 = city.bounds_m()
    order_of = {"town": 0, "suburb": 1, "quarter": 2, "neighbourhood": 3}
    for t, pt, p in layers.get("place", []):
        if t != "Point" or p.get("class") not in order_of:
            continue
        name = p.get("name:tr") or p.get("name")
        name = NAME_FIXES.get(name, name)
        if not name or len(name) > 26:
            continue
        x, z = city.meters(pt[0])
        margin = 600
        if not (x0 + margin < x < x1 - margin and z0 + margin < z < z1 - margin):
            continue
        cands.append((order_of[p["class"]], p.get("rank", 30), name, x, z))
    cands.sort()
    stops, names = [], set()
    for order, rank, name, x, z in cands:
        spacing = 1200 if order == 0 else 1000
        if name in names or any(math.hypot(x - s[1], z - s[2]) < spacing for s in stops):
            continue
        node, dist = nearest_node(nodes, x, z)
        if dist > 900:
            continue
        weight = (1.0, 0.65, 0.5, 0.4)[order] * (1.0 - min(rank, 30) / 90)
        stops.append((name, x, z, round(weight, 3), node))
        names.add(name)
        if len(stops) >= 110:
            break
    return stops


def build_landmarks(city, layers):
    """Elle seçilmiş ünlü mekânlar (Tools/city_landmarks.json); konum aynı adlı OpenStreetMap noktasına oturtulur."""
    curated = json.load(open(os.path.join(REPO, "Tools", "city_landmarks.json"), encoding="utf-8")).get(city.cid, [])
    pois = {}
    for t, pts, p in layers.get("poi", []):
        if t != "Point":
            continue
        name = (p.get("name:tr") or p.get("name") or "").strip().casefold()
        if name:
            pois.setdefault(name, []).append(city.meters(pts[0]))
    x0, z0, x1, z1 = city.bounds_m()
    picked = []
    for name, kind, lat, lon, fame in curated:
        x, z = city.meters(city.gp(lon, lat))
        snapped = min(pois.get(name.casefold(), []), key=lambda q: math.hypot(q[0] - x, q[1] - z), default=None)
        if snapped is not None and math.hypot(snapped[0] - x, snapped[1] - z) < 1500:
            x, z = snapped
        if x0 < x < x1 and z0 < z < z1:
            picked.append((name, kind, x, z, fame))
        else:
            print(f"  uyarı: {name} harita dışında, atlandı")
    return picked


def write_bytes(city, stops, landmarks, nodes, edges, path):
    def s(text):
        b = text.encode("utf-8")
        return struct.pack("<H", len(b)) + b

    x0, z0, x1, z1 = city.bounds_m()
    out = bytearray(b"BTCY") + struct.pack("<H", 1)
    out += s(city.name) + struct.pack("<dd", city.lat0, city.lon0) + struct.pack("<ffff", x0, z0, x1, z1)
    out += struct.pack("<H", len(stops))
    for name, x, z, w, node in stops:
        out += s(name) + struct.pack("<fffi", x, z, w, node)
    out += struct.pack("<H", len(landmarks))
    for name, kind, x, z, fame in landmarks:
        out += s(name) + struct.pack("<Bfff", kind, x, z, fame)
    out += struct.pack("<i", len(nodes))
    for x, z in nodes:
        out += struct.pack("<ff", x, z)
    out += struct.pack("<i", len(edges))
    for a, b, c in edges:
        out += struct.pack("<iiB", a, b, c)
    with open(path, "wb") as f:
        f.write(out)


def main():
    wanted = set(sys.argv[1:])
    template = tile_url_template()
    for cid, name, bbox in CITIES:
        if wanted and cid not in wanted:
            continue
        city = City(cid, name, bbox)
        tiles = fetch_tiles(template, bbox)
        layers = load_layers(city, tiles)
        folder = os.path.join(OUT, cid)
        os.makedirs(folder, exist_ok=True)
        render_map(city, layers, os.path.join(folder, "map.jpg"))
        nodes, edges = build_graph(city, layers)
        stops = build_stops(city, layers, nodes)
        landmarks = build_landmarks(city, layers)
        write_bytes(city, stops, landmarks, nodes, edges, os.path.join(folder, "city.bytes"))
        size = sum(os.path.getsize(os.path.join(folder, f)) for f in os.listdir(folder)) // 1024
        print(f"{name}: {len(tiles)} döşeme, harita {city.img_w}×{city.img_h}, {len(nodes)} düğüm, {len(edges)} kenar, "
              f"{len(stops)} durak, {len(landmarks)} mekân, {size} KB")


if __name__ == "__main__":
    main()
