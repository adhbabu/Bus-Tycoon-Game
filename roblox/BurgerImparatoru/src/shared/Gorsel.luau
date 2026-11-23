-- BURGER İMPARATORU - Burger ve kutu modelleri
-- İmparator Burger: iki köfte, üç ekmek (alt, orta, susamlı üst), özel sos, marul, peynir, turşu.
-- Karton kutu: tepsi + kapak. 3 katlamada kapanır.
-- Bu modül hiçbir şey "require" etmez; hem oyunda hem harita üreticisinde (Lune) çalışır.

local Gorsel = {}

local EKMEK = Color3.fromRGB(222, 158, 78)
local KOFTE = Color3.fromRGB(98, 56, 34)
local ALTIN = Color3.fromRGB(255, 205, 50)
local KARTON = Color3.fromRGB(236, 220, 186)

local function parca(oz)
	local p = Instance.new("Part")
	p.Anchored = true
	p.CanCollide = false
	p.CanTouch = false
	p.CastShadow = true
	p.TopSurface = Enum.SurfaceType.Smooth
	p.BottomSurface = Enum.SurfaceType.Smooth
	p.Material = Enum.Material.SmoothPlastic
	for k, v in pairs(oz) do
		p[k] = v
	end
	return p
end
Gorsel.Parca = parca

-- Dik duran yuvarlak disk (Roblox silindiri X ekseninde yatar, 90 derece çeviriyoruz)
local function disk(cf, kalinlik, cap, renk, malzeme)
	return parca({
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(kalinlik, cap, cap),
		CFrame = cf * CFrame.Angles(0, 0, math.rad(90)),
		Color = renk,
		Material = malzeme or Enum.Material.SmoothPlastic,
	})
end
Gorsel.Disk = disk

----------------------------------------------------------------------
-- İmparator Burger
----------------------------------------------------------------------
--[[
	cf: burgerin alt yüzünün ortası
	olcek: büyütme (1 = kutuya sığan boy)
	altin: köfteler altın olsun mu
	basit: az parçalı sürüm (makineler ve uzak şeyler için)
	Döndürür: parçalar listesi, toplam yükseklik
]]
function Gorsel.Burger(cf, olcek, altin, basit)
	olcek = olcek or 1
	local parcalar = {}
	local y = 0
	local function ekle(p)
		table.insert(parcalar, p)
		return p
	end
	local function kat(kalinlik, cap, renk, malzeme)
		local k = kalinlik * olcek
		local p = ekle(disk(cf * CFrame.new(0, y + k / 2, 0), k, cap * olcek, renk, malzeme))
		y += k
		return p
	end
	local kofteRenk = altin and ALTIN or KOFTE
	local kofteMalzeme = altin and Enum.Material.Foil or Enum.Material.Slate

	local function marul()
		local k = 0.2 * olcek
		ekle(disk(cf * CFrame.new(0, y + k / 2, 0), k, 4.5 * olcek, Color3.fromRGB(95, 190, 60), Enum.Material.Grass))
		if not basit then
			-- kenardan taşan kıvırcık yapraklar
			for i = 1, 7 do
				local a = i / 7 * math.pi * 2
				ekle(parca({
					Size = Vector3.new(0.9, k * 0.8, 0.7) * Vector3.new(olcek, 1, olcek),
					CFrame = cf * CFrame.new(0, y + k / 2, 0) * CFrame.Angles(0, a, 0) * CFrame.new(2.2 * olcek, 0, 0) * CFrame.Angles(0, 0, math.rad(-15)),
					Color = Color3.fromRGB(115, 205, 75),
					Material = Enum.Material.Grass,
				}))
			end
		end
		y += k
	end
	local function sosVeSogan()
		kat(0.06, 4.1, Color3.fromRGB(242, 168, 118))
		if not basit then
			kat(0.05, 3.8, Color3.fromRGB(246, 240, 226))
		else
			y += 0.05 * olcek
		end
	end
	local function kofte()
		local p = kat(0.42, 4.4, kofteRenk, kofteMalzeme)
		if altin then
			p.Reflectance = 0.2
		end
	end
	local function ekmek(kalinlik)
		kat(kalinlik, 4.5, EKMEK)
	end

	ekmek(0.6) -- alt ekmek
	sosVeSogan()
	marul()
	-- peynir: köşeleri taşan kare dilim
	local pk = 0.1 * olcek
	ekle(parca({
		Size = Vector3.new(3.6 * olcek, pk, 3.6 * olcek),
		CFrame = cf * CFrame.new(0, y + pk / 2, 0) * CFrame.Angles(0, math.rad(45), 0),
		Color = Color3.fromRGB(255, 186, 40),
	}))
	y += pk
	kofte()
	ekmek(0.42) -- orta ekmek
	sosVeSogan()
	marul()
	-- turşular
	local tk = 0.08 * olcek
	if not basit then
		for _, x in ipairs({ -0.9, 0.9 }) do
			ekle(disk(cf * CFrame.new(x * olcek, y + tk / 2, 0.4 * olcek * x), tk, 1.3 * olcek, Color3.fromRGB(120, 150, 45)))
		end
	end
	y += tk
	kofte()

	-- üst ekmek: yan yüzü düz bir taban + kubbe, üstünde susamlar
	local ustK = 1.5 * olcek
	local tabanK = ustK * 0.4
	local kubbeK = ustK - tabanK
	local cap = 4.6 * olcek
	ekle(disk(cf * CFrame.new(0, y + tabanK / 2, 0), tabanK, cap, EKMEK))
	local kubbe = ekle(parca({
		Size = Vector3.new(cap, kubbeK * 2, cap),
		CFrame = cf * CFrame.new(0, y + tabanK, 0),
		Color = EKMEK,
	}))
	local mesh = Instance.new("SpecialMesh")
	mesh.MeshType = Enum.MeshType.Sphere
	mesh.Parent = kubbe
	if not basit then
		local tohum = {
			{ 0.2, 0.3 }, { 0.45, 1.2 }, { 0.5, 2.6 }, { 0.42, 3.9 }, { 0.55, 5.1 }, { 0.7, 0.6 },
			{ 0.72, 1.9 }, { 0.68, 3.3 }, { 0.75, 4.5 }, { 0.3, 2.2 }, { 0.25, 4.4 }, { 0.65, 5.8 },
		}
		for _, t in ipairs(tohum) do
			local f, a = t[1], t[2]
			local h = kubbeK * math.sqrt(1 - f * f)
			ekle(parca({
				Size = Vector3.new(0.34, 0.12, 0.2) * olcek,
				CFrame = cf * CFrame.new(math.cos(a) * f * cap / 2, y + tabanK + h, math.sin(a) * f * cap / 2) * CFrame.Angles(0, a, 0),
				Color = Color3.fromRGB(255, 249, 228),
			}))
		end
	end
	y += ustK
	return parcalar, y
end

----------------------------------------------------------------------
-- Logo etiketi (kutuların, tabelaların üstünde)
----------------------------------------------------------------------
function Gorsel.LogoEtiketi(p, yuz, logo, renk, buyukluk)
	local sg = Instance.new("SurfaceGui")
	sg.Name = "Logo"
	sg.Face = yuz
	sg.LightInfluence = 0.4
	sg.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
	sg.PixelsPerStud = 40
	sg.Parent = p
	local daire = Instance.new("Frame")
	daire.AnchorPoint = Vector2.new(0.5, 0.5)
	daire.Position = UDim2.fromScale(0.5, 0.5)
	daire.Size = UDim2.fromScale(buyukluk or 0.7, buyukluk or 0.7)
	daire.BackgroundColor3 = Color3.new(1, 1, 1)
	daire.Parent = sg
	local oran = Instance.new("UIAspectRatioConstraint")
	oran.Parent = daire
	local kose = Instance.new("UICorner")
	kose.CornerRadius = UDim.new(1, 0)
	kose.Parent = daire
	local cizgi = Instance.new("UIStroke")
	cizgi.Color = renk
	cizgi.Thickness = 4
	cizgi.Parent = daire
	local yazi = Instance.new("TextLabel")
	yazi.BackgroundTransparency = 1
	yazi.Size = UDim2.fromScale(0.86, 0.86)
	yazi.Position = UDim2.fromScale(0.07, 0.07)
	yazi.TextScaled = true
	yazi.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
	yazi.Text = logo
	yazi.Parent = daire
	return sg
end

----------------------------------------------------------------------
-- Karton kutu (tepsi + kapak)
----------------------------------------------------------------------
local S = 5.8 -- kutunun eni ve derinliği
local T = 0.12 -- karton kalınlığı
local TH = 1.6 -- tepsi yüksekliği
local LH = 3.2 -- kapak yüksekliği
Gorsel.KutuEni = S
Gorsel.KutuYuksekligi = TH + LH
Gorsel.KartonKalinligi = T

local DUZ = math.rad(90)
local KAPAK_ACIK = math.rad(110)

-- Kutunun her parçasının, verilen aşamadaki yeri.
-- 0 = karton açık, 1 = tepsinin kenarları kalktı, 2 = kapağın kenarları kalktı, 3 = kapak kapandı
-- taban: kutunun alt yüzünün ortası; -Z yönü kutunun önü (oyuncuya bakar)
function Gorsel.KutuPozu(taban, asama)
	local fi = asama >= 1 and 0 or DUZ
	local beta = asama >= 2 and 0 or DUZ
	local alfa = asama >= 3 and 0 or KAPAK_ACIK
	local K = taban * CFrame.new(0, TH, S / 2) * CFrame.Angles(alfa, 0, 0) -- kapağın menteşesi
	return {
		Taban = taban * CFrame.new(0, T / 2, 0),
		TArka = taban * CFrame.new(0, TH / 2, S / 2 - T / 2),
		TOn = taban * CFrame.new(0, 0, -S / 2) * CFrame.Angles(-fi, 0, 0) * CFrame.new(0, TH / 2, T / 2),
		TSol = taban * CFrame.new(-S / 2, 0, 0) * CFrame.Angles(0, 0, fi) * CFrame.new(T / 2, TH / 2, 0),
		TSag = taban * CFrame.new(S / 2, 0, 0) * CFrame.Angles(0, 0, -fi) * CFrame.new(-T / 2, TH / 2, 0),
		KUst = K * CFrame.new(0, LH - T / 2, -S / 2),
		KArka = K * CFrame.new(0, LH / 2, -T / 2),
		KOn = K * CFrame.new(0, LH, -S) * CFrame.Angles(beta, 0, 0) * CFrame.new(0, -LH / 2, T / 2),
		KSol = K * CFrame.new(-S / 2, LH, -S / 2) * CFrame.Angles(0, 0, -beta) * CFrame.new(T / 2, -LH / 2, 0),
		KSag = K * CFrame.new(S / 2, LH, -S / 2) * CFrame.Angles(0, 0, beta) * CFrame.new(-T / 2, -LH / 2, 0),
	}
end

Gorsel.KutuParcalari = { "Taban", "TArka", "TOn", "TSol", "TSag", "KUst", "KArka", "KOn", "KSol", "KSag" }

-- Açık bir karton kutu yapar (Parent'ı yok). stil = { Renk, Logo, Altin }
function Gorsel.KutuYap(taban, stil)
	local poz = Gorsel.KutuPozu(taban, 0)
	local kapakRengi = stil.Altin and ALTIN or stil.Renk
	local boyut = {
		Taban = Vector3.new(S, T, S),
		TArka = Vector3.new(S, TH, T),
		TOn = Vector3.new(S, TH, T),
		TSol = Vector3.new(T, TH, S - 2 * T),
		TSag = Vector3.new(T, TH, S - 2 * T),
		KUst = Vector3.new(S, T, S),
		KArka = Vector3.new(S, LH, T),
		KOn = Vector3.new(S, LH, T),
		KSol = Vector3.new(T, LH, S - 2 * T),
		KSag = Vector3.new(T, LH, S - 2 * T),
	}
	local kutu = {}
	for _, ad in ipairs(Gorsel.KutuParcalari) do
		local kapak = string.sub(ad, 1, 1) == "K"
		kutu[ad] = parca({
			Name = "Kutu" .. ad,
			Size = boyut[ad],
			CFrame = poz[ad],
			Color = kapak and kapakRengi or KARTON,
			Material = (kapak and stil.Altin) and Enum.Material.Foil or Enum.Material.Cardboard,
		})
	end
	Gorsel.LogoEtiketi(kutu.KUst, Enum.NormalId.Top, stil.Logo, kapakRengi, 0.75)
	Gorsel.LogoEtiketi(kutu.KOn, Enum.NormalId.Front, stil.Logo, kapakRengi, 0.8)
	return kutu
end

-- Kapalı paket (bantta giden kutu). cf: alt yüzünün ortası. Döndürür: parçalar listesi
function Gorsel.KapaliKutu(cf, stil, olcek)
	olcek = olcek or 1
	local kapakRengi = stil.Altin and ALTIN or stil.Renk
	local tepsi = parca({
		Name = "PaketTepsi",
		Size = Vector3.new(S, TH, S) * olcek,
		CFrame = cf * CFrame.new(0, TH * olcek / 2, 0),
		Color = KARTON,
		Material = Enum.Material.Cardboard,
	})
	local kapak = parca({
		Name = "PaketKapak",
		Size = Vector3.new(S, LH, S) * olcek,
		CFrame = cf * CFrame.new(0, (TH + LH / 2) * olcek, 0),
		Color = kapakRengi,
		Material = stil.Altin and Enum.Material.Foil or Enum.Material.Cardboard,
	})
	Gorsel.LogoEtiketi(kapak, Enum.NormalId.Top, stil.Logo, kapakRengi, 0.75)
	Gorsel.LogoEtiketi(kapak, Enum.NormalId.Front, stil.Logo, kapakRengi, 0.8)
	return { tepsi, kapak }
end

return Gorsel
