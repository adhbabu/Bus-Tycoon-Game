#!/usr/bin/env python3
"""
Bus Tycoon dünya verisi üreticisi.

Girdi (indirip tek klasöre koyun):
  * GeoNames  TR.txt            https://download.geonames.org/export/dump/TR.zip          (CC BY 4.0)
  * GeoNames  cities15000.txt   https://download.geonames.org/export/dump/cities15000.zip (CC BY 4.0)

Çıktı:
  Assets/_Project/Resources/BusTycoon/cities.csv   81 il (il nüfusu, il merkezi koordinatı) + komşu ülke şehirleri
                                                   + Avrupa Paketi şehirleri ("pack" sütunu: europe)

Harita dokusu (StreamingAssets/Map/map.jpg) ve karayolu ağı (roads.txt) Tools/build_map.py ile üretilir.

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

# Avrupa Paketi (DLC, "europe"): Kıta Avrupası + Britanya, 31 ülke. Biçim FOREIGN ile aynı; nüfuslar metro ölçeğinde.
# Ülke ekonomisi ve kilidi: Runtime/Data/DefaultContent.cs → Regions. Paket: Runtime/Data/ContentPacks.cs.
EUROPE_PACK = "europe"
EUROPE = [
    ("Londra", "GB", "Birleşik Krallık", "London", 14_000_000, True, 1.5),
    ("Manchester", "GB", "Birleşik Krallık", "Manchester", 2_800_000, False, 1.05),
    ("Birmingham", "GB", "Birleşik Krallık", "Birmingham", 2_900_000, False, 1.0),
    ("Liverpool", "GB", "Birleşik Krallık", "Liverpool", 1_600_000, False, 1.1),
    ("Leeds", "GB", "Birleşik Krallık", "Leeds", 1_900_000, False, 1.0),
    ("Glasgow", "GB", "Birleşik Krallık", "Glasgow", 1_800_000, False, 1.05),
    ("Edinburgh", "GB", "Birleşik Krallık", "Edinburgh", 900_000, False, 1.35),
    ("Bristol", "GB", "Birleşik Krallık", "Bristol", 1_000_000, False, 1.05),
    ("Newcastle", "GB", "Birleşik Krallık", "Newcastle upon Tyne", 1_100_000, False, 1.0),
    ("Cardiff", "GB", "Birleşik Krallık", "Cardiff", 1_100_000, False, 1.05),
    ("Dublin", "IE", "İrlanda", "Dublin", 2_000_000, True, 1.25),
    ("Cork", "IE", "İrlanda", "Cork", 400_000, False, 1.1),
    ("Galway", "IE", "İrlanda", "Galway", 200_000, False, 1.25),
    ("Paris", "FR", "Fransa", "Paris", 12_000_000, True, 1.6),
    ("Marsilya", "FR", "Fransa", "Marseille", 1_900_000, False, 1.2),
    ("Lyon", "FR", "Fransa", "Lyon", 2_300_000, False, 1.15),
    ("Toulouse", "FR", "Fransa", "Toulouse", 1_450_000, False, 1.05),
    ("Nice", "FR", "Fransa", "Nice", 1_000_000, False, 1.45),
    ("Nantes", "FR", "Fransa", "Nantes", 1_000_000, False, 1.05),
    ("Strazburg", "FR", "Fransa", "Strasbourg", 800_000, False, 1.15),
    ("Bordo", "FR", "Fransa", "Bordeaux", 1_300_000, False, 1.2),
    ("Lille", "FR", "Fransa", "Lille", 1_500_000, False, 1.0),
    ("Montpellier", "FR", "Fransa", "Montpellier", 800_000, False, 1.1),
    ("Rennes", "FR", "Fransa", "Rennes", 750_000, False, 1.0),
    ("Brüksel", "BE", "Belçika", "Brussels", 2_100_000, True, 1.15),
    ("Anvers", "BE", "Belçika", "Antwerp", 1_100_000, False, 1.1),
    ("Gent", "BE", "Belçika", "Gent", 600_000, False, 1.15),
    ("Liège", "BE", "Belçika", "Liege", 750_000, False, 1.0),
    ("Amsterdam", "NL", "Hollanda", "Amsterdam", 2_500_000, True, 1.45),
    ("Rotterdam", "NL", "Hollanda", "Rotterdam", 1_800_000, False, 1.05),
    ("Lahey", "NL", "Hollanda", "The Hague", 1_000_000, False, 1.05),
    ("Utrecht", "NL", "Hollanda", "Utrecht", 900_000, False, 1.05),
    ("Eindhoven", "NL", "Hollanda", "Eindhoven", 750_000, False, 1.0),
    ("Lüksemburg", "LU", "Lüksemburg", "Luxembourg", 650_000, True, 1.1),
    ("Berlin", "DE", "Almanya", "Berlin", 4_500_000, True, 1.35),
    ("Hamburg", "DE", "Almanya", "Hamburg", 3_300_000, False, 1.15),
    ("Münih", "DE", "Almanya", "Munich", 2_900_000, False, 1.3),
    ("Köln", "DE", "Almanya", "Koeln", 2_100_000, False, 1.15),
    ("Frankfurt", "DE", "Almanya", "Frankfurt am Main", 2_300_000, False, 1.05),
    ("Stuttgart", "DE", "Almanya", "Stuttgart", 2_700_000, False, 1.0),
    ("Düsseldorf", "DE", "Almanya", "Duesseldorf", 1_500_000, False, 1.05),
    ("Dortmund", "DE", "Almanya", "Dortmund", 1_800_000, False, 1.0),
    ("Leipzig", "DE", "Almanya", "Leipzig", 1_000_000, False, 1.05),
    ("Dresden", "DE", "Almanya", "Dresden", 1_300_000, False, 1.15),
    ("Nürnberg", "DE", "Almanya", "Nuremberg", 1_300_000, False, 1.1),
    ("Hannover", "DE", "Almanya", "Hannover", 1_100_000, False, 1.0),
    ("Bremen", "DE", "Almanya", "Bremen", 1_000_000, False, 1.0),
    ("Kopenhag", "DK", "Danimarka", "Copenhagen", 2_100_000, True, 1.3),
    ("Aarhus", "DK", "Danimarka", "Arhus", 950_000, False, 1.05),
    ("Odense", "DK", "Danimarka", "Odense", 500_000, False, 1.0),
    ("Viyana", "AT", "Avusturya", "Vienna", 2_900_000, True, 1.45),
    ("Graz", "AT", "Avusturya", "Graz", 650_000, False, 1.05),
    ("Linz", "AT", "Avusturya", "Linz", 800_000, False, 1.0),
    ("Salzburg", "AT", "Avusturya", "Salzburg", 560_000, False, 1.4),
    ("Innsbruck", "AT", "Avusturya", "Innsbruck", 420_000, False, 1.3),
    ("Zürih", "CH", "İsviçre", "Zuerich", 1_900_000, False, 1.2),
    ("Bern", "CH", "İsviçre", "Bern", 420_000, True, 1.1),
    ("Cenevre", "CH", "İsviçre", "Geneva", 1_000_000, False, 1.25),
    ("Basel", "CH", "İsviçre", "Basel", 900_000, False, 1.05),
    ("Lozan", "CH", "İsviçre", "Lausanne", 420_000, False, 1.15),
    ("Roma", "IT", "İtalya", "Rome", 4_300_000, True, 1.6),
    ("Milano", "IT", "İtalya", "Milan", 5_000_000, False, 1.25),
    ("Napoli", "IT", "İtalya", "Naples", 3_100_000, False, 1.25),
    ("Torino", "IT", "İtalya", "Turin", 2_200_000, False, 1.1),
    ("Floransa", "IT", "İtalya", "Florence", 1_000_000, False, 1.55),
    ("Venedik", "IT", "İtalya", "Venice", 850_000, False, 1.6),
    ("Bologna", "IT", "İtalya", "Bologna", 1_000_000, False, 1.15),
    ("Bari", "IT", "İtalya", "Bari", 1_200_000, False, 1.1),
    ("Cenova", "IT", "İtalya", "Genoa", 820_000, False, 1.1),
    ("Verona", "IT", "İtalya", "Verona", 900_000, False, 1.25),
    ("Palermo", "IT", "İtalya", "Palermo", 1_200_000, False, 1.25),
    ("Madrid", "ES", "İspanya", "Madrid", 6_700_000, True, 1.35),
    ("Barselona", "ES", "İspanya", "Barcelona", 5_600_000, False, 1.55),
    ("Valensiya", "ES", "İspanya", "Valencia", 1_600_000, False, 1.25),
    ("Sevilla", "ES", "İspanya", "Sevilla", 1_500_000, False, 1.4),
    ("Malaga", "ES", "İspanya", "Malaga", 1_000_000, False, 1.4),
    ("Bilbao", "ES", "İspanya", "Bilbao", 1_000_000, False, 1.15),
    ("Zaragoza", "ES", "İspanya", "Zaragoza", 780_000, False, 1.0),
    ("Granada", "ES", "İspanya", "Granada", 530_000, False, 1.35),
    ("Alicante", "ES", "İspanya", "Alicante", 750_000, False, 1.25),
    ("Lizbon", "PT", "Portekiz", "Lisbon", 2_900_000, True, 1.45),
    ("Porto", "PT", "Portekiz", "Porto", 1_700_000, False, 1.35),
    ("Faro", "PT", "Portekiz", "Faro", 450_000, False, 1.35),
    ("Coimbra", "PT", "Portekiz", "Coimbra", 430_000, False, 1.05),
    ("Varşova", "PL", "Polonya", "Warsaw", 3_100_000, True, 1.15),
    ("Krakov", "PL", "Polonya", "Krakow", 1_500_000, False, 1.35),
    ("Wrocław", "PL", "Polonya", "Wroclaw", 1_250_000, False, 1.15),
    ("Poznań", "PL", "Polonya", "Poznan", 1_000_000, False, 1.0),
    ("Gdańsk", "PL", "Polonya", "Gdansk", 1_100_000, False, 1.2),
    ("Łódź", "PL", "Polonya", "Lodz", 1_100_000, False, 1.0),
    ("Katowice", "PL", "Polonya", "Katowice", 2_000_000, False, 1.0),
    ("Prag", "CZ", "Çekya", "Prague", 2_700_000, True, 1.55),
    ("Brno", "CZ", "Çekya", "Brno", 700_000, False, 1.05),
    ("Ostrava", "CZ", "Çekya", "Ostrava", 600_000, False, 1.0),
    ("Bratislava", "SK", "Slovakya", "Bratislava", 730_000, True, 1.15),
    ("Koşice", "SK", "Slovakya", "Kosice", 360_000, False, 1.05),
    ("Budapeşte", "HU", "Macaristan", "Budapest", 3_000_000, True, 1.45),
    ("Debrecen", "HU", "Macaristan", "Debrecen", 400_000, False, 1.0),
    ("Segedin", "HU", "Macaristan", "Szeged", 300_000, False, 1.05),
    ("Peçuy", "HU", "Macaristan", "Pecs", 300_000, False, 1.05),
    ("Lublyana", "SI", "Slovenya", "Ljubljana", 540_000, True, 1.2),
    ("Maribor", "SI", "Slovenya", "Maribor", 300_000, False, 1.05),
    ("Zagreb", "HR", "Hırvatistan", "Zagreb", 1_100_000, True, 1.15),
    ("Split", "HR", "Hırvatistan", "Split", 450_000, False, 1.45),
    ("Dubrovnik", "HR", "Hırvatistan", "Dubrovnik", 150_000, False, 1.6),
    ("Rijeka", "HR", "Hırvatistan", "Rijeka", 300_000, False, 1.15),
    ("Saraybosna", "BA", "Bosna-Hersek", "Sarajevo", 560_000, True, 1.2),
    ("Banja Luka", "BA", "Bosna-Hersek", "Banja Luka", 400_000, False, 1.0),
    ("Mostar", "BA", "Bosna-Hersek", "Mostar", 200_000, False, 1.3),
    ("Belgrad", "RS", "Sırbistan", "Belgrade", 1_700_000, True, 1.15),
    ("Novi Sad", "RS", "Sırbistan", "Novi Sad", 400_000, False, 1.05),
    ("Niş", "RS", "Sırbistan", "Nis", 400_000, False, 1.0),
    ("Podgorica", "ME", "Karadağ", "Podgorica", 230_000, True, 1.15),
    ("Tiran", "AL", "Arnavutluk", "Tirana", 900_000, True, 1.1),
    ("Dıraç", "AL", "Arnavutluk", "Durres", 300_000, False, 1.2),
    ("Üsküp", "MK", "Kuzey Makedonya", "Skopje", 600_000, True, 1.05),
    ("Ohri", "MK", "Kuzey Makedonya", "Ohrid", 100_000, False, 1.4),
    ("Kişinev", "MD", "Moldova", "Chisinau", 750_000, True, 1.0),
    ("Priştine", "XK", "Kosova", "Pristina", 400_000, True, 1.05),
    ("Stockholm", "SE", "İsveç", "Stockholm", 2_400_000, True, 1.35),
    ("Göteborg", "SE", "İsveç", "Gothenburg", 1_100_000, False, 1.15),
    ("Malmö", "SE", "İsveç", "Malmoe", 750_000, False, 1.05),
    ("Oslo", "NO", "Norveç", "Oslo", 1_600_000, True, 1.3),
    ("Bergen", "NO", "Norveç", "Bergen", 420_000, False, 1.35),
    ("Vilnius", "LT", "Litvanya", "Vilnius", 800_000, True, 1.15),
    ("Kaunas", "LT", "Litvanya", "Kaunas", 400_000, False, 1.0),
    ("Riga", "LV", "Letonya", "Riga", 1_000_000, True, 1.2),
    ("Tallinn", "EE", "Estonya", "Tallinn", 600_000, True, 1.3),
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
        rows.append((name, lat, lon, "Türkiye", "TR", population, "primary" if capital else "admin", TOURISM.get(name, 1.0), ""))

    lookup = {}
    for row in read_tsv(os.path.join(data_dir, "cities15000.txt")):
        key, population = (row[8], row[2]), int(row[14] or 0)
        if key not in lookup or population > lookup[key][2]:   # aynı adlı semtler değil, şehrin kendisi
            lookup[key] = (float(row[4]), float(row[5]), population)
    for pack, cities in (("", FOREIGN), (EUROPE_PACK, EUROPE)):
        for name, iso, country, ascii_name, population, capital, tourism in cities:
            if (iso, ascii_name) not in lookup:
                sys.exit(f"GeoNames'te bulunamadı: {iso} {ascii_name}")
            lat, lon, _ = lookup[(iso, ascii_name)]
            rows.append((name, lat, lon, country, iso, population, "primary" if capital else "admin", tourism, pack))

    rows.sort(key=lambda r: (r[4] != "TR", r[8] != "", -r[5]))
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "cities.csv"), "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["city", "lat", "lng", "country", "iso2", "population", "capital", "tourism", "pack"])
        for r in rows:
            writer.writerow([r[0], f"{r[1]:.5f}", f"{r[2]:.5f}", r[3], r[4], r[5], r[6], r[7], r[8]])
    packed = sum(1 for r in rows if r[8])
    print(f"cities.csv: {len(rows)} şehir ({packed} Avrupa Paketi'nde)")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    build_cities(sys.argv[1])
