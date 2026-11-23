-- BURGER İMPARATORU - Ekonomi hesapları
-- Hem sunucu hem oyuncu (client) aynı hesapları buradan yapar, böylece ekrandaki fiyatlar
-- ile sunucunun kabul ettiği fiyatlar hep aynı olur.

local Config = require(script.Parent.Config)

local Ekonomi = {}

local gelistirmeTablosu = {}
for _, g in ipairs(Config.Gelistirmeler) do
	gelistirmeTablosu[g.Anahtar] = g
end

local toplamAgirlik = 0
for _, m in ipairs(Config.Malzemeler) do
	toplamAgirlik += m.Agirlik
end

function Ekonomi.GelistirmeBul(anahtar)
	return gelistirmeTablosu[anahtar]
end

function Ekonomi.BosGelistirmeler()
	local t = {}
	for _, g in ipairs(Config.Gelistirmeler) do
		t[g.Anahtar] = 0
	end
	return t
end

function Ekonomi.Seviye(gelistirmeler, anahtar)
	return (gelistirmeler and gelistirmeler[anahtar]) or 0
end

-- Bu seviyedeki bir geliştirmeyi almak kaç para? (MAX ise nil)
function Ekonomi.Fiyat(anahtar, mevcutSeviye)
	local g = gelistirmeTablosu[anahtar]
	if not g or mevcutSeviye >= g.MaxSeviye then
		return nil
	end
	return math.floor(g.TabanFiyat * g.Carpan ^ mevcutSeviye)
end

function Ekonomi.MaxKat(gelistirmeler)
	return Config.TemelMaxKat + Ekonomi.Seviye(gelistirmeler, "DevTabak") * Config.DevTabakArtisi
end

function Ekonomi.TikBasinaKat(gelistirmeler)
	return 1 + Ekonomi.Seviye(gelistirmeler, "HizliEller")
end

function Ekonomi.LezzetCarpani(gelistirmeler)
	return 1 + Ekonomi.Seviye(gelistirmeler, "Lezzet")
end

-- Çırak Aşçı kaç saniyede bir katlar? (hiç alınmadıysa nil)
function Ekonomi.OtoAralik(gelistirmeler)
	local sv = Ekonomi.Seviye(gelistirmeler, "OtoAsci")
	if sv <= 0 then
		return nil
	end
	return Config.OtoAsciIlkAralik * Config.OtoAsciHizlanma ^ (sv - 1)
end

-- Geliştirmenin şu anki etkisini oyuncuya yazı olarak anlatır.
function Ekonomi.EtkiYazisi(anahtar, seviye)
	if anahtar == "HizliEller" then
		return string.format("Tık başına %d kat", 1 + seviye)
	elseif anahtar == "Lezzet" then
		return string.format("Kat değeri x%d", 1 + seviye)
	elseif anahtar == "OtoAsci" then
		if seviye <= 0 then
			return "Henüz yok"
		end
		return string.format("%.1f saniyede 1 kat", Config.OtoAsciIlkAralik * Config.OtoAsciHizlanma ^ (seviye - 1))
	elseif anahtar == "DevTabak" then
		return string.format("En fazla %d kat", Config.TemelMaxKat + seviye * Config.DevTabakArtisi)
	end
	return ""
end

function Ekonomi.BoyBonusu(katSayisi)
	return 1 + katSayisi / Config.BoyBonusuBolen
end

-- Servis edilince kazanılacak para: burger değeri x boy bonusu
function Ekonomi.ServisParasi(burgerDegeri, katSayisi)
	return math.floor(burgerDegeri * Ekonomi.BoyBonusu(katSayisi))
end

-- rastgele: 0 ile 1 arası sayı veren fonksiyon
function Ekonomi.MalzemeSec(rastgele)
	local r = rastgele() * toplamAgirlik
	for i, m in ipairs(Config.Malzemeler) do
		r -= m.Agirlik
		if r < 0 then
			return i, m
		end
	end
	local son = #Config.Malzemeler
	return son, Config.Malzemeler[son]
end

function Ekonomi.UnvanIndex(toplamKat)
	local idx = 1
	for i, u in ipairs(Config.Unvanlar) do
		if toplamKat >= u.Esik then
			idx = i
		end
	end
	return idx
end

-- 1234 -> "1.2K", 5600000 -> "5.6M"
function Ekonomi.Kisalt(n)
	n = math.floor(n)
	local eksi = n < 0
	n = math.abs(n)
	local yazi
	if n < 1000 then
		yazi = tostring(n)
	else
		local ekler = { "K", "M", "B", "T", "Qa", "Qi" }
		local i = 0
		local x = n
		while x >= 1000 and i < #ekler do
			x /= 1000
			i += 1
		end
		if x >= 100 then
			yazi = string.format("%d%s", math.floor(x), ekler[i])
		else
			yazi = string.format("%.1f%s", math.floor(x * 10) / 10, ekler[i]):gsub("%.0", "")
		end
	end
	return (eksi and "-" or "") .. yazi
end

return Ekonomi
