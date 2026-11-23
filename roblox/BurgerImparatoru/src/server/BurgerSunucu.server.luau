-- BURGER İMPARATORU - Sunucu
-- Arsalar, katlama, paketleme makineleri, dükkan geliştirmeleri, yeni şube, dükkan stili ve kayıt.
-- Oyuncunun ekranı sadece istekte bulunur; parayı ve her kararı sunucu verir (hile yapılamaz).
-- Kutuların katlanması ve bantta gitmesi gibi görüntüleri her oyuncunun ekranı kendisi çizer.

local Players = game:GetService("Players")
local Lighting = game:GetService("Lighting")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local DataStoreService = game:GetService("DataStoreService")
local TextService = game:GetService("TextService")

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
local DuyuruOlay = olay("Duyuru") -- sunucu -> herkes ya da bir oyuncu (yazı)
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

-- Oyun başlayınca tepeler ve göl gerçek araziye (Terrain) dönüşür: yumuşak çimen tepeler, dalgalı su.
-- (Studio'da düzenlerken bloklar olarak görünürler.)
task.spawn(function()
	local arazi = workspace:FindFirstChildOfClass("Terrain")
	if not arazi then
		return
	end
	local ok, hata = pcall(function()
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
	}
end

local function kaydet(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.KayitAcik or not kayitDeposu then
		return
	end
	local paket = {
		Surum = 3,
		Para = v.Para,
		ToplamPaket = v.ToplamPaket,
		Sube = v.Sube,
		Gelistirme = v.Gelistirme,
		Stil = v.Stil,
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

local function yeniBurger(v)
	v.Burger = { Asama = 0, Altin = rastgele:NextNumber() < Ekonomi.AltinSansi(v.Gelistirme) }
end

local function durumGonder(oyuncu)
	local v = veriler[oyuncu]
	if not v or not v.Hazir then
		return
	end
	DurumOlay:FireClient(oyuncu, {
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
	})
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
	if paketlendi then
		local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamPaket)
		kazanc = Ekonomi.PaketDegeri(b.Altin, v.Gelistirme, v.Sube)
		v.Para += kazanc
		v.ToplamPaket += 1
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
		Son = {},
	}
	veriler[oyuncu] = v

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
	v.KayitAcik = basarili
	yeniBurger(v)
	v.Hazir = true

	dukkaniKur(oyuncu)
	skorGuncelle(oyuncu)
	kafaYazisiGuncelle(oyuncu)
	durumGonder(oyuncu)
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

SatinAlOlay.OnServerEvent:Connect(function(oyuncu, anahtar)
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
		dukkaniKur(oyuncu)
	end
	skorGuncelle(oyuncu)
	durumGonder(oyuncu)
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
	DuyuruOlay:FireAllClients(string.format("🏙️ %s %d. şubesini açtı!", oyuncu.DisplayName, v.Sube + 1))
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
		DuyuruOlay:FireClient(oyuncu, "Dükkan adı en fazla " .. Config.AdEnUzun .. " harf olabilir.")
		return
	end
	if uzunluk == 0 then
		ad = varsayilanAd(oyuncu)
	end
	local temiz = adiFiltrele(oyuncu, ad)
	if not temiz then
		temiz = varsayilanAd(oyuncu)
		DuyuruOlay:FireClient(oyuncu, "İsim kontrol edilemedi, şimdilik \"" .. temiz .. "\" yazdık.")
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
				local sans = Ekonomi.AltinSansi(v.Gelistirme)
				local eskiUnvan = Ekonomi.UnvanIndex(v.ToplamPaket)
				local uretti = false
				for m = 1, adet do
					-- makineler aynı anda değil, sırayla çalışsın
					local birikim = (v.MakineBirikim[m] or (m - 1) / adet * sure) + dt
					while birikim >= sure do
						birikim -= sure
						local altin = rastgele:NextNumber() < sans
						v.Para += Ekonomi.PaketDegeri(altin, v.Gelistirme, v.Sube)
						v.ToplamPaket += 1
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
