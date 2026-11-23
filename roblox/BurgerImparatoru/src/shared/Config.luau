-- BURGER İMPARATORU - Oyun ayarları
-- Sayıların hepsi buradan değiştirilebilir. Oyunu dengelemek için bu dosyayla oyna.

local Config = {}

----------------------------------------------------------------------
-- SES: "huss huss huss"
-- 1) roblox/BurgerImparatoru/sesler/huss.mp3 dosyasını Roblox'a yükle
--    (Creator Hub > Creations > Development Items > Audio > Import).
-- 2) Yüklenen sesin numarasını (Asset ID) aşağıya yaz, örnek:
--    Config.HussSesId = "rbxassetid://1234567890"
-- Boş bırakırsan oyun yine çalışır, sadece ses yerine ekranda HUSS yazıları çıkar.
----------------------------------------------------------------------
Config.HussSesId = ""
Config.HussSesSeviyesi = 0.9 -- kendi katlaman (0 ile 1 arası)
Config.BaskasininSesSeviyesi = 0.35 -- başka oyuncuların katlaması (yakındaysan duyarsın)
-- Seste her "huss" kaçıncı saniyede başlıyor? Ekrandaki HUSS! yazıları bu anlarda fırlar.
Config.HussZamanlari = { 0, 0.67, 1.12 }

----------------------------------------------------------------------
-- KATLAMA
----------------------------------------------------------------------
Config.KatlamaBeklemesi = 0.08 -- iki katlama arası en az kaç saniye (hile koruması)
Config.ServisIcinEnAzKat = 3 -- servis etmek için burgerde en az kaç kat olmalı
Config.BoyBonusuBolen = 25 -- her 25 kat, servis parasını 1 kat daha artırır

-- Malzemeler: Agirlik ne kadar büyükse o kadar sık çıkar.
-- Deger = bu katın burgere kattığı para.
-- Sekil: "disk" (yuvarlak), "kare" (peynir gibi), "dalga" (marul gibi)
Config.Malzemeler = {
	{ Ad = "Köfte", Deger = 3, Agirlik = 30, Renk = Color3.fromRGB(110, 62, 38), Kalinlik = 0.55, Cap = 4.3, Sekil = "disk", Malzeme = "Slate" },
	{ Ad = "Peynir", Deger = 2, Agirlik = 22, Renk = Color3.fromRGB(255, 196, 40), Kalinlik = 0.16, Cap = 4.7, Sekil = "kare", Malzeme = "SmoothPlastic" },
	{ Ad = "Marul", Deger = 1, Agirlik = 18, Renk = Color3.fromRGB(110, 200, 70), Kalinlik = 0.22, Cap = 4.9, Sekil = "dalga", Malzeme = "Grass" },
	{ Ad = "Domates", Deger = 1, Agirlik = 14, Renk = Color3.fromRGB(225, 45, 40), Kalinlik = 0.26, Cap = 3.9, Sekil = "disk", Malzeme = "SmoothPlastic" },
	{ Ad = "Soğan", Deger = 1, Agirlik = 8, Renk = Color3.fromRGB(235, 215, 235), Kalinlik = 0.14, Cap = 3.7, Sekil = "disk", Malzeme = "SmoothPlastic" },
	{ Ad = "Turşu", Deger = 2, Agirlik = 6, Renk = Color3.fromRGB(120, 140, 40), Kalinlik = 0.12, Cap = 3.4, Sekil = "disk", Malzeme = "SmoothPlastic" },
	{ Ad = "ALTIN KÖFTE", Deger = 30, Agirlik = 2, Renk = Color3.fromRGB(255, 205, 50), Kalinlik = 0.6, Cap = 4.4, Sekil = "disk", Malzeme = "Foil", Altin = true },
}

Config.AltEkmek = { Ad = "Alt Ekmek", Renk = Color3.fromRGB(222, 160, 80), Kalinlik = 0.7, Cap = 4.5 }
Config.UstEkmek = { Ad = "Üst Ekmek", Renk = Color3.fromRGB(222, 160, 80), Kalinlik = 1.8, Cap = 4.6 }

----------------------------------------------------------------------
-- GELİŞTİRMELER (Dükkan)
-- Fiyat = TabanFiyat * Carpan ^ seviye
----------------------------------------------------------------------
Config.Gelistirmeler = {
	{
		Anahtar = "HizliEller",
		Ad = "Hızlı Eller",
		Ikon = "✋",
		Aciklama = "Her tıkta daha çok kat",
		TabanFiyat = 40,
		Carpan = 1.9,
		MaxSeviye = 9,
	},
	{
		Anahtar = "Lezzet",
		Ad = "Lezzet Ustası",
		Ikon = "🧂",
		Aciklama = "Her kat daha çok para eder",
		TabanFiyat = 60,
		Carpan = 2.1,
		MaxSeviye = 20,
	},
	{
		Anahtar = "OtoAsci",
		Ad = "Çırak Aşçı",
		Ikon = "👨‍🍳",
		Aciklama = "Sen dururken o katlar",
		TabanFiyat = 150,
		Carpan = 2.4,
		MaxSeviye = 10,
	},
	{
		Anahtar = "DevTabak",
		Ad = "Dev Tabak",
		Ikon = "🍽️",
		Aciklama = "Burger daha yükseğe çıkar",
		TabanFiyat = 100,
		Carpan = 2.0,
		MaxSeviye = 8,
	},
}

Config.TemelMaxKat = 30 -- Dev Tabak almadan burger en fazla kaç kat olur
Config.DevTabakArtisi = 25 -- Dev Tabak her seviyede kaç kat ekler
Config.OtoAsciIlkAralik = 3.0 -- Çırak Aşçı 1. seviyede kaç saniyede bir katlar
Config.OtoAsciHizlanma = 0.78 -- her seviyede aralık bu sayıyla çarpılır

----------------------------------------------------------------------
-- UNVANLAR (toplam katlanan kata göre)
----------------------------------------------------------------------
Config.Unvanlar = {
	{ Esik = 0, Ad = "Çırak", Renk = Color3.fromRGB(200, 200, 200) },
	{ Esik = 100, Ad = "Aşçı", Renk = Color3.fromRGB(120, 220, 120) },
	{ Esik = 500, Ad = "Şef", Renk = Color3.fromRGB(90, 170, 255) },
	{ Esik = 2000, Ad = "Usta Şef", Renk = Color3.fromRGB(190, 110, 255) },
	{ Esik = 8000, Ad = "Burger Lordu", Renk = Color3.fromRGB(255, 140, 50) },
	{ Esik = 30000, Ad = "Burger Sultanı", Renk = Color3.fromRGB(255, 70, 90) },
	{ Esik = 100000, Ad = "BURGER İMPARATORU", Renk = Color3.fromRGB(255, 215, 0) },
}

----------------------------------------------------------------------
-- DÜNYA
----------------------------------------------------------------------
Config.TezgahSayisi = 12 -- aynı sunucuda en fazla kaç oyuncu burger yapabilir
Config.TezgahYaricapi = 40 -- tezgahlar ortadaki heykelin etrafında bu uzaklıkta dizilir

Config.KayitAnahtari = "BurgerImparatoru_v1" -- DataStore adı (değiştirirsen herkesin kaydı sıfırlanır)

return Config
