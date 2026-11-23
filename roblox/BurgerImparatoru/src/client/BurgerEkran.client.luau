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
local Dil = require(Ortak:WaitForChild("Dil"))
local T = Dil.T

-- Dil: Config.Dil "tr"/"en" ise o, değilse oyuncunun Roblox dili (Türkçe -> Türkçe, diğerleri -> İngilizce)
do
	local kod = Config.Dil
	if kod ~= "tr" and kod ~= "en" then
		local ok, yerel = pcall(function()
			return game:GetService("LocalizationService").RobloxLocaleId
		end)
		if not ok or type(yerel) ~= "string" or yerel == "" then
			ok, yerel = pcall(function()
				return ben.LocaleId
			end)
		end
		kod = (ok and type(yerel) == "string" and yerel ~= "") and yerel or "en"
	end
	Dil.Ayarla(kod)
end
local PaketGorsel = require(script.Parent:WaitForChild("PaketGorsel"))
local Musteriler = require(script.Parent:WaitForChild("Musteriler"))
local KasabaHalki = require(script.Parent:WaitForChild("KasabaHalki"))
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
local GorevAlOlay = Uzaklar:WaitForChild("GorevAl")
local NPCOlay = Uzaklar:WaitForChild("NPC")
local YarismaOlay = Uzaklar:WaitForChild("Yarisma")

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
	o.Text = T(o.Text)
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
	o.Text = T(o.Text)
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
	AutoLocalize = Dil.Kod ~= "tr", -- Türkçe yazılara Roblox'un otomatik çevirisi karışmasın
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
	Size = UDim2.fromOffset(480, 118),
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
paraAlt.Text = T("Para")
paketAlt.Text = T("Paket")
gelirAlt.Text = T("Makineler / sn")

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
	Text = "👑 " .. T("Çırak"),
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
	Text = klavyeVar and "[B]" or T("Geliştir!"),
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
		Text = kat.Ikon .. " " .. T(kat.Ad),
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
	local baslik = yazi({ Text = T(ad), Position = UDim2.fromOffset(58, 5), Size = UDim2.new(1, -196, 0, 22), TextXAlignment = Enum.TextXAlignment.Left, ZIndex = 12, Parent = satir })
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
	aciklama.Text = T(g.Aciklama)
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
			duyuru(T("Paran yetmiyor! 🍔"), Color3.fromRGB(255, 120, 120), 1.2, T("Biraz daha burger paketle."))
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
	Text = T(
		"Yeni şube açınca paran ve bütün geliştirmelerin sıfırlanır, dükkanın baştan kurulur. Ama paket sayın ve unvanın kalır, her şube bütün paralarını kalıcı olarak %%%d artırır!",
		math.floor(Config.SubeBonusu * 100)
	),
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
		s.Baslik.Text = g.MaxSeviye > 1 and T("%s  •  Sv %d/%d", g.Ad, sv, g.MaxSeviye) or T(g.Ad)
		if not acik then
			local gerekli = Ekonomi.GelistirmeBul(g.Gerekli)
			s.Etki.Text = T("🔒 Önce: %s", gerekli and gerekli.Ad or "?")
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
			s.Al.Text = T("✓ TAMAM")
			s.Al.BackgroundColor3 = Color3.fromRGB(200, 150, 40)
		end
	end
	local subeFiyat = Ekonomi.SubeFiyati(durum.Sube)
	subeBaslik.Text = T("Yeni Şube Aç  •  Şu an %d. şube", durum.Sube + 1)
	subeAciklama.Text = T("Şimdiki para çarpanı x%.1f", Ekonomi.SubeCarpani(durum.Sube))
	subeEtki.Text = T("Yeni şubede x%.1f", Ekonomi.SubeCarpani(durum.Sube + 1))
	if subeOnay > os.clock() then
		subeAl.Text = T("Emin misin? Bas!")
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
		duyuru(T("Yeni şube için 💰 %s lazım", K(fiyat)), Color3.fromRGB(255, 160, 120), 1.6)
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
	PlaceholderText = T("Örn: Mega Burger"),
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
	duyuru(Config.Logolar[secim.Logo] .. " " .. ad, Config.Renkler[secim.Renk].Renk, 2.2, T("Dükkanın açıldı! KATLA'ya bas."))
end)
stilKapat.Activated:Connect(function()
	stilAcKapa(false)
end)

----------------------------------------------------------------------
-- Görev takipçisi (sol), Hüsnü Usta penceresi, yarışma ve etkinlik göstergeleri
----------------------------------------------------------------------
local gorevKutusu = yap("Frame", {
	Name = "Gorev",
	AnchorPoint = Vector2.new(0, 0.5),
	Position = UDim2.new(0, 10, 0.42, 0),
	Size = UDim2.fromOffset(230, 92),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.12,
	Visible = false,
	Parent = ekran,
}, { kose(UDim.new(0, 14)), cizgi(Color3.fromRGB(255, 200, 80), 2) })
olcekEkle(gorevKutusu)
local gorevBaslik = yazi({ Text = "📜 Görev", Position = UDim2.fromOffset(10, 6), Size = UDim2.new(1, -20, 0, 18), TextXAlignment = Enum.TextXAlignment.Left, TextColor3 = Color3.fromRGB(255, 215, 90), Parent = gorevKutusu })
local gorevYazi = yazi({ Text = "", Position = UDim2.fromOffset(10, 26), Size = UDim2.new(1, -20, 0, 18), TextXAlignment = Enum.TextXAlignment.Left, Parent = gorevKutusu })
local gorevCubuk = yap("Frame", { Position = UDim2.fromOffset(10, 50), Size = UDim2.new(1, -20, 0, 14), BackgroundColor3 = Color3.fromRGB(70, 55, 45), Parent = gorevKutusu }, { kose(UDim.new(1, 0)) })
local gorevDolgu = yap("Frame", { Size = UDim2.fromScale(0, 1), BackgroundColor3 = YESIL, Parent = gorevCubuk }, { kose(UDim.new(1, 0)) })
local gorevSayi = yazi({ Text = "", Size = UDim2.fromScale(1, 1), ZIndex = 2, TextScaled = false, TextSize = 12, Parent = gorevCubuk })
local gorevOdulYazi = yazi({ Text = "", Position = UDim2.fromOffset(10, 68), Size = UDim2.new(1, -20, 0, 16), TextXAlignment = Enum.TextXAlignment.Left, TextColor3 = Color3.fromRGB(255, 220, 90), Parent = gorevKutusu })
local gorevAlButonu = buton({
	AnchorPoint = Vector2.new(1, 1),
	Position = UDim2.new(1, -8, 1, -6),
	Size = UDim2.fromOffset(104, 26),
	BackgroundColor3 = YESIL,
	Text = "🎁 ÖDÜLÜ AL",
	Visible = false,
	Parent = gorevKutusu,
}, { kose(UDim.new(0, 8)), cizgi(KOYU, 2), yap("UIPadding", { PaddingTop = UDim.new(0, 4), PaddingBottom = UDim.new(0, 4) }) })
gorevAlButonu.Activated:Connect(function()
	GorevAlOlay:FireServer()
end)

-- Hüsnü Usta ile konuşma penceresi
local gorevPenceresi = yap("Frame", {
	Name = "HusnuUsta",
	AnchorPoint = Vector2.new(0.5, 0.5),
	Position = UDim2.fromScale(0.5, 0.55),
	Size = UDim2.fromOffset(440, 210),
	BackgroundColor3 = PANEL,
	BackgroundTransparency = 0.03,
	Visible = false,
	ZIndex = 15,
	Parent = ekran,
}, { kose(UDim.new(0, 18)), cizgi(Color3.fromRGB(255, 200, 80), 3) })
olcekEkle(gorevPenceresi)
yazi({ Text = Config.NPCler.Gorev.Yuz, Position = UDim2.fromOffset(14, 14), Size = UDim2.fromOffset(70, 70), ZIndex = 16, Parent = gorevPenceresi })
yazi({ Text = T(Config.NPCler.Gorev.Ad), Position = UDim2.fromOffset(92, 14), Size = UDim2.new(1, -110, 0, 26), TextXAlignment = Enum.TextXAlignment.Left, TextColor3 = Color3.fromRGB(255, 215, 90), ZIndex = 16, Parent = gorevPenceresi })
local husnuKonusma = yazi({
	Text = "",
	Position = UDim2.fromOffset(92, 44),
	Size = UDim2.new(1, -108, 0, 96),
	TextScaled = false,
	TextSize = 17,
	TextWrapped = true,
	TextXAlignment = Enum.TextXAlignment.Left,
	TextYAlignment = Enum.TextYAlignment.Top,
	ZIndex = 16,
	Parent = gorevPenceresi,
})
local husnuAl = buton({ AnchorPoint = Vector2.new(1, 1), Position = UDim2.new(1, -140, 1, -14), Size = UDim2.fromOffset(150, 42), BackgroundColor3 = YESIL, Text = "🎁 ÖDÜLÜ AL", ZIndex = 16, Parent = gorevPenceresi }, { kose(UDim.new(0, 12)), cizgi(KOYU, 2), yap("UIPadding", { PaddingTop = UDim.new(0, 7), PaddingBottom = UDim.new(0, 7) }) })
local husnuTamam = buton({ AnchorPoint = Vector2.new(1, 1), Position = UDim2.new(1, -14, 1, -14), Size = UDim2.fromOffset(116, 42), BackgroundColor3 = Color3.fromRGB(90, 120, 200), Text = "Tamam", ZIndex = 16, Parent = gorevPenceresi }, { kose(UDim.new(0, 12)), cizgi(KOYU, 2), yap("UIPadding", { PaddingTop = UDim.new(0, 7), PaddingBottom = UDim.new(0, 7) }) })
husnuAl.Activated:Connect(function()
	GorevAlOlay:FireServer()
	gorevPenceresi.Visible = false
end)
husnuTamam.Activated:Connect(function()
	gorevPenceresi.Visible = false
end)

local function gorevGuncelle()
	if not durum or not durum.Gorev then
		return
	end
	local g = durum.Gorev
	local bitti = g.Ilerleme >= g.Hedef
	gorevKutusu.Visible = true
	gorevBaslik.Text = T("📜 Görev %d", g.N) .. (bitti and "  ✅" or "")
	gorevYazi.Text = Ekonomi.GorevYazisi(g.Tip, g.Hedef)
	gorevDolgu.Size = UDim2.fromScale(math.clamp(g.Ilerleme / g.Hedef, 0, 1), 1)
	gorevSayi.Text = K(g.Ilerleme) .. " / " .. K(g.Hedef)
	gorevOdulYazi.Text = bitti and "" or T("Ödül: 💰 %s", K(g.Odul))
	gorevAlButonu.Visible = bitti
	husnuAl.Visible = bitti
	if bitti then
		husnuKonusma.Text = T("Aferin evlat! Görevini bitirdin. 👏\nİşte ödülün: 💰 %s", K(g.Odul))
	else
		husnuKonusma.Text = T(
			"Merhaba evlat! Sana bir görevim var:\n%s (%s/%s)\nBitirirsen sana 💰 %s veririm!",
			Ekonomi.GorevYazisi(g.Tip, g.Hedef),
			K(g.Ilerleme),
			K(g.Hedef),
			K(g.Odul)
		)
	end
end

-- Yarışma göstergesi
local yarismaKutusu = yap("Frame", {
	Name = "Yarisma",
	AnchorPoint = Vector2.new(0.5, 0),
	Position = UDim2.new(0.5, 0, 0, 132),
	Size = UDim2.fromOffset(360, 84),
	BackgroundColor3 = Color3.fromRGB(30, 30, 36),
	BackgroundTransparency = 0.1,
	Visible = false,
	Parent = ekran,
}, { kose(UDim.new(0, 16)), cizgi(SARI, 3) })
olcekEkle(yarismaKutusu)
local yarismaUst = yazi({ Text = "", Position = UDim2.fromOffset(10, 6), Size = UDim2.new(1, -20, 0, 40), TextColor3 = SARI, Parent = yarismaKutusu })
yaziCizgisi(KOYU, 2).Parent = yarismaUst
local yarismaAlt = yazi({ Text = "", Position = UDim2.fromOffset(10, 50), Size = UDim2.new(1, -20, 0, 26), Parent = yarismaKutusu })
local yarisma = nil -- { Baslangic, Bitis, Sayac } (bu ekranın saatine göre)

-- Etkinlik göstergeleri (unvan çubuğunun altında)
local etkinlikSirasi = yap("Frame", {
	Position = UDim2.fromOffset(16, 92),
	Size = UDim2.fromOffset(448, 24),
	BackgroundTransparency = 1,
	Parent = ust,
}, { yap("UIListLayout", { FillDirection = Enum.FillDirection.Horizontal, HorizontalAlignment = Enum.HorizontalAlignment.Center, Padding = UDim.new(0, 6) }) })
local function etkinlikHapi(renk)
	local l = yazi({
		Size = UDim2.fromOffset(0, 24),
		AutomaticSize = Enum.AutomaticSize.X,
		BackgroundTransparency = 0.05,
		BackgroundColor3 = renk,
		TextScaled = false,
		TextSize = 15,
		Visible = false,
		Parent = etkinlikSirasi,
	}, nil)
	kose(UDim.new(0, 10)).Parent = l
	yap("UIPadding", { PaddingLeft = UDim.new(0, 8), PaddingRight = UDim.new(0, 8) }).Parent = l
	yaziCizgisi(KOYU, 1.5).Parent = l
	return l
end
local ikiKatHap = etkinlikHapi(Color3.fromRGB(230, 90, 40))
local altinSaatHap = etkinlikHapi(Color3.fromRGB(220, 170, 20))
local hediyeHap = etkinlikHapi(Color3.fromRGB(200, 50, 80))
local hediyeHazirZamani = 0

local function sureYazi(sn)
	sn = math.max(0, math.floor(sn))
	return string.format("%d:%02d", sn // 60, sn % 60)
end

task.spawn(function()
	while true do
		task.wait(0.2)
		local simdi = workspace:GetServerTimeNow()
		local ikiKat = (ben:GetAttribute("IkiKatBitis") or 0) - simdi
		ikiKatHap.Visible = ikiKat > 0
		if ikiKat > 0 then
			ikiKatHap.Text = T("🔥 2 KAT PARA %s", sureYazi(ikiKat))
		end
		local altinSaat = (ReplicatedStorage:GetAttribute("AltinSaatBitis") or 0) - simdi
		altinSaatHap.Visible = altinSaat > 0
		if altinSaat > 0 then
			altinSaatHap.Text = T("⏰ ALTIN SAAT x2 %s", sureYazi(altinSaat))
		end
		if durum then
			local kalan = hediyeHazirZamani - os.clock()
			hediyeHap.Visible = true
			hediyeHap.Text = kalan > 0 and T("🎁 Hediye %s", sureYazi(kalan)) or T("🎁 Hediye hazır! Meydana gel")
		end
		-- yarışma
		if yarisma then
			local t = os.clock()
			yarismaKutusu.Visible = true
			if t < yarisma.Baslangic then
				yarismaUst.Text = "⏱️ " .. math.ceil(yarisma.Baslangic - t) .. "..."
				yarismaAlt.Text = T("Hazır ol! KATLA'ya hızlı hızlı bas!")
			elseif t < yarisma.Bitis then
				yarismaUst.Text = T("⏱️ %.1f  •  %d TIK", yarisma.Bitis - t, yarisma.Sayac)
				yarismaAlt.Text = "HUSS! HUSS! HUSS!"
			else
				yarismaUst.Text = T("⏱️ BİTTİ  •  %d TIK", yarisma.Sayac)
				yarismaAlt.Text = T("Hakem sayıyor...")
			end
		elseif yarismaKutusu.Visible and not yarismaKutusu:GetAttribute("Sonuc") then
			yarismaKutusu.Visible = false
		end
	end
end)

----------------------------------------------------------------------
-- Ekranı güncelle
----------------------------------------------------------------------
local function arayuzGuncelle()
	if not durum then
		return
	end
	gorevGuncelle()
	paraYazi.Text = K(durum.Para)
	paketYazi.Text = K(durum.ToplamPaket)
	local gelir = Ekonomi.MakineGeliri(durum.Gelistirme, durum.Sube)
	gelirYazi.Text = gelir > 0 and ("+" .. K(gelir)) or "-"
	gelirAlt.Text = gelir > 0 and T("%d makine / sn", Ekonomi.Seviye(durum.Gelistirme, "Makine")) or T("Makine yok")
	paketAlt.Text = durum.Sube > 0 and T("Paket • 🏙️ x%.1f", Ekonomi.SubeCarpani(durum.Sube)) or T("Paket")
	for i, n in ipairs(noktalar) do
		n.BackgroundColor3 = (i <= durum.Asama) and SARI or PANEL
	end

	local idx = Ekonomi.UnvanIndex(durum.ToplamPaket)
	local u = Config.Unvanlar[idx]
	local sonraki = Config.Unvanlar[idx + 1]
	if sonraki then
		unvanDolgu.Size = UDim2.fromScale(math.clamp((durum.ToplamPaket - u.Esik) / (sonraki.Esik - u.Esik), 0, 1), 1)
		unvanYazi.Text = T("👑 %s  •  %s / %s paket → %s", u.Ad, K(durum.ToplamPaket), K(sonraki.Esik), sonraki.Ad)
	else
		unvanDolgu.Size = UDim2.fromScale(1, 1)
		unvanYazi.Text = T("👑 %s  •  EN TEPEDESİN!", u.Ad)
	end
	unvanDolgu.BackgroundColor3 = u.Renk
	katlaYazi.Text = durum.ArsaIndex > 0 and T("KATLA!") or T("BEKLE")

	if durum.KayitAcik then
		kayitYazi.Visible = false
	else
		kayitYazi.Visible = true
		kayitYazi.Text = RunService:IsStudio() and T("💾 Studio testinde kayıt kapalı (oyunu yayınlayınca açılır)")
			or T("💾 Kayıt yüklenemedi, bu seferki ilerlemen kaydedilmeyecek")
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
		duyuru(T("Bütün arsalar dolu!"), Color3.fromRGB(255, 150, 120), 1.5, T("Biri çıkınca dükkanın açılacak."))
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
	if yarisma and simdi >= yarisma.Baslangic and simdi < yarisma.Bitis then
		yarisma.Sayac += 1
	end

	local hiz = 1 + math.min(kombo, 40) * 0.008
	hussCal(hiz)
	hussSerisi(hiz, 1 + math.min(kombo, 30) * 0.01)
	zipla(katlaOlcek, 0.86)
	if kombo >= 3 then
		komboYazi.Visible = true
		komboYazi.Text = T("KOMBO x%d", kombo)
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
	hediyeHazirZamani = os.clock() + (yeni.HediyeKalan or 0)
	if eski then
		for _, g in ipairs(Config.Gelistirmeler) do
			local once = Ekonomi.Seviye(eski.Gelistirme, g.Anahtar)
			local simdi = Ekonomi.Seviye(yeni.Gelistirme, g.Anahtar)
			if simdi > once then
				duyuru(g.Ikon .. " " .. T(g.Ad) .. (g.MaxSeviye > 1 and (" " .. T("Sv") .. " " .. simdi) or "") .. "!", Color3.fromRGB(140, 255, 140), 1.4, Ekonomi.EtkiYazisi(g.Anahtar, simdi))
				local s = satirlar[g.Anahtar]
				if s then
					s.Satir.BackgroundColor3 = Color3.fromRGB(80, 170, 90)
					gecis(s.Satir, 0.5, { BackgroundColor3 = SATIR })
				end
			end
		end
		if yeni.Sube > eski.Sube then
			duyuru(T("🏙️ %d. ŞUBE AÇILDI!", yeni.Sube + 1), SARI, 2.6, T("Bütün paralar artık x%.1f", Ekonomi.SubeCarpani(yeni.Sube)))
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
			duyuru(T("🍔 BURGER İMPARATORU 👑"), Color3.fromRGB(255, 215, 80), 2.4, T("KATLA'ya bas: 3 katlamada burger paketlenir!"))
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
				duyuru(T("✨ ALTIN BURGER! ✨"), SARI, 1.6, "+" .. K(kazanc) .. " 💰")
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
		duyuru(T("👑 YENİ UNVAN: %s!", u.Ad), u.Renk, 2.4, T("Burger İmparatorluğuna bir adım daha!"))
		hussSerisi(1.1, 1.4)
	end
	if kim then
		sohbetMesaji(T("👑 %s artık %s!", kim.DisplayName, u.Ad))
	end
end)

NPCOlay.OnClientEvent:Connect(function(ne)
	if ne == "Gorev" then
		gorevGuncelle()
		gorevPenceresi.Visible = true
	end
end)

YarismaOlay.OnClientEvent:Connect(function(bilgi)
	if type(bilgi) ~= "table" then
		return
	end
	if bilgi.Durum == "basladi" then
		local simdi = os.clock()
		yarisma = { Baslangic = simdi + (bilgi.GeriSayim or 3), Bitis = simdi + (bilgi.GeriSayim or 3) + (bilgi.Sure or 15), Sayac = 0 }
		yarismaKutusu:SetAttribute("Sonuc", false)
		duyuru(T("⏱️ HUSS YARIŞMASI!"), SARI, 2, T("Rekorun: %d tık", bilgi.Rekor or 0))
	elseif bilgi.Durum == "bitti" then
		yarisma = nil
		yarismaKutusu.Visible = true
		yarismaKutusu:SetAttribute("Sonuc", true)
		yarismaUst.Text = T("🏁 %d TIK  •  +%s 💰", bilgi.Sayac or 0, K(bilgi.Odul or 0))
		yarismaAlt.Text = bilgi.YeniRekor and T("🏆 YENİ REKOR!") or T("Rekorun: %d tık", bilgi.Rekor or 0)
		hussSerisi(1.1, 1.3)
		task.delay(3.5, function()
			if not yarisma then
				yarismaKutusu:SetAttribute("Sonuc", false)
				yarismaKutusu.Visible = false
			end
		end)
	end
end)

DuyuruOlay.OnClientEvent:Connect(function(metin, ...)
	if type(metin) ~= "string" then
		return
	end
	local ok, cevrilmis = pcall(T, metin, ...)
	metin = ok and cevrilmis or metin
	duyuru(metin, Color3.fromRGB(255, 230, 150), 2.4)
	sohbetMesaji(metin)
end)

----------------------------------------------------------------------
-- Başlat
----------------------------------------------------------------------
task.spawn(function()
	Dil.DunyayiCevir(workspace) -- haritadaki tabelalar, NPC adları, dükkan yazıları
end)
task.spawn(function()
	Musteriler.Baslat(Config, Gorsel)
end)
task.spawn(function()
	KasabaHalki.Baslat(Config, Gorsel)
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
