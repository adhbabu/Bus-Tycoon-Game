-- BURGER İMPARATORU - Sunucu
-- Katlama, servis, dükkan, unvanlar ve kayıt burada. Oyuncunun ekranı (client) sadece
-- "katla", "servis et", "satın al" ister; her şeyi sunucu kontrol eder, böylece hile yapılamaz.

local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local DataStoreService = game:GetService("DataStoreService")
local RunService = game:GetService("RunService")
local TweenService = game:GetService("TweenService")

local Ortak = ReplicatedStorage:WaitForChild("Ortak")
local Config = require(Ortak:WaitForChild("Config"))
local Ekonomi = require(Ortak:WaitForChild("Ekonomi"))
local Dunya = require(script.Parent:WaitForChild("Dunya"))

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
local ServisOlay = olay("ServisEt") -- oyuncu -> sunucu
local SatinAlOlay = olay("SatinAl") -- oyuncu -> sunucu (anahtar)
local DurumOlay = olay("Durum") -- sunucu -> oyuncu (durum tablosu)
local KatlandiOlay = olay("Katlandi") -- sunucu -> herkes (kim, tezgah, kaç kat, altın sayısı, otomatik mi)
local ServisEdildiOlay = olay("ServisEdildi") -- sunucu -> herkes (kim, tezgah, para, kat)
local UnvanOlay = olay("YeniUnvan") -- sunucu -> herkes (kim, unvan sırası)
local DurumIsteOlay = olay("DurumIste") -- oyuncu -> sunucu (ekran açılınca durumu ister)
uzaklar.Parent = ReplicatedStorage

----------------------------------------------------------------------
-- Dünya
----------------------------------------------------------------------
local tezgahlar, servisKlasoru = Dunya.Kur()
local rastgele = Random.new()

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
		-- Studio'da API erişimi kapalıysa tekrar denemenin anlamı yok
		if string.find(mesaj, "StudioAccessToApisNotAllowed") or string.find(mesaj, "publish") then
			break
		end
		task.wait(1.5)
	end
	return nil, false
end

----------------------------------------------------------------------
-- Oyuncu verileri
----------------------------------------------------------------------
local veriler = {} -- [Player] = veri tablosu

local function sayiYap(x, varsayilan)
	if type(x) == "number" and x == x and x ~= math.huge and x ~= -math.huge then
		return x
	end
	return varsayilan
end

local function veriHazirla(ham)
	local gelistirme = Ekonomi.BosGelistirmeler()
	if type(ham) == "table" and type(ham.Gelistirme) == "table" then
		for _, g in ipairs(Config.Gelistirmeler) do
			local sv = math.floor(sayiYap(ham.Gelistirme[g.Anahtar], 0))
			gelistirme[g.Anahtar] = math.clamp(sv, 0, g.MaxSeviye)
		end
	end
	ham = type(ham) == "table" and ham or {}
	return {
		Para = math.max(0, math.floor(sayiYap(ham.Para, 0))),
		ToplamKat = math.max(0, math.floor(sayiYap(ham.ToplamKat, 0))),
		Rekor = math.max(0, math.floor(sayiYap(ham.Rekor, 0))),
		Gelistirme = gelistirme,
	}
end

local function kaydet(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.KayitAcik or not kayitDeposu then
		return
	end
	local paket = {
		Surum = 1,
		Para = v.Para,
		ToplamKat = v.ToplamKat,
		Rekor = v.Rekor,
		Gelistirme = v.Gelistirme,
	}
	for deneme = 1, 3 do
		local ok, hata = pcall(function()
			kayitDeposu:SetAsync(kayitAnahtari(oyuncu), paket, { oyuncu.UserId })
		end)
		if ok then
			return
		end
		warn("[Burger İmparatoru] Kayıt yapılamadı (" .. deneme .. "): " .. tostring(hata))
		task.wait(1)
	end
end

----------------------------------------------------------------------
-- Ekran güncellemeleri
----------------------------------------------------------------------
local function unvan(v)
	return Config.Unvanlar[Ekonomi.UnvanIndex(v.ToplamKat)]
end

local function durumGonder(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir then
		return
	end
	DurumOlay:FireClient(oyuncu, {
		Para = v.Para,
		ToplamKat = v.ToplamKat,
		Rekor = v.Rekor,
		UnvanIndex = Ekonomi.UnvanIndex(v.ToplamKat),
		BurgerKat = v.Burger.Kat,
		BurgerDeger = v.Burger.Deger,
		MaxKat = Ekonomi.MaxKat(v.Gelistirme),
		Gelistirme = v.Gelistirme,
		TezgahVar = v.Tezgah ~= nil,
		KayitAcik = v.KayitAcik,
	})
end

local function tabelaGuncelle(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Tezgah then
		return
	end
	local t = v.Tezgah
	t.TabelaBaslik.Text = "🍔 " .. oyuncu.DisplayName
	t.TabelaAlt.Text = string.format("%d kat • %s", v.Burger.Kat, unvan(v).Ad)
end

local function skorGuncelle(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Skor then
		return
	end
	v.Skor.Kat.Value = v.ToplamKat
	v.Skor.Para.Value = v.Para
	v.Skor.Unvan.Value = unvan(v).Ad
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
		bb.Size = UDim2.new(6, 0, 1.1, 0)
		bb.StudsOffset = Vector3.new(0, 2.4, 0)
		bb.MaxDistance = 70
		bb.LightInfluence = 0
		local yazi = Instance.new("TextLabel")
		yazi.Name = "Yazi"
		yazi.BackgroundTransparency = 1
		yazi.Size = UDim2.fromScale(1, 1)
		yazi.Font = Enum.Font.FredokaOne
		yazi.TextScaled = true
		yazi.TextStrokeTransparency = 0.2
		yazi.Parent = bb
		bb.Parent = kafa
	end
	local u = unvan(v)
	bb.Yazi.Text = "👑 " .. u.Ad
	bb.Yazi.TextColor3 = u.Renk
end

----------------------------------------------------------------------
-- Burger
----------------------------------------------------------------------
local DUSME_BILGISI = TweenInfo.new(0.22, Enum.EasingStyle.Quad, Enum.EasingDirection.Out)

local function katiYerlestir(v, tur, animasyon)
	local t = v.Tezgah
	local alt = t.TabanCFrame * CFrame.new(0, v.Burger.Yukseklik, 0)
	local parcalar, k = Dunya.KatYap(tur, alt, 1, rastgele)
	for _, p in ipairs(parcalar) do
		if animasyon then
			local hedef = p.CFrame
			p.CFrame = hedef + Vector3.new(0, 3.5, 0)
			p.Parent = t.Katlar
			TweenService:Create(p, DUSME_BILGISI, { CFrame = hedef }):Play()
		else
			p.Parent = t.Katlar
		end
	end
	v.Burger.Yukseklik += k
end

local function yeniBurger(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Tezgah then
		return
	end
	v.Tezgah.Katlar:ClearAllChildren()
	v.Burger = { Kat = 0, Deger = 0, Yukseklik = 0 }
	katiYerlestir(v, Config.AltEkmek, true)
end

local function unvanKontrol(oyuncu, eskiIndex)
	local v = veriler[oyuncu]
	local yeniIndex = Ekonomi.UnvanIndex(v.ToplamKat)
	if yeniIndex > eskiIndex then
		UnvanOlay:FireAllClients(oyuncu, yeniIndex)
		kafaYazisiGuncelle(oyuncu)
	end
end

-- adet kadar kat ekler. Döndürür: eklenen kat sayısı
local function katEkle(oyuncu, adet, otomatik)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Tezgah then
		return 0
	end
	local maxKat = Ekonomi.MaxKat(v.Gelistirme)
	local carpan = Ekonomi.LezzetCarpani(v.Gelistirme)
	local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamKat)
	local eklenen, altinSayisi = 0, 0

	for _ = 1, adet do
		if v.Burger.Kat >= maxKat then
			break
		end
		local _, m = Ekonomi.MalzemeSec(function()
			return rastgele:NextNumber()
		end)
		katiYerlestir(v, m, true)
		v.Burger.Kat += 1
		v.Burger.Deger += m.Deger * carpan
		v.ToplamKat += 1
		eklenen += 1
		if m.Altin then
			altinSayisi += 1
		end
	end

	if eklenen > 0 then
		KatlandiOlay:FireAllClients(oyuncu, v.Tezgah.Index, eklenen, altinSayisi, otomatik)
		tabelaGuncelle(oyuncu)
		skorGuncelle(oyuncu)
		unvanKontrol(oyuncu, eskiUnvan)
	end
	durumGonder(oyuncu)
	return eklenen
end

local UCUS_BILGISI = TweenInfo.new(1.3, Enum.EasingStyle.Quad, Enum.EasingDirection.In)

-- Burgeri servis eder, parayı verir. sessiz = oyundan çıkarken (animasyon yok)
local function servisEt(oyuncu, sessiz)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Tezgah then
		return
	end
	local kat = v.Burger.Kat
	if kat < Config.ServisIcinEnAzKat then
		return
	end

	local kazanc = Ekonomi.ServisParasi(v.Burger.Deger, kat)
	v.Para += kazanc
	if kat > v.Rekor then
		v.Rekor = kat
	end

	if sessiz then
		-- Aynı burger iki kez para vermesin (kapanışta hem BindToClose hem PlayerRemoving çalışır)
		v.Burger = { Kat = 0, Deger = 0, Yukseklik = v.Burger.Yukseklik }
	else
		-- Üst ekmeği koy, burgeri havaya uçur
		katiYerlestir(v, Config.UstEkmek, false)
		local ucanKlasor = Instance.new("Folder")
		ucanKlasor.Name = oyuncu.Name
		for _, p in ipairs(v.Tezgah.Katlar:GetChildren()) do
			p.Parent = ucanKlasor
		end
		ucanKlasor.Parent = servisKlasoru
		local donus = CFrame.Angles(0, math.rad(rastgele:NextNumber(200, 520)), 0)
		local merkez = v.Tezgah.TabanCFrame
		for _, p in ipairs(ucanKlasor:GetChildren()) do
			if p:IsA("BasePart") then
				-- Burgerin ortası etrafında dönerek yukarı uçsun
				local goreli = merkez:ToObjectSpace(p.CFrame)
				local hedef = (merkez + Vector3.new(0, 40, 0)) * donus * goreli
				TweenService:Create(p, UCUS_BILGISI, { CFrame = hedef, Transparency = 1 }):Play()
			end
		end
		task.delay(UCUS_BILGISI.Time + 0.1, function()
			ucanKlasor:Destroy()
		end)

		ServisEdildiOlay:FireAllClients(oyuncu, v.Tezgah.Index, kazanc, kat)
		yeniBurger(oyuncu)
		tabelaGuncelle(oyuncu)
		skorGuncelle(oyuncu)
		durumGonder(oyuncu)
	end
end

----------------------------------------------------------------------
-- Tezgah dağıtımı
----------------------------------------------------------------------
local function bosTezgahAl(oyuncu)
	for _, t in ipairs(tezgahlar) do
		if t.Sahip == nil then
			t.Sahip = oyuncu
			return t
		end
	end
	return nil
end

local function tezgahiBirak(t)
	t.Sahip = nil
	t.Katlar:ClearAllChildren()
	t.TabelaBaslik.Text = "🍔 Boş Tezgah"
	t.TabelaAlt.Text = "Gel burger katla!"
end

local function tezgahaIsinla(oyuncu)
	local v = veriler[oyuncu]
	local karakter = oyuncu.Character
	if not v or not v.Tezgah or not karakter then
		return
	end
	local kok = karakter:WaitForChild("HumanoidRootPart", 5)
	if kok then
		task.wait(0.1)
		karakter:PivotTo(v.Tezgah.OyuncuCFrame)
	end
end

----------------------------------------------------------------------
-- Oyuncu girişi / çıkışı
----------------------------------------------------------------------
local function karakterGeldi(oyuncu, karakter)
	task.spawn(tezgahaIsinla, oyuncu)
	karakter:WaitForChild("Head", 5)
	kafaYazisiGuncelle(oyuncu)
end

local function oyuncuGeldi(oyuncu)
	if veriler[oyuncu] then
		return
	end
	local v = {
		Hazir = false,
		KayitAcik = false,
		Para = 0,
		ToplamKat = 0,
		Rekor = 0,
		Gelistirme = Ekonomi.BosGelistirmeler(),
		Burger = { Kat = 0, Deger = 0, Yukseklik = 0 },
		Jeton = 4,
		SonJeton = os.clock(),
		OtoBirikim = 0,
	}
	veriler[oyuncu] = v

	-- Skor tablosu (sağ üstteki liste)
	local skorlar = Instance.new("Folder")
	skorlar.Name = "leaderstats"
	local katDeger = Instance.new("IntValue")
	katDeger.Name = "Kat"
	katDeger.Parent = skorlar
	local paraDeger = Instance.new("IntValue")
	paraDeger.Name = "Para"
	paraDeger.Parent = skorlar
	local unvanDeger = Instance.new("StringValue")
	unvanDeger.Name = "Unvan"
	unvanDeger.Value = Config.Unvanlar[1].Ad
	unvanDeger.Parent = skorlar
	skorlar.Parent = oyuncu
	v.Skor = { Kat = katDeger, Para = paraDeger, Unvan = unvanDeger }

	v.Tezgah = bosTezgahAl(oyuncu)

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
	v.ToplamKat = hazir.ToplamKat
	v.Rekor = hazir.Rekor
	v.Gelistirme = hazir.Gelistirme
	v.KayitAcik = basarili
	v.Hazir = true

	yeniBurger(oyuncu)
	tabelaGuncelle(oyuncu)
	skorGuncelle(oyuncu)
	kafaYazisiGuncelle(oyuncu)
	durumGonder(oyuncu)
end

local function oyuncuGitti(oyuncu)
	local v = veriler[oyuncu]
	if not v then
		return
	end
	-- Yarım kalan burger boşa gitmesin: otomatik servis et
	if v.Hazir then
		servisEt(oyuncu, true)
		kaydet(oyuncu)
	end
	if v.Tezgah then
		tezgahiBirak(v.Tezgah)
	end
	veriler[oyuncu] = nil

	-- Tezgahsız bekleyen biri varsa ona ver
	for bekleyen, bv in pairs(veriler) do
		if bv.Tezgah == nil then
			local t = bosTezgahAl(bekleyen)
			if not t then
				break
			end
			bv.Tezgah = t
			if bv.Hazir then
				yeniBurger(bekleyen)
				tabelaGuncelle(bekleyen)
				durumGonder(bekleyen)
			end
			task.spawn(tezgahaIsinla, bekleyen)
		end
	end
end

Players.PlayerAdded:Connect(oyuncuGeldi)
Players.PlayerRemoving:Connect(oyuncuGitti)
for _, oyuncu in ipairs(Players:GetPlayers()) do
	task.spawn(oyuncuGeldi, oyuncu)
end

----------------------------------------------------------------------
-- Oyuncu istekleri
----------------------------------------------------------------------
-- Hile koruması: saniyede en fazla (1 / KatlamaBeklemesi) katlama, küçük patlamalara izin var
local JETON_KAPASITE = 4
local function jetonHarca(v)
	local simdi = os.clock()
	local dolum = (simdi - v.SonJeton) / Config.KatlamaBeklemesi
	v.SonJeton = simdi
	v.Jeton = math.min(JETON_KAPASITE, v.Jeton + dolum)
	if v.Jeton < 1 then
		return false
	end
	v.Jeton -= 1
	return true
end

KatlaOlay.OnServerEvent:Connect(function(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Tezgah then
		return
	end
	if not jetonHarca(v) then
		durumGonder(oyuncu) -- ekran fazladan saydıysa düzelsin
		return
	end
	katEkle(oyuncu, Ekonomi.TikBasinaKat(v.Gelistirme), false)
end)

DurumIsteOlay.OnServerEvent:Connect(function(oyuncu)
	durumGonder(oyuncu)
end)

local sonServis = {}
ServisOlay.OnServerEvent:Connect(function(oyuncu)
	local simdi = os.clock()
	if sonServis[oyuncu] and simdi - sonServis[oyuncu] < 0.5 then
		return
	end
	sonServis[oyuncu] = simdi
	servisEt(oyuncu, false)
end)
Players.PlayerRemoving:Connect(function(oyuncu)
	sonServis[oyuncu] = nil
end)

SatinAlOlay.OnServerEvent:Connect(function(oyuncu, anahtar)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or type(anahtar) ~= "string" then
		return
	end
	local g = Ekonomi.GelistirmeBul(anahtar)
	if not g then
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
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
end)

----------------------------------------------------------------------
-- Çırak Aşçı (otomatik katlama)
----------------------------------------------------------------------
RunService.Heartbeat:Connect(function(dt)
	for oyuncu, v in pairs(veriler) do
		if v.Hazir and v.Tezgah then
			local aralik = Ekonomi.OtoAralik(v.Gelistirme)
			if aralik then
				v.OtoBirikim += dt
				if v.OtoBirikim >= aralik then
					if v.Burger.Kat < Ekonomi.MaxKat(v.Gelistirme) then
						v.OtoBirikim -= aralik
						katEkle(oyuncu, 1, true)
					else
						v.OtoBirikim = aralik -- tabak dolu, servis bekleniyor
					end
				end
			end
		end
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
				servisEt(oyuncu, true)
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

print("[Burger İmparatoru] Sunucu hazır! Tezgah sayısı: " .. #tezgahlar)
