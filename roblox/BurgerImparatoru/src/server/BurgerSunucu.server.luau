-- BURGER İMPARATORU - Sunucu
-- Katlama, paketleme, dükkan, unvanlar ve kayıt burada. Oyuncunun ekranı (client) sadece
-- "katla" ve "satın al" ister; her şeyi sunucu kontrol eder, böylece hile yapılamaz.

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
local SatinAlOlay = olay("SatinAl") -- oyuncu -> sunucu (anahtar)
local DurumIsteOlay = olay("DurumIste") -- oyuncu -> sunucu (ekran açılınca durumu ister)
local DurumOlay = olay("Durum") -- sunucu -> oyuncu (durum tablosu)
-- sunucu -> herkes: kim, tezgah, kaç katlama, paketlendi mi, kazanç, altın mı, otomatik mi
local KatlandiOlay = olay("Katlandi")
local UnvanOlay = olay("YeniUnvan") -- sunucu -> herkes (kim, unvan sırası)
uzaklar.Parent = ReplicatedStorage

----------------------------------------------------------------------
-- Dünya
----------------------------------------------------------------------
local tezgahlar, ucanKlasor = Dunya.Kur()
local rastgele = Random.new()
local function zar()
	return rastgele:NextNumber()
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
		ToplamPaket = math.max(0, math.floor(sayiYap(ham.ToplamPaket, 0))),
		Gelistirme = gelistirme,
	}
end

local function kaydet(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.KayitAcik or not kayitDeposu then
		return
	end
	local paket = {
		Surum = 2,
		Para = v.Para,
		ToplamPaket = v.ToplamPaket,
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
	return Config.Unvanlar[Ekonomi.UnvanIndex(v.ToplamPaket)]
end

local function durumGonder(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir then
		return
	end
	local b = v.Burger
	DurumOlay:FireClient(oyuncu, {
		Para = v.Para,
		ToplamPaket = v.ToplamPaket,
		UnvanIndex = Ekonomi.UnvanIndex(v.ToplamPaket),
		Asama = b and b.Asama or 0,
		BurgerDegeri = b and Ekonomi.PaketDegeri(b.Deger, v.Gelistirme) or 0,
		Altin = b ~= nil and b.Altin,
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
	t.TabelaAlt.Text = string.format("%s paket • %s", Ekonomi.Kisalt(v.ToplamPaket), unvan(v).Ad)
end

local function skorGuncelle(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Skor then
		return
	end
	v.Skor.Paket.Value = v.ToplamPaket
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
-- Burger ve kağıt
----------------------------------------------------------------------
local KATLAMA = TweenInfo.new(0.17, Enum.EasingStyle.Quad, Enum.EasingDirection.Out)
local PATLAMA = TweenInfo.new(0.15, Enum.EasingStyle.Back, Enum.EasingDirection.Out)
local UCUS = TweenInfo.new(0.45, Enum.EasingStyle.Quad, Enum.EasingDirection.InOut)
local SARMA_SURESI = 0.17 -- son kanatlar katlanınca bu kadar sonra burger pakete dönüşür
local BEKLEME = 0.2 -- paket tezgahta bu kadar durup kuleye uçar
local KAGIT_PARCALARI = { "Orta", "Sol", "Sag", "On", "Arka" }

-- gecikme: önceki paket tezgahtan kalkana kadar yeni burger yukarıda bekler
local function yeniBurger(oyuncu, gecikme)
	local v = veriler[oyuncu]
	if not v or not v.Tezgah then
		return
	end
	local t = v.Tezgah
	local liste, deger, altin = Ekonomi.BurgerTarifi(zar, v.Gelistirme)

	local klasor = Instance.new("Folder")
	klasor.Name = "Burger"
	local parcalar = {}

	-- Burger, kağıdın ortasında
	local taban = t.KagitCFrame * CFrame.new(0, Dunya.KagitKalinligi(), 0)
	local cf = taban
	local katlar = { Config.AltEkmek }
	for _, m in ipairs(liste) do
		table.insert(katlar, m)
	end
	table.insert(katlar, Config.UstEkmek)
	local yukseklik = 0
	for _, m in ipairs(katlar) do
		local yeni, k = Dunya.KatYap(m, cf, 1, rastgele)
		for _, p in ipairs(yeni) do
			table.insert(parcalar, p)
		end
		cf = cf * CFrame.new(0, k, 0)
		yukseklik += k
	end

	local kagit = Dunya.KagitYap(t.KagitCFrame, altin, yukseklik)
	for _, ad in ipairs(KAGIT_PARCALARI) do
		table.insert(parcalar, kagit[ad])
	end

	-- Yukarıdan düşerek gelsin
	local dusme = TweenInfo.new(0.25, Enum.EasingStyle.Back, Enum.EasingDirection.Out, 0, false, gecikme or 0)
	for _, p in ipairs(parcalar) do
		local hedef = p.CFrame
		p.CFrame = hedef + Vector3.new(0, 6, 0)
		p.Parent = klasor
		TweenService:Create(p, dusme, { CFrame = hedef }):Play()
	end
	klasor.Parent = t.PaketKlasoru

	v.Burger = { Asama = 0, Deger = deger, Altin = altin, Kagit = kagit, Klasor = klasor, Yukseklik = yukseklik }
end

local function modelParcalari(model)
	local liste = {}
	for _, p in ipairs(model:GetDescendants()) do
		if p:IsA("BasePart") then
			table.insert(liste, p)
		end
	end
	return liste
end

local function kuleyiBosalt(t)
	t.Yigin.Nesil += 1
	for _, paket in pairs(t.Yigin.Paketler) do
		for _, p in ipairs(modelParcalari(paket)) do
			TweenService:Create(p, TweenInfo.new(0.6, Enum.EasingStyle.Quad, Enum.EasingDirection.In), {
				CFrame = p.CFrame + Vector3.new(0, 25, 0),
			}):Play()
		end
		task.delay(0.62, function()
			paket:Destroy()
		end)
	end
	t.Yigin.Paketler = {}
end

-- Kağıda sarılan burgeri pakete çevirir, kuleye uçurur, yeni burger getirir
local function paketle(oyuncu)
	local v = veriler[oyuncu]
	local t = v.Tezgah
	local b = v.Burger

	-- Kuledeki yerini şimdiden ayır
	local y = t.Yigin
	if y.Sayac >= Config.YiginEnFazla then
		kuleyiBosalt(t)
		y.Sayac = 0
	end
	y.Sayac += 1
	local sira = y.Sayac
	local nesil = y.Nesil
	local boy = Dunya.PaketBoyu()
	local hedef = t.YiginCFrame
		* CFrame.new(0, (sira - 1) * Dunya.YiginAdimi() + boy.Y / 2, 0)
		* CFrame.Angles(0, math.rad(rastgele:NextNumber(-25, 25)), 0)

	local klasor = b.Klasor
	local altin = b.Altin
	local merkez = t.KagitCFrame * CFrame.new(0, boy.Y / 2, 0)
	klasor.Parent = ucanKlasor

	task.delay(SARMA_SURESI, function()
		-- Kanatlar kapandı: kağıt + burger yerine sarılmış paket "pof" diye belirir
		klasor:Destroy()
		local paket = Dunya.PaketliBurger(merkez, altin)
		local govde = paket.PrimaryPart
		local tamBoy = govde.Size
		govde.Size = tamBoy * 0.75
		paket.Parent = ucanKlasor
		TweenService:Create(govde, PATLAMA, { Size = tamBoy }):Play()

		task.delay(BEKLEME, function()
			if not paket.Parent then
				return
			end
			for _, p in ipairs(modelParcalari(paket)) do
				local goreli = merkez:ToObjectSpace(p.CFrame)
				TweenService:Create(p, UCUS, { CFrame = hedef * goreli }):Play()
			end
			task.delay(UCUS.Time + 0.02, function()
				if t.Yigin.Nesil ~= nesil or not paket.Parent then
					paket:Destroy() -- bu arada kule boşaltıldı ya da oyuncu çıktı
					return
				end
				paket.Parent = t.YiginKlasoru
				t.Yigin.Paketler[sira] = paket
			end)
		end)
	end)

	yeniBurger(oyuncu, SARMA_SURESI + BEKLEME + 0.05)
end

local function unvanKontrol(oyuncu, eskiIndex)
	local v = veriler[oyuncu]
	local yeniIndex = Ekonomi.UnvanIndex(v.ToplamPaket)
	if yeniIndex > eskiIndex then
		UnvanOlay:FireAllClients(oyuncu, yeniIndex)
		kafaYazisiGuncelle(oyuncu)
	end
end

-- adet kadar katlama yapar (burger paketlenince durur)
local function katla(oyuncu, adet, otomatik)
	local v = veriler[oyuncu]
	if not v or not v.Hazir or not v.Tezgah or not v.Burger then
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

	-- Kağıdın kanatlarını burgerin üstüne katla
	local poz = Dunya.KagitPozu(v.Tezgah.KagitCFrame, b.Asama, b.Yukseklik)
	for _, ad in ipairs(KAGIT_PARCALARI) do
		local p = b.Kagit[ad]
		if p.CFrame ~= poz[ad] then
			TweenService:Create(p, KATLAMA, { CFrame = poz[ad] }):Play()
		end
	end

	local paketlendi = b.Asama >= Config.KatlamaSayisi
	local kazanc = 0
	local altin = b.Altin
	if paketlendi then
		local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamPaket)
		kazanc = Ekonomi.PaketDegeri(b.Deger, v.Gelistirme)
		v.Para += kazanc
		v.ToplamPaket += 1
		paketle(oyuncu)
		tabelaGuncelle(oyuncu)
		skorGuncelle(oyuncu)
		unvanKontrol(oyuncu, eskiUnvan)
	end

	KatlandiOlay:FireAllClients(oyuncu, v.Tezgah.Index, eklenen, paketlendi, kazanc, altin, otomatik)
	durumGonder(oyuncu)
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
	t.PaketKlasoru:ClearAllChildren()
	t.YiginKlasoru:ClearAllChildren()
	t.Yigin = { Paketler = {}, Nesil = t.Yigin.Nesil + 1, Sayac = 0 }
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
		ToplamPaket = 0,
		Gelistirme = Ekonomi.BosGelistirmeler(),
		Burger = nil,
		Jeton = 4,
		SonJeton = os.clock(),
		OtoBirikim = 0,
	}
	veriler[oyuncu] = v

	-- Skor tablosu (sağ üstteki liste)
	local skorlar = Instance.new("Folder")
	skorlar.Name = "leaderstats"
	local paketDeger = Instance.new("IntValue")
	paketDeger.Name = "Paket"
	paketDeger.Parent = skorlar
	local paraDeger = Instance.new("IntValue")
	paraDeger.Name = "Para"
	paraDeger.Parent = skorlar
	local unvanDeger = Instance.new("StringValue")
	unvanDeger.Name = "Unvan"
	unvanDeger.Value = Config.Unvanlar[1].Ad
	unvanDeger.Parent = skorlar
	skorlar.Parent = oyuncu
	v.Skor = { Paket = paketDeger, Para = paraDeger, Unvan = unvanDeger }

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
	v.ToplamPaket = hazir.ToplamPaket
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
	if v.Hazir then
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
-- Hile koruması: saniyede en fazla (1 / KatlamaBeklemesi) tıklama, küçük patlamalara izin var
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
	katla(oyuncu, Ekonomi.TikBasinaKatlama(v.Gelistirme), false)
end)

DurumIsteOlay.OnServerEvent:Connect(function(oyuncu)
	durumGonder(oyuncu)
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
		if v.Hazir and v.Tezgah and v.Burger then
			local aralik = Ekonomi.OtoAralik(v.Gelistirme)
			if aralik then
				v.OtoBirikim += dt
				if v.OtoBirikim >= aralik then
					v.OtoBirikim -= aralik
					katla(oyuncu, 1, true)
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
