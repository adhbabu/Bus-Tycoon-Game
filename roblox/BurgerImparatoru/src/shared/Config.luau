-- BURGER İMPARATORU - Oyun ayarları
-- Sayıların hepsi buradan değiştirilebilir. Oyunu dengelemek için bu dosyayla oyna.

local Config = {}

----------------------------------------------------------------------
-- SES: "huss huss huss"
-- 1) roblox/BurgerImparatoru/sesler/huss.mp3 dosyasını Roblox'a yükle
--    (Creator Hub > Creations > Development Items > Audio > Import).
-- 2) Yüklenen sesin numarasını (Asset ID) aşağıya yaz, örnek:
--    Config.HussSesId = "rbxassetid://1234567890"
-- Boş bırakırsan oyun yine çalışır, sadece ses yerine ekranda HUSS! yazıları çıkar.
----------------------------------------------------------------------
Config.HussSesId = "rbxassetid://116644917042731"

----------------------------------------------------------------------
-- DİL: oyun Türkçe ve İngilizce. "otomatik" = Roblox dili Türkçe olan Türkçe, diğer herkes İngilizce görür.
-- Studio'da denemek için "tr" ya da "en" yazabilirsin. Çeviriler: Ortak > Dil
----------------------------------------------------------------------
Config.Dil = "otomatik"
Config.HussSesSeviyesi = 0.9 -- kendi katlaman (0 ile 1 arası)
Config.MakineSesSeviyesi = 0.22 -- kendi makinelerin
Config.BaskasininSesSeviyesi = 0.3 -- başka oyuncuların dükkanı (yakındaysan duyarsın)
-- Seste her "huss" kaçıncı saniyede başlıyor? Ekrandaki HUSS! yazıları bu anlarda fırlar.
Config.HussZamanlari = { 0.04, 0.24, 0.44 }
-- Her KATLA basışı (E) sesten TEK bir huss çalar: 1. katlama 1. huss'ı, 2. katlama 2.'yi, 3. katlama 3.'yü.
-- Böylece bir burger = 3 basış = 3 huss. Sayılar her huss'ın başladığı ve bittiği saniye.
-- (Makineler bütün burgeri bir kerede paketlediği için sesin tamamını, yani 3 huss'ı çalar.)
Config.HussParcalari = { { 0.03, 0.235 }, { 0.235, 0.435 }, { 0.435, 0.70 } }

----------------------------------------------------------------------
-- KATLAMA VE PAKETLEME
-- İmparator Burger açık bir karton kutuda gelir. Her katlamada kutu biraz daha kapanır:
-- 1) tepsinin kenarları kalkar, 2) kapağın kenarları kalkar, 3) kapak kapanır = PAKET!
----------------------------------------------------------------------
Config.KatlamaSayisi = 3
Config.KatlamaBeklemesi = 0.08 -- iki tıklama arası en az kaç saniye (hile koruması)
Config.PaketTabanDegeri = 5 -- bir İmparator Burger paketi kaç para eder (geliştirmeler bunu çarpar)
Config.AltinCarpani = 8 -- ALTIN BURGER normalin kaç katı para eder
Config.AltinTabanSansi = 0.03 -- %3 ihtimalle ALTIN BURGER
Config.AltinSansArtisi = 0.02 -- "Altın Şans" geliştirmesinin her seviyesi +%2
Config.BurgerOlcegi = 0.55 -- tezgahtaki kutu ve burgerin boyu (1 = kocaman)
Config.BantKutuOlcegi = 0.38 -- makinelerden bantta giden kutuların boyu

----------------------------------------------------------------------
-- DÜKKAN STİLİ (oyunun başında herkes seçer)
----------------------------------------------------------------------
Config.Logolar = { "🍔", "👑", "🔥", "⭐", "🐉", "🦁", "🚀", "💎", "🌶️", "🍟", "🐔", "🎩" }
Config.Renkler = {
	{ Ad = "Kırmızı", Renk = Color3.fromRGB(220, 55, 50) },
	{ Ad = "Turuncu", Renk = Color3.fromRGB(245, 140, 40) },
	{ Ad = "Sarı", Renk = Color3.fromRGB(245, 200, 45) },
	{ Ad = "Yeşil", Renk = Color3.fromRGB(75, 185, 90) },
	{ Ad = "Turkuaz", Renk = Color3.fromRGB(40, 185, 190) },
	{ Ad = "Mavi", Renk = Color3.fromRGB(60, 120, 230) },
	{ Ad = "Mor", Renk = Color3.fromRGB(150, 90, 220) },
	{ Ad = "Pembe", Renk = Color3.fromRGB(240, 110, 170) },
	{ Ad = "Siyah", Renk = Color3.fromRGB(50, 50, 58) },
	{ Ad = "Beyaz", Renk = Color3.fromRGB(235, 235, 235) },
}
Config.AdEnUzun = 20 -- dükkan adı en fazla kaç harf

----------------------------------------------------------------------
-- GELİŞTİRMELER (Dükkan menüsü)
-- Fiyat = TabanFiyat * Carpan ^ seviye
-- Un = dükkanın ünü: her paketin parasını bu oranda artırır (0.15 = +%15)
-- Gerekli = bunu almadan önce alınması gereken geliştirme
----------------------------------------------------------------------
Config.Kategoriler = {
	{ Anahtar = "Mutfak", Ad = "Mutfak", Ikon = "🍳" },
	{ Anahtar = "Calisanlar", Ad = "Çalışanlar", Ikon = "👷" },
	{ Anahtar = "Otomasyon", Ad = "Makineler", Ikon = "⚙️" },
	{ Anahtar = "Dukkan", Ad = "Dükkan", Ikon = "🏪" },
	{ Anahtar = "Sube", Ad = "Yeni Şube", Ikon = "🏙️" },
}

Config.Gelistirmeler = {
	-- Mutfak
	{ Anahtar = "HizliEller", Kategori = "Mutfak", Ad = "Hızlı Eller", Ikon = "✋", Aciklama = "Her tıkta 2 katlama", TabanFiyat = 250, Carpan = 1, MaxSeviye = 1 },
	{ Anahtar = "Tarif", Kategori = "Mutfak", Ad = "Gizli Sos", Ikon = "🧂", Aciklama = "Her paket daha çok para eder", TabanFiyat = 100, Carpan = 1.6, MaxSeviye = 40 },
	{ Anahtar = "AltinSans", Kategori = "Mutfak", Ad = "Altın Şans", Ikon = "✨", Aciklama = "ALTIN BURGER daha sık çıkar", TabanFiyat = 250, Carpan = 1.9, MaxSeviye = 10 },
	-- Çalışanlar (dükkanında gerçekten dururlar)
	{ Anahtar = "Asci", Kategori = "Calisanlar", Ad = "Aşçı", Ikon = "👨‍🍳", Aciklama = "Tezgahında senin yerine katlar", TabanFiyat = 600, Carpan = 2.2, MaxSeviye = 5 },
	{ Anahtar = "Kasiyer", Kategori = "Calisanlar", Ad = "Kasiyer", Ikon = "🧑‍💼", Aciklama = "Müşterilere güler yüz. Ün +%20", TabanFiyat = 3000, Carpan = 1, MaxSeviye = 1, Un = 0.2 },
	{ Anahtar = "Teknisyen", Kategori = "Calisanlar", Ad = "Teknisyen", Ikon = "🧑‍🔧", Aciklama = "Makineler %25 daha kısa sürede paketler", TabanFiyat = 25000, Carpan = 1, MaxSeviye = 1, Gerekli = "Makine" },
	{ Anahtar = "Temizlikci", Kategori = "Calisanlar", Ad = "Temizlikçi", Ikon = "🧹", Aciklama = "Pırıl pırıl dükkan. Ün +%15", TabanFiyat = 60000, Carpan = 1, MaxSeviye = 1, Un = 0.15 },
	-- Otomasyon
	{ Anahtar = "Makine", Kategori = "Otomasyon", Ad = "Paketleme Makinesi", Ikon = "🤖", Aciklama = "Sen dururken burger paketler", TabanFiyat = 1000, Carpan = 3, MaxSeviye = 6 },
	{ Anahtar = "MakineHizi", Kategori = "Otomasyon", Ad = "Makine Hızı", Ikon = "⚡", Aciklama = "Bütün makineler daha hızlı", TabanFiyat = 1500, Carpan = 2.1, MaxSeviye = 12, Gerekli = "Makine" },
	-- Dükkan (binan büyür, ün kazanırsın)
	{ Anahtar = "Duvar", Kategori = "Dukkan", Ad = "Duvarlar ve Camlar", Ikon = "🧱", Aciklama = "Gerçek bir dükkan! Ün +%15", TabanFiyat = 8000, Carpan = 1, MaxSeviye = 1, Un = 0.15 },
	{ Anahtar = "Cati", Kategori = "Dukkan", Ad = "Çatı ve Neon Tabela", Ikon = "💡", Aciklama = "Logon gece gündüz parlar. Ün +%20", TabanFiyat = 40000, Carpan = 1, MaxSeviye = 1, Un = 0.2, Gerekli = "Duvar" },
	{ Anahtar = "Oturma", Kategori = "Dukkan", Ad = "Oturma Alanı", Ikon = "🪑", Aciklama = "Şemsiyeli masalar. Ün +%20", TabanFiyat = 150000, Carpan = 1, MaxSeviye = 1, Un = 0.2, Gerekli = "Cati" },
	{ Anahtar = "Dekor", Kategori = "Dukkan", Ad = "Çiçek ve Işıklar", Ikon = "🌸", Aciklama = "Saksılar, ışık zinciri. Ün +%25", TabanFiyat = 300000, Carpan = 1, MaxSeviye = 1, Un = 0.25, Gerekli = "Oturma" },
	{ Anahtar = "DevTabela", Kategori = "Dukkan", Ad = "Çatıda Dev Burger", Ikon = "🍔", Aciklama = "Herkes dükkanını uzaktan görür. Ün +%50", TabanFiyat = 900000, Carpan = 1, MaxSeviye = 1, Un = 0.5, Gerekli = "Dekor" },
}

Config.MakineIlkSure = 6 -- 1 makine kaç saniyede 1 paket yapar (hız almadan)
Config.MakineHizlanma = 0.85 -- her "Makine Hızı" seviyesinde süre bu sayıyla çarpılır
Config.MakineEnKisaSure = 0.8
Config.TeknisyenCarpani = 0.75 -- teknisyen varken makine süresi bununla çarpılır
Config.AsciIlkSure = 2.0 -- aşçı 1. seviyede kaç saniyede bir katlar
Config.AsciHizlanma = 0.75 -- her seviyede bu sayıyla çarpılır

-- Müşteriler (sadece görüntü): dükkanın ünü ve makineleri arttıkça daha sık gelirler
Config.Musteri = {
	EnFazla = 5, -- bir dükkanda aynı anda en fazla kaç müşteri
	TemelAralik = 7, -- hiç geliştirme yokken kaç saniyede bir müşteri gelir
	EnKisaAralik = 1.6,
	Hiz = 7, -- yürüme hızı
}

Config.GunSuresi = 600 -- bir gün (sabah -> gece -> sabah) kaç saniye sürer

----------------------------------------------------------------------
-- MEYDANDAKİ NPC'LER VE ETKİNLİKLER
----------------------------------------------------------------------
-- Konuşulabilen NPC'ler (yanına gidip F'ye bas ya da dokun)
Config.NPCler = {
	Gorev = { Ad = "Hüsnü Usta", Unvan = "📜 Görevler", Yer = Vector3.new(-17, 0.4, -24), Yuz = "🧔", Gomlek = Color3.fromRGB(150, 60, 40) },
	Yarisma = { Ad = "Hakem Hülya", Unvan = "⏱️ Huss Yarışması", Yer = Vector3.new(17, 0.4, -24), Yuz = "😎", Gomlek = Color3.fromRGB(40, 40, 45) },
	Hediye = { Ad = "Hediyeci Hasan", Unvan = "🎁 Bedava Hediye", Yer = Vector3.new(-17, 0.4, 24), Yuz = "🥳", Gomlek = Color3.fromRGB(200, 40, 60) },
}

-- Görevler: her görev bitince ödül, sonra bir sonraki görev (gittikçe büyür)
Config.Gorev = {
	-- Tip: "Paket" = elle paket yap, "Katla" = KATLA'ya bas, "Altin" = altın burger bul, "Para" = para kazan
	Sira = { "Katla", "Paket", "Para", "Paket", "Altin", "Katla", "Para", "Paket" },
	IlkHedef = { Katla = 30, Paket = 10, Para = 500, Altin = 1 },
	Buyume = 1.35, -- her turda hedef bu kadar büyür
	OdulSaniye = 45, -- ödül: makine gelirinin bu kadar saniyesi (en az aşağıdaki kadar)
	EnAzOdul = 150,
}

-- Huss Yarışması: süre içinde olabildiğince çok KATLA'ya bas
Config.Yarisma = { Sure = 15, Bekleme = 45, TikOdulu = 1.0 } -- ödül = tık sayısı x paket parası x TikOdulu

-- Hediye: her 10 dakikada bir bedava
Config.Hediye = { Bekleme = 600, IkiKatSuresi = 60, ParaSaniye = 60, EnAzPara = 200 }

-- Altın Saat: arada bir bütün kasabada paketler 2 kat para eder, müşteriler akın eder
Config.AltinSaat = { EnAz = 300, EnCok = 480, Sure = 45, Carpan = 2 }

-- Kasabada gezen insanların söyledikleri
Config.KasabaSozleri = {
	"Huss huss huss! 🍔", "Bu kasabanın en iyi burgeri kimde?", "İmparator Burger yemeden gitmem!",
	"Altın burger gören var mı? ✨", "Ketçap mı hardal mı? İkisi! 😋", "Bugün 3 burger yedim, hâlâ açım!",
	"Şu dev balon da ne öyle? 🎈", "Hüsnü Usta görev veriyormuş!", "Huss yarışmasında rekor kıracağım!",
	"Kasabanın yeni imparatoru kim olacak? 👑", "Patatesler de bedava mı? 🍟", "Kuyruk çok uzun ama değer!",
}
Config.KasabaHalkiSayisi = 16

-- Yeni Şube: her şeyi sıfırlar (stilin ve paket sayın kalır), kalıcı para çarpanı verir
Config.SubeTabanFiyat = 2500000
Config.SubeCarpan = 4 -- her yeni şube öncekinin 4 katı pahalı
Config.SubeBonusu = 0.5 -- her şube paralara +%50

----------------------------------------------------------------------
-- UNVANLAR (toplam paketlenen burgere göre)
----------------------------------------------------------------------
Config.Unvanlar = {
	{ Esik = 0, Ad = "Çırak", Ikon = "🥄", Renk = Color3.fromRGB(200, 200, 200) },
	{ Esik = 50, Ad = "Aşçı", Ikon = "🍳", Renk = Color3.fromRGB(120, 220, 120) },
	{ Esik = 300, Ad = "Şef", Ikon = "👨‍🍳", Renk = Color3.fromRGB(90, 170, 255) },
	{ Esik = 1500, Ad = "Usta Şef", Ikon = "🎖️", Renk = Color3.fromRGB(190, 110, 255) },
	{ Esik = 7500, Ad = "Burger Lordu", Ikon = "🍔", Renk = Color3.fromRGB(255, 140, 50) },
	{ Esik = 30000, Ad = "Burger Sultanı", Ikon = "💎", Renk = Color3.fromRGB(255, 70, 90) },
	{ Esik = 150000, Ad = "BURGER İMPARATORU", Ikon = "👑", Renk = Color3.fromRGB(255, 215, 0) },
}

----------------------------------------------------------------------
-- DÜKKAN YERLEŞİMİ (arsanın içindeki yerler, arsanın ortasına göre; -Z = yola bakan ön)
-- Hem sunucu (binayı kurar) hem oyuncu ekranı (kutuları oynatır) bunu kullanır.
----------------------------------------------------------------------
Config.Yerlesim = {
	ArsaBoyu = Vector3.new(100, 1, 84),
	BinaMerkez = Vector3.new(0, 0, 8), -- binanın tabanının ortası
	BinaBoyu = Vector3.new(70, 16, 46), -- en, yükseklik, derinlik
	TezgahYuksekligi = 3.4,
	AnaTezgah = Vector3.new(-17, 0, -19.5), -- KATLA kutusunun durduğu tezgah (binanın önünde)
	OyuncuYeri = Vector3.new(-17, 0, -27.5), -- oyuncu buraya ışınlanır, tezgaha bakar
	SatisNoktasi = Vector3.new(20, 0, -19), -- paketler burada satılır
	MakineZ = 25, -- makineler arka duvarın önünde
	MakineX = { -29, -21, -13, -5, 3, 11 },
	BantZ = 17, -- makinelerin önündeki ana bant
	BantX = 20, -- bant buradan öne, satış penceresine döner
	Masalar = { Vector3.new(-41, 0, -35), Vector3.new(-41, 0, -25), Vector3.new(39, 0, -36), Vector3.new(41, 0, -26) },
	-- çalışanların durduğu yerler ve baktıkları yön
	Calisanlar = {
		Asci = { Yer = Vector3.new(-23.6, 0, -19.5), Bakis = Vector3.new(1, 0, 0) },
		Kasiyer = { Yer = Vector3.new(16.4, 0, -12.6), Bakis = Vector3.new(0, 0, -1) },
		Teknisyen = { Yer = Vector3.new(0, 0, 21), Bakis = Vector3.new(0, 0, 1) },
		Temizlikci = { Yer = Vector3.new(6, 0, -25), Bakis = Vector3.new(-1, 0, -0.4) },
	},
	MusteriGiris = -45, -- müşteriler bu çizgiden (kaldırımdan) gelir
	KuyrukAralik = 2.6, -- satış penceresinde sıradaki müşteriler arası
	TabelaYeri = Vector3.new(0, 0, -37),
	-- yerdeki satın alma butonları: binanın iki yanında birer sıra
	ButonX = 42,
	ButonZ = { -9, -3, 3, 9, 15, 21, 27 },
}

-- Yerdeki butonların sırası (tycoon gibi: üstüne bas, geliştirmeyi al). Sol = binanın sol yanı.
Config.Butonlar = {
	Sol = { "HizliEller", "Tarif", "AltinSans", "Asci", "Kasiyer", "Teknisyen", "Temizlikci" },
	Sag = { "Makine", "MakineHizi", "Duvar", "Cati", "Oturma", "Dekor", "DevTabela" },
}

----------------------------------------------------------------------
-- ROBUX MAĞAZASI (💎 butonu)
-- Id = Roblox'taki numara (Creator Hub > oyunun > Monetization > Passes / Developer Products).
-- Id 0 olan ürün mağazada "Yakında" görünür, satılmaz. Fiyat sadece gösterim içindir; gerçek fiyat
-- Roblox'ta ne yazıyorsa odur (oyun açılınca Roblox'tan okunur).
-- Oyunu bozmasınlar diye: para paketleri oyuncunun o anki kazancına göre verilir (birkaç dakikalık
-- kazanç kadar), 2 kat para süreli, geçişler kalıcı ama oyunu tek başına bitirmez.
----------------------------------------------------------------------
Config.Robux = {
	-- Geçişler (bir kere alınır, hep geçerli)
	Gecisler = {
		{ Anahtar = "IkiKatPara", Id = 2092999785, Fiyat = 79, Ikon = "💰", Ad = "2 Kat Para", Aciklama = "Bütün paketler hep 2 kat para eder" },
		{ Anahtar = "VIP", Id = 2093191786, Fiyat = 99, Ikon = "⭐", Ad = "VIP", Aciklama = "+%25 para, başında ve sohbette VIP yazısı" },
		{ Anahtar = "AltinSans", Id = 2092753876, Fiyat = 39, Ikon = "✨", Ad = "Altın Şans x2", Aciklama = "ALTIN BURGER 2 kat daha sık çıkar" },
		{ Anahtar = "OtoKatla", Id = 2094361721, Fiyat = 49, Ikon = "🔁", Ad = "Otomatik Katlama", Aciklama = "Açınca KATLA'ya senin yerine basar" },
	},
	-- Ürünler (istediğin kadar alınır)
	Urunler = {
		{ Anahtar = "ParaKucuk", Id = 3735411282, Fiyat = 15, Ikon = "💵", Ad = "Para Kesesi", Aciklama = "10 dakikalık kazancın kadar para", Saniye = 600, EnAz = 2000 },
		{ Anahtar = "ParaOrta", Id = 3735411312, Fiyat = 39, Ikon = "💰", Ad = "Para Çuvalı", Aciklama = "45 dakikalık kazancın kadar para", Saniye = 2700, EnAz = 10000 },
		{ Anahtar = "ParaBuyuk", Id = 3735411315, Fiyat = 99, Ikon = "🏦", Ad = "Para Kasası", Aciklama = "3 saatlik kazancın kadar para", Saniye = 10800, EnAz = 50000 },
		{ Anahtar = "IkiKat15", Id = 3735411317, Fiyat = 25, Ikon = "🔥", Ad = "15 Dakika 2 Kat Para", Aciklama = "15 dakika boyunca bütün paketler 2 kat", Sure = 900 },
		{ Anahtar = "AltinSaat", Id = 3735411319, Fiyat = 29, Ikon = "⏰", Ad = "Herkese Altın Saat", Aciklama = "Bütün sunucuya hemen Altın Saat başlat" },
	},
	VipCarpani = 1.25,
	OtoKatlaAraligi = 0.3, -- otomatik katlama kaç saniyede bir basar
}

----------------------------------------------------------------------
-- OYUNCULARI GERİ GETİREN ŞEYLER (🎉 Ödüller paneli)
-- Para ödülleri hep oyuncunun o anki kazancına göre: saniyede makinelerden ya da elle katlayarak
-- kazandığı (hangisi büyükse) x Saniye, en az EnAz.
----------------------------------------------------------------------
-- Günlük ödül: her gün gel, seri büyüsün (7. günden sonra hep 7. günün ödülü)
Config.Gunluk = {
	Bekleme = 20 * 3600, -- yeni ödül için kaç saniye geçmeli
	SeriBozulma = 48 * 3600, -- bu kadar gelmezsen seri 1'e döner
	EnUzunSeri = 7,
	Saniye = 120, -- ödül = gün x bu kadar saniyelik kazanç
	EnAz = 500, -- ödül = gün x en az bu kadar
}
-- Aynı sunucudaki her Roblox arkadaşın: +%10 para (en çok +%30). Arkadaşınla oyna!
Config.ArkadasBonusu = { Kisi = 0.1, EnCok = 0.3 }
-- Roblox grubu: numarasını yaz (0 = kapalı). Gruptakiler +%10 para alır.
Config.GrupId = 0
Config.GrupBonusu = 0.1
-- Oyunda bu kadar dakika oynayınca bir kere "oyunu favorile" penceresi çıkar
Config.FavoriSoruDakika = 8
-- Kodlar (videolarda paylaş!): büyük harfle yaz. Her oyuncu her kodu bir kere kullanır.
Config.Kodlar = {
	HUSS = { Saniye = 300, EnAz = 1000 },
	BURGER = { Saniye = 300, EnAz = 1000 },
	TYCOON = { Saniye = 600, EnAz = 2500 },
}
-- Rozetler (Id = Roblox'taki rozet numarası, 0 = kapalı)
Config.Rozetler = {
	{ Anahtar = "Hosgeldin", Id = 0, Ad = "Welcome to Burger Town!", Aciklama = "You joined Burger Tycoon. Huss huss huss!" },
	{ Anahtar = "IlkPaket", Id = 0, Paket = 1, Ad = "First Burger", Aciklama = "Pack your first Emperor Burger." },
	{ Anahtar = "Paket1000", Id = 0, Paket = 1000, Ad = "Burger Machine", Aciklama = "Pack 1,000 burgers." },
	{ Anahtar = "IlkSube", Id = 0, Sube = 1, Ad = "New Branch", Aciklama = "Open your first new branch." },
	{ Anahtar = "Imparator", Id = 0, Paket = 150000, Ad = "BURGER EMPEROR", Aciklama = "Become the BURGER EMPEROR (150,000 burgers)." },
}
-- Meydandaki dünya sıralaması tahtası (en çok paket yapanlar)
Config.Siralama = { Ad = "BurgerImparatoru_Siralama_v1", Yer = Vector3.new(-30, 0.4, 0), Guncelleme = 120, Kisi = 10 }

Config.ArsaSayisi = 8
Config.KayitAnahtari = "BurgerImparatoru_v3" -- DataStore adı (değiştirirsen herkesin kaydı sıfırlanır)

return Config
