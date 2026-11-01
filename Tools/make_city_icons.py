#!/usr/bin/env python3
"""
Her şehrin kendine özgü ikonu: şehrin en bilinen yanını anlatan bir Twemoji (https://github.com/jdecked/twemoji,
grafikler CC BY 4.0) ve kısa "nesiyle ünlü" açıklaması.

  Assets/_Project/Resources/BusTycoon/UI/cities.png        8 sütun, 80 px hücre (72 px ikon + 4 px saydam pay)
  Assets/_Project/Resources/BusTycoon/UI/city_icons.txt    "şehir<TAB>hücre<TAB>açıklama" satırları (UICityIcons okur)

Şehir adları Resources/BusTycoon/cities.csv'deki "city" sütunuyla aynıdır; her şehrin bir ikonu olmalıdır. Bir ülke
içinde ikonlar farklıdır (Avrupa Paketi'nde farklı ülkeler aynı ikonu paylaşabilir: katedral, kale, liman...).
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
    # Avrupa Paketi (aynı ikon farklı ülkelerde kullanılabilir; bir ülke içinde her şehir farklıdır)
    "Londra": ("1f482", "Big Ben ve kraliyet muhafızları"),
    "Manchester": ("26bd", "Futbolun başkenti"),
    "Birmingham": ("1f3ed", "Sanayi devriminin kalbi"),
    "Liverpool": ("1f3b8", "Beatles'ın şehri"),
    "Leeds": ("1f9f6", "Yün ve tekstil geçmişi"),
    "Glasgow": ("1f6a2", "Clyde tersaneleri"),
    "Edinburgh": ("1f3f0", "Kale ve festival şehri"),
    "Bristol": ("1f388", "Sıcak hava balonu festivali"),
    "Newcastle": ("26cf", "Kömür madenciliği geçmişi"),
    "Cardiff": ("1f409", "Gal ejderhası"),
    "Dublin": ("2618", "Yonca ve pub'lar"),
    "Cork": ("2693", "Liman şehri"),
    "Galway": ("1f3bb", "Kelt müziği"),
    "Paris": ("1f5fc", "Eyfel Kulesi"),
    "Marsilya": ("26f5", "Akdeniz limanı"),
    "Lyon": ("1f9c0", "Fransız mutfağının başkenti"),
    "Toulouse": ("1f680", "Havacılık ve uzay"),
    "Nice": ("1f3d6", "Fransız Rivierası"),
    "Nantes": ("1f418", "Dev mekanik fil"),
    "Strazburg": ("1f384", "Noel pazarı ve Avrupa Parlamentosu"),
    "Bordo": ("1f377", "Şarap bağları"),
    "Lille": ("1f9c7", "Gofret ve Flaman mirası"),
    "Montpellier": ("1f31e", "Güneşli öğrenci şehri"),
    "Rennes": ("1f95e", "Breton krepi"),
    "Brüksel": ("1f36b", "Çikolata ve AB'nin merkezi"),
    "Anvers": ("1f48e", "Elmas başkenti"),
    "Gent": ("26ea", "Ortaçağ kanalları ve katedral"),
    "Liège": ("1f35f", "Patates kızartması"),
    "Amsterdam": ("1f6b2", "Kanallar ve bisikletler"),
    "Rotterdam": ("1f3d7", "Avrupa'nın en büyük limanı"),
    "Lahey": ("2696", "Uluslararası Adalet Divanı"),
    "Utrecht": ("1f430", "Miffy'nin şehri, kanallar"),
    "Eindhoven": ("1f4a1", "Işık şehri, teknoloji"),
    "Lüksemburg": ("1f3e6", "Finans merkezi"),
    "Berlin": ("1f43b", "Brandenburg Kapısı, ayı simgesi"),
    "Hamburg": ("1f6f3", "Liman ve Elbphilharmonie"),
    "Münih": ("1f968", "Oktoberfest ve brezel"),
    "Köln": ("26ea", "Köln Katedrali"),
    "Frankfurt": ("1f4b6", "Avrupa Merkez Bankası"),
    "Stuttgart": ("1f697", "Otomobil şehri"),
    "Düsseldorf": ("1f457", "Moda ve Ren kıyısı"),
    "Dortmund": ("26bd", "Futbol ve Ruhr"),
    "Leipzig": ("1f3bc", "Bach ve müzik"),
    "Dresden": ("1f3a8", "Barok sanat, Zwinger"),
    "Nürnberg": ("1f9f8", "Oyuncak fuarı"),
    "Hannover": ("1f3aa", "Dünyanın en büyük fuarları"),
    "Bremen": ("1f413", "Bremen mızıkacıları"),
    "Kopenhag": ("1f9dc", "Küçük Deniz Kızı"),
    "Aarhus": ("1f308", "Gökkuşağı panoraması"),
    "Odense": ("1f4d6", "Andersen masalları"),
    "Viyana": ("1f370", "Klasik müzik ve Sacher pastası"),
    "Graz": ("1f55b", "Saat Kulesi"),
    "Linz": ("1f916", "Dijital sanat festivali"),
    "Salzburg": ("1f3b9", "Mozart'ın doğduğu şehir"),
    "Innsbruck": ("26f7", "Alp kayak merkezi"),
    "Zürih": ("231a", "Saatler ve bankalar"),
    "Bern": ("1f43b", "Ayılar ve Saat Kulesi"),
    "Cenevre": ("1f54a", "Barış ve uluslararası kuruluşlar"),
    "Basel": ("1f48a", "İlaç sanayi ve sanat fuarı"),
    "Lozan": ("1f3c5", "Olimpiyat Komitesi"),
    "Roma": ("1f3df", "Kolezyum"),
    "Milano": ("1f45c", "Moda başkenti"),
    "Napoli": ("1f355", "Pizzanın doğduğu yer"),
    "Torino": ("1f3d4", "Alplerin eteğinde otomobil şehri"),
    "Floransa": ("1f5bc", "Rönesans sanatı"),
    "Venedik": ("1f6f6", "Gondollar ve kanallar"),
    "Bologna": ("1f35d", "Bolonez ve en eski üniversite"),
    "Bari": ("1f41f", "Adriyatik balıkçıları"),
    "Cenova": ("1f9ed", "Kolomb'un şehri"),
    "Verona": ("1f498", "Romeo ve Juliet"),
    "Palermo": ("1f34b", "Sicilya limonları ve pazarlar"),
    "Madrid": ("1f451", "Kraliyet Sarayı ve Prado"),
    "Barselona": ("26ea", "Gaudí ve Sagrada Familia"),
    "Valensiya": ("1f958", "Paellanın memleketi"),
    "Sevilla": ("1f483", "Flamenko"),
    "Malaga": ("1f3d6", "Costa del Sol plajları"),
    "Bilbao": ("1f3a8", "Guggenheim Müzesi"),
    "Zaragoza": ("1f3fa", "Roma ve Endülüs mirası"),
    "Granada": ("1f3f0", "Elhamra Sarayı"),
    "Alicante": ("1f334", "Palmiyeli sahil"),
    "Lizbon": ("1f68b", "Sarı tramvaylar"),
    "Porto": ("1f377", "Porto şarabı"),
    "Faro": ("1f30a", "Algarve sahilleri"),
    "Coimbra": ("1f393", "Tarihî üniversite"),
    "Varşova": ("1f9dc", "Deniz kızı heykeli, eski şehir"),
    "Krakov": ("1f409", "Wawel ejderhası"),
    "Wrocław": ("1f9d9", "Cüce heykelleri"),
    "Poznań": ("1f410", "Tos vuran keçiler"),
    "Gdańsk": ("1f7e0", "Kehribar başkenti"),
    "Łódź": ("1f3ac", "Film okulu ve sinema"),
    "Katowice": ("26cf", "Silezya maden ocakları"),
    "Prag": ("1f37a", "Bira ve Karl Köprüsü"),
    "Brno": ("1f9ec", "Mendel ve genetik"),
    "Ostrava": ("1f3ed", "Çelik ve sanayi"),
    "Bratislava": ("1f3f0", "Tuna kıyısında kale"),
    "Koşice": ("26ea", "Gotik katedral"),
    "Budapeşte": ("1f6c1", "Kaplıcalar ve Tuna"),
    "Debrecen": ("1f33b", "Çiçek karnavalı"),
    "Segedin": ("1f336", "Kırmızı biber ve balık çorbası"),
    "Peçuy": ("1f3fa", "Zsolnay seramikleri"),
    "Lublyana": ("1f409", "Ejderha Köprüsü"),
    "Maribor": ("1f347", "Dünyanın en yaşlı asması"),
    "Zagreb": ("1f454", "Kravatın doğduğu yer"),
    "Split": ("1f3db", "Diocletianus Sarayı"),
    "Dubrovnik": ("1f3f0", "Surlarla çevrili inci"),
    "Rijeka": ("2693", "Adriyatik limanı"),
    "Saraybosna": ("2615", "Başçarşı ve Boşnak kahvesi"),
    "Banja Luka": ("1f333", "Yeşil şehir, Vrbas"),
    "Mostar": ("1f309", "Tarihî Mostar Köprüsü"),
    "Belgrad": ("1f3f0", "Kalemegdan Kalesi"),
    "Novi Sad": ("1f3b5", "EXIT festivali"),
    "Niş": ("1f451", "Büyük Konstantin'in doğduğu yer"),
    "Podgorica": ("26f0", "Dağlar arasında başkent"),
    "Tiran": ("1f3a8", "Renkli binalar"),
    "Dıraç": ("1f3db", "Roma amfitiyatrosu"),
    "Üsküp": ("1f5ff", "Heykeller şehri"),
    "Ohri": ("1f3de", "Ohri Gölü"),
    "Kişinev": ("1f377", "Şarap mahzenleri"),
    "Priştine": ("1f4da", "Ulusal Kütüphane"),
    "Stockholm": ("1f3c6", "Nobel ödülleri"),
    "Göteborg": ("1f6a2", "İskandinavya'nın en büyük limanı"),
    "Malmö": ("1f309", "Öresund Köprüsü"),
    "Oslo": ("26f5", "Viking gemileri"),
    "Bergen": ("1f327", "Yağmurlu fiyort şehri"),
    "Vilnius": ("1f388", "Sıcak hava balonları"),
    "Kaunas": ("1f3c0", "Basketbol tutkusu"),
    "Riga": ("1f3d8", "Art Nouveau binaları"),
    "Tallinn": ("1f4bb", "Dijital devlet, Ortaçağ surları"),
}


def fetch(code):
    with urllib.request.urlopen(BASE + code + ".png", timeout=30) as response:
        return Image.open(io.BytesIO(response.read())).convert("RGBA")


def main():
    with open(os.path.join(RES, "cities.csv"), encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    cities = [row["city"] for row in rows]
    country = {row["city"]: row["iso2"] for row in rows}
    missing = [c for c in cities if c not in CITY_ICONS]
    extra = [c for c in CITY_ICONS if c not in cities]
    # Bir ülke içinde her şehrin ikonu farklıdır (farklı ülkeler aynı ikonu paylaşabilir)
    seen, duplicates = {}, []
    for city, (code, _) in CITY_ICONS.items():
        key = (country.get(city), code)
        if key in seen:
            duplicates.append(f"{seen[key]}/{city}")
        seen[key] = city
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
