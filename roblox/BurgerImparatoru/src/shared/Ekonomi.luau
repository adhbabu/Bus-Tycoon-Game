-- BURGER İMPARATORU - Ekonomi hesapları
-- Hem sunucu hem oyuncu ekranı aynı hesapları buradan yapar, böylece ekrandaki fiyatlar
-- ile sunucunun kabul ettiği fiyatlar hep aynı olur.

local Config = require(script.Parent.Config)

local Ekonomi = {}

local tablo = {}
for _, g in ipairs(Config.Gelistirmeler) do
	tablo[g.Anahtar] = g
end

function Ekonomi.GelistirmeBul(anahtar)
	return tablo[anahtar]
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

-- Bu geliştirmenin kilidi açık mı? (Gerekli olan alınmış mı)
function Ekonomi.KilidiAcik(gelistirmeler, anahtar)
	local g = tablo[anahtar]
	if not g then
		return false
	end
	return g.Gerekli == nil or Ekonomi.Seviye(gelistirmeler, g.Gerekli) > 0
end

-- Bir sonraki seviyenin fiyatı (MAX ise nil)
function Ekonomi.Fiyat(anahtar, mevcutSeviye)
	local g = tablo[anahtar]
	if not g or mevcutSeviye >= g.MaxSeviye then
		return nil
	end
	return math.floor(g.TabanFiyat * g.Carpan ^ mevcutSeviye)
end

function Ekonomi.TikBasinaKatlama(gelistirmeler)
	return math.min(Config.KatlamaSayisi, 1 + Ekonomi.Seviye(gelistirmeler, "HizliEller"))
end

function Ekonomi.TarifCarpani(gelistirmeler)
	return 1 + 0.5 * Ekonomi.Seviye(gelistirmeler, "Tarif")
end

-- Dükkanın ünü: alınan bina geliştirmelerinin toplamı
function Ekonomi.UnCarpani(gelistirmeler)
	local un = 1
	for _, g in ipairs(Config.Gelistirmeler) do
		if g.Un and Ekonomi.Seviye(gelistirmeler, g.Anahtar) > 0 then
			un += g.Un
		end
	end
	return un
end

function Ekonomi.SubeCarpani(sube)
	return 1 + (sube or 0) * Config.SubeBonusu
end

function Ekonomi.AltinSansi(gelistirmeler)
	return Config.AltinTabanSansi + Ekonomi.Seviye(gelistirmeler, "AltinSans") * Config.AltinSansArtisi
end

-- Bir paketin parası
function Ekonomi.PaketDegeri(altin, gelistirmeler, sube)
	local d = Config.PaketTabanDegeri
		* (altin and Config.AltinCarpani or 1)
		* Ekonomi.TarifCarpani(gelistirmeler)
		* Ekonomi.UnCarpani(gelistirmeler)
		* Ekonomi.SubeCarpani(sube)
	return math.floor(d)
end

-- Bir makine kaç saniyede bir paket yapar
function Ekonomi.MakineSuresi(gelistirmeler)
	local s = Config.MakineIlkSure * Config.MakineHizlanma ^ Ekonomi.Seviye(gelistirmeler, "MakineHizi")
	s = math.max(Config.MakineEnKisaSure, s)
	if Ekonomi.Seviye(gelistirmeler, "Teknisyen") > 0 then
		s *= Config.TeknisyenCarpani
	end
	return s
end

-- Aşçı kaç saniyede bir katlar (aşçı yoksa nil)
function Ekonomi.AsciAraligi(gelistirmeler)
	local sv = Ekonomi.Seviye(gelistirmeler, "Asci")
	if sv <= 0 then
		return nil
	end
	return Config.AsciIlkSure * Config.AsciHizlanma ^ (sv - 1)
end

-- Makinelerin ve aşçının saniyede getirdiği ortalama para (sen hiç tıklamasan da)
function Ekonomi.MakineGeliri(gelistirmeler, sube)
	local paketSaniye = 0
	local adet = Ekonomi.Seviye(gelistirmeler, "Makine")
	if adet > 0 then
		paketSaniye += adet / Ekonomi.MakineSuresi(gelistirmeler)
	end
	local asci = Ekonomi.AsciAraligi(gelistirmeler)
	if asci then
		paketSaniye += 1 / (asci * Config.KatlamaSayisi)
	end
	if paketSaniye <= 0 then
		return 0
	end
	local sans = Ekonomi.AltinSansi(gelistirmeler)
	local ortalama = (1 - sans) * Ekonomi.PaketDegeri(false, gelistirmeler, sube)
		+ sans * Ekonomi.PaketDegeri(true, gelistirmeler, sube)
	return paketSaniye * ortalama
end

function Ekonomi.SubeFiyati(sube)
	return math.floor(Config.SubeTabanFiyat * Config.SubeCarpan ^ (sube or 0))
end

-- Geliştirmenin şu anki etkisini yazıyla anlatır
function Ekonomi.EtkiYazisi(anahtar, seviye)
	local g = tablo[anahtar]
	if anahtar == "HizliEller" then
		return string.format("Tık başına %d katlama", math.min(Config.KatlamaSayisi, 1 + seviye))
	elseif anahtar == "Tarif" then
		return string.format("Paket parası x%.1f", 1 + 0.5 * seviye)
	elseif anahtar == "AltinSans" then
		return string.format("Altın burger %%%d", math.floor((Config.AltinTabanSansi + seviye * Config.AltinSansArtisi) * 100 + 0.5))
	elseif anahtar == "Makine" then
		return string.format("%d / %d makine", seviye, g.MaxSeviye)
	elseif anahtar == "MakineHizi" then
		local s = math.max(Config.MakineEnKisaSure, Config.MakineIlkSure * Config.MakineHizlanma ^ seviye)
		return string.format("%.1f sn'de 1 paket", s)
	elseif anahtar == "Asci" then
		if seviye <= 0 then
			return "Henüz yok"
		end
		return string.format("%.1f sn'de 1 katlama", Config.AsciIlkSure * Config.AsciHizlanma ^ (seviye - 1))
	elseif anahtar == "Teknisyen" then
		return seviye > 0 and "Alındı ✓" or "Makine süresi -%25"
	elseif g and g.Un then
		return seviye > 0 and "Alındı ✓" or string.format("Ün +%%%d", math.floor(g.Un * 100 + 0.5))
	end
	return ""
end

function Ekonomi.UnvanIndex(toplamPaket)
	local idx = 1
	for i, u in ipairs(Config.Unvanlar) do
		if toplamPaket >= u.Esik then
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
