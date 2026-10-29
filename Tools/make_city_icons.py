#!/usr/bin/env python3
"""
Her şehrin kendine özgü ikonu: şehrin en bilinen yanını anlatan bir Twemoji (https://github.com/jdecked/twemoji,
grafikler CC BY 4.0) ve kısa "nesiyle ünlü" açıklaması.

  Assets/_Project/Resources/BusTycoon/UI/cities.png        8 sütun, 80 px hücre (72 px ikon + 4 px saydam pay)
  Assets/_Project/Resources/BusTycoon/UI/city_icons.txt    "şehir<TAB>hücre<TAB>açıklama" satırları (UICityIcons okur)

Şehir adları Resources/BusTycoon/cities.csv'deki "city" sütunuyla aynıdır; her şehrin bir ikonu olmalıdır.
Kullanım: python3 Tools/make_city_icons.py      Gereksinim: Pillow, internet (jsdelivr)
"""
import csv
import io
import os
import sys
import urllib.request

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon")
BASE = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/"
COLUMNS, CELL, ICON = 8, 80, 72

# şehir: (Twemoji kodu, açıklama)
CITY_ICONS = {
    "İstanbul": ("1f309", "Boğaz köprüleri, iki kıta"),
    "Ankara": ("1f3db", "Başkent, Anıtkabir"),
    "İzmir": ("1f570", "Saat Kulesi ve Kordon"),
    "Bursa": ("1f333", "Yeşil Bursa, Uludağ"),
    "Antalya": ("1f3d6", "Turizmin başkenti"),
    "Konya": ("1f33e", "Tahıl ambarı, Mevlana"),
    "Adana": ("1f336", "Adana kebap"),
    "Şanlıurfa": ("1f5ff", "Göbeklitepe"),
    "Gaziantep": ("1f959", "Mutfak şehri, baklava"),
    "Kocaeli": ("1f3ed", "Sanayinin kalbi"),
    "Mersin": ("1f34b", "Narenciye ve liman"),
    "Diyarbakır": ("1f349", "Dev karpuzlar, surlar"),
    "Hatay": ("1f37d", "UNESCO gastronomi şehri"),
    "Manisa": ("1f347", "Üzüm bağları"),
    "Kayseri": ("1f969", "Pastırma, Erciyes"),
    "Samsun": ("26f4", "Bandırma Vapuru, 19 Mayıs"),
    "Balıkesir": ("1fad2", "Zeytin, Kaz Dağları"),
    "Kahramanmaraş": ("1f366", "Maraş dondurması"),
    "Aydın": ("1f3fa", "Antik kentler, incir"),
    "Tekirdağ": ("1f9c6", "Tekirdağ köftesi"),
    "Van": ("1f408", "Van kedisi"),
    "Sakarya": ("1f3de", "Sapanca Gölü"),
    "Denizli": ("1f413", "Denizli horozu, Pamukkale"),
    "Muğla": ("26f5", "Bodrum ve Marmaris koyları"),
    "Eskişehir": ("1f393", "Öğrenci şehri, Odunpazarı"),
    "Mardin": ("1f3d8", "Taş evler"),
    "Trabzon": ("1f41f", "Hamsi ve Sümela"),
    "Malatya": ("1f351", "Kayısı"),
    "Ordu": ("1f330", "Fındık"),
    "Erzurum": ("26f7", "Palandöken kayak merkezi"),
    "Afyonkarahisar": ("2668", "Termal sular ve kaymak"),
    "Sivas": ("1f415", "Kangal köpeği"),
    "Batman": ("1f6e2", "Petrol"),
    "Adıyaman": ("1f304", "Nemrut'ta gün doğumu"),
    "Tokat": ("1f346", "Tokat kebabı"),
    "Elazığ": ("26f0", "Harput ve Hazar Gölü"),
    "Zonguldak": ("26cf", "Kömür madenleri"),
    "Kütahya": ("1fad6", "Çini"),
    "Çanakkale": ("1f434", "Truva Atı"),
    "Osmaniye": ("1f95c", "Yer fıstığı"),
    "Şırnak": ("1f3d4", "Cudi Dağı"),
    "Çorum": ("1fad8", "Leblebi"),
    "Ağrı": ("1f5fb", "Ağrı Dağı"),
    "Giresun": ("1f352", "Kirazın anavatanı, fındık"),
    "Isparta": ("1f339", "Gül"),
    "Aksaray": ("1f42a", "Sultanhanı Kervansarayı"),
    "Yozgat": ("1f332", "Çamlık Milli Parkı"),
    "Düzce": ("1f30a", "Akçakoca kıyıları"),
    "Edirne": ("1f54c", "Selimiye Camii"),
    "Muş": ("1f337", "Muş lalesi"),
    "Kastamonu": ("1f9c4", "Taşköprü sarımsağı"),
    "Kırklareli": ("1f33b", "Ayçiçeği tarlaları"),
    "Uşak": ("1f9f6", "Halı ve battaniye"),
    "Niğde": ("1f954", "Patates"),
    "Bitlis": ("1f30b", "Nemrut Krater Gölü"),
    "Rize": ("1f375", "Çay"),
    "Amasya": ("1f34e", "Elma, kral mezarları"),
    "Siirt": ("1f356", "Büryan kebabı"),
    "Bolu": ("1f468-200d-1f373", "Mengen aşçıları"),
    "Nevşehir": ("1f388", "Kapadokya balonları"),
    "Yalova": ("1f338", "Çiçekçilik"),
    "Bingöl": ("1f41d", "Bal"),
    "Hakkari": ("1f985", "Cilo Dağları"),
    "Kırıkkale": ("1f529", "Sanayi"),
    "Burdur": ("1f9a9", "Salda Gölü, flamingolar"),
    "Kars": ("1f9c0", "Kaşar peyniri, Ani"),
    "Karaman": ("1f58b", "Türk Dil Bayramı"),
    "Karabük": ("1f3e1", "Safranbolu evleri"),
    "Kırşehir": ("1f3b6", "Neşet Ertaş, bozlak"),
    "Erzincan": ("1f97e", "Kemaliye, Karanlık Kanyon"),
    "Bilecik": ("1f451", "Osmanlı'nın kuruluşu"),
    "Sinop": ("2693", "Doğal liman, en kuzey"),
    "Iğdır": ("1f348", "Iğdır kavunu"),
    "Bartın": ("1f6f6", "Amasra kıyıları"),
    "Çankırı": ("1f9c2", "Tuz mağarası"),
    "Artvin": ("1f43b", "Ormanlar, Karagöl"),
    "Kilis": ("1f33f", "Zeytinyağı"),
    "Gümüşhane": ("1f36c", "Pestil ve köme"),
    "Ardahan": ("1f404", "Yaylalar"),
    "Tunceli": ("1f4a7", "Munzur gözeleri"),
    "Bayburt": ("1f4d6", "Dede Korkut"),
    "Atina": ("1f989", "Akropolis, Athena'nın baykuşu"),
    "Bükreş": ("1f3f0", "Parlamento Sarayı"),
    "Sofya": ("26ea", "Aleksandr Nevski Katedrali"),
    "Tiflis": ("1f6c1", "Kükürtlü hamamlar"),
    "Selanik": ("1f5fc", "Beyaz Kule"),
    "Köstence": ("1f6a2", "Karadeniz limanı"),
    "Filibe": ("1f3ad", "Antik tiyatro"),
    "Kutaisi": ("1f995", "Dinozor izleri"),
    "Varna": ("1f3dd", "Karadeniz plajları"),
    "Burgaz": ("1f41a", "Plajlar ve tuz gölleri"),
    "Batum": ("1f334", "Palmiyeli bulvar"),
    "Dedeağaç": ("1f9a2", "Meriç Deltası kuğuları"),
}


def fetch(code):
    with urllib.request.urlopen(BASE + code + ".png", timeout=30) as response:
        return Image.open(io.BytesIO(response.read())).convert("RGBA")


def main():
    with open(os.path.join(RES, "cities.csv"), encoding="utf-8") as f:
        cities = [row["city"] for row in csv.DictReader(f)]
    missing = [c for c in cities if c not in CITY_ICONS]
    extra = [c for c in CITY_ICONS if c not in cities]
    codes = [code for code, _ in CITY_ICONS.values()]
    duplicates = sorted({c for c in codes if codes.count(c) > 1})
    if missing or extra or duplicates:
        sys.exit(f"Eksik: {missing}  Fazla: {extra}  Tekrarlanan ikon: {duplicates}")

    rows = (len(cities) + COLUMNS - 1) // COLUMNS
    atlas = Image.new("RGBA", (COLUMNS * CELL, rows * CELL), (0, 0, 0, 0))
    pad = (CELL - ICON) // 2
    lines = ["# şehir\thücre\taçıklama — Tools/make_city_icons.py üretir (ikonlar: Twemoji, CC BY 4.0)"]
    for index, city in enumerate(cities):
        code, description = CITY_ICONS[city]
        icon = fetch(code)
        if icon.size != (ICON, ICON):
            icon = icon.resize((ICON, ICON), Image.LANCZOS)
        atlas.alpha_composite(icon, ((index % COLUMNS) * CELL + pad, (index // COLUMNS) * CELL + pad))
        lines.append(f"{city}\t{index}\t{description}")

    atlas.save(os.path.join(RES, "UI", "cities.png"), optimize=True)
    with open(os.path.join(RES, "UI", "city_icons.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"yazıldı: UI/cities.png {atlas.size}, UI/city_icons.txt ({len(cities)} şehir)")


if __name__ == "__main__":
    main()
