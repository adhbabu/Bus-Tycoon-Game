#!/usr/bin/env python3
"""
Bus Tycoon dünya verisi üreticisi.

Girdi (indirip tek klasöre koyun):
  * GeoNames  TR.txt            https://download.geonames.org/export/dump/TR.zip          (CC BY 4.0)
  * GeoNames  cities15000.txt   https://download.geonames.org/export/dump/cities15000.zip (CC BY 4.0)

Çıktı:
  Assets/_Project/Resources/BusTycoon/cities.csv   81 il (il nüfusu, il merkezi koordinatı) + komşu ülke şehirleri

Harita dokusu (map.jpg) ve karayolu ağı (roads.txt) Tools/build_map.py ile üretilir.

Kullanım:  python3 Tools/build_world_data.py <veri_klasörü>
"""
import csv
import os
import sys

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


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    build_cities(sys.argv[1])
