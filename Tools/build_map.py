#!/usr/bin/env python3
"""
Otobüs İmparatoru harita dokusu ve karayolu ağı üreticisi.

Girdi (indirip tek klasöre koyun; hepsi Natural Earth, kamu malı):
  https://github.com/nvkelso/natural-earth-vector/tree/master/geojson
    ne_10m_admin_0_countries.geojson         ülkeler
    ne_10m_admin_1_states_provinces.geojson  iller (Türkiye il sınırları)
    ne_10m_lakes.geojson                     göller
    ne_10m_rivers_lake_centerlines.geojson   nehirler
    ne_10m_roads.geojson                     karayolları ve feribot hatları
  https://naciscdn.org/naturalearth/10m/raster/SR_HR.zip → SR_HR.tif   gölgeli kabartma (dağlar)

Çıktı:
  Assets/_Project/Resources/BusTycoon/map.jpg        4096 px Mercator doku (sınırlar DefaultContent.MapBounds ile AYNI);
                                                     kenarına doğru hafifçe bulanıklaşır
  Assets/_Project/Resources/BusTycoon/map_outer.jpg  haritanın çevresi (DefaultContent.OuterMapBounds): düşük çözünürlük,
                                                     uzaklaştıkça bulanıklaşır, kararır ve denize/sise karışır —
                                                     harita "tak" diye bitmez, devam ediyormuş gibi görünür
  Assets/_Project/Resources/BusTycoon/roads.txt      yol ağı: düğümler + kenarlar (Runtime/World/RoadNetwork.cs okur)

Kullanım:  python3 Tools/build_map.py <veri_klasörü> [--preview önizleme.png]
Gereksinim: Pillow, numpy
"""
import argparse
import heapq
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

# --- Harita sınırları: Runtime/Data/DefaultContent.cs → MapBounds ile birebir aynı tutun ---
MIN_LON, MAX_LON = 19.0, 47.0
MIN_LAT, MAX_LAT = 34.0, 45.0
TEX_WIDTH = 4096
SUPERSAMPLE = 2
# --- Çevre haritası: DefaultContent.OuterMap* ile aynı. Kamera en uzaktan ~14° kuzeye, ~20° yana görebilir. ---
OUTER_LON, OUTER_LAT = (1.0, 65.0), (27.0, 54.0)
OUTER_WIDTH = 2048

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon")

# Oynanabilir ülkeler (DefaultContent bölgeleri) canlı, diğerleri soluk.
PLAYABLE = {"TUR", "BGR", "GRC", "GEO", "ROU"}
LOWLAND = {"TUR": (140, 158, 104)}
LOWLAND_PLAYABLE = (128, 148, 104)
LOWLAND_OTHER = (104, 112, 100)
MOUNTAIN = (164, 146, 108)
MOUNTAIN_OTHER = (120, 112, 102)
SEA_DEEP = (13, 38, 66)
SEA = (19, 53, 88)
SEA_SHALLOW = (42, 104, 136)
LAKE = (54, 116, 152)
RIVER = (64, 128, 168, 210)
FLAT_SHADE = 206.0          # SR_HR'de düz arazinin gri değeri

# Yol sınıfları — RoadNetwork.RoadClass ile aynı sıra: 0 otoyol, 1 ana yol, 2 diğer yol, 3 feribot.
# Natural Earth'te Türkiye'deki devlet yollarının çoğu "Road" ya da "Unknown" türündedir.
ROAD_TYPES = {"Major Highway": 0, "Secondary Highway": 1, "Road": 1, "Unknown": 2, "Ferry Route": 3}
MAX_FERRY_KM = 30.0         # boğaz/körfez geçişleri; uzun deniz hatları otobüs ağına girmez
CLIP_MARGIN = 0.6           # derece; sınır dışına taşan yollar bir miktar korunur
SNAP_DEG = 0.012            # ~1,2 km: uç noktası başka bir yola bu kadar yakınsa kavşak say
SIMPLIFY_DEG = 0.004        # ~400 m Douglas–Peucker toleransı
BRIDGE_DEG = 0.06           # ~6 km: kopuk küçük parçaları ana ağa bağlama mesafesi

# Natural Earth 1:10m verisinden yeni otoyol köprüleri (boylam, enlem): uçlar en yakın yol düğümüne bağlanır.
NEW_CROSSINGS = [
    ("Osmangazi Köprüsü (2016)", (29.512, 40.762), (29.508, 40.722)),
    ("1915 Çanakkale Köprüsü (2022)", (26.655, 40.370), (26.706, 40.334)),
]


def merc_y(lat):
    return math.log(math.tan(math.pi / 4 + math.radians(lat) / 2))


Y_MIN, Y_MAX = merc_y(MIN_LAT), merc_y(MAX_LAT)


class View:
    """Mercator görüntü alanı: coğrafi sınırlar + piksel boyutu."""

    def __init__(self, lon, lat, width):
        self.min_lon, self.max_lon = lon
        self.min_lat, self.max_lat = lat
        self.y_min, self.y_max = merc_y(self.min_lat), merc_y(self.max_lat)
        self.width = width
        # Blok sıkıştırma (DXT/ASTC) için kenarlar 4'ün katı olmalı; en-boy farkı ihmal edilebilir (%0,05).
        self.height = 4 * round(width * (self.y_max - self.y_min) / math.radians(self.max_lon - self.min_lon) / 4)
        # Çekirdek haritaya göre piksel yoğunluğu: bulanıklık yarıçapları buna göre ölçeklenir.
        self.density = (width / (self.max_lon - self.min_lon)) / (TEX_WIDTH / (MAX_LON - MIN_LON))

    def project(self, lon, lat, scale=1):
        return ((lon - self.min_lon) / (self.max_lon - self.min_lon) * self.width * scale,
                (1.0 - (merc_y(lat) - self.y_min) / (self.y_max - self.y_min)) * self.height * scale)

    def near(self, ring, margin=3.0):
        return any(self.min_lon - margin <= x <= self.max_lon + margin and self.min_lat - margin <= y <= self.max_lat + margin
                   for x, y in ring[:: max(1, len(ring) // 50)])


CORE = View((MIN_LON, MAX_LON), (MIN_LAT, MAX_LAT), TEX_WIDTH)
OUTER = View(OUTER_LON, OUTER_LAT, OUTER_WIDTH)
TEX_HEIGHT = CORE.height


def load(data_dir, name):
    with open(os.path.join(data_dir, name + ".geojson"), encoding="utf-8") as f:
        return json.load(f)["features"]


def polygons(geometry):
    if geometry is None:
        return
    if geometry["type"] == "Polygon":
        yield geometry["coordinates"]
    elif geometry["type"] == "MultiPolygon":
        yield from geometry["coordinates"]


def lines(geometry):
    if geometry is None:
        return
    if geometry["type"] == "LineString":
        yield geometry["coordinates"]
    elif geometry["type"] == "MultiLineString":
        yield from geometry["coordinates"]


def km(a, b):
    lat1, lat2 = math.radians(a[1]), math.radians(b[1])
    dlat, dlon = lat2 - lat1, math.radians(b[0] - a[0])
    h = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
    return 2 * 6371.0088 * math.asin(math.sqrt(h))


# =============================================================================== harita dokusu

def render_map(data_dir, view, outer=False):
    """Haritayı verilen görüntü alanında çizer. Vektörler 2× çizilip küçültülür (yumuşak kenar); kabartma ve renk
    hesabı doğrudan doku boyutunda yapılır. Çevre haritasında il sınırları yerine soluk ana yollar çizilir."""
    width, height = view.width * SUPERSAMPLE, view.height * SUPERSAMPLE
    size = (view.width, view.height)

    def ring_points(ring):
        return [view.project(x, max(-85.0, min(85.0, y)), SUPERSAMPLE) for x, y in ring]

    countries = [f for f in load(data_dir, "ne_10m_admin_0_countries")]

    # 1) Kara maskesi ve ülke renkleri (ova rengi + dağ rengi)
    land = Image.new("L", (width, height), 0)
    lowland = Image.new("RGB", (width, height), LOWLAND_OTHER)
    mountain = Image.new("RGB", (width, height), MOUNTAIN_OTHER)
    land_draw, low_draw, mount_draw = ImageDraw.Draw(land), ImageDraw.Draw(lowland), ImageDraw.Draw(mountain)
    for feature in countries:
        iso3 = feature["properties"].get("ADM0_A3")
        low = LOWLAND.get(iso3, LOWLAND_PLAYABLE if iso3 in PLAYABLE else LOWLAND_OTHER)
        high = MOUNTAIN if iso3 in PLAYABLE else MOUNTAIN_OTHER
        for polygon in polygons(feature["geometry"]):
            if not view.near(polygon[0]):
                continue
            outer_ring = ring_points(polygon[0])
            land_draw.polygon(outer_ring, fill=255)
            low_draw.polygon(outer_ring, fill=low)
            mount_draw.polygon(outer_ring, fill=high)
            for hole in polygon[1:]:
                land_draw.polygon(ring_points(hole), fill=0)

    # Kıyı çizgisi: kara maskesinin dış kenarı (ülkeler arası sınırlar hariç)
    grown = np.asarray(land.filter(ImageFilter.MaxFilter(3 * SUPERSAMPLE // 2 * 2 + 1)), np.int16)
    coast_edge = Image.fromarray(np.clip(grown - np.asarray(land, np.int16), 0, 255).astype(np.uint8)).resize(size, Image.BOX)

    land = land.resize(size, Image.BOX)
    lowland = lowland.resize(size, Image.BOX)
    mountain = mountain.resize(size, Image.BOX)

    shade, rough = relief_layers(data_dir, view)
    land_a = np.asarray(land, dtype=np.float32) / 255.0
    low_a = np.asarray(lowland, dtype=np.float32)
    high_a = np.asarray(mountain, dtype=np.float32)
    terrain = (low_a + (high_a - low_a) * (rough[..., None] * 0.6)) * shade[..., None]

    # 2) Deniz: açıkta koyu, kıyıda sığ su parlaması
    glow_near = np.asarray(land.filter(ImageFilter.GaussianBlur(14 * view.density)), dtype=np.float32) / 255.0
    glow_far = np.asarray(land.filter(ImageFilter.GaussianBlur(70 * view.density)), dtype=np.float32) / 255.0
    sea = np.empty_like(terrain)
    for c in range(3):
        depth = SEA_DEEP[c] + (SEA[c] - SEA_DEEP[c]) * np.clip(glow_far * 2.2, 0, 1)
        sea[..., c] = depth + (SEA_SHALLOW[c] - depth) * np.clip(glow_near * 1.6, 0, 1) ** 1.4

    image_a = sea + (terrain - sea) * land_a[..., None]
    image = Image.fromarray(np.clip(image_a, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    del terrain, sea, image_a, low_a, high_a

    # 3) Göller, nehirler, il ve ülke sınırları (2× çizilip küçültülür)
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    for feature in load(data_dir, "ne_10m_lakes"):
        for polygon in polygons(feature["geometry"]):
            if view.near(polygon[0], 0.5):
                draw.polygon(ring_points(polygon[0]), fill=LAKE + (255,), outline=(36, 84, 116, 255))

    for feature in load(data_dir, "ne_10m_rivers_lake_centerlines"):
        rank = feature["properties"].get("scalerank") or 10
        if rank > 7:
            continue
        river_width = max(1, round((2.6 if rank <= 3 else 1.9 if rank <= 5 else 1.3) * SUPERSAMPLE))
        for line in lines(feature["geometry"]):
            if view.near(line, 0.5):
                draw.line(ring_points(line), fill=RIVER, width=river_width, joint="curve")

    if not outer:
        for feature in load(data_dir, "ne_10m_admin_1_states_provinces"):
            if feature["properties"].get("adm0_a3") != "TUR":
                continue
            for polygon in polygons(feature["geometry"]):
                draw.line(ring_points(polygon[0]), fill=(255, 255, 240, 38), width=SUPERSAMPLE, joint="curve")

    for feature in countries:
        for polygon in polygons(feature["geometry"]):
            if view.near(polygon[0]):
                draw.line(ring_points(polygon[0]), fill=(24, 26, 26, 150), width=3 * SUPERSAMPLE, joint="curve")
    for feature in countries:
        for polygon in polygons(feature["geometry"]):
            if view.near(polygon[0]):
                draw.line(ring_points(polygon[0]), fill=(240, 230, 205, 105), width=SUPERSAMPLE, joint="curve")

    if outer:
        # Oyunda yollar ayrı çizilir (RoadNetwork); çevrede ise yolların devamı dokuya soluk olarak işlenir.
        for feature in load(data_dir, "ne_10m_roads"):
            road_class = ROAD_TYPES.get(feature["properties"].get("type"))
            if road_class not in (0, 1):
                continue
            color = (255, 184, 77, 190) if road_class == 0 else (250, 242, 219, 110)
            for line in lines(feature["geometry"]):
                if view.near(line, 0.5):
                    draw.line(ring_points(line), fill=color, width=SUPERSAMPLE * (2 if road_class == 0 else 1), joint="curve")

    image = Image.alpha_composite(image, overlay.resize(size, Image.LANCZOS))
    del overlay

    coast = Image.new("RGBA", size, (16, 40, 60, 255))
    return Image.composite(coast, image, coast_edge.point(lambda v: v * 150 // 255)).convert("RGB")


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def blur_levels(image, sigmas, distance_px, sigma_of):
    """Uzaklığa göre artan bulanıklık: önceden bulanıklaştırılmış katmanlar arasında piksel piksel ara değer."""
    layers = [np.asarray(image if s == 0 else image.filter(ImageFilter.GaussianBlur(s)), dtype=np.float32) for s in sigmas]
    wanted = sigma_of(distance_px)
    result = np.zeros_like(layers[0])
    for i in range(len(sigmas) - 1):
        lo, hi = sigmas[i], sigmas[i + 1]
        weight_lo = np.clip((hi - wanted) / (hi - lo), 0, 1) * (wanted >= lo) * (wanted <= hi)
        weight_hi = np.clip((wanted - lo) / (hi - lo), 0, 1) * (wanted >= lo) * (wanted < hi)
        result += layers[i] * weight_lo[..., None] + layers[i + 1] * weight_hi[..., None]
    result += layers[-1] * (wanted >= sigmas[-1])[..., None]
    return result


def soften_core_edges(core, outer):
    """Çekirdek haritanın dış şeridi kenara doğru çevre haritasına karışır: çevre dokusu, ekran kartının
    çizeceği gibi (çift doğrusal) çekirdek çözünürlüğüne büyütülür; kenarda iki doku piksel piksel aynıdır,
    dikiş görünmez. İçeri doğru keskin harita yumuşakça geri gelir."""
    w, h = core.size
    x0, y0 = OUTER.project(MIN_LON, MAX_LAT)
    x1, y1 = OUTER.project(MAX_LON, MIN_LAT)
    outer_up = outer.resize((w, h), Image.BILINEAR, box=(x0, y0, x1, y1))
    band = 0.04 * w
    x = np.minimum(np.arange(w, dtype=np.float32), np.arange(w, dtype=np.float32)[::-1])
    y = np.minimum(np.arange(h, dtype=np.float32), np.arange(h, dtype=np.float32)[::-1])
    edge = np.minimum(x[None, :], y[:, None])
    weight = 1 - smoothstep(0, band, edge)
    mask = Image.fromarray((weight * 255).astype(np.uint8))
    return Image.composite(outer_up, core, mask)


def finish_outer(image):
    """Çevre haritası: çekirdekten uzaklaştıkça bulanıklık, hafif kararma ve renk solması; dış kenarda koyu
    denize (kameranın arka plan rengi) karışır."""
    w, h = image.size
    x0, y0 = OUTER.project(MIN_LON, MAX_LAT)
    x1, y1 = OUTER.project(MAX_LON, MIN_LAT)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx = np.maximum(np.maximum(x0 - xx, xx - x1), 0)
    dy = np.maximum(np.maximum(y0 - yy, yy - y1), 0)
    distance = np.sqrt(dx * dx + dy * dy)                  # çekirdek dikdörtgenine uzaklık (piksel)
    border = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy))

    result = blur_levels(image, [0, 1.5, 3.0, 5.0, 8.0], distance, lambda d: np.minimum(8.0, d * 0.045))
    luminance = result @ np.array([0.299, 0.587, 0.114], np.float32)
    fade = smoothstep(0, 0.22 * w, distance)
    result = result + (luminance[..., None] - result) * (0.35 * fade)[..., None]   # renk solması
    result *= (1.0 - 0.3 * fade)[..., None]                                      # kararma
    fog = 1.0 - smoothstep(0, 0.11 * w, border)
    result = result + (np.array(SEA_DEEP, np.float32) - result) * fog[..., None]
    return Image.fromarray(np.clip(result, 0, 255).astype(np.uint8), "RGB")


def build_textures(data_dir):
    outer = finish_outer(render_map(data_dir, OUTER, outer=True))
    path = os.path.join(OUT_DIR, "map_outer.jpg")
    outer.save(path, quality=88, subsampling=0, optimize=True)
    print(f"map_outer.jpg: {outer.size[0]}×{outer.size[1]}, {os.path.getsize(path) // 1024} KB")

    core = soften_core_edges(render_map(data_dir, CORE), outer)
    path = os.path.join(OUT_DIR, "map.jpg")
    core.save(path, quality=93, subsampling=0, optimize=True)
    print(f"map.jpg: {core.size[0]}×{core.size[1]}, {os.path.getsize(path) // 1024} KB")


def relief_layers(data_dir, view):
    """SR_HR (eşdikdörtgen) → Mercator gölge çarpanı ve engebe (0..1)."""
    Image.MAX_IMAGE_PIXELS = None
    width, height = view.width, view.height
    source = Image.open(os.path.join(data_dir, "SR_HR.tif"))
    sw, sh = source.size
    lat_top, lat_bottom = view.max_lat + 0.5, view.min_lat - 0.5
    box = (round((view.min_lon + 180) / 360 * sw), round((90 - lat_top) / 180 * sh),
           round((view.max_lon + 180) / 360 * sw), round((90 - lat_bottom) / 180 * sh))
    crop = source.crop(box)
    crop_h = crop.size[1]
    columns = np.asarray(crop.resize((width, crop_h), Image.BICUBIC), dtype=np.float32)

    # Her çıktı satırının enlemi → kaynak satırı (doğrusal ara değer)
    rows = (np.arange(height, dtype=np.float64) + 0.5) / height
    lat = np.degrees(2 * np.arctan(np.exp(view.y_max - rows * (view.y_max - view.y_min))) - np.pi / 2)
    src = (lat_top - lat) / (lat_top - lat_bottom) * crop_h - 0.5
    i0 = np.clip(np.floor(src).astype(int), 0, crop_h - 1)
    i1 = np.clip(i0 + 1, 0, crop_h - 1)
    t = np.clip(src - i0, 0, 1)[:, None].astype(np.float32)
    values = columns[i0] * (1 - t) + columns[i1] * t
    # PIL bulanıklaştırması kayan nokta görüntü kabul etmez; 8 bit gri yeterince hassas.
    gray = Image.fromarray(np.clip(values, 0, 255).astype(np.uint8))
    values = np.asarray(gray.filter(ImageFilter.GaussianBlur(max(0.4, 1.2 * view.density))), dtype=np.float32)

    shade = np.clip(values / FLAT_SHADE, 0.55, 1.2) ** 0.7
    variance = np.clip(((values - FLAT_SHADE) / 50.0) ** 2, 0, 4) * 63.0
    variance = Image.fromarray(variance.astype(np.uint8)).filter(ImageFilter.GaussianBlur(9 * view.density))
    rough = np.clip(np.sqrt(np.asarray(variance, dtype=np.float32) / 63.0) * 1.4 - 0.12, 0, 1)
    return shade.astype(np.float32), rough.astype(np.float32)


# =============================================================================== yol ağı

def clip_line(line):
    """Harita (+pay) dışına çıkan kısımları keser; içeride kalan ardışık parçaları döndürür."""
    pieces, current = [], []
    for x, y in line:
        inside = MIN_LON - CLIP_MARGIN <= x <= MAX_LON + CLIP_MARGIN and MIN_LAT - CLIP_MARGIN <= y <= MAX_LAT + CLIP_MARGIN
        if inside:
            current.append((x, y))
        elif current:
            pieces.append(current)
            current = []
    if current:
        pieces.append(current)
    return [p for p in pieces if len(p) >= 2]


def simplify(points, tolerance):
    """Douglas–Peucker; uç noktalar korunur."""
    if len(points) <= 2:
        return points
    keep = [False] * len(points)
    keep[0] = keep[-1] = True
    stack = [(0, len(points) - 1)]
    while stack:
        first, last = stack.pop()
        ax, ay = points[first]
        bx, by = points[last]
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy) or 1e-12
        best, index = 0.0, -1
        for i in range(first + 1, last):
            px, py = points[i]
            d = abs(dy * (px - ax) - dx * (py - ay)) / length
            if d > best:
                best, index = d, i
        if best > tolerance and index > 0:
            keep[index] = True
            stack.append((first, index))
            stack.append((index, last))
    return [p for p, k in zip(points, keep) if k]


def key(p):
    return (round(p[0] * 1e5), round(p[1] * 1e5))


def build_roads(data_dir):
    raw = []
    for feature in load(data_dir, "ne_10m_roads"):
        road_class = ROAD_TYPES.get(feature["properties"].get("type"))
        if road_class is None:
            continue
        for line in lines(feature["geometry"]):
            for piece in clip_line([tuple(p[:2]) for p in line]):
                if road_class == 3 and sum(km(a, b) for a, b in zip(piece, piece[1:])) > MAX_FERRY_KM:
                    continue
                raw.append((road_class, piece))

    # 1) Kavşaklar: birden çok yolun paylaştığı noktalar + uç noktalar. Natural Earth'te yolların çoğu
    #    başka bir yolun iki noktası ARASINA değer (T kavşak); uç nokta en yakın yol parçasına izdüşürülür ve
    #    o noktaya yeni bir kavşak eklenir.
    usage = {}
    for _, piece in raw:
        for p in set(key(q) for q in piece):
            usage[p] = usage.get(p, 0) + 1
    junctions = {k for k, n in usage.items() if n > 1}

    cell = SNAP_DEG * 2
    segment_grid = {}
    for li, (_, piece) in enumerate(raw):
        for si in range(len(piece) - 1):
            (ax, ay), (bx, by) = piece[si], piece[si + 1]
            for cx in range(int(min(ax, bx) / cell) - 1, int(max(ax, bx) / cell) + 2):
                for cy in range(int(min(ay, by) / cell) - 1, int(max(ay, by) / cell) + 2):
                    segment_grid.setdefault((cx, cy), []).append((li, si))

    inserts = {}   # (yol, parça) → [(t, nokta)]
    for li, (_, piece) in enumerate(raw):
        for end in (0, -1):
            p = piece[end]
            if key(p) in junctions:
                continue
            best, best_d = None, SNAP_DEG
            for lj, sj in segment_grid.get((int(p[0] / cell), int(p[1] / cell)), ()):
                if lj == li and (sj == 0 if end == 0 else sj == len(piece) - 2):
                    continue
                (ax, ay), (bx, by) = raw[lj][1][sj], raw[lj][1][sj + 1]
                dx, dy = bx - ax, by - ay
                length2 = dx * dx + dy * dy
                t = 0.0 if length2 == 0 else max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / length2))
                q = (ax + t * dx, ay + t * dy)
                d = math.hypot(q[0] - p[0], q[1] - p[1])
                if d < best_d:
                    best, best_d = (lj, sj, t, q), d
            if best is None:
                junctions.add(key(p))
                continue
            lj, sj, t, q = best
            if t < 1e-6:
                q = raw[lj][1][sj]
            elif t > 1 - 1e-6:
                q = raw[lj][1][sj + 1]
            else:
                inserts.setdefault((lj, sj), []).append((t, q))
            piece[end] = q
            junctions.add(key(q))

    for (lj, sj), points in sorted(inserts.items(), key=lambda item: (item[0][0], -item[0][1])):
        piece = raw[lj][1]
        for _, q in sorted(points, reverse=True):
            piece.insert(sj + 1, q)

    # 2) Yolları kavşaklarda kenarlara böl
    nodes, node_index, edges = [], {}, []

    def node(p):
        k = key(p)
        if k not in node_index:
            node_index[k] = len(nodes)
            nodes.append(p)
        return node_index[k]

    for road_class, piece in raw:
        start = 0
        for i in range(1, len(piece)):
            if key(piece[i]) in junctions or i == len(piece) - 1:
                segment = piece[start:i + 1]
                a, b = node(segment[0]), node(segment[-1])
                if a != b or len(segment) > 2:
                    edges.append([a, b, road_class, simplify(segment, SIMPLIFY_DEG)])
                start = i

    # 3) Kopuk küçük parçaları ana ağa bağla
    stitch_dead_ends(nodes, edges)
    edges = bridge_components(nodes, edges)
    add_crossings(nodes, edges)
    return nodes, edges


def stitch_dead_ends(nodes, edges, max_km=4.0):
    """Çıkmaz yol uçlarını (tek bağlantılı düğüm) birkaç km içindeki başka bir düğüme bağlar.
    Natural Earth'te ülke verilerinin birleştiği sınır kapılarında yollar çoğu zaman birbirine değmez."""
    degree, neighbours = {}, {}
    for a, b, _, _ in edges:
        degree[a] = degree.get(a, 0) + 1
        degree[b] = degree.get(b, 0) + 1
        neighbours.setdefault(a, set()).add(b)
        neighbours.setdefault(b, set()).add(a)
    cell = 0.05
    grid = {}
    for n in degree:
        p = nodes[n]
        grid.setdefault((int(p[0] / cell), int(p[1] / cell)), []).append(n)
    added = 0
    for n, d in list(degree.items()):
        if d != 1:
            continue
        p = nodes[n]
        best, best_km = None, max_km
        gx, gy = int(p[0] / cell), int(p[1] / cell)
        for cx in (gx - 1, gx, gx + 1):
            for cy in (gy - 1, gy, gy + 1):
                for m in grid.get((cx, cy), ()):
                    if m == n or m in neighbours[n]:
                        continue
                    dist = km(p, nodes[m])
                    if dist < best_km:
                        best, best_km = m, dist
        if best is not None:
            road_class = next(c for a, b, c, _ in edges if n in (a, b))
            edges.append([n, best, road_class, [p, nodes[best]]])
            degree[n] = 2
            neighbours[n].add(best)
            neighbours[best].add(n)
            added += 1
    print(f"Çıkmaz uç birleştirme: {added} bağlantı")


def add_crossings(nodes, edges):
    """Köprü uçlarını en yakın yol noktasına bağlar; o nokta bir kenarın ortasındaysa kenar ikiye bölünür."""
    for name, a, b in NEW_CROSSINGS:
        ends = []
        for p in (a, b):
            best = None
            for index, (_, _, _, points) in enumerate(edges):
                for k, q in enumerate(points):
                    d = km(q, p)
                    if best is None or d < best[0]:
                        best = (d, index, k)
            d, index, k = best
            if d > 15:
                print(f"Uyarı: {name} ucu yola {d:.0f} km uzak, eklenmedi")
                break
            ends.append(split_edge(nodes, edges, index, k))
        if len(ends) == 2:
            edges.append([ends[0], ends[1], 0, [nodes[ends[0]], a, b, nodes[ends[1]]]])
            print(f"{name} eklendi")


def split_edge(nodes, edges, index, k):
    a, b, road_class, points = edges[index]
    if k == 0:
        return a
    if k == len(points) - 1:
        return b
    nodes.append(points[k])
    middle = len(nodes) - 1
    edges[index] = [a, middle, road_class, points[:k + 1]]
    edges.append([middle, b, road_class, points[k:]])
    return middle


def components(count, edges):
    parent = list(range(count))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for a, b, *_ in edges:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    return [find(i) for i in range(count)]


def bridge_components(nodes, edges):
    for _ in range(4):
        roots = components(len(nodes), edges)
        sizes = {}
        for r in roots:
            sizes[r] = sizes.get(r, 0) + 1
        main = max(sizes, key=sizes.get)
        used = {n for e in edges for n in e[:2]}
        main_nodes = [i for i in used if roots[i] == main]
        grid = {}
        for i in main_nodes:
            p = nodes[i]
            grid.setdefault((int(p[0] / BRIDGE_DEG), int(p[1] / BRIDGE_DEG)), []).append(i)
        added = 0
        best_link = {}
        for i in used:
            r = roots[i]
            if r == main:
                continue
            p = nodes[i]
            gx, gy = int(p[0] / BRIDGE_DEG), int(p[1] / BRIDGE_DEG)
            for cx in (gx - 1, gx, gx + 1):
                for cy in (gy - 1, gy, gy + 1):
                    for j in grid.get((cx, cy), ()):
                        d = math.hypot(nodes[j][0] - p[0], nodes[j][1] - p[1])
                        if d < BRIDGE_DEG and d < best_link.get(r, (BRIDGE_DEG, 0, 0))[0]:
                            best_link[r] = (d, i, j)
        for d, i, j in best_link.values():
            edges.append([i, j, 2, [nodes[i], nodes[j]]])
            added += 1
        if added == 0:
            break
    return edges


def write_roads(nodes, edges):
    used = sorted({n for e in edges for n in e[:2]})
    remap = {old: new for new, old in enumerate(used)}
    path = os.path.join(OUT_DIR, "roads.txt")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Otobüs İmparatoru yol ağı — Natural Earth 1:10m (kamu malı). Tools/build_map.py üretir; elle düzenlemeyin.\n")
        f.write("# N düğüm sayısı, sonra 'boylam enlem' (derece × 10000). E kenar sayısı, sonra\n")
        f.write("# 'a b sınıf k  ara noktalar…' (sınıf: 0 otoyol, 1 ana yol, 2 diğer yol, 3 feribot)\n")
        f.write(f"N {len(used)}\n")
        for old in used:
            lon, lat = nodes[old]
            f.write(f"{round(lon * 1e4)} {round(lat * 1e4)}\n")
        f.write(f"E {len(edges)}\n")
        for a, b, road_class, points in edges:
            inner = points[1:-1]
            coords = " ".join(f"{round(x * 1e4)} {round(y * 1e4)}" for x, y in inner)
            f.write(f"{remap[a]} {remap[b]} {road_class} {len(inner)}{' ' + coords if coords else ''}\n")
    print(f"roads.txt: {len(used)} düğüm, {len(edges)} kenar, {os.path.getsize(path) // 1024} KB")
    return [nodes[i] for i in used], [[remap[a], remap[b], c, p] for a, b, c, p in edges]


# =============================================================================== doğrulama

COST = {0: 0.8, 1: 0.95, 2: 1.1, 3: 1.6}   # RoadNetwork.cs ile aynı


def check_cities(nodes, edges):
    """Her şehrin ağa uzaklığını ve birkaç örnek güzergâhı raporlar."""
    import csv
    cities = {}
    with open(os.path.join(OUT_DIR, "cities.csv"), encoding="utf-8") as f:
        for row in csv.DictReader(f):
            cities[row["city"]] = (float(row["lng"]), float(row["lat"]))
    adjacency = [[] for _ in nodes]
    for a, b, c, points in edges:
        length = sum(km(p, q) for p, q in zip(points, points[1:]))
        adjacency[a].append((b, length * COST[c] + (25 if c == 3 else 0)))
        adjacency[b].append((a, length * COST[c] + (25 if c == 3 else 0)))
    roots = components(len(nodes), edges)
    main = max(set(roots), key=roots.count)

    def nearest(p):
        best, best_d = None, 1e9
        for i, q in enumerate(nodes):
            if roots[i] != main:
                continue
            d = km(p, q)
            if d < best_d:
                best, best_d = i, d
        return best, best_d

    far = []
    snapped = {}
    for name, p in cities.items():
        i, d = nearest(p)
        snapped[name] = i
        if d > 12:
            far.append(f"{name} {d:.0f} km")
    print("Ağa 12 km'den uzak şehirler:", ", ".join(far) if far else "yok")

    def route_km(a, b):
        dist = {snapped[a]: 0.0}
        heap = [(0.0, snapped[a])]
        while heap:
            d, n = heapq.heappop(heap)
            if n == snapped[b]:
                return d
            if d > dist.get(n, 1e18):
                continue
            for m, w in adjacency[n]:
                nd = d + w
                if nd < dist.get(m, 1e18):
                    dist[m] = nd
                    heapq.heappush(heap, (nd, m))
        return None

    for a, b in [("İstanbul", "Ankara"), ("İstanbul", "İzmir"), ("Ankara", "Antalya"), ("İstanbul", "Sofya"),
                 ("Edirne", "Çanakkale"), ("Trabzon", "Tiflis"), ("Van", "Hakkari"), ("İzmir", "Atina")]:
        cost = route_km(a, b)
        straight = km(cities[a], cities[b])
        print(f"  {a}–{b}: kuş uçuşu {straight:.0f} km, yol maliyeti {cost:.0f}" if cost else f"  {a}–{b}: YOL YOK")
    return cities, snapped


def preview(nodes, edges, out):
    """Hızlı kontrol için yol ağını harita üzerine çizer."""
    image = Image.open(os.path.join(OUT_DIR, "map.jpg")).convert("RGB")
    w, h = image.size
    draw = ImageDraw.Draw(image)

    def project(lon, lat):
        return ((lon - MIN_LON) / (MAX_LON - MIN_LON) * w, (1.0 - (merc_y(lat) - Y_MIN) / (Y_MAX - Y_MIN)) * h)

    colors = {0: (255, 190, 70), 1: (250, 240, 200), 2: (235, 230, 215), 3: (120, 190, 255)}
    for c in (2, 1, 0, 3):
        for a, b, road_class, points in edges:
            if road_class == c:
                draw.line([project(*p) for p in points], fill=colors[c], width=3 if c == 0 else 2)
    image.save(out)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("data_dir")
    parser.add_argument("--preview", help="yol ağını harita üzerine çizen kontrol görüntüsü")
    parser.add_argument("--roads-only", action="store_true", help="yalnızca roads.txt üret")
    args = parser.parse_args()

    if not args.roads_only:
        build_textures(args.data_dir)
    road_nodes, road_edges = write_roads(*build_roads(args.data_dir))
    check_cities(road_nodes, road_edges)
    if args.preview:
        preview(road_nodes, road_edges, args.preview)
