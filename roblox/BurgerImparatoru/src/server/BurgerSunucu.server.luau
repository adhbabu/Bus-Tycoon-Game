-- BURGER İMPARATORU - Sunucu
-- Arsalar, katlama, paketleme makineleri, dükkan geliştirmeleri, yeni şube, dükkan stili ve kayıt.
-- Oyuncunun ekranı sadece istekte bulunur; parayı ve her kararı sunucu verir (hile yapılamaz).
-- Kutuların katlanması ve bantta gitmesi gibi görüntüleri her oyuncunun ekranı kendisi çizer.

local Players = game:GetService("Players")
local Lighting = game:GetService("Lighting")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local DataStoreService = game:GetService("DataStoreService")
local TextService = game:GetService("TextService")
local MarketplaceService = game:GetService("MarketplaceService")
local BadgeService = game:GetService("BadgeService")

local Ortak = ReplicatedStorage:WaitForChild("Ortak")
local Config = require(Ortak:WaitForChild("Config"))
local Ekonomi = require(Ortak:WaitForChild("Ekonomi"))
local Gorsel = require(Ortak:WaitForChild("Gorsel"))
local Dukkan = require(script.Parent:WaitForChild("Dukkan"))
local HaritaUretici = require(script.Parent:WaitForChild("HaritaUretici"))

----------------------------------------------------------------------
-- Uzak olaylar (RemoteEvent)
----------------------------------------------------------------------
local uzaklar = Instance.new("Folder")
uzaklar.Name = "Uzaklar"
local function olay(ad)
	local e = Instance.new("RemoteEvent")
	e.Name = ad
	e.Parent = uzaklar
	return e
end
local KatlaOlay = olay("Katla") -- oyuncu -> sunucu
local SatinAlOlay = olay("SatinAl") -- oyuncu -> sunucu (anahtar)
local SubeAcOlay = olay("SubeAc") -- oyuncu -> sunucu
local StilSecOlay = olay("StilSec") -- oyuncu -> sunucu ({Logo, Renk, Ad})
local DurumIsteOlay = olay("DurumIste") -- oyuncu -> sunucu
local EvimeGitOlay = olay("EvimeGit") -- oyuncu -> sunucu (dükkanına ışınlan)
local DurumOlay = olay("Durum") -- sunucu -> oyuncu (durum tablosu)
-- sunucu -> herkes: kim, arsa, aşama, paketlendi mi, kazanç, bu burger altın mı, sıradaki altın mı, aşçı mı katladı
local KatlandiOlay = olay("Katlandi")
local UretimOlay = olay("Uretim") -- sunucu -> herkes: { {arsa, makine, altin}, ... }
local UnvanOlay = olay("YeniUnvan") -- sunucu -> herkes (kim, unvan sırası)
local DuyuruOlay = olay("Duyuru") -- sunucu -> herkes ya da bir oyuncu (Türkçe yazı ya da biçim + argümanlar; ekran kendi dilinde gösterir)
local GorevAlOlay = olay("GorevAl") -- oyuncu -> sunucu (görev ödülünü al)
local NPCOlay = olay("NPC") -- sunucu -> oyuncu (NPC ile konuştu: hangi pencere açılsın)
local YarismaOlay = olay("Yarisma") -- sunucu -> oyuncu (yarışma başladı / bitti)
local GunlukAlOlay = olay("GunlukAl") -- oyuncu -> sunucu (günlük ödülü al)
local KodOlay = olay("KodKullan") -- oyuncu -> sunucu (kod yazısı)
local FavoriOlay = olay("FavoriSoruldu") -- oyuncu -> sunucu ("favorile" penceresi gösterildi)
uzaklar.Parent = ReplicatedStorage

local rastgele = Random.new()

----------------------------------------------------------------------
-- Harita ve arsalar
----------------------------------------------------------------------
local harita = workspace:FindFirstChild("Harita")
if not harita then
	-- Harita dosyası yoksa (örneğin Rojo ile sadece kod aktarıldıysa) aynısını kodla kur
	harita = HaritaUretici.Uret(Config, Gorsel)
	harita.Parent = workspace
end
local arsaKlasoru = harita:WaitForChild("Arsalar")

-- Oyun başlayınca tepeler, kasabanın dışındaki çayırlar ve göl gerçek araziye (Terrain) dönüşür:
-- rüzgârda sallanan çimenler, yumuşak tepeler, dalgalı su.
-- (Studio'da düzenlerken bloklar olarak görünürler.)
task.spawn(function()
	local arazi = workspace:FindFirstChildOfClass("Terrain")
	if not arazi then
		return
	end
	local ok, hata = pcall(function()
		local cayirlar = harita:FindFirstChild("Cayirlar")
		if cayirlar then
			for _, c in ipairs(cayirlar:GetChildren()) do
				if c:IsA("BasePart") then
					arazi:FillBlock(c.CFrame, c.Size, Enum.Material.Grass)
					c:Destroy()
				end
			end
		end
		local tepeler = harita:FindFirstChild("Tepeler")
		if tepeler then
			for _, t in ipairs(tepeler:GetChildren()) do
				if t:IsA("BasePart") then
					arazi:FillBall(t.CFrame.Position, t.Size.X / 2, Enum.Material.Grass)
					t:Destroy()
				end
			end
		end
		for _, gol in ipairs(harita:GetDescendants()) do
			if gol:IsA("BasePart") and gol.Name == "Gol" then
				local r = gol.Size.Y / 2
				arazi:FillCylinder(CFrame.new(gol.CFrame.Position.X, -0.6, gol.CFrame.Position.Z), 2, r, Enum.Material.Water)
				gol.Transparency = 1
			end
		end
	end)
	if not ok then
		warn("[Burger İmparatoru] Arazi kurulamadı: " .. tostring(hata))
	end
end)

-- Gün ve gece: gün dörtte üç sürer, gece kısa; gece neon tabelalar ve lambalar parlar
task.spawn(function()
	local baslangic = os.clock()
	while true do
		local p = ((os.clock() - baslangic) / Config.GunSuresi) % 1
		local saat
		if p < 0.75 then
			saat = 7 + p / 0.75 * 12 -- 07:00 -> 19:00
		else
			saat = 19 + (p - 0.75) / 0.25 * 12 -- 19:00 -> 07:00
		end
		Lighting.ClockTime = saat % 24
		task.wait(0.5)
	end
end)
local arsalar = {}
for i = 1, Config.ArsaSayisi do
	local m = arsaKlasoru:FindFirstChild("Arsa" .. i)
	if m then
		arsalar[i] = { Index = i, Model = m, Sahip = nil }
		Dukkan.Kur(m, nil)
	end
end

----------------------------------------------------------------------
-- Kayıt (DataStore)
----------------------------------------------------------------------
local kayitDeposu = nil
do
	local ok, sonuc = pcall(function()
		return DataStoreService:GetDataStore(Config.KayitAnahtari)
	end)
	if ok then
		kayitDeposu = sonuc
	else
		warn("[Burger İmparatoru] Kayıt sistemi açılamadı: " .. tostring(sonuc))
	end
end

-- Dünya sıralaması için ayrı, sıralı depo
local siralamaDeposu = nil
pcall(function()
	siralamaDeposu = DataStoreService:GetOrderedDataStore(Config.Siralama.Ad)
end)

local function kayitAnahtari(oyuncu)
	return "oyuncu_" .. oyuncu.UserId
end

local function kayitYukle(oyuncu)
	if not kayitDeposu then
		return nil, false
	end
	for deneme = 1, 3 do
		local ok, sonuc = pcall(function()
			return kayitDeposu:GetAsync(kayitAnahtari(oyuncu))
		end)
		if ok then
			return sonuc, true
		end
		local mesaj = tostring(sonuc)
		warn("[Burger İmparatoru] Kayıt yüklenemedi (" .. deneme .. "): " .. mesaj)
		if string.find(mesaj, "StudioAccessToApisNotAllowed") or string.find(mesaj, "publish") then
			break -- Studio'da API erişimi kapalı, tekrar denemeye gerek yok
		end
		task.wait(1.5)
	end
	return nil, false
end

local veriler = {} -- [Player] = veri

local function sayi(x, varsayilan)
	if type(x) == "number" and x == x and x ~= math.huge and x ~= -math.huge then
		return x
	end
	return varsayilan
end

local function tamSayi(x, enAz, enCok, varsayilan)
	return math.clamp(math.floor(sayi(x, varsayilan)), enAz, enCok)
end

-- Robux ürünü alımlarının numaraları (aynı alım iki kere verilmesin diye son 50 tanesi saklanır)
local function alimListesi(x)
	local liste = {}
	if type(x) == "table" then
		for _, id in ipairs(x) do
			if type(id) == "string" and #id <= 100 then
				table.insert(liste, id)
			end
		end
	end
	while #liste > 50 do
		table.remove(liste, 1)
	end
	return liste
end

-- Kullanılmış kodlar (sadece Config'te olanlar saklanır)
local function kodListesi(x)
	local liste = {}
	if type(x) == "table" then
		for _, k in ipairs(x) do
			if type(k) == "string" and Config.Kodlar[k] and not table.find(liste, k) then
				table.insert(liste, k)
			end
		end
	end
	return liste
end

local function veriHazirla(ham)
	ham = type(ham) == "table" and ham or {}
	local gelistirme = Ekonomi.BosGelistirmeler()
	if type(ham.Gelistirme) == "table" then
		for _, g in ipairs(Config.Gelistirmeler) do
			gelistirme[g.Anahtar] = tamSayi(ham.Gelistirme[g.Anahtar], 0, g.MaxSeviye, 0)
		end
	end
	local stil = nil
	if type(ham.Stil) == "table" and type(ham.Stil.Ad) == "string" then
		stil = {
			Logo = tamSayi(ham.Stil.Logo, 1, #Config.Logolar, 1),
			Renk = tamSayi(ham.Stil.Renk, 1, #Config.Renkler, 1),
			Ad = string.sub(ham.Stil.Ad, 1, 80),
		}
	end
	return {
		Para = math.max(0, math.floor(sayi(ham.Para, 0))),
		ToplamPaket = math.max(0, math.floor(sayi(ham.ToplamPaket, 0))),
		Sube = tamSayi(ham.Sube, 0, 1000, 0),
		Gelistirme = gelistirme,
		Stil = stil,
		GorevN = tamSayi(ham.GorevN, 1, 1000000, 1),
		GorevIlerleme = math.max(0, math.floor(sayi(ham.GorevIlerleme, 0))),
		YarismaRekor = tamSayi(ham.YarismaRekor, 0, 100000, 0),
		SonHediye = math.floor(sayi(ham.SonHediye, 0)),
		Alimlar = alimListesi(ham.Alimlar),
		IkiKatKalan = math.clamp(sayi(ham.IkiKatKalan, 0), 0, 86400),
		SonGunluk = math.max(0, math.floor(sayi(ham.SonGunluk, 0))),
		GunlukSeri = tamSayi(ham.GunlukSeri, 0, 100000, 0),
		Kodlar = kodListesi(ham.Kodlar),
		FavoriSoruldu = ham.FavoriSoruldu == true,
	}
end

local function kaydet(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.KayitAcik or not kayitDeposu then
		return false
	end
	local paket = {
		Surum = 3,
		Para = v.Para,
		ToplamPaket = v.ToplamPaket,
		Sube = v.Sube,
		Gelistirme = v.Gelistirme,
		Stil = v.Stil,
		GorevN = v.GorevN,
		GorevIlerleme = v.GorevIlerleme,
		YarismaRekor = v.YarismaRekor,
		SonHediye = v.SonHediye,
		Alimlar = v.Alimlar,
		IkiKatKalan = v.IkiKatBitis and math.max(0, math.floor(v.IkiKatBitis - os.clock())) or 0,
		SonGunluk = v.SonGunluk,
		GunlukSeri = v.GunlukSeri,
		Kodlar = v.Kodlar,
		FavoriSoruldu = v.FavoriSoruldu,
	}
	-- dünya sıralaması (ayrı depo; olmazsa oyun yine çalışır)
	if siralamaDeposu then
		pcall(function()
			siralamaDeposu:SetAsync(tostring(oyuncu.UserId), math.floor(v.ToplamPaket))
		end)
	end
	for deneme = 1, 3 do
		local ok, hata = pcall(function()
			kayitDeposu:SetAsync(kayitAnahtari(oyuncu), paket, { oyuncu.UserId })
		end)
		if ok then
			return true
		end
		warn("[Burger İmparatoru] Kayıt yapılamadı (" .. deneme .. "): " .. tostring(hata))
		task.wait(1)
	end
	return false
end

----------------------------------------------------------------------
-- Yardımcılar
----------------------------------------------------------------------
local function varsayilanAd(oyuncu)
	return string.sub(oyuncu.DisplayName, 1, Config.AdEnUzun - 7) .. " Burger"
end

local function stilCoz(oyuncu, v)
	local s = v.Stil or { Logo = 1, Renk = 1, Ad = varsayilanAd(oyuncu) }
	return {
		Logo = Config.Logolar[s.Logo] or Config.Logolar[1],
		Renk = (Config.Renkler[s.Renk] or Config.Renkler[1]).Renk,
		Ad = s.Ad,
	}
end

local function unvan(v)
	return Config.Unvanlar[Ekonomi.UnvanIndex(v.ToplamPaket)]
end

-- Altın burger şansı ("Altın Şans x2" geçişi varsa 2 kat)
local function altinSansi(v)
	return Ekonomi.AltinSansi(v.Gelistirme) * ((v.Gecis and v.Gecis.AltinSans) and 2 or 1)
end

local function yeniBurger(v)
	v.Burger = { Asama = 0, Altin = rastgele:NextNumber() < altinSansi(v) }
end

-- Ek çarpanlar: Altın Saat (herkes), hediye ya da Robux'la alınan 2 kat para (süreli),
-- Robux geçişleri (2 Kat Para, VIP)
local altinSaatBitis = 0
local function ekstraCarpan(v)
	local c = 1
	if (v.Arkadas or 0) > 0 then
		c *= 1 + math.min(Config.ArkadasBonusu.EnCok, Config.ArkadasBonusu.Kisi * v.Arkadas)
	end
	if v.Grupta then
		c *= 1 + Config.GrupBonusu
	end
	if v.Gecis then
		if v.Gecis.IkiKatPara then
			c *= 2
		end
		if v.Gecis.VIP then
			c *= Config.Robux.VipCarpani
		end
	end
	if os.clock() < altinSaatBitis then
		c *= Config.AltinSaat.Carpan
	end
	if v.IkiKatBitis and os.clock() < v.IkiKatBitis then
		c *= 2
	end
	return c
end

local function gorevOdulu(v)
	local gelir = Ekonomi.MakineGeliri(v.Gelistirme, v.Sube)
	local paket = Ekonomi.PaketDegeri(false, v.Gelistirme, v.Sube)
	return math.max(Config.Gorev.EnAzOdul, math.floor(gelir * Config.Gorev.OdulSaniye), paket * 12)
end

local function gorevIlerle(v, tip, miktar)
	local gTip, hedef = Ekonomi.Gorev(v.GorevN)
	if gTip == tip and v.GorevIlerleme < hedef then
		v.GorevIlerleme = math.min(hedef, v.GorevIlerleme + miktar)
	end
end

-- Oyuncunun saniyelik kazancı: makinelerden ya da elle katlayarak (hangisi büyükse).
-- Bütün para ödülleri (günlük, kod, para paketleri) buna göre, böylece hep işe yarar ama oyunu bitirmez.
local function saniyelikKazanc(v)
	return math.max(Ekonomi.MakineGeliri(v.Gelistirme, v.Sube), Ekonomi.PaketDegeri(false, v.Gelistirme, v.Sube))
end

-- Günlük ödül: alınabilir mi, kaçıncı gün, ne kadar
local function gunlukSonrakiSeri(v)
	local gecen = os.time() - v.SonGunluk
	if v.SonGunluk == 0 or gecen > Config.Gunluk.SeriBozulma then
		return 1
	end
	return v.GunlukSeri + 1
end
local function gunlukOdul(v, seri)
	local gun = math.min(seri, Config.Gunluk.EnUzunSeri)
	return math.max(Config.Gunluk.EnAz * gun, math.floor(saniyelikKazanc(v) * Config.Gunluk.Saniye * gun))
end
local function gunlukDurumu(v)
	local seri = gunlukSonrakiSeri(v)
	return {
		Kalan = math.max(0, v.SonGunluk + Config.Gunluk.Bekleme - os.time()),
		Seri = seri,
		Odul = gunlukOdul(v, seri),
	}
end

local function durumGonder(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir then
		return
	end
	local gTip, gHedef = Ekonomi.Gorev(v.GorevN)
	DurumOlay:FireClient(oyuncu, {
		Gorev = { N = v.GorevN, Tip = gTip, Hedef = gHedef, Ilerleme = v.GorevIlerleme, Odul = gorevOdulu(v) },
		YarismaRekor = v.YarismaRekor,
		HediyeKalan = math.max(0, v.SonHediye + Config.Hediye.Bekleme - os.time()),
		Ekstra = ekstraCarpan(v),
		Para = v.Para,
		ToplamPaket = v.ToplamPaket,
		Sube = v.Sube,
		UnvanIndex = Ekonomi.UnvanIndex(v.ToplamPaket),
		Asama = v.Burger.Asama,
		Altin = v.Burger.Altin,
		Gelistirme = v.Gelistirme,
		ArsaIndex = v.Arsa and v.Arsa.Index or 0,
		KayitAcik = v.KayitAcik,
		StilVar = v.Stil ~= nil,
		Stil = v.Stil or { Logo = 1, Renk = 1, Ad = varsayilanAd(oyuncu) },
		Gunluk = gunlukDurumu(v),
		Arkadas = v.Arkadas,
		Grupta = v.Grupta,
		GrupVar = Config.GrupId ~= 0,
		Kodlar = v.Kodlar,
	})
end

-- Rozetler: hak edilince bir kere verilir (Roblox'ta zaten varsa tekrar denenmez)
local function rozetVer(oyuncu, r)
	local v = veriler[oyuncu]
	if not v or r.Id == 0 or v.Rozet[r.Anahtar] then
		return
	end
	v.Rozet[r.Anahtar] = true
	task.spawn(function()
		local ok, var = pcall(function()
			return BadgeService:UserHasBadgeAsync(oyuncu.UserId, r.Id)
		end)
		if ok and not var then
			pcall(function()
				BadgeService:AwardBadge(oyuncu.UserId, r.Id)
			end)
		end
	end)
end
local function rozetKontrol(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir then
		return
	end
	for _, r in ipairs(Config.Rozetler) do
		if r.Id ~= 0 and not v.Rozet[r.Anahtar] then
			if (r.Paket == nil or v.ToplamPaket >= r.Paket) and (r.Sube == nil or v.Sube >= r.Sube) then
				rozetVer(oyuncu, r)
			end
		end
	end
end

local function skorGuncelle(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Skor then
		return
	end
	v.Skor.Paket.Value = v.ToplamPaket
	v.Skor.Para.Value = v.Para
	v.Skor.Unvan.Value = unvan(v).Ikon
	rozetKontrol(oyuncu)
end

local function kafaYazisiGuncelle(oyuncu)
	local v = veriler[oyuncu]
	local karakter = oyuncu.Character
	if not v or not karakter then
		return
	end
	local kafa = karakter:FindFirstChild("Head")
	if not kafa then
		return
	end
	local bb = kafa:FindFirstChild("UnvanYazisi")
	if not bb then
		bb = Instance.new("BillboardGui")
		bb.Name = "UnvanYazisi"
		bb.Size = UDim2.new(5, 0, 0.9, 0)
		bb.StudsOffset = Vector3.new(0, 2.2, 0)
		bb.MaxDistance = 60
		bb.LightInfluence = 0
		local yazi = Instance.new("TextLabel")
		yazi.Name = "Yazi"
		yazi.BackgroundTransparency = 1
		yazi.Size = UDim2.fromScale(1, 1)
		yazi.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
		yazi.TextScaled = true
		yazi.TextStrokeTransparency = 0.2
		yazi.Parent = bb
		bb.Parent = kafa
	end
	local vip = kafa:FindFirstChild("VipYazisi")
	if v.Gecis and v.Gecis.VIP and not vip then
		vip = bb:Clone()
		vip.Name = "VipYazisi"
		vip.StudsOffset = Vector3.new(0, 3.1, 0)
		vip.Yazi.Text = "⭐ VIP ⭐"
		vip.Yazi.TextColor3 = Color3.fromRGB(255, 215, 60)
		vip.Parent = kafa
	end
	local u = unvan(v)
	bb.Yazi.Text = "👑 " .. u.Ad .. (v.Sube > 0 and ("  🏙️" .. v.Sube) or "")
	bb.Yazi.TextColor3 = u.Renk
end

local function unvanKontrol(oyuncu, eskiIndex)
	local v = veriler[oyuncu]
	local yeniIndex = Ekonomi.UnvanIndex(v.ToplamPaket)
	if yeniIndex > eskiIndex then
		UnvanOlay:FireAllClients(oyuncu, yeniIndex)
		kafaYazisiGuncelle(oyuncu)
	end
end

local function dukkaniKur(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Arsa then
		return
	end
	Dukkan.Kur(v.Arsa.Model, {
		Stil = stilCoz(oyuncu, v),
		Gelistirme = v.Gelistirme,
		UserId = oyuncu.UserId,
		SahipAdi = oyuncu.DisplayName,
	})
end

local function evineIsinla(oyuncu)
	local v = veriler[oyuncu]
	local karakter = oyuncu.Character
	if not v or not v.Arsa or not karakter then
		return
	end
	local kok = karakter:WaitForChild("HumanoidRootPart", 5)
	if kok then
		karakter:PivotTo(Dukkan.OyuncuCFrame(v.Arsa.Model))
	end
end

----------------------------------------------------------------------
-- Katlama (KATLA butonu)
----------------------------------------------------------------------
local function katla(oyuncu, adet, otomatik)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Arsa then
		return
	end
	local b = v.Burger
	local eklenen = 0
	for _ = 1, adet do
		if b.Asama >= Config.KatlamaSayisi then
			break
		end
		b.Asama += 1
		eklenen += 1
	end
	if eklenen == 0 then
		return
	end
	local paketlendi = b.Asama >= Config.KatlamaSayisi
	local kazanc = 0
	if not otomatik then
		gorevIlerle(v, "Katla", 1)
	end
	if paketlendi then
		local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamPaket)
		kazanc = Ekonomi.PaketDegeri(b.Altin, v.Gelistirme, v.Sube, ekstraCarpan(v))
		v.Para += kazanc
		v.ToplamPaket += 1
		if not otomatik then
			gorevIlerle(v, "Paket", 1)
		end
		if b.Altin then
			gorevIlerle(v, "Altin", 1)
		end
		gorevIlerle(v, "Para", kazanc)
		yeniBurger(v)
		skorGuncelle(oyuncu)
		unvanKontrol(oyuncu, eskiUnvan)
	end
	KatlandiOlay:FireAllClients(oyuncu, v.Arsa.Index, b.Asama, paketlendi, kazanc, b.Altin, v.Burger.Altin, otomatik == true)
	durumGonder(oyuncu)
end

----------------------------------------------------------------------
-- Arsa dağıtımı
----------------------------------------------------------------------
local function bosArsaAl(oyuncu)
	for i = 1, Config.ArsaSayisi do
		local a = arsalar[i]
		if a and a.Sahip == nil then
			a.Sahip = oyuncu
			return a
		end
	end
	return nil
end

local function arsayiBirak(a)
	a.Sahip = nil
	Dukkan.Kur(a.Model, nil)
end

----------------------------------------------------------------------
-- Oyuncu girişi / çıkışı
----------------------------------------------------------------------
local function karakterGeldi(oyuncu, karakter)
	task.spawn(function()
		task.wait(0.1)
		evineIsinla(oyuncu)
	end)
	karakter:WaitForChild("Head", 5)
	kafaYazisiGuncelle(oyuncu)
end

-- Roblox grubu: gruptakiler +%10 para (Config.GrupId)
local function grupKontrolEt(oyuncu)
	if Config.GrupId == 0 then
		return
	end
	local ok, ici = pcall(function()
		return oyuncu:IsInGroup(Config.GrupId)
	end)
	local v = veriler[oyuncu]
	if ok and ici and v then
		v.Grupta = true
		durumGonder(oyuncu)
	end
end

-- Arkadaş bonusu: aynı sunucudaki her Roblox arkadaşın +%10 para (en çok +%30)
local arkadaslikOnbellek = {}
local function arkadasMi(a, b)
	local k = math.min(a.UserId, b.UserId) .. ":" .. math.max(a.UserId, b.UserId)
	if arkadaslikOnbellek[k] == nil then
		local ok, sonuc = pcall(function()
			return a:IsFriendsWith(b.UserId)
		end)
		arkadaslikOnbellek[k] = ok and sonuc == true
	end
	return arkadaslikOnbellek[k]
end
local function arkadaslariSay()
	local liste = {}
	for oyuncu in pairs(veriler) do
		table.insert(liste, oyuncu)
	end
	for _, oyuncu in ipairs(liste) do
		local n = 0
		for _, diger in ipairs(liste) do
			if diger ~= oyuncu and diger.Parent and veriler[diger] and arkadasMi(oyuncu, diger) then
				n += 1
			end
		end
		local v = veriler[oyuncu]
		if v and n ~= v.Arkadas then
			v.Arkadas = n
			oyuncu:SetAttribute("ArkadasSayisi", n)
			durumGonder(oyuncu)
		end
	end
end

-- Robux geçişleri: oyuna girerken hangilerine sahip olduğuna bak
local function gecisBul(id)
	for _, g in ipairs(Config.Robux.Gecisler) do
		if g.Id ~= 0 and g.Id == id then
			return g
		end
	end
	return nil
end
local function gecisVer(oyuncu, g)
	local v = veriler[oyuncu]
	if not v or v.Gecis[g.Anahtar] then
		return
	end
	v.Gecis[g.Anahtar] = true
	oyuncu:SetAttribute("Gecis_" .. g.Anahtar, true)
	if g.Anahtar == "AltinSans" and v.Hazir and v.Burger.Asama == 0 and not v.Burger.Altin then
		yeniBurger(v)
	end
	kafaYazisiGuncelle(oyuncu)
	durumGonder(oyuncu)
end
local function gecisleriKontrolEt(oyuncu)
	for _, g in ipairs(Config.Robux.Gecisler) do
		if g.Id ~= 0 then
			local ok, var = pcall(function()
				return MarketplaceService:UserOwnsGamePassAsync(oyuncu.UserId, g.Id)
			end)
			if ok and var and oyuncu.Parent then
				gecisVer(oyuncu, g)
			end
		end
	end
end

local function oyuncuGeldi(oyuncu)
	if veriler[oyuncu] then
		return
	end
	local v = {
		Hazir = false,
		KayitAcik = false,
		Para = 0,
		ToplamPaket = 0,
		Sube = 0,
		Gelistirme = Ekonomi.BosGelistirmeler(),
		Stil = nil,
		Burger = { Asama = 0, Altin = false },
		Jeton = 4,
		SonJeton = os.clock(),
		MakineBirikim = {},
		AsciBirikim = 0,
		GorevN = 1,
		GorevIlerleme = 0,
		YarismaRekor = 0,
		SonHediye = 0,
		Son = {},
		Alimlar = {},
		Gecis = {},
		SonGunluk = 0,
		GunlukSeri = 0,
		Kodlar = {},
		FavoriSoruldu = false,
		Rozet = {},
		Arkadas = 0,
		Grupta = false,
	}
	veriler[oyuncu] = v

	local skorlar = Instance.new("Folder")
	skorlar.Name = "leaderstats"
	local paketDeger = Instance.new("IntValue")
	paketDeger.Name = "📦"
	paketDeger.Parent = skorlar
	local paraDeger = Instance.new("IntValue")
	paraDeger.Name = "💰"
	paraDeger.Parent = skorlar
	local unvanDeger = Instance.new("StringValue")
	unvanDeger.Name = "👑"
	unvanDeger.Value = Config.Unvanlar[1].Ikon
	unvanDeger.Parent = skorlar
	skorlar.Parent = oyuncu
	v.Skor = { Paket = paketDeger, Para = paraDeger, Unvan = unvanDeger }

	v.Arsa = bosArsaAl(oyuncu)

	oyuncu.CharacterAdded:Connect(function(karakter)
		karakterGeldi(oyuncu, karakter)
	end)
	if oyuncu.Character then
		task.spawn(karakterGeldi, oyuncu, oyuncu.Character)
	end

	local ham, basarili = kayitYukle(oyuncu)
	if oyuncu.Parent == nil or veriler[oyuncu] ~= v then
		return -- yüklenirken çıktı
	end
	local hazir = veriHazirla(ham)
	v.Para = hazir.Para
	v.ToplamPaket = hazir.ToplamPaket
	v.Sube = hazir.Sube
	v.Gelistirme = hazir.Gelistirme
	v.Stil = hazir.Stil
	v.GorevN = hazir.GorevN
	v.GorevIlerleme = hazir.GorevIlerleme
	v.YarismaRekor = hazir.YarismaRekor
	v.SonHediye = hazir.SonHediye
	v.Alimlar = hazir.Alimlar
	v.SonGunluk = hazir.SonGunluk
	v.GunlukSeri = hazir.GunlukSeri
	v.Kodlar = hazir.Kodlar
	v.FavoriSoruldu = hazir.FavoriSoruldu
	oyuncu:SetAttribute("FavoriSoruldu", v.FavoriSoruldu)
	if hazir.IkiKatKalan > 0 then
		v.IkiKatBitis = os.clock() + hazir.IkiKatKalan
		oyuncu:SetAttribute("IkiKatBitis", workspace:GetServerTimeNow() + hazir.IkiKatKalan)
	end
	v.KayitAcik = basarili
	yeniBurger(v)
	v.Hazir = true

	dukkaniKur(oyuncu)
	skorGuncelle(oyuncu)
	kafaYazisiGuncelle(oyuncu)
	durumGonder(oyuncu)
	task.spawn(gecisleriKontrolEt, oyuncu)
	task.spawn(grupKontrolEt, oyuncu)
	task.spawn(rozetKontrol, oyuncu)
	task.spawn(arkadaslariSay)
	for _, r in ipairs(Config.Rozetler) do
		if r.Anahtar == "Hosgeldin" then
			rozetVer(oyuncu, r)
		end
	end
	if not v.Arsa then
		DuyuruOlay:FireClient(oyuncu, "Bütün arsalar dolu! Biri çıkınca dükkanın açılacak.")
	end
end

local function oyuncuGitti(oyuncu)
	local v = veriler[oyuncu]
	if not v then
		return
	end
	if v.Hazir then
		kaydet(oyuncu)
	end
	if v.Arsa then
		arsayiBirak(v.Arsa)
	end
	veriler[oyuncu] = nil

	-- Arsası olmayan bekleyen biri varsa ona ver
	for bekleyen, bv in pairs(veriler) do
		if bv.Arsa == nil then
			local a = bosArsaAl(bekleyen)
			if not a then
				break
			end
			bv.Arsa = a
			if bv.Hazir then
				dukkaniKur(bekleyen)
				durumGonder(bekleyen)
			end
			task.spawn(evineIsinla, bekleyen)
		end
	end
end

Players.PlayerAdded:Connect(oyuncuGeldi)
Players.PlayerRemoving:Connect(oyuncuGitti)
Players.PlayerRemoving:Connect(function()
	task.defer(arkadaslariSay)
end)
for _, oyuncu in ipairs(Players:GetPlayers()) do
	task.spawn(oyuncuGeldi, oyuncu)
end

----------------------------------------------------------------------
-- Oyuncu istekleri
----------------------------------------------------------------------
-- saniyede en fazla (1 / KatlamaBeklemesi) tıklama, küçük patlamalara izin var
local JETON_KAPASITE = 4
local function jetonHarca(v)
	local simdi = os.clock()
	v.Jeton = math.min(JETON_KAPASITE, v.Jeton + (simdi - v.SonJeton) / Config.KatlamaBeklemesi)
	v.SonJeton = simdi
	if v.Jeton < 1 then
		return false
	end
	v.Jeton -= 1
	return true
end

-- Aynı isteği çok sık yapmasın
local function cokSik(v, ad, sure)
	local simdi = os.clock()
	if v.Son[ad] and simdi - v.Son[ad] < sure then
		return true
	end
	v.Son[ad] = simdi
	return false
end

KatlaOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Arsa then
		return
	end
	if not jetonHarca(v) then
		durumGonder(oyuncu) -- ekran fazladan saydıysa düzelsin
		return
	end
	local y = v.Yarisma
	if y and os.clock() >= y.Baslangic and os.clock() < y.Bitis then
		y.Sayac += 1
	end
	katla(oyuncu, Ekonomi.TikBasinaKatlama(v.Gelistirme))
end)

DurumIsteOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if v and not cokSik(v, "Durum", 0.5) then
		durumGonder(oyuncu)
	end
end)

EvimeGitOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if v and not cokSik(v, "Ev", 1) then
		evineIsinla(oyuncu)
	end
end)

local BINA_PARCASI = {
	Makine = true, Duvar = true, Cati = true, Oturma = true, Dekor = true, DevTabela = true,
	Asci = true, Kasiyer = true, Teknisyen = true, Temizlikci = true,
}

-- Bir geliştirmeyi satın al (dükkan menüsü de yerdeki butonlar da bunu kullanır)
local function satinAl(oyuncu, anahtar)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or type(anahtar) ~= "string" then
		return
	end
	local g = Ekonomi.GelistirmeBul(anahtar)
	if not g or not Ekonomi.KilidiAcik(v.Gelistirme, anahtar) then
		return
	end
	local seviye = v.Gelistirme[anahtar] or 0
	local fiyat = Ekonomi.Fiyat(anahtar, seviye)
	if not fiyat or v.Para < fiyat then
		durumGonder(oyuncu)
		return
	end
	v.Para -= fiyat
	v.Gelistirme[anahtar] = seviye + 1
	if BINA_PARCASI[anahtar] then
		dukkaniKur(oyuncu) -- yerdeki butonlar da yeniden kurulur
	elseif v.Arsa then
		Dukkan.ButonlariKur(v.Arsa.Model, { Gelistirme = v.Gelistirme, UserId = oyuncu.UserId })
	end
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
end

SatinAlOlay.OnServerEvent:Connect(satinAl)

-- Günlük ödül
GunlukAlOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or cokSik(v, "Gunluk", 1) then
		return
	end
	if v.SonGunluk ~= 0 and os.time() < v.SonGunluk + Config.Gunluk.Bekleme then
		durumGonder(oyuncu)
		return
	end
	local seri = gunlukSonrakiSeri(v)
	local odul = gunlukOdul(v, seri)
	v.GunlukSeri = seri
	v.SonGunluk = os.time()
	v.Para += odul
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
	DuyuruOlay:FireClient(oyuncu, "📅 %d. gün ödülü: +%s para!", seri, Ekonomi.Kisalt(odul))
end)

-- Kodlar (videolarda paylaşılır): her oyuncu her kodu bir kere kullanır
KodOlay.OnServerEvent:Connect(function(oyuncu, metin)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or type(metin) ~= "string" or cokSik(v, "Kod", 1) then
		return
	end
	local kod = string.upper((string.gsub(string.sub(metin, 1, 30), "%s", "")))
	local k = Config.Kodlar[kod]
	if type(k) ~= "table" then
		DuyuruOlay:FireClient(oyuncu, "🎟️ Böyle bir kod yok.")
		return
	end
	if table.find(v.Kodlar, kod) then
		DuyuruOlay:FireClient(oyuncu, "🎟️ Bu kodu zaten kullandın.")
		return
	end
	local para = math.max(k.EnAz or 0, math.floor(saniyelikKazanc(v) * (k.Saniye or 0)))
	table.insert(v.Kodlar, kod)
	v.Para += para
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
	DuyuruOlay:FireClient(oyuncu, "🎟️ %s kodu: +%s para!", kod, Ekonomi.Kisalt(para))
end)

-- "Oyunu favorile" penceresi bir kere gösterilir
FavoriOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if v and v.Hazir and not v.FavoriSoruldu then
		v.FavoriSoruldu = true
		oyuncu:SetAttribute("FavoriSoruldu", true)
	end
end)

-- Yerdeki satın alma butonları: dükkan sahibi üstüne basınca alır. Üstünde durdukça tekrar almaz;
-- çok seviyeli geliştirmeyi bir daha almak için butondan inip yeniden basmak gerekir.
local basili = {} -- oyuncu -> { Anahtar, Konum }
local function butonaBasildi(buton, temas)
	local anahtar = buton:GetAttribute("Gelistirme")
	local karakter = temas and temas.Parent
	local oyuncu = karakter and Players:GetPlayerFromCharacter(karakter)
	if not oyuncu or type(anahtar) ~= "string" or buton:GetAttribute("Sahip") ~= oyuncu.UserId then
		return
	end
	local b = basili[oyuncu]
	if b and b.Anahtar == anahtar then
		return
	end
	basili[oyuncu] = { Anahtar = anahtar, Konum = buton.CFrame.Position }
	satinAl(oyuncu, anahtar)
end
arsaKlasoru.DescendantAdded:Connect(function(d)
	if d:IsA("BasePart") and d.Name == "SatinAlButonu" then
		d.Touched:Connect(function(temas)
			butonaBasildi(d, temas)
		end)
	end
end)
task.spawn(function()
	while true do
		task.wait(0.25)
		for oyuncu, b in pairs(basili) do
			local karakter = oyuncu.Character
			local kok = karakter and karakter:FindFirstChild("HumanoidRootPart")
			local fark = kok and (kok.CFrame.Position - b.Konum) or nil
			if oyuncu.Parent == nil or not fark or Vector3.new(fark.X, 0, fark.Z).Magnitude > 3.5 then
				basili[oyuncu] = nil
			end
		end
	end
end)

SubeAcOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or cokSik(v, "Sube", 2) then
		return
	end
	local fiyat = Ekonomi.SubeFiyati(v.Sube)
	if v.Para < fiyat then
		durumGonder(oyuncu)
		return
	end
	v.Sube += 1
	v.Para = 0
	v.Gelistirme = Ekonomi.BosGelistirmeler()
	v.MakineBirikim = {}
	v.AsciBirikim = 0
	yeniBurger(v)
	dukkaniKur(oyuncu)
	skorGuncelle(oyuncu)
	kafaYazisiGuncelle(oyuncu)
	durumGonder(oyuncu)
	DuyuruOlay:FireAllClients("🏙️ %s %d. şubesini açtı!", oyuncu.DisplayName, v.Sube + 1)
	task.spawn(kaydet, oyuncu)
end)

local function adiFiltrele(oyuncu, ad)
	local ok, sonuc = pcall(function()
		local filtre = TextService:FilterStringAsync(ad, oyuncu.UserId)
		return filtre:GetNonChatStringForBroadcastAsync()
	end)
	if ok and type(sonuc) == "string" then
		return sonuc
	end
	return nil
end

StilSecOlay.OnServerEvent:Connect(function(oyuncu, istek)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or type(istek) ~= "table" or cokSik(v, "Stil", 1.5) then
		return
	end
	local logo, renk, ad = istek.Logo, istek.Renk, istek.Ad
	if type(logo) ~= "number" or type(renk) ~= "number" or type(ad) ~= "string" then
		return
	end
	logo = math.floor(logo)
	renk = math.floor(renk)
	if logo < 1 or logo > #Config.Logolar or renk < 1 or renk > #Config.Renkler then
		return
	end
	-- kontrol karakterlerini ve baştaki/sondaki boşlukları temizle
	ad = string.gsub(ad, "%c", "")
	ad = string.match(ad, "^%s*(.-)%s*$") or ""
	local uzunluk = utf8.len(ad)
	if uzunluk == nil or uzunluk > Config.AdEnUzun then
		DuyuruOlay:FireClient(oyuncu, "Dükkan adı en fazla %d harf olabilir.", Config.AdEnUzun)
		return
	end
	if uzunluk == 0 then
		ad = varsayilanAd(oyuncu)
	end
	local temiz = adiFiltrele(oyuncu, ad)
	if not temiz then
		temiz = varsayilanAd(oyuncu)
		DuyuruOlay:FireClient(oyuncu, 'İsim kontrol edilemedi, şimdilik "%s" yazdık.', temiz)
	elseif temiz ~= ad then
		DuyuruOlay:FireClient(oyuncu, "Bu isim Roblox kurallarına uymuyor, bazı harfler gizlendi.")
	end
	v.Stil = { Logo = logo, Renk = renk, Ad = temiz }
	dukkaniKur(oyuncu)
	durumGonder(oyuncu)
	task.spawn(kaydet, oyuncu)
end)

----------------------------------------------------------------------
-- Paketleme makineleri (otomasyon)
----------------------------------------------------------------------
local URETIM_ARALIGI = 0.25
local EN_FAZLA_GORUNTU = 80 -- bir turda ekranlara en fazla kaç kutu gönderilsin

task.spawn(function()
	local son = os.clock()
	while true do
		task.wait(URETIM_ARALIGI)
		local simdi = os.clock()
		local dt = math.min(simdi - son, 2)
		son = simdi
		local liste = {}
		for oyuncu, v in pairs(veriler) do
			-- Aşçı: tezgahta oyuncunun yerine katlar
			local asci = Ekonomi.AsciAraligi(v.Gelistirme)
			if v.Hazir and v.Arsa and asci then
				v.AsciBirikim += dt
				local tur = 0
				while v.AsciBirikim >= asci and tur < 4 do
					v.AsciBirikim -= asci
					tur += 1
					katla(oyuncu, 1, true)
				end
				v.AsciBirikim = math.min(v.AsciBirikim, asci) -- takılma olursa birikmesin
			end
			local adet = math.min(v.Gelistirme.Makine or 0, #Config.Yerlesim.MakineX)
			if v.Hazir and v.Arsa and adet > 0 then
				local sure = Ekonomi.MakineSuresi(v.Gelistirme)
				local sans = altinSansi(v)
				local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamPaket)
				local uretti = false
				local ekstra = ekstraCarpan(v)
				for m = 1, adet do
					-- makineler aynı anda değil, sırayla çalışsın
					local birikim = (v.MakineBirikim[m] or (m - 1) / adet * sure) + dt
					while birikim >= sure do
						birikim -= sure
						local altin = rastgele:NextNumber() < sans
						local deger = Ekonomi.PaketDegeri(altin, v.Gelistirme, v.Sube, ekstra)
						v.Para += deger
						v.ToplamPaket += 1
						gorevIlerle(v, "Para", deger)
						if altin then
							gorevIlerle(v, "Altin", 1)
						end
						uretti = true
						if #liste < EN_FAZLA_GORUNTU then
							table.insert(liste, { v.Arsa.Index, m, altin })
						end
					end
					v.MakineBirikim[m] = birikim
				end
				if uretti then
					skorGuncelle(oyuncu)
					unvanKontrol(oyuncu, eskiUnvan)
					durumGonder(oyuncu)
				end
			end
		end
		if #liste > 0 then
			UretimOlay:FireAllClients(liste)
		end
	end
end)

----------------------------------------------------------------------
-- Meydandaki NPC'ler: görev ustası, huss yarışması hakemi, hediyeci
----------------------------------------------------------------------
local npcKlasoru = Instance.new("Folder")
npcKlasoru.Name = "NPCler"
npcKlasoru.Parent = harita

local function npcYap(anahtar, bilgi, eylem, tetik)
	local konum = bilgi.Yer
	local cf = CFrame.lookAt(konum, Vector3.new(0, konum.Y, 0)) -- meydanın ortasına bakar
	local insan = Gorsel.Insan(cf, {
		Gomlek = bilgi.Gomlek,
		Pantolon = Color3.fromRGB(50, 50, 60),
		Ten = Gorsel.Tenler[2],
		Yuz = bilgi.Yuz,
		Sapka = anahtar == "Gorev" and "sef" or "kep",
		SapkaRenk = Color3.fromRGB(250, 205, 50),
	})
	local m = Instance.new("Model")
	m.Name = "NPC_" .. anahtar
	for _, p in ipairs(insan.Liste) do
		p.CanCollide = true
		p.Parent = m
	end
	m.PrimaryPart = insan.Govde
	m:SetAttribute("Rol", "NPC")
	local bb = Instance.new("BillboardGui")
	bb.Size = UDim2.new(9, 0, 2.2, 0)
	bb.StudsOffset = Vector3.new(0, 2.9, 0)
	bb.MaxDistance = 90
	bb.LightInfluence = 0
	bb.Parent = insan.Bas
	local ad = Instance.new("TextLabel")
	ad.BackgroundTransparency = 1
	ad.Size = UDim2.fromScale(1, 0.5)
	ad.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
	ad.TextScaled = true
	ad.TextColor3 = Color3.new(1, 1, 1)
	ad.TextStrokeTransparency = 0.2
	ad.Text = bilgi.Ad
	ad.Parent = bb
	local unvanYazi = ad:Clone()
	unvanYazi.Position = UDim2.fromScale(0, 0.5)
	unvanYazi.TextColor3 = Color3.fromRGB(255, 220, 80)
	unvanYazi.Text = bilgi.Unvan
	unvanYazi.Parent = bb
	local pr = Instance.new("ProximityPrompt")
	pr.ActionText = eylem
	pr.ObjectText = bilgi.Ad
	pr.KeyboardKeyCode = Enum.KeyCode.F
	pr.GamepadKeyCode = Enum.KeyCode.ButtonX
	pr.HoldDuration = 0
	pr.MaxActivationDistance = 12
	pr.RequiresLineOfSight = false
	pr.Parent = insan.Govde
	pr.Triggered:Connect(function(oyuncu)
		tetik(oyuncu)
	end)
	m.Parent = npcKlasoru
	return m, cf
end

-- Görev ustası: görev penceresini açar
npcYap("Gorev", Config.NPCler.Gorev, "Konuş", function(oyuncu)
	local v = veriler[oyuncu]
	if v and v.Hazir then
		durumGonder(oyuncu)
		NPCOlay:FireClient(oyuncu, "Gorev")
	end
end)

GorevAlOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or cokSik(v, "Gorev", 0.5) then
		return
	end
	local _, hedef = Ekonomi.Gorev(v.GorevN)
	if v.GorevIlerleme < hedef then
		return
	end
	local odul = gorevOdulu(v)
	v.Para += odul
	v.GorevN += 1
	v.GorevIlerleme = 0
	DuyuruOlay:FireClient(oyuncu, "📜 Görev tamam! +%s para", Ekonomi.Kisalt(odul))
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
end)

-- Huss Yarışması: 3-2-1, sonra 15 saniye boyunca KATLA'ya basabildiğin kadar bas
local sunucuRekoru = 0
local function yarismaBaslat(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Arsa or v.Yarisma then
		return
	end
	local kalan = (v.YarismaSon or -1e9) + Config.Yarisma.Bekleme - os.clock()
	if kalan > 0 then
		DuyuruOlay:FireClient(oyuncu, "Hakem Hülya: Biraz dinlen! %d saniye sonra tekrar gel.", math.ceil(kalan))
		return
	end
	local simdi = os.clock()
	v.Yarisma = { Baslangic = simdi + 3, Bitis = simdi + 3 + Config.Yarisma.Sure, Sayac = 0 }
	YarismaOlay:FireClient(oyuncu, { Durum = "basladi", GeriSayim = 3, Sure = Config.Yarisma.Sure, Rekor = v.YarismaRekor })
end
npcYap("Yarisma", Config.NPCler.Yarisma, "Yarış!", yarismaBaslat)

task.spawn(function()
	while true do
		task.wait(0.1)
		local simdi = os.clock()
		for oyuncu, v in pairs(veriler) do
			local y = v.Yarisma
			if y and simdi >= y.Bitis then
				v.Yarisma = nil
				v.YarismaSon = simdi
				local paket = Ekonomi.PaketDegeri(false, v.Gelistirme, v.Sube, ekstraCarpan(v))
				local odul = math.floor(y.Sayac * paket * Config.Yarisma.TikOdulu)
				v.Para += odul
				local yeniRekor = y.Sayac > v.YarismaRekor
				if yeniRekor then
					v.YarismaRekor = y.Sayac
				end
				YarismaOlay:FireClient(oyuncu, { Durum = "bitti", Sayac = y.Sayac, Odul = odul, Rekor = v.YarismaRekor, YeniRekor = yeniRekor })
				if y.Sayac > sunucuRekoru and y.Sayac > 0 then
					sunucuRekoru = y.Sayac
					DuyuruOlay:FireAllClients("🏆 %s Huss Yarışmasında %d tık! Kasaba rekoru!", oyuncu.DisplayName, y.Sayac)
				end
				skorGuncelle(oyuncu)
				durumGonder(oyuncu)
			end
		end
	end
end)

-- Hediyeci: her 10 dakikada bir bedava hediye
local function hediyeVer(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or cokSik(v, "Hediye", 1) then
		return
	end
	local kalan = v.SonHediye + Config.Hediye.Bekleme - os.time()
	if kalan > 0 then
		DuyuruOlay:FireClient(oyuncu, "Hediyeci Hasan: Sıradaki hediye %d:%02d sonra! 🎁", kalan // 60, kalan % 60)
		return
	end
	v.SonHediye = os.time()
	if rastgele:NextNumber() < 0.3 then
		v.IkiKatBitis = os.clock() + Config.Hediye.IkiKatSuresi
		oyuncu:SetAttribute("IkiKatBitis", workspace:GetServerTimeNow() + Config.Hediye.IkiKatSuresi)
		DuyuruOlay:FireClient(oyuncu, "🎁 Hediye: %d saniye boyunca 2 KAT PARA! 🔥", Config.Hediye.IkiKatSuresi)
	else
		local para = math.max(
			Config.Hediye.EnAzPara,
			math.floor(Ekonomi.MakineGeliri(v.Gelistirme, v.Sube) * Config.Hediye.ParaSaniye),
			Ekonomi.PaketDegeri(false, v.Gelistirme, v.Sube) * 20
		)
		para = math.floor(para * rastgele:NextNumber(1, 2))
		v.Para += para
		DuyuruOlay:FireClient(oyuncu, "🎁 Hediye: +%s para!", Ekonomi.Kisalt(para))
	end
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
end
local hediyeNpc, hediyeCf = npcYap("Hediye", Config.NPCler.Hediye, "Hediyeni Al", hediyeVer)
do
	-- NPC'nin yanında kocaman bir hediye kutusu
	local kutuCf = hediyeCf * CFrame.new(3.6, 1.8, 0)
	local kutu = Gorsel.Parca({ Name = "HediyeKutusu", Size = Vector3.new(3.4, 3.4, 3.4), CFrame = kutuCf, Color = Color3.fromRGB(220, 50, 70), CanCollide = true })
	kutu.Parent = hediyeNpc
	for _, boy in ipairs({ Vector3.new(3.5, 3.5, 0.6), Vector3.new(0.6, 3.5, 3.5) }) do
		local serit = Gorsel.Parca({ Name = "Kurdele", Size = boy, CFrame = kutuCf, Color = Color3.fromRGB(255, 215, 60) })
		serit.Parent = hediyeNpc
	end
	local fiyonk = Gorsel.Parca({ Name = "Fiyonk", Shape = Enum.PartType.Ball, Size = Vector3.new(1.4, 1.4, 1.4), CFrame = kutuCf * CFrame.new(0, 2.1, 0), Color = Color3.fromRGB(255, 215, 60) })
	fiyonk.Parent = hediyeNpc
end

-- Altın Saat: arada bir (ya da biri Robux'la herkese hediye edince) bütün kasabada paketler 2 kat para eder
local function herkeseDurum()
	for oyuncu in pairs(veriler) do
		task.spawn(durumGonder, oyuncu)
	end
end
local function altinSaatBaslat(hediyeEden)
	local simdi = os.clock()
	altinSaatBitis = math.max(simdi, altinSaatBitis) + Config.AltinSaat.Sure -- açıksa uzar
	local kalan = altinSaatBitis - simdi
	ReplicatedStorage:SetAttribute("AltinSaatBitis", workspace:GetServerTimeNow() + kalan)
	if hediyeEden then
		DuyuruOlay:FireAllClients("⏰ %s herkese ALTIN SAAT hediye etti! %d saniye bütün paketler 2 kat! 🔥", hediyeEden, math.floor(kalan))
	else
		DuyuruOlay:FireAllClients("⏰ ALTIN SAAT! %d saniye boyunca bütün paketler 2 kat para! 🔥", math.floor(kalan))
	end
	herkeseDurum()
	task.delay(kalan + 0.2, herkeseDurum)
end
task.spawn(function()
	while true do
		task.wait(rastgele:NextNumber(Config.AltinSaat.EnAz, Config.AltinSaat.EnCok))
		altinSaatBaslat(nil)
		task.wait(Config.AltinSaat.Sure)
	end
end)

----------------------------------------------------------------------
-- Robux mağazası: geçişler (kalıcı) ve ürünler (tekrar alınabilir)
----------------------------------------------------------------------
local function urunBul(id)
	for _, u in ipairs(Config.Robux.Urunler) do
		if u.Id ~= 0 and u.Id == id then
			return u
		end
	end
	return nil
end

-- Para paketi oyuncunun o anki kazancına göre: saniyede makinelerden ya da elle katlayarak
-- kazandığının (hangisi büyükse) birkaç dakikalığı. Böylece hiçbir zaman oyunu bitirmez.
local function paraPaketi(v, u)
	local saniyelik = math.max(Ekonomi.MakineGeliri(v.Gelistirme, v.Sube), Ekonomi.PaketDegeri(false, v.Gelistirme, v.Sube))
	return math.max(u.EnAz or 0, math.floor(saniyelik * (u.Saniye or 0)))
end

local function urunVer(oyuncu, v, u)
	if u.Saniye then
		local para = paraPaketi(v, u)
		v.Para += para
		skorGuncelle(oyuncu)
		DuyuruOlay:FireClient(oyuncu, "💵 +%s para!", Ekonomi.Kisalt(para))
	elseif u.Sure then
		local simdi = os.clock()
		v.IkiKatBitis = math.max(simdi, v.IkiKatBitis or 0) + u.Sure -- açıksa uzar
		oyuncu:SetAttribute("IkiKatBitis", workspace:GetServerTimeNow() + (v.IkiKatBitis - simdi))
		DuyuruOlay:FireClient(oyuncu, "🔥 %d dakika 2 KAT PARA!", math.floor(u.Sure / 60))
	elseif u.Anahtar == "AltinSaat" then
		altinSaatBaslat(oyuncu.DisplayName)
	end
	durumGonder(oyuncu)
end

-- Roblox her ürün alımını buraya sorar. Aynı alım (PurchaseId) asla iki kere verilmez; kayıt
-- yapılamazsa "sonra tekrar sor" denir, alım kaybolmaz.
MarketplaceService.ProcessReceipt = function(bilgi)
	local oyuncu = Players:GetPlayerByUserId(bilgi.PlayerId)
	local v = oyuncu and veriler[oyuncu]
	if not v or not v.Hazir then
		return Enum.ProductPurchaseDecision.NotProcessedYet
	end
	local u = urunBul(bilgi.ProductId)
	if not u then
		warn("[Burger İmparatoru] Bilinmeyen Robux ürünü: " .. tostring(bilgi.ProductId))
		return Enum.ProductPurchaseDecision.NotProcessedYet
	end
	local alimId = tostring(bilgi.PurchaseId)
	if table.find(v.Alimlar, alimId) then
		return Enum.ProductPurchaseDecision.PurchaseGranted -- bu alım zaten verildi
	end
	urunVer(oyuncu, v, u)
	table.insert(v.Alimlar, alimId)
	while #v.Alimlar > 50 do
		table.remove(v.Alimlar, 1)
	end
	if v.KayitAcik and not kaydet(oyuncu) then
		return Enum.ProductPurchaseDecision.NotProcessedYet
	end
	DuyuruOlay:FireClient(oyuncu, "🎉 %s alındı! Teşekkürler!", u.Ad)
	return Enum.ProductPurchaseDecision.PurchaseGranted
end

MarketplaceService.PromptGamePassPurchaseFinished:Connect(function(oyuncu, id, alindi)
	local g = alindi and gecisBul(id)
	if g and veriler[oyuncu] then
		gecisVer(oyuncu, g)
		DuyuruOlay:FireClient(oyuncu, "🎉 %s alındı! Teşekkürler!", g.Ad)
	end
end)

----------------------------------------------------------------------
-- Meydanda dünya sıralaması tahtası (en çok paket yapanlar)
----------------------------------------------------------------------
local function siralamaTahtasiKur()
	local yer = Config.Siralama.Yer
	local cf = CFrame.lookAt(yer, Vector3.new(0, yer.Y, 0)) -- meydanın ortasına bakar
	local m = Instance.new("Model")
	m.Name = "SiralamaTahtasi"
	for _, x in ipairs({ -6.5, 6.5 }) do
		local direk = Gorsel.Parca({ Name = "Direk", Size = Vector3.new(0.8, 13, 0.8), CFrame = cf * CFrame.new(x, 6.5, 0.4), Color = Color3.fromRGB(70, 45, 30), Material = Enum.Material.Wood, CanCollide = true })
		direk.Parent = m
	end
	local pano = Gorsel.Parca({ Name = "Pano", Size = Vector3.new(14, 10, 0.6), CFrame = cf * CFrame.new(0, 7.5, 0), Color = Color3.fromRGB(40, 28, 22), Material = Enum.Material.WoodPlanks, CanCollide = true })
	pano.Parent = m
	local cerceve = Gorsel.Parca({ Name = "Cerceve", Size = Vector3.new(14.8, 10.8, 0.4), CFrame = cf * CFrame.new(0, 7.5, 0.2), Color = Color3.fromRGB(250, 205, 50), Material = Enum.Material.SmoothPlastic, CanCollide = true })
	cerceve.Parent = m
	local sg = Instance.new("SurfaceGui")
	sg.Face = Enum.NormalId.Front
	sg.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	sg.PixelsPerStud = 40
	sg.LightInfluence = 0.2
	sg.Parent = pano
	local function satir(ad, metin, y, boy, renk)
		local t = Instance.new("TextLabel")
		t.Name = ad
		t.BackgroundTransparency = 1
		t.Position = UDim2.fromScale(0.05, y)
		t.Size = UDim2.fromScale(0.9, boy)
		t.TextScaled = true
		t.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
		t.TextColor3 = renk
		t.TextXAlignment = Enum.TextXAlignment.Left
		t.Text = metin
		t.Parent = sg
		return t
	end
	local baslik = satir("Baslik", "🏆 DÜNYA SIRALAMASI", 0.03, 0.12, Color3.fromRGB(255, 215, 70))
	baslik.TextXAlignment = Enum.TextXAlignment.Center
	local alt = satir("Alt", "En çok paket yapanlar", 0.15, 0.06, Color3.fromRGB(230, 210, 190))
	alt.TextXAlignment = Enum.TextXAlignment.Center
	local satirlar = {}
	for i = 1, Config.Siralama.Kisi do
		satirlar[i] = satir("Sira" .. i, "", 0.23 + (i - 1) * 0.075, 0.068, i == 1 and Color3.fromRGB(255, 225, 90) or Color3.new(1, 1, 1))
	end
	m.Parent = harita
	return satirlar
end

task.spawn(function()
	local satirlar = siralamaTahtasiKur()
	local isimler = {}
	while true do
		local liste = nil
		if siralamaDeposu then
			local ok, sayfa = pcall(function()
				return siralamaDeposu:GetSortedAsync(false, Config.Siralama.Kisi)
			end)
			if ok and sayfa then
				local ok2, veri = pcall(function()
					return sayfa:GetCurrentPage()
				end)
				if ok2 and type(veri) == "table" and #veri > 0 then
					liste = veri
				end
			end
		end
		if not liste then
			-- Studio'da ya da depo boşsa: sunucudaki oyuncular
			liste = {}
			for oyuncu, v in pairs(veriler) do
				table.insert(liste, { key = tostring(oyuncu.UserId), value = v.ToplamPaket })
			end
			table.sort(liste, function(a, b)
				return a.value > b.value
			end)
		end
		for i, t in ipairs(satirlar) do
			local e = liste[i]
			local metin = ""
			if e then
				local id = tonumber(e.key)
				local ad = id and isimler[id]
				if id and not ad then
					local p = Players:GetPlayerByUserId(id)
					ad = p and p.Name
					if not ad then
						local ok, n = pcall(function()
							return Players:GetNameFromUserIdAsync(id)
						end)
						ad = ok and n or nil
					end
					isimler[id] = ad
				end
				metin = string.format("%d. %s  •  📦 %s", i, ad or "?", Ekonomi.Kisalt(tonumber(e.value) or 0))
			end
			t.Text = metin
		end
		task.wait(Config.Siralama.Guncelleme)
	end
end)

----------------------------------------------------------------------
-- Otomatik kayıt
----------------------------------------------------------------------
task.spawn(function()
	while true do
		task.wait(60)
		for oyuncu in pairs(veriler) do
			task.spawn(kaydet, oyuncu)
		end
	end
end)

game:BindToClose(function()
	local bekleyen = 0
	for oyuncu, v in pairs(veriler) do
		if v.Hazir and v.KayitAcik then
			bekleyen += 1
			task.spawn(function()
				kaydet(oyuncu)
				bekleyen -= 1
			end)
		end
	end
	local baslangic = os.clock()
	while bekleyen > 0 and os.clock() - baslangic < 25 do
		task.wait(0.1)
	end
end)

print("[Burger İmparatoru] Sunucu hazır! Arsa sayısı: " .. #arsalar)
