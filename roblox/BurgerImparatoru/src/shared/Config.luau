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
Config.BaskasininSesSeviyesi = 0.35 -- başka oyuncuların katlaması (yakındaysan duyarsın)
-- Seste her "huss" kaçıncı saniyede başlıyor? Ekrandaki HUSS! yazıları bu anlarda fırlar.
Config.HussZamanlari = { 0.04, 0.24, 0.44 }

----------------------------------------------------------------------
-- KATLAMA VE PAKETLEME
-- Her burger açık bir kutunun içinde gelir. 3 katlamada kutu kapanır, burger paketlenir.
----------------------------------------------------------------------
Config.KatlamaSayisi = 3 -- bir burger kaç katlamada paketlenir
Config.KatlamaBeklemesi = 0.08 -- iki tıklama arası en az kaç saniye (hile koruması)
Config.PaketTabanDegeri = 4 -- her paket en az bu kadar para eder (+ içindeki malzemeler)

-- Malzemeler: Deger = paketin değerine eklediği para.
-- Sekil: "disk" (yuvarlak), "kare" (peynir gibi), "dalga" (marul gibi)
Config.Malzemeler = {
	Kofte = { Ad = "Köfte", Deger = 3, Renk = Color3.fromRGB(110, 62, 38), Kalinlik = 0.55, Cap = 4.3, Sekil = "disk", Malzeme = "Slate" },
	Peynir = { Ad = "Peynir", Deger = 2, Renk = Color3.fromRGB(255, 196, 40), Kalinlik = 0.16, Cap = 4.7, Sekil = "kare", Malzeme = "SmoothPlastic" },
	Marul = { Ad = "Marul", Deger = 1, Renk = Color3.fromRGB(110, 200, 70), Kalinlik = 0.22, Cap = 4.9, Sekil = "dalga", Malzeme = "Grass" },
	Domates = { Ad = "Domates", Deger = 1, Renk = Color3.fromRGB(225, 45, 40), Kalinlik = 0.26, Cap = 3.9, Sekil = "disk", Malzeme = "SmoothPlastic" },
	Sogan = { Ad = "Soğan", Deger = 1, Renk = Color3.fromRGB(235, 215, 235), Kalinlik = 0.14, Cap = 3.7, Sekil = "disk", Malzeme = "SmoothPlastic" },
	Tursu = { Ad = "Turşu", Deger = 2, Renk = Color3.fromRGB(120, 140, 40), Kalinlik = 0.12, Cap = 3.4, Sekil = "disk", Malzeme = "SmoothPlastic" },
	AltinKofte = { Ad = "ALTIN KÖFTE", Deger = 30, Renk = Color3.fromRGB(255, 205, 50), Kalinlik = 0.6, Cap = 4.4, Sekil = "disk", Malzeme = "Foil", Altin = true },
}

-- Burger tarifi, alttan üste. Sans = o malzemenin burgerde olma ihtimali (1 = hep var).
-- İlk köfte bazen ALTIN KÖFTE olur (bkz. AltinTabanSansi).
Config.Tarif = {
	{ Malzeme = "Kofte", Sans = 1 },
	{ Malzeme = "Peynir", Sans = 0.75 },
	{ Malzeme = "Kofte", Sans = 0.2 },
	{ Malzeme = "Marul", Sans = 0.6 },
	{ Malzeme = "Domates", Sans = 0.5 },
	{ Malzeme = "Sogan", Sans = 0.3 },
	{ Malzeme = "Tursu", Sans = 0.3 },
}

Config.AltinTabanSansi = 0.03 -- %3 ihtimalle ALTIN BURGER
Config.AltinSansArtisi = 0.02 -- "Altın Şans" geliştirmesinin her seviyesi +%2

Config.AltEkmek = { Ad = "Alt Ekmek", Renk = Color3.fromRGB(222, 160, 80), Kalinlik = 0.7, Cap = 4.5 }
Config.UstEkmek = { Ad = "Üst Ekmek", Renk = Color3.fromRGB(222, 160, 80), Kalinlik = 1.8, Cap = 4.6 }

-- Paket kutusu
Config.KutuGenislik = 5.8
Config.KutuYukseklik = 5.0
Config.KutuRengi = Color3.fromRGB(250, 238, 214)
Config.KapakRengi = Color3.fromRGB(215, 45, 45)
Config.AltinKapakRengi = Color3.fromRGB(255, 200, 40)
Config.YiginEnFazla = 6 -- tezgahın yanındaki paket kulesi en fazla kaç kutu olur

----------------------------------------------------------------------
-- GELİŞTİRMELER (Dükkan)
-- Fiyat = TabanFiyat * Carpan ^ seviye
----------------------------------------------------------------------
Config.Gelistirmeler = {
	{
		Anahtar = "HizliEller",
		Ad = "Hızlı Eller",
		Ikon = "✋",
		Aciklama = "Her tıkta daha çok katlama",
		TabanFiyat = 75,
		Carpan = 4,
		MaxSeviye = 2,
	},
	{
		Anahtar = "Lezzet",
		Ad = "Lezzet Ustası",
		Ikon = "🧂",
		Aciklama = "Her paket daha çok para eder",
		TabanFiyat = 40,
		Carpan = 1.9,
		MaxSeviye = 25,
	},
	{
		Anahtar = "OtoAsci",
		Ad = "Çırak Aşçı",
		Ikon = "👨‍🍳",
		Aciklama = "Sen dururken o katlar",
		TabanFiyat = 120,
		Carpan = 2.3,
		MaxSeviye = 10,
	},
	{
		Anahtar = "AltinSans",
		Ad = "Altın Şans",
		Ikon = "✨",
		Aciklama = "ALTIN BURGER daha sık çıkar",
		TabanFiyat = 100,
		Carpan = 2.2,
		MaxSeviye = 10,
	},
}

Config.OtoAsciIlkAralik = 3.0 -- Çırak Aşçı 1. seviyede kaç saniyede bir katlar
Config.OtoAsciHizlanma = 0.78 -- her seviyede aralık bu sayıyla çarpılır

----------------------------------------------------------------------
-- UNVANLAR (toplam paketlenen burgere göre)
----------------------------------------------------------------------
Config.Unvanlar = {
	{ Esik = 0, Ad = "Çırak", Renk = Color3.fromRGB(200, 200, 200) },
	{ Esik = 20, Ad = "Aşçı", Renk = Color3.fromRGB(120, 220, 120) },
	{ Esik = 100, Ad = "Şef", Renk = Color3.fromRGB(90, 170, 255) },
	{ Esik = 400, Ad = "Usta Şef", Renk = Color3.fromRGB(190, 110, 255) },
	{ Esik = 1500, Ad = "Burger Lordu", Renk = Color3.fromRGB(255, 140, 50) },
	{ Esik = 5000, Ad = "Burger Sultanı", Renk = Color3.fromRGB(255, 70, 90) },
	{ Esik = 20000, Ad = "BURGER İMPARATORU", Renk = Color3.fromRGB(255, 215, 0) },
}

----------------------------------------------------------------------
-- DÜNYA
----------------------------------------------------------------------
Config.TezgahSayisi = 12 -- aynı sunucuda en fazla kaç oyuncu burger yapabilir
Config.TezgahYaricapi = 46 -- tezgahlar ortadaki heykelin etrafında bu uzaklıkta dizilir

Config.KayitAnahtari = "BurgerImparatoru_v2" -- DataStore adı (değiştirirsen herkesin kaydı sıfırlanır)

return Config
