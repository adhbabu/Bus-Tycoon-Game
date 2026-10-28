#!/usr/bin/env python3
"""
Bus Tycoon dünya verisi üreticisi.

Girdi (indirip tek klasöre koyun):
  * GeoNames  TR.txt            https://download.geonames.org/export/dump/TR.zip          (CC BY 4.0)
  * GeoNames  cities15000.txt   https://download.geonames.org/export/dump/cities15000.zip (CC BY 4.0)
  * Natural Earth 1:50m ülkeler https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson
  * Natural Earth 1:50m göller  https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_lakes.geojson

Çıktı:
  Assets/_Project/Resources/BusTycoon/cities.csv   81 il (il nüfusu, il merkezi koordinatı) + komşu ülke şehirleri
  Assets/_Project/Resources/BusTycoon/map.png      Mercator harita dokusu (sınırlar DefaultContent.cs ile AYNI olmalı)

Kullanım:  python3 Tools/build_world_data.py <veri_klasörü>
Gereksinim: Pillow
"""
import csv
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

# --- Harita sınırları: Runtime/Data/DefaultContent.cs → MapBounds ile birebir aynı tutun ---
MIN_LON, MAX_LON = 19.0, 47.0
MIN_LAT, MAX_LAT = 34.0, 45.0
TEX_WIDTH = 2048
SUPERSAMPLE = 2

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon")

# Turizm / cazibe çarpanı (varsayılan 1.0)
TOURISM = {
    "Antalya": 1.5, "Muğla": 1.45, "Nevşehir": 1.45, "İstanbul": 1.2, "Aydın": 1.2, "Çanakkale": 1.25,
    "İzmir": 1.15, "Trabzon": 1.15, "Mardin": 1.15, "Rize": 1.1, "Artvin": 1.1, "Edirne": 1.1, "Bolu": 1.1,
    "Denizli": 1.1, "Bursa": 1.05, "Şanlıurfa": 1.05, "Gaziantep": 1.05, "Balıkesir": 1.05, "Konya": 1.05,
    "Hatay": 1.05, "Van": 1.05, "Kars": 1.05, "Yalova": 1.05,
}

# GeoNames yazımı → yaygın Türkçe yazım
NAME_FIX = {"Elâzığ": "Elazığ", "Hakkâri": "Hakkari"}

# Komşu ülke şehirleri: (Türkçe ad, ISO2, ülke adı, GeoNames asciiname, yaklaşık bölge/metro nüfusu, başkent?, turizm)
# Nüfuslar, Türk illeriyle tutarlı olması için şehir değil bölge/metro ölçeğinde yaklaşık değerlerdir.
FOREIGN = [
    ("Sofya", "BG", "Bulgaristan", "Sofia", 1_300_000, True, 1.1),
    ("Filibe", "BG", "Bulgaristan", "Plovdiv", 630_000, False, 1.05),
    ("Varna", "BG", "Bulgaristan", "Varna", 430_000, False, 1.3),
    ("Burgaz", "BG", "Bulgaristan", "Burgas", 380_000, False, 1.25),
    ("Atina", "GR", "Yunanistan", "Athens", 3_800_000, True, 1.4),
    ("Selanik", "GR", "Yunanistan", "Thessaloniki", 1_100_000, False, 1.25),
    ("Dedeağaç", "GR", "Yunanistan", "Alexandroupoli", 140_000, False, 1.1),
    ("Tiflis", "GE", "Gürcistan", "Tbilisi", 1_200_000, True, 1.15),
    ("Batum", "GE", "Gürcistan", "Batumi", 350_000, False, 1.4),
    ("Kutaisi", "GE", "Gürcistan", "Kutaisi", 480_000, False, 1.05),
    ("Bükreş", "RO", "Romanya", "Bucharest", 2_400_000, True, 1.15),
    ("Köstence", "RO", "Romanya", "Constanta", 650_000, False, 1.2),
]

PLAYABLE = {"TUR": (93, 122, 79), "BGR": (104, 120, 86), "GRC": (104, 120, 86), "GEO": (104, 120, 86), "ROU": (104, 120, 86)}
OTHER_LAND = (58, 68, 56)
SEA = (22, 54, 88)
SHALLOW = (40, 82, 120)
BORDER = (200, 214, 190, 150)


def read_tsv(path):
    csv.field_size_limit(10 ** 9)
    with open(path, encoding="utf-8") as f:
        yield from csv.reader(f, delimiter="\t", quoting=csv.QUOTE_NONE)


def build_cities(data_dir):
    provinces, seats = {}, {}
    for row in read_tsv(os.path.join(data_dir, "TR.txt")):
        if row[7] == "ADM1":
            provinces[row[10]] = (row[1], int(row[14] or 0))
        elif row[7] in ("PPLA", "PPLC"):
            seats[row[10]] = (float(row[4]), float(row[5]), row[7] == "PPLC")

    rows = []
    for code, (name, population) in provinces.items():
        lat, lon, capital = seats[code]
        name = NAME_FIX.get(name, name)
        rows.append((name, lat, lon, "Türkiye", "TR", population, "primary" if capital else "admin", TOURISM.get(name, 1.0)))

    lookup = {}
    for row in read_tsv(os.path.join(data_dir, "cities15000.txt")):
        lookup[(row[8], row[2])] = (float(row[4]), float(row[5]))
    for name, iso, country, ascii_name, population, capital, tourism in FOREIGN:
        lat, lon = lookup[(iso, ascii_name)]
        rows.append((name, lat, lon, country, iso, population, "primary" if capital else "admin", tourism))

    rows.sort(key=lambda r: (r[4] != "TR", -r[5]))
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "cities.csv"), "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["city", "lat", "lng", "country", "iso2", "population", "capital", "tourism"])
        for r in rows:
            writer.writerow([r[0], f"{r[1]:.5f}", f"{r[2]:.5f}", r[3], r[4], r[5], r[6], r[7]])
    print(f"cities.csv: {len(rows)} şehir")


def merc_y(lat):
    lat = max(-85.0, min(85.0, lat))
    return math.log(math.tan(math.pi / 4 + math.radians(lat) / 2))


def build_map(data_dir):
    lon_range = math.radians(MAX_LON - MIN_LON)
    y_min, y_max = merc_y(MIN_LAT), merc_y(MAX_LAT)
    width = TEX_WIDTH * SUPERSAMPLE
    height = round(TEX_WIDTH * (y_max - y_min) / lon_range) * SUPERSAMPLE

    def project(lon, lat):
        x = (lon - MIN_LON) / (MAX_LON - MIN_LON) * width
        y = (1.0 - (merc_y(lat) - y_min) / (y_max - y_min)) * height  # görüntüde y aşağı → kuzey üstte
        return (x, y)

    def rings(geometry):
        if geometry["type"] == "Polygon":
            yield geometry["coordinates"]
        elif geometry["type"] == "MultiPolygon":
            yield from geometry["coordinates"]

    countries = json.load(open(os.path.join(data_dir, "ne_50m_admin_0_countries.geojson"), encoding="utf-8"))["features"]
    lakes = json.load(open(os.path.join(data_dir, "ne_50m_lakes.geojson"), encoding="utf-8"))["features"]

    image = Image.new("RGB", (width, height), SEA)

    # 1) Sığ su halesi (kıyı parlaması)
    halo = Image.new("L", (width, height), 0)
    halo_draw = ImageDraw.Draw(halo)
    for feature in countries:
        for polygon in rings(feature["geometry"]):
            halo_draw.polygon([project(*p) for p in polygon[0]], fill=255)
    halo = halo.filter(ImageFilter.GaussianBlur(10 * SUPERSAMPLE))
    image = Image.composite(Image.new("RGB", (width, height), SHALLOW), image, halo)

    # 2) Kara + 3) göller
    draw = ImageDraw.Draw(image)
    for feature in countries:
        iso3 = feature["properties"].get("ADM0_A3")
        color = PLAYABLE.get(iso3, OTHER_LAND)
        for polygon in rings(feature["geometry"]):
            draw.polygon([project(*p) for p in polygon[0]], fill=color)
            for hole in polygon[1:]:
                draw.polygon([project(*p) for p in hole], fill=SEA)
    for feature in lakes:
        for polygon in rings(feature["geometry"]):
            draw.polygon([project(*p) for p in polygon[0]], fill=SHALLOW)

    # 4) Enlem/boylam ağı ve ülke sınırları (yarı saydam katman)
    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    over = ImageDraw.Draw(overlay)
    for lon in range(int(MIN_LON), int(MAX_LON) + 1, 2):
        over.line([project(lon, MIN_LAT), project(lon, MAX_LAT)], fill=(255, 255, 255, 14), width=SUPERSAMPLE)
    for lat in range(int(MIN_LAT), int(MAX_LAT) + 1, 2):
        over.line([project(MIN_LON, lat), project(MAX_LON, lat)], fill=(255, 255, 255, 14), width=SUPERSAMPLE)
    for feature in countries:
        for polygon in rings(feature["geometry"]):
            over.line([project(*p) for p in polygon[0]], fill=BORDER, width=2 * SUPERSAMPLE, joint="curve")
    image = Image.alpha_composite(image.convert("RGBA"), overlay).convert("RGB")

    image = image.resize((width // SUPERSAMPLE, height // SUPERSAMPLE), Image.LANCZOS)
    image.save(os.path.join(OUT_DIR, "map.png"), optimize=True)
    print(f"map.png: {image.size[0]}×{image.size[1]}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    build_cities(sys.argv[1])
    build_map(sys.argv[1])
