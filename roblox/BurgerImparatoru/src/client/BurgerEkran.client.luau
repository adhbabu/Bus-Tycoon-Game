-- BURGER İMPARATORU - Oyuncunun ekranı
-- KATLA butonu, huss huss huss sesi, HUSS! yazıları, para/paket göstergeleri, dükkan menüsü,
-- dükkanını kur (logo, renk, ad) ekranı ve yeni şube.

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
local Gorsel = require(Ortak:WaitForChild("Gorsel"))
local PaketGorsel = require(script.Parent:WaitForChild("PaketGorsel"))
local Musteriler = require(script.Parent:WaitForChild("Musteriler"))
local Uzaklar = ReplicatedStorage:WaitForChild("Uzaklar")
local KatlaOlay = Uzaklar:WaitForChild("Katla")
local SatinAlOlay = Uzaklar:WaitForChild("SatinAl")
local SubeAcOlay = Uzaklar:WaitForChild("SubeAc")
local StilSecOlay = Uzaklar:WaitForChild("StilSec")
local DurumIsteOlay = Uzaklar:WaitForChild("DurumIste")
local EvimeGitOlay = Uzaklar:WaitForChild("EvimeGit")
local DurumOlay = Uzaklar:WaitForChild("Durum")
local KatlandiOlay = Uzaklar:WaitForChild("Katlandi")
local UretimOlay = Uzaklar:WaitForChild("Uretim")
local UnvanOlay = Uzaklar:WaitForChild("YeniUnvan")
local DuyuruOlay = Uzaklar:WaitForChild("Duyuru")

local FONT = Font.fromEnum(Enum.Font.FredokaOne)
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
	s.Volume = seviye or Config.HussSesSeviyesi
	s.TimePosition = 0
	s:Play()
end

-- Başka bir dükkandan gelen 3 boyutlu ses (yakındaysan duyarsın)
local sonUzakSes = {}
local function uzakHuss(parca, anahtar, aralik)
	if not sesAcik or not sesVar or not parca then
		return
	end
	local simdi = os.clock()
	if sonUzakSes[anahtar] and simdi - sonUzakSes[anahtar] < aralik then
		return
	end
	sonUzakSes[anahtar] = simdi
	local s = Instance.new("Sound")
	s.SoundId = Config.HussSesId
	s.Volume = Config.BaskasininSesSeviyesi
	s.RollOffMode = Enum.RollOffMode.InverseTapered
	s.RollOffMinDistance = 10
	s.RollOffMaxDistance = 90
	s.PlaybackSpeed = rastgele:NextNumber(0.95, 1.1)
	s.Parent = parca
	s:Play()
	Debris:AddItem(s, 2)
end

local function arsaParcasi(sira, ad)
	local harita = workspace:FindFirstChild("Harita")
	local arsalar = harita and harita:FindFirstChild("Arsalar")
	local arsa = arsalar and arsalar:FindFirstChild("Arsa" .. tostring(sira))
	local dukkan = arsa and arsa:FindFirstChild("Dukkan")
	return dukkan and dukkan:FindFirstChild(ad, true)
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

local function kose(r)
	return yap("UICorner", { CornerRadius = r })
end

local function cizgi(renk, kalinlik)
	return yap("UIStroke", { Color = renk, Thickness = kalinlik, ApplyStrokeMode = Enum.ApplyStrokeMode.Border })
end

local function yaziCizgisi(renk, kalinlik)
	return yap("UIStroke", { Color = renk, Thickness = kalinlik, ApplyStrokeMode = Enum.ApplyStrokeMode.Contextual })
end

local function yazi(oz)
	local o = {
		BackgroundTransparency = 1,
		FontFace = FONT,
		TextScaled = true,
		TextColor3 = Color3.new(1, 1, 1),
	}
	for k, v in pairs(oz) do
		o[k] = v
	end
	return yap("TextLabel", o)
end

local function buton(oz, cocuklar)
	local o = {
		FontFace = FONT,
		TextScaled = true,
		TextColor3 = Color3.new(1, 1, 1),
		AutoButtonColor = true,
	}
	for k, v in pairs(oz) do
		o[k] = v
	end
	return yap("TextButton", o, cocuklar)
end

local function gecis(nesne, sure, hedef, stil, yon)
	local t = TweenService:Create(nesne, TweenInfo.new(sure, stil or Enum.EasingStyle.Quad, yon or Enum.EasingDirection.Out), hedef)
	t:Play()
	return t
end

local KOYU = Color3.fromRGB(45, 28, 18)
local PANEL = Color3.fromRGB(38, 26, 20)
local SATIR = Color3.fromRGB(62, 45, 35)
local SARI = Color3.fromRGB(255, 214, 20) -- videodaki HUSS! yazısının rengi
local YESIL = Color3.fromRGB(70, 190, 90)
local GRI = Color3.fromRGB(110, 100, 95)

local ekran = yap("ScreenGui", {
	Name = "BurgerArayuz",
	ResetOnSpawn = false,
	ZIndexBehavior = Enum.ZIndexBehavior.Sibling,
	Parent = ben:WaitForChild("PlayerGui"),
})

local olcekler = {}
local function olcekEkle(gui)
	table.insert(olcekler, yap("UIScale", { Parent = gui }))
end
local function olcekGuncelle()
	local kamera = workspace.CurrentCamera
	if not kamera then
		return
	end
	local vp = kamera.ViewportSize
	local s = math.clamp(math.min(vp.X / 1280, vp.Y / 800), 0.6, 1)
	for _, o in ipairs(olcekler) do
		o.Scale = s
	end
end

local klavyeVar = UserInputService.KeyboardEnabled

----------------------------------------------------------------------
-- Üst bölüm: para, paket, makine geliri, unvan
----------------------------------------------------------------------
local ust = yap("Frame", {
	Name = "Ust",
	AnchorPoint = Vector2.new(0.5, 0),
	Position = UDim2.new(0.5, 0, 0, 6),
	Size = UDim2.fromOffset(480, 92),
	BackgroundTransparency = 1,
	Parent = ekran,
})
olcekEkle(ust)

local hapSirasi = yap("Frame", {
	Size = UDim2.fromOffset(480, 44),
	BackgroundTransparency = 1,
	Parent = ust,
}, {
	yap("UIListLayout", {
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Center,
		Padding = UDim.new(0, 8),
		SortOrder = Enum.SortOrder.LayoutOrder,
	}),
})

local function hap(sira, ikon, renk, genislik)
	local f = yap("Frame", {
		LayoutOrder = sira,
		Size = UDim2.fromOffset(genislik, 44),
		BackgroundColor3 = PANEL,
		BackgroundTransparency = 0.12,
		Parent = hapSirasi,
	}, { kose(UDim.new(0, 12)), cizgi(renk, 2) })
	yazi({ Text = ikon, Size = UDim2.fromOffset(34, 44), Position = UDim2.fromOffset(4, 0), Parent = f, TextScaled = false, TextSize = 22 })
	local deger = yazi({
		Text = "...",
		Position = UDim2.fromOffset(40, 4),
		Size = UDim2.new(1, -46, 0, 22),
		TextXAlignment = Enum.TextXAlignment.Left,
		Parent = f,
	})
	local alt = yazi({
		Text = "",
		Position = UDim2.fromOffset(40, 26),
		Size = UDim2.new(1, -46, 0, 13),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = renk,
		Parent = f,
	})
	return deger, alt
end

local paraYazi, paraAlt = hap(1, "💰", Color3.fromRGB(255, 210, 60), 152)
local paketYazi, paketAlt = hap(2, "📦", Color3.fromRGB(255, 140, 50), 152)
local gelirYazi, gelirAlt = hap(3, "⚙️", Color3.fromRGB(90, 220, 110), 152)
paraAlt.Text = "Para"
paketAlt.Text = "Paket"
gelirAlt.Text = "Makineler / sn"

local unvanCubuk = yap("Frame", {
	Position = UDim2.fromOffset(16, 50),
	Size = UDim2.fromOffset(448, 22),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.12,
	ClipsDescendants = true,
	Parent = ust,
}, { kose(UDim.new(0, 10)), cizgi(Color3.fromRGB(255, 215, 0), 1.5) })
local unvanDolgu = yap("Frame", {
	Size = UDim2.fromScale(0, 1),
	BackgroundColor3 = Color3.fromRGB(255, 215, 0),
	BackgroundTransparency = 0.35,
	Parent = unvanCubuk,
}, { kose(UDim.new(0, 10)) })
local unvanYazi = yazi({
	Text = "👑 Çırak",
	Size = UDim2.new(1, -12, 1, -4),
	Position = UDim2.fromOffset(6, 2),
	ZIndex = 2,
	Parent = unvanCubuk,
})
yaziCizgisi(KOYU, 1.5).Parent = unvanYazi

local kayitYazi = yazi({
	Text = "",
	Position = UDim2.fromOffset(16, 75),
	Size = UDim2.fromOffset(448, 14),
	TextColor3 = Color3.fromRGB(255, 205, 205),
	Visible = false,
	Parent = ust,
})
yaziCizgisi(KOYU, 1).Parent = kayitYazi

-- Sağdaki küçük butonlar: ses, stil, dükkanıma dön
local kucukSira = yap("Frame", {
	AnchorPoint = Vector2.new(0, 0),
	Position = UDim2.new(1, 8, 0, 2),
	Size = UDim2.fromOffset(130, 40),
	BackgroundTransparency = 1,
	Parent = ust,
}, {
	yap("UIListLayout", { FillDirection = Enum.FillDirection.Horizontal, Padding = UDim.new(0, 6) }),
})
local function kucukButon(metin)
	return buton({
		Size = UDim2.fromOffset(38, 38),
		BackgroundColor3 = PANEL,
		BackgroundTransparency = 0.12,
		Text = metin,
		TextScaled = false,
		TextSize = 20,
		Parent = kucukSira,
	}, { kose(UDim.new(1, 0)), cizgi(Color3.new(1, 1, 1), 1.5) })
end
local sesButonu = kucukButon("🔊")
sesButonu.Name = "Ses"
local stilButonu = kucukButon("🎨")
stilButonu.Name = "Stil"
local evButonu = kucukButon("🏠")
evButonu.Name = "Ev"

----------------------------------------------------------------------
-- Alt bölüm: KATLA ve DÜKKAN
----------------------------------------------------------------------
local alt = yap("Frame", {
	Name = "Alt",
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -12),
	Size = UDim2.fromOffset(420, 150),
	BackgroundTransparency = 1,
	Parent = ekran,
})
olcekEkle(alt)

local katlaButonu = yap("TextButton", {
	Name = "Katla",
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, 0),
	Size = UDim2.fromOffset(118, 118),
	BackgroundColor3 = Color3.new(1, 1, 1),
	AutoButtonColor = false,
	Text = "",
	Parent = alt,
}, {
	kose(UDim.new(1, 0)),
	cizgi(KOYU, 4),
	yap("UIGradient", { Color = ColorSequence.new(Color3.fromRGB(255, 190, 70), Color3.fromRGB(240, 90, 30)), Rotation = 90 }),
})
local katlaOlcek = yap("UIScale", { Parent = katlaButonu })
yazi({ Text = "🍔", Size = UDim2.new(1, 0, 0.34, 0), Position = UDim2.fromScale(0, 0.12), Parent = katlaButonu })
local katlaYazi = yazi({ Text = "KATLA!", Size = UDim2.new(0.82, 0, 0.24, 0), Position = UDim2.fromScale(0.09, 0.48), Parent = katlaButonu })
yaziCizgisi(KOYU, 2.5).Parent = katlaYazi
yazi({
	Text = "[E]",
	Visible = klavyeVar,
	Size = UDim2.new(1, 0, 0.12, 0),
	Position = UDim2.fromScale(0, 0.75),
	TextColor3 = Color3.fromRGB(255, 240, 200),
	Parent = katlaButonu,
})

-- KATLA'nın üstündeki 3 nokta
local noktaSirasi = yap("Frame", {
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -124),
	Size = UDim2.fromOffset(100, 20),
	BackgroundTransparency = 1,
	Parent = alt,
}, {
	yap("UIListLayout", {
		FillDirection = Enum.FillDirection.Horizontal,
		HorizontalAlignment = Enum.HorizontalAlignment.Center,
		VerticalAlignment = Enum.VerticalAlignment.Center,
		Padding = UDim.new(0, 8),
	}),
})
local noktalar = {}
for i = 1, Config.KatlamaSayisi do
	noktalar[i] = yap("Frame", {
		LayoutOrder = i,
		Size = UDim2.fromOffset(18, 18),
		BackgroundColor3 = PANEL,
		Parent = noktaSirasi,
	}, { kose(UDim.new(1, 0)), cizgi(KOYU, 2) })
end

local dukkanButonu = yap("TextButton", {
	Name = "Dukkan",
	AnchorPoint = Vector2.new(1, 1),
	Position = UDim2.new(0.5, -72, 1, -16),
	Size = UDim2.fromOffset(124, 56),
	BackgroundColor3 = Color3.new(1, 1, 1),
	AutoButtonColor = false,
	Text = "",
	Parent = alt,
}, {
	kose(UDim.new(0, 16)),
	cizgi(KOYU, 3),
	yap("UIGradient", { Color = ColorSequence.new(Color3.fromRGB(120, 180, 255), Color3.fromRGB(50, 100, 230)), Rotation = 90 }),
})
local dukkanOlcek = yap("UIScale", { Parent = dukkanButonu })
local dukkanYazi = yazi({ Text = "🛒 DÜKKAN", Size = UDim2.new(0.9, 0, 0.46, 0), Position = UDim2.fromScale(0.05, 0.1), Parent = dukkanButonu })
yaziCizgisi(KOYU, 2).Parent = dukkanYazi
local dukkanAlt = yazi({
	Text = klavyeVar and "[B]" or "Geliştir!",
	Size = UDim2.new(0.9, 0, 0.3, 0),
	Position = UDim2.fromScale(0.05, 0.6),
	TextColor3 = Color3.fromRGB(255, 255, 235),
	Parent = dukkanButonu,
})
yaziCizgisi(KOYU, 1).Parent = dukkanAlt
-- alınabilecek bir şey varsa DÜKKAN butonunda nokta yanar
local dukkanIsareti = yap("Frame", {
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.new(1, -4, 0, 4),
	Size = UDim2.fromOffset(18, 18),
	BackgroundColor3 = Color3.fromRGB(255, 60, 60),
	Visible = false,
	Parent = dukkanButonu,
}, { kose(UDim.new(1, 0)), cizgi(Color3.new(1, 1, 1), 2) })

local komboYazi = yazi({
	AnchorPoint = Vector2.new(0.5, 1),
	Position = UDim2.new(0.5, 0, 1, -146),
	Size = UDim2.fromOffset(200, 30),
	Text = "",
	TextColor3 = SARI,
	Visible = false,
	ZIndex = 4,
	Parent = alt,
})
yaziCizgisi(KOYU, 2.5).Parent = komboYazi
local komboOlcek = yap("UIScale", { Parent = komboYazi })

----------------------------------------------------------------------
-- Duyurular
----------------------------------------------------------------------
local duyuruAlani = yap("Frame", {
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.fromScale(0.5, 0.3),
	Size = UDim2.fromOffset(560, 76),
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
	local ana = yazi({ Text = metin, Size = UDim2.new(1, 0, 0.6, 0), TextColor3 = renk or Color3.new(1, 1, 1), Parent = f })
	local c1 = yaziCizgisi(KOYU, 3)
	c1.Parent = ana
	local ikinci, c2
	if altMetin then
		ikinci = yazi({ Text = altMetin, Position = UDim2.fromScale(0.1, 0.63), Size = UDim2.new(0.8, 0, 0.32, 0), Parent = f })
		c2 = yaziCizgisi(KOYU, 2)
		c2.Parent = ikinci
	end
	local olcek = yap("UIScale", { Scale = 0.3, Parent = f })
	gecis(olcek, 0.28, { Scale = 1 }, Enum.EasingStyle.Back)
	task.delay(sure or 1.6, function()
		if sonDuyuru ~= f then
			return
		end
		gecis(ana, 0.3, { TextTransparency = 1 })
		gecis(c1, 0.3, { Transparency = 1 })
		if ikinci then
			gecis(ikinci, 0.3, { TextTransparency = 1 })
			gecis(c2, 0.3, { Transparency = 1 })
		end
		task.wait(0.32)
		if sonDuyuru == f then
			sonDuyuru = nil
			f:Destroy()
		end
	end)
end

local function sohbetMesaji(metin)
	pcall(function()
		local kanallar = TextChatService:FindFirstChild("TextChannels")
		local genel = kanallar and kanallar:FindFirstChild("RBXGeneral")
		if genel then
			genel:DisplaySystemMessage(metin)
		end
	end)
end

----------------------------------------------------------------------
-- Uçan yazılar: HUSS! ve +para
----------------------------------------------------------------------
local aktifYazi = 0
local function ucanYazi(metin, renk, buyukluk, x, y)
	if aktifYazi > 22 then
		return
	end
	aktifYazi += 1
	buyukluk = buyukluk or 1
	local l = yazi({
		AnchorPoint = Vector2.new(0.5, 0.5),
		Position = UDim2.new(0.5, x, 1, y),
		Size = UDim2.fromOffset(150 * buyukluk, 42 * buyukluk),
		Text = metin,
		TextColor3 = renk,
		Rotation = rastgele:NextInteger(-14, 14),
		ZIndex = 5,
		Parent = alt,
	})
	local c = yaziCizgisi(KOYU, 3)
	c.Parent = l
	local olcek = yap("UIScale", { Scale = 0.3, Parent = l })
	gecis(olcek, 0.13, { Scale = 1.1 }, Enum.EasingStyle.Back)
	task.delay(0.18, function()
		local sure = 0.45
		gecis(l, sure, { Position = l.Position - UDim2.fromOffset(0, 36), TextTransparency = 1 }, Enum.EasingStyle.Quad, Enum.EasingDirection.In)
		gecis(c, sure, { Transparency = 1 }, Enum.EasingStyle.Quad, Enum.EasingDirection.In)
		task.wait(sure + 0.02)
		l:Destroy()
		aktifYazi -= 1
	end)
end

local function hussYazisi(buyukluk)
	ucanYazi("HUSS!", SARI, buyukluk, rastgele:NextInteger(-170, 170), rastgele:NextInteger(-215, -150))
end

-- Sesteki her "huss" anında bir HUSS! yazısı (videodaki gibi üç kez)
local function hussSerisi(hiz, buyukluk)
	for _, t in ipairs(Config.HussZamanlari) do
		task.delay(t / (hiz or 1), hussYazisi, buyukluk)
	end
end

local function zipla(olcek, kuculme)
	olcek.Scale = kuculme or 0.86
	gecis(olcek, 0.2, { Scale = 1 }, Enum.EasingStyle.Back)
end

local function salla(gui)
	local bas = gui.Position
	task.spawn(function()
		for i = 1, 6 do
			gui.Position = bas + UDim2.fromOffset((i % 2 == 0) and 6 or -6, 0)
			task.wait(0.035)
		end
		gui.Position = bas
	end)
end

----------------------------------------------------------------------
-- Dükkan menüsü (sekmeli)
----------------------------------------------------------------------
local dukkan = yap("Frame", {
	Name = "DukkanPaneli",
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.fromScale(0.5, 0.5),
	Size = UDim2.fromOffset(560, 420),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.04,
	Visible = false,
	ZIndex = 10,
	Parent = ekran,
}, { kose(UDim.new(0, 18)), cizgi(Color3.fromRGB(255, 160, 60), 3) })
olcekEkle(dukkan)

local dukkanBaslik = yazi({
	Text = "🛒 DÜKKAN",
	Position = UDim2.fromOffset(16, 10),
	Size = UDim2.new(1, -80, 0, 34),
	TextXAlignment = Enum.TextXAlignment.Left,
	TextColor3 = Color3.fromRGB(255, 210, 80),
	ZIndex = 11,
	Parent = dukkan,
})
yaziCizgisi(KOYU, 2).Parent = dukkanBaslik
local kapatButonu = buton({
	AnchorPoint = Vector2.new(1, 0),
	Position = UDim2.new(1, -12, 0, 10),
	Size = UDim2.fromOffset(34, 34),
	BackgroundColor3 = Color3.fromRGB(230, 70, 70),
	Text = "✕",
	ZIndex = 11,
	Parent = dukkan,
}, { kose(UDim.new(1, 0)), cizgi(KOYU, 2) })

local sekmeSirasi = yap("Frame", {
	Position = UDim2.fromOffset(16, 52),
	Size = UDim2.new(1, -32, 0, 32),
	BackgroundTransparency = 1,
	ZIndex = 11,
	Parent = dukkan,
}, { yap("UIListLayout", { FillDirection = Enum.FillDirection.Horizontal, Padding = UDim.new(0, 6), SortOrder = Enum.SortOrder.LayoutOrder }) })

local icerik = yap("ScrollingFrame", {
	Position = UDim2.fromOffset(16, 92),
	Size = UDim2.new(1, -32, 1, -104),
	BackgroundTransparency = 1,
	BorderSizePixel = 0,
	ScrollBarThickness = 6,
	CanvasSize = UDim2.new(),
	AutomaticCanvasSize = Enum.AutomaticSize.Y,
	ZIndex = 11,
	Parent = dukkan,
}, { yap("UIListLayout", { Padding = UDim.new(0, 6), SortOrder = Enum.SortOrder.LayoutOrder }) })

local seciliKategori = "Mutfak"
local sekmeler = {}
local satirlar = {}

for i, kat in ipairs(Config.Kategoriler) do
	local s = buton({
		LayoutOrder = i,
		Size = UDim2.fromOffset(100, 32),
		BackgroundColor3 = SATIR,
		Text = kat.Ikon .. " " .. kat.Ad,
		ZIndex = 12,
		Parent = sekmeSirasi,
	}, { kose(UDim.new(0, 10)), yap("UIPadding", { PaddingTop = UDim.new(0, 6), PaddingBottom = UDim.new(0, 6), PaddingLeft = UDim.new(0, 4), PaddingRight = UDim.new(0, 4) }) })
	sekmeler[kat.Anahtar] = s
end

local function satirYap(sira, ikon, ad)
	local satir = yap("Frame", {
		LayoutOrder = sira,
		Size = UDim2.new(1, -8, 0, 62),
		BackgroundColor3 = SATIR,
		ZIndex = 11,
		Parent = icerik,
	}, { kose(UDim.new(0, 12)) })
	yazi({ Text = ikon, Position = UDim2.fromOffset(6, 9), Size = UDim2.fromOffset(44, 44), ZIndex = 12, Parent = satir })
	local baslik = yazi({ Text = ad, Position = UDim2.fromOffset(58, 5), Size = UDim2.new(1, -196, 0, 22), TextXAlignment = Enum.TextXAlignment.Left, ZIndex = 12, Parent = satir })
	local aciklama = yazi({
		Text = "",
		Position = UDim2.fromOffset(58, 28),
		Size = UDim2.new(1, -196, 0, 14),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = Color3.fromRGB(220, 200, 180),
		ZIndex = 12,
		Parent = satir,
	})
	local etki = yazi({
		Text = "",
		Position = UDim2.fromOffset(58, 44),
		Size = UDim2.new(1, -196, 0, 14),
		TextXAlignment = Enum.TextXAlignment.Left,
		TextColor3 = Color3.fromRGB(255, 220, 90),
		ZIndex = 12,
		Parent = satir,
	})
	local al = buton({
		AnchorPoint = Vector2.new(1, 0.5),
		Position = UDim2.new(1, -10, 0.5, 0),
		Size = UDim2.fromOffset(124, 42),
		BackgroundColor3 = YESIL,
		Text = "",
		ZIndex = 12,
		Parent = satir,
	}, { kose(UDim.new(0, 12)), cizgi(KOYU, 2), yap("UIPadding", { PaddingTop = UDim.new(0, 7), PaddingBottom = UDim.new(0, 7), PaddingLeft = UDim.new(0, 6), PaddingRight = UDim.new(0, 6) }) })
	return satir, baslik, aciklama, etki, al
end

for i, g in ipairs(Config.Gelistirmeler) do
	local satir, baslik, aciklama, etki, al = satirYap(i, g.Ikon, g.Ad)
	aciklama.Text = g.Aciklama
	satirlar[g.Anahtar] = { Satir = satir, Baslik = baslik, Etki = etki, Al = al, G = g }
	al.Activated:Connect(function()
		if not durum then
			return
		end
		if not Ekonomi.KilidiAcik(durum.Gelistirme, g.Anahtar) then
			salla(al)
			return
		end
		local fiyat = Ekonomi.Fiyat(g.Anahtar, Ekonomi.Seviye(durum.Gelistirme, g.Anahtar))
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

-- Yeni Şube sekmesi
local subeSatir, subeBaslik, subeAciklama, subeEtki, subeAl = satirYap(100, "🏙️", "Yeni Şube Aç")
subeSatir.Size = UDim2.new(1, -8, 0, 62)
local subeBilgi = yazi({
	LayoutOrder = 101,
	Size = UDim2.new(1, -8, 0, 64),
	Text = "Yeni şube açınca paran ve bütün geliştirmelerin sıfırlanır, dükkanın baştan kurulur. "
		.. "Ama paket sayın ve unvanın kalır, her şube bütün paralarını kalıcı olarak %"
		.. math.floor(Config.SubeBonusu * 100)
		.. " artırır!",
	TextScaled = false,
	TextSize = 15,
	TextWrapped = true,
	TextXAlignment = Enum.TextXAlignment.Left,
	TextColor3 = Color3.fromRGB(230, 215, 195),
	ZIndex = 12,
	Parent = icerik,
})
local subeOnay = 0

local function sekmeSec(anahtar)
	seciliKategori = anahtar
	for k, s in pairs(sekmeler) do
		s.BackgroundColor3 = (k == anahtar) and Color3.fromRGB(240, 130, 40) or SATIR
	end
	for _, s in pairs(satirlar) do
		s.Satir.Visible = s.G.Kategori == anahtar
	end
	subeSatir.Visible = anahtar == "Sube"
	subeBilgi.Visible = anahtar == "Sube"
	icerik.CanvasPosition = Vector2.new(0, 0)
end
for k, s in pairs(sekmeler) do
	s.Activated:Connect(function()
		sekmeSec(k)
	end)
end

local function alinabilirMi(anahtar)
	if not durum or not Ekonomi.KilidiAcik(durum.Gelistirme, anahtar) then
		return false
	end
	local fiyat = Ekonomi.Fiyat(anahtar, Ekonomi.Seviye(durum.Gelistirme, anahtar))
	return fiyat ~= nil and durum.Para >= fiyat
end

local function dukkanGuncelle()
	if not durum then
		return
	end
	local herhangi = false
	for anahtar, s in pairs(satirlar) do
		local g = s.G
		local sv = Ekonomi.Seviye(durum.Gelistirme, anahtar)
		local fiyat = Ekonomi.Fiyat(anahtar, sv)
		local acik = Ekonomi.KilidiAcik(durum.Gelistirme, anahtar)
		s.Baslik.Text = g.MaxSeviye > 1 and string.format("%s  •  Sv %d/%d", g.Ad, sv, g.MaxSeviye) or g.Ad
		if not acik then
			local gerekli = Ekonomi.GelistirmeBul(g.Gerekli)
			s.Etki.Text = "🔒 Önce: " .. (gerekli and gerekli.Ad or "?")
			s.Al.Text = "🔒"
			s.Al.BackgroundColor3 = GRI
		elseif fiyat then
			if g.MaxSeviye > 1 then
				s.Etki.Text = Ekonomi.EtkiYazisi(anahtar, sv) .. "  →  " .. Ekonomi.EtkiYazisi(anahtar, sv + 1)
			else
				s.Etki.Text = Ekonomi.EtkiYazisi(anahtar, sv)
			end
			s.Al.Text = "💰 " .. K(fiyat)
			local yeter = durum.Para >= fiyat
			s.Al.BackgroundColor3 = yeter and YESIL or GRI
			herhangi = herhangi or yeter
		else
			s.Etki.Text = Ekonomi.EtkiYazisi(anahtar, sv)
			s.Al.Text = "✓ TAMAM"
			s.Al.BackgroundColor3 = Color3.fromRGB(200, 150, 40)
		end
	end
	local subeFiyat = Ekonomi.SubeFiyati(durum.Sube)
	subeBaslik.Text = string.format("Yeni Şube Aç  •  Şu an %d. şube", durum.Sube + 1)
	subeAciklama.Text = string.format("Şimdiki para çarpanı x%.1f", Ekonomi.SubeCarpani(durum.Sube))
	subeEtki.Text = string.format("Yeni şubede x%.1f", Ekonomi.SubeCarpani(durum.Sube + 1))
	if subeOnay > os.clock() then
		subeAl.Text = "Emin misin? Bas!"
		subeAl.BackgroundColor3 = Color3.fromRGB(230, 70, 70)
	else
		subeAl.Text = "💰 " .. K(subeFiyat)
		subeAl.BackgroundColor3 = durum.Para >= subeFiyat and YESIL or GRI
	end
	dukkanIsareti.Visible = herhangi
end

subeAl.Activated:Connect(function()
	if not durum then
		return
	end
	local fiyat = Ekonomi.SubeFiyati(durum.Sube)
	if durum.Para < fiyat then
		salla(subeAl)
		duyuru("Yeni şube için 💰 " .. K(fiyat) .. " lazım", Color3.fromRGB(255, 160, 120), 1.6)
		return
	end
	if subeOnay > os.clock() then
		subeOnay = 0
		SubeAcOlay:FireServer()
	else
		subeOnay = os.clock() + 3
		task.delay(3.05, dukkanGuncelle)
	end
	dukkanGuncelle()
end)

local function dukkanAcKapa(ac)
	if ac == nil then
		ac = not dukkan.Visible
	end
	dukkan.Visible = ac
	if ac then
		sekmeSec(seciliKategori)
		dukkanGuncelle()
	end
end

----------------------------------------------------------------------
-- Dükkanını kur: logo, renk, ad
----------------------------------------------------------------------
local stilPanel = yap("Frame", {
	Name = "StilPaneli",
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.fromScale(0.5, 0.5),
	Size = UDim2.fromOffset(560, 440),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.02,
	Visible = false,
	ZIndex = 20,
	Parent = ekran,
}, { kose(UDim.new(0, 18)), cizgi(Color3.fromRGB(255, 200, 60), 3) })
olcekEkle(stilPanel)

local stilBaslik = yazi({
	Text = "🎨 DÜKKANINI KUR",
	Position = UDim2.fromOffset(16, 10),
	Size = UDim2.new(1, -80, 0, 34),
	TextXAlignment = Enum.TextXAlignment.Left,
	TextColor3 = Color3.fromRGB(255, 210, 80),
	ZIndex = 21,
	Parent = stilPanel,
})
yaziCizgisi(KOYU, 2).Parent = stilBaslik
local stilKapat = buton({
	AnchorPoint = Vector2.new(1, 0),
	Position = UDim2.new(1, -12, 0, 10),
	Size = UDim2.fromOffset(34, 34),
	BackgroundColor3 = Color3.fromRGB(230, 70, 70),
	Text = "✕",
	ZIndex = 21,
	Parent = stilPanel,
}, { kose(UDim.new(1, 0)), cizgi(KOYU, 2) })

local function stilEtiket(metin, y)
	yazi({ Text = metin, Position = UDim2.fromOffset(18, y), Size = UDim2.fromOffset(240, 20), TextXAlignment = Enum.TextXAlignment.Left, ZIndex = 21, Parent = stilPanel })
end

local secim = { Logo = 1, Renk = 1, Ad = "" }

stilEtiket("1) Logonu seç", 52)
local logoIzgara = yap("Frame", {
	Position = UDim2.fromOffset(16, 76),
	Size = UDim2.fromOffset(310, 104),
	BackgroundTransparency = 1,
	ZIndex = 21,
	Parent = stilPanel,
}, { yap("UIGridLayout", { CellSize = UDim2.fromOffset(46, 46), CellPadding = UDim2.fromOffset(6, 6), SortOrder = Enum.SortOrder.LayoutOrder }) })
local logoButonlari = {}
for i, logo in ipairs(Config.Logolar) do
	local b = buton({ LayoutOrder = i, BackgroundColor3 = SATIR, Text = logo, TextScaled = false, TextSize = 26, ZIndex = 22, Parent = logoIzgara }, { kose(UDim.new(0, 10)) })
	local c = cizgi(Color3.new(1, 1, 1), 0)
	c.Parent = b
	logoButonlari[i] = { Buton = b, Cizgi = c }
end

stilEtiket("2) Dükkan rengini seç", 188)
local renkIzgara = yap("Frame", {
	Position = UDim2.fromOffset(16, 212),
	Size = UDim2.fromOffset(310, 84),
	BackgroundTransparency = 1,
	ZIndex = 21,
	Parent = stilPanel,
}, { yap("UIGridLayout", { CellSize = UDim2.fromOffset(38, 38), CellPadding = UDim2.fromOffset(8, 8), SortOrder = Enum.SortOrder.LayoutOrder }) })
local renkButonlari = {}
for i, r in ipairs(Config.Renkler) do
	local b = buton({ LayoutOrder = i, BackgroundColor3 = r.Renk, Text = "", ZIndex = 22, Parent = renkIzgara }, { kose(UDim.new(1, 0)) })
	local c = cizgi(Color3.new(1, 1, 1), 0)
	c.Parent = b
	renkButonlari[i] = { Buton = b, Cizgi = c }
end

stilEtiket("3) Dükkanının adı", 304)
local adKutusu = yap("TextBox", {
	Position = UDim2.fromOffset(16, 328),
	Size = UDim2.fromOffset(310, 36),
	BackgroundColor3 = Color3.fromRGB(250, 245, 235),
	TextColor3 = KOYU,
	PlaceholderText = "Örn: Mega Burger",
	PlaceholderColor3 = Color3.fromRGB(150, 140, 130),
	Text = "",
	ClearTextOnFocus = false,
	FontFace = FONT,
	TextScaled = true,
	ZIndex = 22,
	Parent = stilPanel,
}, { kose(UDim.new(0, 10)), yap("UIPadding", { PaddingLeft = UDim.new(0, 8), PaddingRight = UDim.new(0, 8), PaddingTop = UDim.new(0, 6), PaddingBottom = UDim.new(0, 6) }) })

-- Önizleme: tabela
yazi({ Text = "Tabelan:", Position = UDim2.fromOffset(346, 52), Size = UDim2.fromOffset(190, 20), TextXAlignment = Enum.TextXAlignment.Left, ZIndex = 21, Parent = stilPanel })
local onizleme = yap("Frame", {
	Position = UDim2.fromOffset(346, 78),
	Size = UDim2.fromOffset(196, 150),
	BackgroundColor3 = Config.Renkler[1].Renk,
	ZIndex = 21,
	Parent = stilPanel,
}, { kose(UDim.new(0, 14)), cizgi(Color3.new(1, 1, 1), 3) })
local onizlemeLogo = yazi({ Text = Config.Logolar[1], Position = UDim2.fromScale(0.25, 0.06), Size = UDim2.fromScale(0.5, 0.5), ZIndex = 22, Parent = onizleme })
local onizlemeAd = yazi({ Text = "", Position = UDim2.fromScale(0.05, 0.62), Size = UDim2.fromScale(0.9, 0.3), ZIndex = 22, Parent = onizleme })
yaziCizgisi(KOYU, 2).Parent = onizlemeAd

local stilKaydet = buton({
	Position = UDim2.fromOffset(346, 314),
	Size = UDim2.fromOffset(196, 54),
	BackgroundColor3 = YESIL,
	Text = "✅ DÜKKANI AÇ!",
	ZIndex = 22,
	Parent = stilPanel,
}, { kose(UDim.new(0, 14)), cizgi(KOYU, 3), yap("UIPadding", { PaddingTop = UDim.new(0, 10), PaddingBottom = UDim.new(0, 10), PaddingLeft = UDim.new(0, 8), PaddingRight = UDim.new(0, 8) }) })
local stilNot = yazi({
	Text = "Dükkan adını herkes görür, kibar bir isim seç 🙂",
	Position = UDim2.fromOffset(16, 380),
	Size = UDim2.new(1, -32, 0, 18),
	TextColor3 = Color3.fromRGB(220, 200, 180),
	ZIndex = 21,
	Parent = stilPanel,
})

local function varsayilanAd()
	return string.sub(ben.DisplayName, 1, Config.AdEnUzun - 7) .. " Burger"
end

local function stilGoster()
	for i, b in ipairs(logoButonlari) do
		b.Cizgi.Thickness = (i == secim.Logo) and 3 or 0
		b.Buton.BackgroundColor3 = (i == secim.Logo) and Color3.fromRGB(240, 130, 40) or SATIR
	end
	for i, b in ipairs(renkButonlari) do
		b.Cizgi.Thickness = (i == secim.Renk) and 4 or 0
	end
	onizleme.BackgroundColor3 = Config.Renkler[secim.Renk].Renk
	onizlemeLogo.Text = Config.Logolar[secim.Logo]
	local ad = adKutusu.Text ~= "" and adKutusu.Text or varsayilanAd()
	onizlemeAd.Text = ad
end

for i, b in ipairs(logoButonlari) do
	b.Buton.Activated:Connect(function()
		secim.Logo = i
		stilGoster()
	end)
end
for i, b in ipairs(renkButonlari) do
	b.Buton.Activated:Connect(function()
		secim.Renk = i
		stilGoster()
	end)
end
adKutusu:GetPropertyChangedSignal("Text"):Connect(function()
	-- en fazla AdEnUzun harf
	local t = adKutusu.Text
	local n = utf8.len(t)
	if n and n > Config.AdEnUzun then
		adKutusu.Text = string.sub(t, 1, utf8.offset(t, Config.AdEnUzun + 1) - 1)
		return
	end
	stilGoster()
end)

local function stilAcKapa(ac)
	stilPanel.Visible = ac
	if ac then
		dukkanAcKapa(false)
		if durum and durum.Stil then
			secim.Logo = durum.Stil.Logo
			secim.Renk = durum.Stil.Renk
			adKutusu.Text = durum.StilVar and durum.Stil.Ad or ""
		end
		stilKapat.Visible = durum ~= nil and durum.StilVar
		stilGoster()
	end
end

stilKaydet.Activated:Connect(function()
	local ad = adKutusu.Text
	if ad == "" then
		ad = varsayilanAd()
	end
	StilSecOlay:FireServer({ Logo = secim.Logo, Renk = secim.Renk, Ad = ad })
	stilAcKapa(false)
	duyuru(Config.Logolar[secim.Logo] .. " " .. ad, Config.Renkler[secim.Renk].Renk, 2.2, "Dükkanın açıldı! KATLA'ya bas.")
end)
stilKapat.Activated:Connect(function()
	stilAcKapa(false)
end)

----------------------------------------------------------------------
-- Ekranı güncelle
----------------------------------------------------------------------
local function arayuzGuncelle()
	if not durum then
		return
	end
	paraYazi.Text = K(durum.Para)
	paketYazi.Text = K(durum.ToplamPaket)
	local gelir = Ekonomi.MakineGeliri(durum.Gelistirme, durum.Sube)
	gelirYazi.Text = gelir > 0 and ("+" .. K(gelir)) or "-"
	gelirAlt.Text = gelir > 0 and string.format("%d makine / sn", Ekonomi.Seviye(durum.Gelistirme, "Makine")) or "Makine yok"
	paketAlt.Text = durum.Sube > 0 and string.format("Paket • 🏙️ x%.1f", Ekonomi.SubeCarpani(durum.Sube)) or "Paket"
	for i, n in ipairs(noktalar) do
		n.BackgroundColor3 = (i <= durum.Asama) and SARI or PANEL
	end

	local idx = Ekonomi.UnvanIndex(durum.ToplamPaket)
	local u = Config.Unvanlar[idx]
	local sonraki = Config.Unvanlar[idx + 1]
	if sonraki then
		unvanDolgu.Size = UDim2.fromScale(math.clamp((durum.ToplamPaket - u.Esik) / (sonraki.Esik - u.Esik), 0, 1), 1)
		unvanYazi.Text = string.format("👑 %s  •  %s / %s paket → %s", u.Ad, K(durum.ToplamPaket), K(sonraki.Esik), sonraki.Ad)
	else
		unvanDolgu.Size = UDim2.fromScale(1, 1)
		unvanYazi.Text = "👑 " .. u.Ad .. "  •  EN TEPEDESİN!"
	end
	unvanDolgu.BackgroundColor3 = u.Renk
	katlaYazi.Text = durum.ArsaIndex > 0 and "KATLA!" or "BEKLE"

	if durum.KayitAcik then
		kayitYazi.Visible = false
	else
		kayitYazi.Visible = true
		kayitYazi.Text = RunService:IsStudio() and "💾 Studio testinde kayıt kapalı (oyunu yayınlayınca açılır)"
			or "💾 Kayıt yüklenemedi, bu seferki ilerlemen kaydedilmeyecek"
	end
	dukkanGuncelle()
end

----------------------------------------------------------------------
-- Eylemler
----------------------------------------------------------------------
local function katla()
	if not durum then
		return
	end
	if durum.ArsaIndex == 0 then
		duyuru("Bütün arsalar dolu!", Color3.fromRGB(255, 150, 120), 1.5, "Biri çıkınca dükkanın açılacak.")
		return
	end
	local simdi = os.clock()
	if simdi - sonKatlama < KATLAMA_ARALIGI then
		return
	end
	if simdi - sonKatlama < 0.9 then
		kombo += 1
	else
		kombo = 1
	end
	sonKatlama = simdi

	-- ekranı hemen güncelle (sunucunun cevabı gelince düzelir)
	durum.Asama += Ekonomi.TikBasinaKatlama(durum.Gelistirme)
	if durum.Asama >= Config.KatlamaSayisi then
		durum.Asama = 0
		durum.ToplamPaket += 1
	end
	KatlaOlay:FireServer()

	local hiz = 1 + math.min(kombo, 40) * 0.008
	hussCal(hiz)
	hussSerisi(hiz, 1 + math.min(kombo, 30) * 0.01)
	zipla(katlaOlcek, 0.86)
	if kombo >= 3 then
		komboYazi.Visible = true
		komboYazi.Text = "KOMBO x" .. kombo
		zipla(komboOlcek, 1.25)
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
stilButonu.Activated:Connect(function()
	stilAcKapa(not stilPanel.Visible)
end)
evButonu.Activated:Connect(function()
	EvimeGitOlay:FireServer()
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

RunService.RenderStepped:Connect(function()
	if komboYazi.Visible and os.clock() - sonKatlama > 0.9 then
		komboYazi.Visible = false
		kombo = 0
	end
end)

----------------------------------------------------------------------
-- Sunucudan gelenler
----------------------------------------------------------------------
local ilkDurum = true
DurumOlay.OnClientEvent:Connect(function(yeni)
	local eski = durum
	durum = yeni
	if eski then
		for _, g in ipairs(Config.Gelistirmeler) do
			local once = Ekonomi.Seviye(eski.Gelistirme, g.Anahtar)
			local simdi = Ekonomi.Seviye(yeni.Gelistirme, g.Anahtar)
			if simdi > once then
				duyuru(g.Ikon .. " " .. g.Ad .. (g.MaxSeviye > 1 and (" Sv " .. simdi) or "") .. "!", Color3.fromRGB(140, 255, 140), 1.4, Ekonomi.EtkiYazisi(g.Anahtar, simdi))
				local s = satirlar[g.Anahtar]
				if s then
					s.Satir.BackgroundColor3 = Color3.fromRGB(80, 170, 90)
					gecis(s.Satir, 0.5, { BackgroundColor3 = SATIR })
				end
			end
		end
		if yeni.Sube > eski.Sube then
			duyuru("🏙️ " .. (yeni.Sube + 1) .. ". ŞUBE AÇILDI!", SARI, 2.6, string.format("Bütün paralar artık x%.1f", Ekonomi.SubeCarpani(yeni.Sube)))
			hussSerisi(1, 1.4)
		end
	end
	if ilkDurum then
		ilkDurum = false
		if yeni.ArsaIndex > 0 then
			PaketGorsel.Esitle(yeni.ArsaIndex, yeni.Asama, yeni.Altin)
		end
		if not yeni.StilVar then
			stilAcKapa(true)
		elseif yeni.ArsaIndex > 0 then
			duyuru("🍔 BURGER İMPARATORU 👑", Color3.fromRGB(255, 215, 80), 2.4, "KATLA'ya bas: 3 katlamada burger paketlenir!")
		end
	end
	arayuzGuncelle()
end)

KatlandiOlay.OnClientEvent:Connect(function(kim, sira, asama, paketlendi, kazanc, altin, siradakiAltin, otomatik)
	PaketGorsel.Katla(sira, asama, paketlendi, altin, siradakiAltin)
	if otomatik then
		Musteriler.AsciKatladi(sira)
	end
	if kim == ben then
		if otomatik then
			-- aşçı katladı: kısık huss ve küçük yazı
			hussCal(rastgele:NextNumber(0.95, 1.05), Config.HussSesSeviyesi * 0.5)
			ucanYazi("👨‍🍳 HUSS!", SARI, 0.6, rastgele:NextInteger(-170, 170), rastgele:NextInteger(-205, -160))
		end
		if paketlendi then
			ucanYazi("📦 +" .. K(kazanc), Color3.fromRGB(140, 255, 120), 0.85, rastgele:NextInteger(-40, 40), -175)
			if altin then
				duyuru("✨ ALTIN BURGER! ✨", SARI, 1.6, "+" .. K(kazanc) .. " 💰")
			end
		end
	else
		uzakHuss(arsaParcasi(sira, "AnaTezgahUst"), "k" .. tostring(sira), 0.3)
	end
end)

local sonMakineSesi = 0
UretimOlay.OnClientEvent:Connect(function(liste)
	if type(liste) ~= "table" then
		return
	end
	local benimki = durum and durum.ArsaIndex or 0
	for _, u in ipairs(liste) do
		local sira, makine, altin = u[1], u[2], u[3]
		PaketGorsel.Uretim(sira, makine, altin)
		if sira == benimki then
			local simdi = os.clock()
			if simdi - sonMakineSesi > 0.7 then
				sonMakineSesi = simdi
				hussCal(rastgele:NextNumber(1.05, 1.2), Config.MakineSesSeviyesi)
			end
		else
			uzakHuss(arsaParcasi(sira, "Makine" .. tostring(makine)), "m" .. tostring(sira), 1)
		end
	end
end)

UnvanOlay.OnClientEvent:Connect(function(kim, index)
	local u = Config.Unvanlar[index]
	if not u then
		return
	end
	if kim == ben then
		duyuru("👑 YENİ UNVAN: " .. u.Ad .. "!", u.Renk, 2.4, "Burger İmparatorluğuna bir adım daha!")
		hussSerisi(1.1, 1.4)
	end
	if kim then
		sohbetMesaji("👑 " .. kim.DisplayName .. " artık " .. u.Ad .. "!")
	end
end)

DuyuruOlay.OnClientEvent:Connect(function(metin)
	if type(metin) ~= "string" then
		return
	end
	duyuru(metin, Color3.fromRGB(255, 230, 150), 2.4)
	sohbetMesaji(metin)
end)

----------------------------------------------------------------------
-- Başlat
----------------------------------------------------------------------
task.spawn(function()
	Musteriler.Baslat(Config, Gorsel)
end)
PaketGorsel.SatisaGeldi = Musteriler.PaketGeldi
task.spawn(function()
	PaketGorsel.Baslat(Config, Gorsel)
	if durum and durum.ArsaIndex > 0 then
		PaketGorsel.Esitle(durum.ArsaIndex, durum.Asama, durum.Altin)
	end
end)

-- Ekran hazır: sunucudan güncel durumu iste
DurumIsteOlay:FireServer()

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
