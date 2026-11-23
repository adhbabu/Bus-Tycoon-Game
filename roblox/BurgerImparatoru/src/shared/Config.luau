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
Config.HussSesId = ""
Config.HussSesSeviyesi = 0.9 -- kendi katlaman (0 ile 1 arası)
Config.MakineSesSeviyesi = 0.22 -- kendi makinelerin
Config.BaskasininSesSeviyesi = 0.3 -- başka oyuncuların dükkanı (yakındaysan duyarsın)
-- Seste her "huss" kaçıncı saniyede başlıyor? Ekrandaki HUSS! yazıları bu anlarda fırlar.
Config.HussZamanlari = { 0.04, 0.24, 0.44 }

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

-- Yeni Şube: her şeyi sıfırlar (stilin ve paket sayın kalır), kalıcı para çarpanı verir
Config.SubeTabanFiyat = 2500000
Config.SubeCarpan = 4 -- her yeni şube öncekinin 4 katı pahalı
Config.SubeBonusu = 0.5 -- her şube paralara +%50

----------------------------------------------------------------------
-- UNVANLAR (toplam paketlenen burgere göre)
----------------------------------------------------------------------
Config.Unvanlar = {
	{ Esik = 0, Ad = "Çırak", Renk = Color3.fromRGB(200, 200, 200) },
	{ Esik = 50, Ad = "Aşçı", Renk = Color3.fromRGB(120, 220, 120) },
	{ Esik = 300, Ad = "Şef", Renk = Color3.fromRGB(90, 170, 255) },
	{ Esik = 1500, Ad = "Usta Şef", Renk = Color3.fromRGB(190, 110, 255) },
	{ Esik = 7500, Ad = "Burger Lordu", Renk = Color3.fromRGB(255, 140, 50) },
	{ Esik = 30000, Ad = "Burger Sultanı", Renk = Color3.fromRGB(255, 70, 90) },
	{ Esik = 150000, Ad = "BURGER İMPARATORU", Renk = Color3.fromRGB(255, 215, 0) },
}

----------------------------------------------------------------------
-- DÜKKAN YERLEŞİMİ (arsanın içindeki yerler, arsanın ortasına göre; -Z = yola bakan ön)
-- Hem sunucu (binayı kurar) hem oyuncu ekranı (kutuları oynatır) bunu kullanır.
----------------------------------------------------------------------
Config.Yerlesim = {
	ArsaBoyu = Vector3.new(70, 1, 60),
	BinaMerkez = Vector3.new(0, 0, 5), -- binanın tabanının ortası
	BinaBoyu = Vector3.new(50, 12, 34), -- en, yükseklik, derinlik
	TezgahYuksekligi = 3.4,
	AnaTezgah = Vector3.new(-12, 0, -16), -- KATLA kutusunun durduğu tezgah (binanın önünde)
	OyuncuYeri = Vector3.new(-12, 0, -24), -- oyuncu buraya ışınlanır, tezgaha bakar
	SatisNoktasi = Vector3.new(14, 0, -15.5), -- paketler burada satılır
	MakineZ = 17, -- makineler arka duvarın önünde
	MakineX = { -21, -15, -9, -3, 3, 9 },
	BantZ = 10, -- makinelerin önündeki ana bant
	BantX = 14, -- bant buradan öne, satış penceresine döner
	Masalar = { Vector3.new(-28.5, 0, -26), Vector3.new(-28.5, 0, -18), Vector3.new(27, 0, -27), Vector3.new(28.5, 0, -19) },
	-- çalışanların durduğu yerler ve baktıkları yön
	Calisanlar = {
		Asci = { Yer = Vector3.new(-18.6, 0, -16), Bakis = Vector3.new(1, 0, 0) },
		Kasiyer = { Yer = Vector3.new(10.4, 0, -9.6), Bakis = Vector3.new(0, 0, -1) },
		Teknisyen = { Yer = Vector3.new(0, 0, 13.2), Bakis = Vector3.new(0, 0, 1) },
		Temizlikci = { Yer = Vector3.new(5, 0, -21), Bakis = Vector3.new(-1, 0, -0.4) },
	},
	MusteriGiris = -33, -- müşteriler bu çizgiden (kaldırımdan) gelir
	KuyrukAralik = 2.6, -- satış penceresinde sıradaki müşteriler arası
	TabelaYeri = Vector3.new(0, 0, -28),
}

Config.ArsaSayisi = 8
Config.KayitAnahtari = "BurgerImparatoru_v3" -- DataStore adı (değiştirirsen herkesin kaydı sıfırlanır)

return Config
