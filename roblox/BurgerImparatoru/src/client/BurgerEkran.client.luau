-- BURGER İMPARATORU - Oyuncunun ekranı
-- KATLA butonu, "huss huss huss" sesi, HUSS! yazıları, para/paket/unvan göstergeleri ve dükkan.

local Players = game:GetService("Players")
local ReplicatedStorage = game:GetService("ReplicatedStorage")
local TweenService = game:GetService("TweenService")
local ContextActionService = game:GetService("ContextActionService")
local SoundService = game:GetService("SoundService")
local UserInputService = game:GetService("UserInputService")
local RunService = game:GetService("RunService")
local TextChatService = game:GetService("TextChatService")
local Debris = game:GetService("Debris")

local ben = Players.LocalPlayer
local Ortak = ReplicatedStorage:WaitForChild("Ortak")
local Config = require(Ortak:WaitForChild("Config"))
local Ekonomi = require(Ortak:WaitForChild("Ekonomi"))
local Uzaklar = ReplicatedStorage:WaitForChild("Uzaklar")
local KatlaOlay = Uzaklar:WaitForChild("Katla")
local SatinAlOlay = Uzaklar:WaitForChild("SatinAl")
local DurumIsteOlay = Uzaklar:WaitForChild("DurumIste")
local DurumOlay = Uzaklar:WaitForChild("Durum")
local KatlandiOlay = Uzaklar:WaitForChild("Katlandi")
local UnvanOlay = Uzaklar:WaitForChild("YeniUnvan")

local FONT = Enum.Font.FredokaOne
local rastgele = Random.new()
local K = Ekonomi.Kisalt

local durum = nil -- sunucudan gelen son durum
local sesAcik = true
local kombo = 0
local sonKatlama = 0
local KATLAMA_ARALIGI = math.max(0.09, Config.KatlamaBeklemesi + 0.01)

----------------------------------------------------------------------
-- Ses: huss huss huss
----------------------------------------------------------------------
local sesVar = Config.HussSesId ~= nil and Config.HussSesId ~= ""
local sesHavuzu = {}
local sesSirasi = 0
if sesVar then
	for i = 1, 4 do
		local s = Instance.new("Sound")
		s.Name = "Huss" .. i
		s.SoundId = Config.HussSesId
		s.Volume = Config.HussSesSeviyesi
		s.Parent = SoundService
		sesHavuzu[i] = s
	end
elseif RunService:IsStudio() then
	warn("[Burger İmparatoru] Huss sesi yok: Config.HussSesId boş. README'deki 'Sesi ekle' adımlarına bak.")
end

local function hussCal(hiz, seviye)
	if not sesAcik or not sesVar then
		return
	end
	sesSirasi = sesSirasi % #sesHavuzu + 1
	local s = sesHavuzu[sesSirasi]
	s.PlaybackSpeed = hiz or 1
	s.Volume = Config.HussSesSeviyesi * (seviye or 1)
	s.TimePosition = 0
	s:Play()
end

local sonUzakSes = {}
local function tezgahTabagi(index)
	local klasor = workspace:FindFirstChild("Tezgahlar")
	local model = klasor and klasor:FindFirstChild("Tezgah" .. tostring(index))
	return model and model:FindFirstChild("Tabak")
end

-- Başka oyuncunun tezgahından gelen 3 boyutlu huss sesi (yakındaysan duyarsın)
local function uzakHuss(tezgahIndex)
	if not sesAcik or not sesVar then
		return
	end
	local simdi = os.clock()
	if sonUzakSes[tezgahIndex] and simdi - sonUzakSes[tezgahIndex] < 0.25 then
		return
	end
	sonUzakSes[tezgahIndex] = simdi
	local tabak = tezgahTabagi(tezgahIndex)
	if not tabak then
		return
	end
	local s = Instance.new("Sound")
	s.SoundId = Config.HussSesId
	s.Volume = Config.BaskasininSesSeviyesi
	s.RollOffMode = Enum.RollOffMode.InverseTapered
	s.RollOffMinDistance = 8
	s.RollOffMaxDistance = 90
	s.PlaybackSpeed = rastgele:NextNumber(0.95, 1.1)
	s.Parent = tabak
	s:Play()
	Debris:AddItem(s, 3)
end

local function konfetiPatlat(tezgahIndex, adet)
	local tabak = tezgahTabagi(tezgahIndex)
	local nokta = tabak and tabak:FindFirstChild("KonfetiNoktasi")
	local konfeti = nokta and nokta:FindFirstChild("Konfeti")
	if konfeti then
		konfeti:Emit(adet)
	end
end

----------------------------------------------------------------------
-- Arayüz yardımcıları
----------------------------------------------------------------------
local function yap(sinif, ozellikler, cocuklar)
	local o = Instance.new(sinif)
	local ebeveyn = nil
	for k, v in pairs(ozellikler or {}) do
		if k == "Parent" then
			ebeveyn = v
		else
			o[k] = v
		end
	end
	for _, c in ipairs(cocuklar or {}) do
		c.Parent = o
	end
	if ebeveyn then
		o.Parent = ebeveyn
	end
	return o
end

local function kose(yaricap)
	return yap("UICorner", { CornerRadius = yaricap })
end

local function cizgi(renk, kalinlik)
	return yap("UIStroke", { Color = renk, Thickness = kalinlik, ApplyStrokeMode = Enum.ApplyStrokeMode.Border })
end

local function yaziCizgisi(renk, kalinlik)
	return yap("UIStroke", { Color = renk, Thickness = kalinlik, ApplyStrokeMode = Enum.ApplyStrokeMode.Contextual })
end

local function yazi(ozellikler)
	local o = {
		BackgroundTransparency = 1,
		Font = FONT,
		TextScaled = true,
		TextColor3 = Color3.new(1, 1, 1),
	}
	for k, v in pairs(ozellikler) do
		o[k] = v
	end
	return yap("TextLabel", o)
end

local function gecis(nesne, sure, hedef, stil, yon)
	local t = TweenService:Create(nesne, TweenInfo.new(sure, stil or Enum.EasingStyle.Quad, yon or Enum.EasingDirection.Out), hedef)
	t:Play()
	return t
end

local KOYU = Color3.fromRGB(45, 28, 18)
local PANEL = Color3.fromRGB(38, 26, 20)
local HUSS_SARI = Color3.fromRGB(255, 214, 20) -- videodaki HUSS! yazısının rengi

local ekran = yap("ScreenGui", {
	Name = "BurgerArayuz",
	ResetOnSpawn = false,
	ZIndexBehavior = Enum.ZIndexBehavior.Sibling,
	Parent = ben:WaitForChild("PlayerGui"),
})

local olcekler = {}
local function olcekEkle(gui)
	local s = yap("UIScale", { Parent = gui })
	table.insert(olcekler, s)
end
local function olcekGuncelle()
	local kamera = workspace.CurrentCamera
	if not kamera then
		return
	end
	local vp = kamera.ViewportSize
	local s = math.clamp(math.min(vp.X / 1150, vp.Y / 760), 0.5, 1.3)
	for _, o in ipairs(olcekler) do
		o.Scale = s
	end
end

local klavyeVar = UserInputService.KeyboardEnabled

----------------------------------------------------------------------
-- Üst panel: para, paket, şu anki burger, unvan çubuğu
----------------------------------------------------------------------
local ust = yap("Frame", {
	Name = "Ust",
	AnchorPoint = Vector2.new(0.5, 0),
	Position = UDim2.new(0.5, 0, 0, 8),
	Size = UDim2.fromOffset(640, 120),
	BackgroundTransparency = 1,
	Parent = ekran,
})
olcekEkle(ust)

local hapSirasi = yap("Frame", {
	Size = UDim2.fromOffset(640, 56),
	BackgroundTransparency = 1,
	Parent = ust,
}, {
	yap("UIListLayout", {
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Center,
		Padding = UDim.new(0, 10),
		SortOrder = Enum.SortOrder.LayoutOrder,
	}),
})

local function hap(sira, ikon, renk, genislik)
	local f = yap("Frame", {
		LayoutOrder = sira,
		Size = UDim2.fromOffset(genislik, 56),
		BackgroundColor3 = PANEL,
		BackgroundTransparency = 0.1,
		Parent = hapSirasi,
	}, { kose(UDim.new(0, 16)), cizgi(renk, 3) })
	yazi({ Text = ikon, Size = UDim2.fromOffset(50, 56), Position = UDim2.fromOffset(4, 0), Parent = f })
	local deger = yazi({
		Text = "...",
		Position = UDim2.fromOffset(54, 6),
		Size = UDim2.new(1, -62, 0, 30),
		TextXAlignment = Enum.TextXAlignment.Left,
		Parent = f,
	})
	local alt = yazi({
		Text = "",
		Position = UDim2.fromOffset(54, 34),
		Size = UDim2.new(1, -62, 0, 17),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = renk,
		Parent = f,
	})
	return deger, alt, f
end

local paraYazi, paraAlt = hap(1, "💰", Color3.fromRGB(255, 210, 60), 200)
local paketYazi, paketAlt = hap(2, "📦", Color3.fromRGB(255, 140, 50), 200)
local burgerYazi, burgerAlt = hap(3, "🍔", Color3.fromRGB(90, 220, 110), 200)
paraAlt.Text = "Para"
paketAlt.Text = "Paketlenen burger"

local unvanCubuk = yap("Frame", {
	Position = UDim2.fromOffset(10, 64),
	Size = UDim2.fromOffset(620, 30),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.1,
	ClipsDescendants = true,
	Parent = ust,
}, { kose(UDim.new(0, 12)), cizgi(Color3.fromRGB(255, 215, 0), 2) })
local unvanDolgu = yap("Frame", {
	Size = UDim2.fromScale(0, 1),
	BackgroundColor3 = Color3.fromRGB(255, 215, 0),
	BackgroundTransparency = 0.35,
	Parent = unvanCubuk,
}, { kose(UDim.new(0, 12)) })
local unvanYazi = yazi({
	Text = "👑 Çırak",
	Size = UDim2.new(1, -16, 1, -6),
	Position = UDim2.fromOffset(8, 3),
	ZIndex = 2,
	Parent = unvanCubuk,
})
yaziCizgisi(KOYU, 2).Parent = unvanYazi

local kayitYazi = yazi({
	Text = "",
	Position = UDim2.fromOffset(10, 98),
	Size = UDim2.fromOffset(620, 18),
	TextColor3 = Color3.fromRGB(255, 200, 200),
	Visible = false,
	Parent = ust,
})
yaziCizgisi(KOYU, 1.5).Parent = kayitYazi

local sesButonu = yap("TextButton", {
	Name = "Ses",
	AnchorPoint = Vector2.new(0, 0),
	Position = UDim2.new(1, 8, 0, 6),
	Size = UDim2.fromOffset(46, 46),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.1,
	Font = FONT,
	TextScaled = true,
	Text = "🔊",
	TextColor3 = Color3.new(1, 1, 1),
	Parent = ust,
}, { kose(UDim.new(1, 0)), cizgi(Color3.fromRGB(255, 255, 255), 2) })

----------------------------------------------------------------------
-- Alt bölüm: KATLA ve DÜKKAN
----------------------------------------------------------------------
local alt = yap("Frame", {
	Name = "Alt",
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -16),
	Size = UDim2.fromOffset(600, 200),
	BackgroundTransparency = 1,
	Parent = ekran,
})
olcekEkle(alt)

local katlaButonu = yap("TextButton", {
	Name = "Katla",
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, 0),
	Size = UDim2.fromOffset(184, 184),
	BackgroundColor3 = Color3.fromRGB(255, 255, 255),
	AutoButtonColor = false,
	Text = "",
	Parent = alt,
}, {
	kose(UDim.new(1, 0)),
	cizgi(KOYU, 6),
	yap("UIGradient", {
		Color = ColorSequence.new(Color3.fromRGB(255, 190, 70), Color3.fromRGB(240, 90, 30)),
		Rotation = 90,
	}),
})
local katlaOlcek = yap("UIScale", { Parent = katlaButonu })
yazi({ Text = "🍔", Size = UDim2.new(1, 0, 0.36, 0), Position = UDim2.fromScale(0, 0.12), Parent = katlaButonu })
local katlaYazi = yazi({
	Text = "KATLA!",
	Size = UDim2.new(0.84, 0, 0.24, 0),
	Position = UDim2.fromScale(0.08, 0.5),
	Parent = katlaButonu,
})
yaziCizgisi(KOYU, 3).Parent = katlaYazi
yazi({
	Text = "[E]",
	Visible = klavyeVar,
	Size = UDim2.new(1, 0, 0.12, 0),
	Position = UDim2.fromScale(0, 0.76),
	TextColor3 = Color3.fromRGB(255, 240, 200),
	Parent = katlaButonu,
})

-- KATLA'nın üstünde 3 nokta: kaçıncı katlamadasın
local noktaSirasi = yap("Frame", {
	Name = "Katlamalar",
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -192),
	Size = UDim2.fromOffset(150, 34),
	BackgroundTransparency = 1,
	Parent = alt,
}, {
	yap("UIListLayout", {
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Center,
		VerticalAlignment = Enum.VerticalAlignment.Center,
		Padding = UDim.new(0, 12),
	}),
})
local noktalar = {}
for i = 1, Config.KatlamaSayisi do
	local n = yap("Frame", {
		LayoutOrder = i,
		Size = UDim2.fromOffset(30, 30),
		BackgroundColor3 = PANEL,
		Parent = noktaSirasi,
	}, { kose(UDim.new(1, 0)), cizgi(KOYU, 3) })
	noktalar[i] = { Cerceve = n, Olcek = yap("UIScale", { Parent = n }) }
end

local dukkanButonu = yap("TextButton", {
	Name = "Dukkan",
	AnchorPoint = Vector2.new(1, 1),
	Position = UDim2.new(0.5, -110, 1, -24),
	Size = UDim2.fromOffset(176, 82),
	BackgroundColor3 = Color3.new(1, 1, 1),
	AutoButtonColor = false,
	Text = "",
	Parent = alt,
}, {
	kose(UDim.new(0, 20)),
	cizgi(KOYU, 4),
	yap("UIGradient", { Color = ColorSequence.new(Color3.fromRGB(120, 180, 255), Color3.fromRGB(50, 100, 230)), Rotation = 90 }),
})
local dukkanOlcek = yap("UIScale", { Parent = dukkanButonu })
local dukkanYazi = yazi({ Text = "🛒 DÜKKAN", Size = UDim2.new(0.9, 0, 0.44, 0), Position = UDim2.fromScale(0.05, 0.1), Parent = dukkanButonu })
yaziCizgisi(KOYU, 2.5).Parent = dukkanYazi
local dukkanAlt = yazi({
	Text = klavyeVar and "[B]" or "Geliştir!",
	Size = UDim2.new(0.9, 0, 0.3, 0),
	Position = UDim2.fromScale(0.05, 0.58),
	TextColor3 = Color3.fromRGB(255, 255, 235),
	Parent = dukkanButonu,
})
yaziCizgisi(KOYU, 1.5).Parent = dukkanAlt

local komboYazi = yazi({
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -232),
	Size = UDim2.fromOffset(300, 46),
	Text = "",
	TextColor3 = HUSS_SARI,
	Visible = false,
	ZIndex = 4,
	Parent = alt,
})
yaziCizgisi(KOYU, 3).Parent = komboYazi
local komboOlcek = yap("UIScale", { Parent = komboYazi })

----------------------------------------------------------------------
-- Duyurular (ortada beliren büyük yazılar)
----------------------------------------------------------------------
local duyuruAlani = yap("Frame", {
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.fromScale(0.5, 0.36),
	Size = UDim2.fromOffset(760, 110),
	BackgroundTransparency = 1,
	Parent = ekran,
})
olcekEkle(duyuruAlani)
local sonDuyuru = nil

local function duyuru(metin, renk, sure, altMetin)
	if sonDuyuru then
		sonDuyuru:Destroy()
	end
	local f = yap("Frame", { Size = UDim2.fromScale(1, 1), BackgroundTransparency = 1, Parent = duyuruAlani })
	sonDuyuru = f
	local ana = yazi({
		Text = metin,
		Size = UDim2.new(1, 0, 0.62, 0),
		TextColor3 = renk or Color3.new(1, 1, 1),
		Parent = f,
	})
	local c1 = yaziCizgisi(KOYU, 4)
	c1.Parent = ana
	local ikinci, c2
	if altMetin then
		ikinci = yazi({
			Text = altMetin,
			Position = UDim2.fromScale(0.1, 0.64),
			Size = UDim2.new(0.8, 0, 0.32, 0),
			Parent = f,
		})
		c2 = yaziCizgisi(KOYU, 3)
		c2.Parent = ikinci
	end
	local olcek = yap("UIScale", { Scale = 0.3, Parent = f })
	gecis(olcek, 0.3, { Scale = 1 }, Enum.EasingStyle.Back)
	task.delay(sure or 1.6, function()
		if sonDuyuru ~= f then
			return
		end
		gecis(ana, 0.35, { TextTransparency = 1 })
		gecis(c1, 0.35, { Transparency = 1 })
		if ikinci then
			gecis(ikinci, 0.35, { TextTransparency = 1 })
			gecis(c2, 0.35, { Transparency = 1 })
		end
		gecis(olcek, 0.35, { Scale = 1.2 })
		task.wait(0.4)
		-- Bu arada yeni bir duyuru geldiyse o zaten bunu sildi
		if sonDuyuru == f then
			sonDuyuru = nil
			f:Destroy()
		end
	end)
end

----------------------------------------------------------------------
-- Uçan yazılar: HUSS! ve +para
----------------------------------------------------------------------
local aktifYazi = 0

local function ucanYazi(metin, renk, buyukluk, x, y)
	if aktifYazi > 24 then
		return
	end
	aktifYazi += 1
	buyukluk = buyukluk or 1
	local l = yazi({
		AnchorPoint = Vector2.new(0.5, 0.5),
		Position = UDim2.new(0.5, x, 1, y),
		Size = UDim2.fromOffset(230 * buyukluk, 66 * buyukluk),
		Text = metin,
		TextColor3 = renk,
		Rotation = rastgele:NextInteger(-14, 14),
		ZIndex = 5,
		Parent = alt,
	})
	local c = yaziCizgisi(KOYU, 4)
	c.Parent = l
	local olcek = yap("UIScale", { Scale = 0.3, Parent = l })
	gecis(olcek, 0.14, { Scale = 1.15 }, Enum.EasingStyle.Back)
	task.delay(0.2, function()
		local sure = 0.5
		gecis(l, sure, { Position = l.Position - UDim2.fromOffset(0, 50), TextTransparency = 1 }, Enum.EasingStyle.Quad, Enum.EasingDirection.In)
		gecis(c, sure, { Transparency = 1 }, Enum.EasingStyle.Quad, Enum.EasingDirection.In)
		gecis(olcek, sure, { Scale = 0.8 })
		task.wait(sure + 0.02)
		l:Destroy()
		aktifYazi -= 1
	end)
end

local function hussYazisi(buyukluk)
	ucanYazi("HUSS!", HUSS_SARI, buyukluk, rastgele:NextInteger(-230, 230), rastgele:NextInteger(-310, -200))
end

-- Sesteki her "huss" anında bir HUSS! yazısı fırlat (videodaki gibi üç kez)
local function hussSerisi(hiz, buyukluk)
	for _, t in ipairs(Config.HussZamanlari) do
		task.delay(t / (hiz or 1), hussYazisi, buyukluk)
	end
end

local function zipla(olcek, kuculme)
	olcek.Scale = kuculme or 0.86
	gecis(olcek, 0.22, { Scale = 1 }, Enum.EasingStyle.Back)
end

local function salla(gui)
	local bas = gui.Position
	task.spawn(function()
		for i = 1, 6 do
			gui.Position = bas + UDim2.fromOffset((i % 2 == 0) and 8 or -8, 0)
			task.wait(0.035)
		end
		gui.Position = bas
	end)
end

----------------------------------------------------------------------
-- Dükkan paneli
----------------------------------------------------------------------
local dukkan = yap("Frame", {
	Name = "DukkanPaneli",
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.new(0.5, 0, 0.5, -10),
	Size = UDim2.fromOffset(600, 486),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.04,
	Visible = false,
	ZIndex = 10,
	Parent = ekran,
}, { kose(UDim.new(0, 24)), cizgi(Color3.fromRGB(255, 160, 60), 4) })
olcekEkle(dukkan)

local dukkanBaslik = yazi({
	Text = "🛒 DÜKKAN",
	Position = UDim2.fromOffset(20, 12),
	Size = UDim2.new(1, -110, 0, 50),
	TextXAlignment = Enum.TextXAlignment.Left,
	TextColor3 = Color3.fromRGB(255, 210, 80),
	ZIndex = 11,
	Parent = dukkan,
})
yaziCizgisi(KOYU, 3).Parent = dukkanBaslik
local kapatButonu = yap("TextButton", {
	AnchorPoint = Vector2.new(1, 0),
	Position = UDim2.new(1, -14, 0, 14),
	Size = UDim2.fromOffset(48, 48),
	BackgroundColor3 = Color3.fromRGB(230, 70, 70),
	Font = FONT,
	TextScaled = true,
	Text = "✕",
	TextColor3 = Color3.new(1, 1, 1),
	ZIndex = 11,
	Parent = dukkan,
}, { kose(UDim.new(1, 0)), cizgi(KOYU, 3) })

local satirlar = {}
for i, g in ipairs(Config.Gelistirmeler) do
	local satir = yap("Frame", {
		Position = UDim2.fromOffset(18, 74 + (i - 1) * 100),
		Size = UDim2.new(1, -36, 0, 90),
		BackgroundColor3 = Color3.fromRGB(64, 46, 36),
		ZIndex = 11,
		Parent = dukkan,
	}, { kose(UDim.new(0, 18)) })
	yazi({ Text = g.Ikon, Position = UDim2.fromOffset(8, 10), Size = UDim2.fromOffset(70, 70), ZIndex = 12, Parent = satir })
	local ad = yazi({
		Text = g.Ad,
		Position = UDim2.fromOffset(86, 8),
		Size = UDim2.new(1, -270, 0, 32),
		TextXAlignment = Enum.TextXAlignment.Left,
		ZIndex = 12,
		Parent = satir,
	})
	local aciklama = yazi({
		Text = g.Aciklama,
		Position = UDim2.fromOffset(86, 42),
		Size = UDim2.new(1, -270, 0, 18),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = Color3.fromRGB(220, 200, 180),
		ZIndex = 12,
		Parent = satir,
	})
	local etki = yazi({
		Text = "",
		Position = UDim2.fromOffset(86, 62),
		Size = UDim2.new(1, -270, 0, 18),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = Color3.fromRGB(255, 220, 90),
		ZIndex = 12,
		Parent = satir,
	})
	local al = yap("TextButton", {
		AnchorPoint = Vector2.new(1, 0.5),
		Position = UDim2.new(1, -12, 0.5, 0),
		Size = UDim2.fromOffset(160, 62),
		BackgroundColor3 = Color3.fromRGB(70, 190, 90),
		AutoButtonColor = true,
		Font = FONT,
		TextScaled = true,
		Text = "",
		TextColor3 = Color3.new(1, 1, 1),
		ZIndex = 12,
		Parent = satir,
	}, {
		kose(UDim.new(0, 16)),
		cizgi(KOYU, 3),
		yap("UIPadding", {
			PaddingLeft = UDim.new(0, 8),
			PaddingRight = UDim.new(0, 8),
			PaddingTop = UDim.new(0, 8),
			PaddingBottom = UDim.new(0, 8),
		}),
	})
	satirlar[g.Anahtar] = { Satir = satir, Ad = ad, Aciklama = aciklama, Etki = etki, Al = al, Gelistirme = g }

	al.Activated:Connect(function()
		if not durum then
			return
		end
		local sv = Ekonomi.Seviye(durum.Gelistirme, g.Anahtar)
		local fiyat = Ekonomi.Fiyat(g.Anahtar, sv)
		if not fiyat then
			return
		end
		if durum.Para < fiyat then
			salla(al)
			duyuru("Paran yetmiyor! 🍔", Color3.fromRGB(255, 120, 120), 1.2, "Biraz daha burger paketle.")
			return
		end
		SatinAlOlay:FireServer(g.Anahtar)
	end)
end

local function dukkanGuncelle()
	if not durum then
		return
	end
	for anahtar, s in pairs(satirlar) do
		local g = s.Gelistirme
		local sv = Ekonomi.Seviye(durum.Gelistirme, anahtar)
		local fiyat = Ekonomi.Fiyat(anahtar, sv)
		s.Ad.Text = string.format("%s  •  Sv %d/%d", g.Ad, sv, g.MaxSeviye)
		if fiyat then
			s.Etki.Text = Ekonomi.EtkiYazisi(anahtar, sv) .. "  →  " .. Ekonomi.EtkiYazisi(anahtar, sv + 1)
			s.Al.Text = "💰 " .. K(fiyat)
			local yeter = durum.Para >= fiyat
			s.Al.BackgroundColor3 = yeter and Color3.fromRGB(70, 190, 90) or Color3.fromRGB(110, 100, 95)
		else
			s.Etki.Text = Ekonomi.EtkiYazisi(anahtar, sv) .. "  (en üst seviye)"
			s.Al.Text = "MAX ⭐"
			s.Al.BackgroundColor3 = Color3.fromRGB(200, 150, 40)
		end
	end
end

local function dukkanAcKapa(ac)
	if ac == nil then
		ac = not dukkan.Visible
	end
	dukkan.Visible = ac
	if ac then
		dukkanGuncelle()
		local o = dukkan:FindFirstChildOfClass("UIScale")
		local hedef = o.Scale
		o.Scale = hedef * 0.8
		gecis(o, 0.2, { Scale = hedef }, Enum.EasingStyle.Back)
	end
end

----------------------------------------------------------------------
-- Ekranı güncelle
----------------------------------------------------------------------
local function noktalariGuncelle()
	local asama = durum and durum.Asama or 0
	for i, n in ipairs(noktalar) do
		local dolu = i <= asama
		n.Cerceve.BackgroundColor3 = dolu and HUSS_SARI or PANEL
	end
end

local function arayuzGuncelle()
	if not durum then
		return
	end
	paraYazi.Text = K(durum.Para)
	paketYazi.Text = K(durum.ToplamPaket)
	burgerYazi.Text = string.format("%d / %d katlama", durum.Asama, Config.KatlamaSayisi)
	burgerAlt.Text = (durum.Altin and "✨ ALTIN! " or "Değeri: ") .. "+" .. K(durum.BurgerDegeri) .. " 💰"
	noktalariGuncelle()

	-- Unvan çubuğu
	local idx = Ekonomi.UnvanIndex(durum.ToplamPaket)
	local u = Config.Unvanlar[idx]
	local sonraki = Config.Unvanlar[idx + 1]
	if sonraki then
		local oran = (durum.ToplamPaket - u.Esik) / (sonraki.Esik - u.Esik)
		unvanDolgu.Size = UDim2.fromScale(math.clamp(oran, 0, 1), 1)
		unvanYazi.Text = string.format("👑 %s   •   %s / %s paket  →  %s", u.Ad, K(durum.ToplamPaket), K(sonraki.Esik), sonraki.Ad)
	else
		unvanDolgu.Size = UDim2.fromScale(1, 1)
		unvanYazi.Text = "👑 " .. u.Ad .. "   •   EN TEPEDESİN!   •   " .. K(durum.ToplamPaket) .. " paket"
	end
	unvanDolgu.BackgroundColor3 = u.Renk

	katlaYazi.Text = durum.TezgahVar and "KATLA!" or "BEKLE"

	if durum.KayitAcik then
		kayitYazi.Visible = false
	else
		kayitYazi.Visible = true
		kayitYazi.Text = RunService:IsStudio() and "💾 Studio testinde kayıt kapalı (oyunu yayınlayınca açılır)"
			or "💾 Kayıt yüklenemedi, bu seferki ilerlemen kaydedilmeyecek"
	end

	if dukkan.Visible then
		dukkanGuncelle()
	end
end

----------------------------------------------------------------------
-- Eylemler
----------------------------------------------------------------------
local function katla()
	if not durum then
		return
	end
	if not durum.TezgahVar then
		duyuru("Bütün tezgahlar dolu!", Color3.fromRGB(255, 150, 120), 1.5, "Biri çıkınca sıra sende.")
		return
	end
	local simdi = os.clock()
	if simdi - sonKatlama < KATLAMA_ARALIGI then
		return
	end

	-- Kombo: hızlı katladıkça ses hızlanır, yazılar büyür
	if simdi - sonKatlama < 0.9 then
		kombo += 1
	else
		kombo = 1
	end
	sonKatlama = simdi

	-- Sunucunun cevabını beklemeden ekranı hemen güncelle (cevap gelince düzelir)
	durum.Asama += Ekonomi.TikBasinaKatlama(durum.Gelistirme)
	if durum.Asama >= Config.KatlamaSayisi then
		durum.Asama = 0
		durum.ToplamPaket += 1
	end

	KatlaOlay:FireServer()

	local hiz = 1 + math.min(kombo, 40) * 0.008
	hussCal(hiz, 1)
	hussSerisi(hiz, 1 + math.min(kombo, 30) * 0.012)
	zipla(katlaOlcek, 0.86)

	if kombo >= 3 then
		komboYazi.Visible = true
		komboYazi.Text = "KOMBO x" .. kombo
		zipla(komboOlcek, 1.3)
	end
	arayuzGuncelle()
end

katlaButonu.Activated:Connect(katla)
dukkanButonu.Activated:Connect(function()
	zipla(dukkanOlcek, 0.85)
	dukkanAcKapa()
end)
kapatButonu.Activated:Connect(function()
	dukkanAcKapa(false)
end)
sesButonu.Activated:Connect(function()
	sesAcik = not sesAcik
	sesButonu.Text = sesAcik and "🔊" or "🔇"
	if not sesAcik then
		for _, s in ipairs(sesHavuzu) do
			s:Stop()
		end
	end
end)

ContextActionService:BindAction("BurgerKatla", function(_, girdi)
	if girdi == Enum.UserInputState.Begin then
		katla()
	end
	return Enum.ContextActionResult.Sink
end, false, Enum.KeyCode.E, Enum.KeyCode.ButtonR2)

ContextActionService:BindAction("BurgerDukkan", function(_, girdi)
	if girdi == Enum.UserInputState.Begin then
		dukkanAcKapa()
	end
	return Enum.ContextActionResult.Sink
end, false, Enum.KeyCode.B, Enum.KeyCode.ButtonY)

-- Kombo yazısı bir süre katlamazsan kaybolur
RunService.RenderStepped:Connect(function()
	if komboYazi.Visible and os.clock() - sonKatlama > 0.9 then
		komboYazi.Visible = false
		kombo = 0
	end
end)

----------------------------------------------------------------------
-- Sunucudan gelenler
----------------------------------------------------------------------
local function sohbetMesaji(metin)
	pcall(function()
		local kanallar = TextChatService:FindFirstChild("TextChannels")
		local genel = kanallar and kanallar:FindFirstChild("RBXGeneral")
		if genel then
			genel:DisplaySystemMessage(metin)
		end
	end)
end

local ilkDurum = true
DurumOlay.OnClientEvent:Connect(function(yeni)
	local eski = durum
	durum = yeni
	if eski then
		for _, g in ipairs(Config.Gelistirmeler) do
			local once = Ekonomi.Seviye(eski.Gelistirme, g.Anahtar)
			local simdi = Ekonomi.Seviye(yeni.Gelistirme, g.Anahtar)
			if simdi > once then
				duyuru(g.Ikon .. " " .. g.Ad .. " Sv " .. simdi .. "!", Color3.fromRGB(140, 255, 140), 1.4, Ekonomi.EtkiYazisi(g.Anahtar, simdi))
				local s = satirlar[g.Anahtar]
				if s then
					s.Satir.BackgroundColor3 = Color3.fromRGB(80, 170, 90)
					gecis(s.Satir, 0.5, { BackgroundColor3 = Color3.fromRGB(64, 46, 36) })
				end
			end
		end
	end
	if ilkDurum then
		ilkDurum = false
		if yeni.TezgahVar then
			duyuru("🍔 BURGER İMPARATORU 👑", Color3.fromRGB(255, 215, 80), 2.6, "KATLA'ya bas: 3 katlamada burger paketlenir!")
		else
			duyuru("Bütün tezgahlar dolu!", Color3.fromRGB(255, 150, 120), 3, "Biri çıkınca sıra sende.")
		end
	end
	arayuzGuncelle()
end)

KatlandiOlay.OnClientEvent:Connect(function(kim, tezgahIndex, eklenen, paketlendi, kazanc, altin, otomatik)
	if paketlendi then
		konfetiPatlat(tezgahIndex, altin and 70 or 14)
	end
	if kim == ben then
		if otomatik then
			hussCal(rastgele:NextNumber(0.95, 1.05), 0.45)
			ucanYazi("👨‍🍳 huss", HUSS_SARI, 0.6, rastgele:NextInteger(-230, 230), rastgele:NextInteger(-300, -220))
		end
		if paketlendi then
			ucanYazi("📦 +" .. K(kazanc) .. " 💰", Color3.fromRGB(140, 255, 120), 0.9, rastgele:NextInteger(-60, 60), -250)
			if altin then
				duyuru("✨ ALTIN BURGER! ✨", HUSS_SARI, 1.6, "+" .. K(kazanc) .. " 💰")
			end
		end
	else
		uzakHuss(tezgahIndex)
	end
end)

UnvanOlay.OnClientEvent:Connect(function(kim, index)
	local u = Config.Unvanlar[index]
	if not u then
		return
	end
	if kim == ben then
		duyuru("👑 YENİ UNVAN: " .. u.Ad .. "!", u.Renk, 2.6, "Burger İmparatorluğuna bir adım daha!")
		hussSerisi(1, 1.5)
	end
	if kim then
		sohbetMesaji("👑 " .. kim.DisplayName .. " artık " .. u.Ad .. "!")
	end
end)

-- Ekran hazır: sunucudan güncel durumu iste (kayıt hâlâ yükleniyorsa sunucu hazır olunca kendisi yollar)
DurumIsteOlay:FireServer()

----------------------------------------------------------------------
olcekGuncelle()
workspace:GetPropertyChangedSignal("CurrentCamera"):Connect(function()
	olcekGuncelle()
	if workspace.CurrentCamera then
		workspace.CurrentCamera:GetPropertyChangedSignal("ViewportSize"):Connect(olcekGuncelle)
	end
end)
if workspace.CurrentCamera then
	workspace.CurrentCamera:GetPropertyChangedSignal("ViewportSize"):Connect(olcekGuncelle)
end
