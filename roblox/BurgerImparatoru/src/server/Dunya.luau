-- BURGER İMPARATORU - Dünyayı (mutfak, tezgahlar, dev burger heykeli) kodla kurar
-- ve burger katlarını (köfte, peynir, marul...) parça parça yapar.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Config = require(ReplicatedStorage:WaitForChild("Ortak"):WaitForChild("Config"))

local Dunya = {}

local ZEMIN_Y = 0.2 -- karo zeminin üstü
local TEZGAH_YUKSEKLIK = 3.4
local YUKARI = Vector3.new(0, 1, 0)

local TEZGAH_RENKLERI = {
	Color3.fromRGB(255, 99, 99),
	Color3.fromRGB(255, 170, 60),
	Color3.fromRGB(255, 220, 70),
	Color3.fromRGB(120, 210, 110),
	Color3.fromRGB(80, 200, 200),
	Color3.fromRGB(90, 150, 255),
	Color3.fromRGB(170, 120, 255),
	Color3.fromRGB(255, 120, 200),
}

local function parca(ozellikler)
	local p = Instance.new("Part")
	p.Anchored = true
	p.TopSurface = Enum.SurfaceType.Smooth
	p.BottomSurface = Enum.SurfaceType.Smooth
	p.Material = Enum.Material.SmoothPlastic
	for k, v in pairs(ozellikler) do
		if k ~= "Parent" then
			p[k] = v
		end
	end
	if ozellikler.Parent then
		p.Parent = ozellikler.Parent
	end
	return p
end

local function malzemeEnum(ad)
	local ok, deger = pcall(function()
		return Enum.Material[ad]
	end)
	if ok and deger then
		return deger
	end
	return Enum.Material.SmoothPlastic
end

-- Dik duran yuvarlak disk. Roblox'ta silindirin ekseni X yönündedir, o yüzden 90 derece çeviriyoruz.
local function disk(merkez, kalinlik, cap, renk, malzeme)
	return parca({
		Shape = Enum.PartType.Cylinder,
		Size = Vector3.new(kalinlik, cap, cap),
		CFrame = merkez * CFrame.Angles(0, 0, math.rad(90)),
		Color = renk,
		Material = malzeme or Enum.Material.SmoothPlastic,
	})
end

local function burgerParcasiAyarla(p)
	p.CanCollide = false
	p.CanTouch = false
	p.CastShadow = true
end

--[[
	Bir burger katı yapar.
	tur: Config.Malzemeler içinden bir malzeme, Config.AltEkmek ya da Config.UstEkmek
	altCFrame: katın ALT yüzünün ortası (yukarı bakan)
	olcek: büyütme (heykel için 3 gibi)
	Döndürür: parçalar listesi (yerlerine konmuş), katın yüksekliği
]]
function Dunya.KatYap(tur, altCFrame, olcek, rastgele)
	olcek = olcek or 1
	rastgele = rastgele or Random.new()
	local parcalar = {}
	local k = tur.Kalinlik * olcek
	local cap = tur.Cap * olcek
	local donus = CFrame.Angles(0, rastgele:NextNumber(0, math.pi * 2), 0)

	if tur == Config.UstEkmek then
		-- Altta yan yüzü düz bir silindir, üstünde kubbe. Kubbenin alt yarısı silindirin içinde saklı.
		local tabanK = k * 0.45
		local kubbeK = k - tabanK
		table.insert(parcalar, disk(altCFrame * CFrame.new(0, tabanK / 2, 0), tabanK, cap, tur.Renk))
		local kubbe = parca({
			Size = Vector3.new(cap, kubbeK * 2, cap),
			CFrame = altCFrame * CFrame.new(0, tabanK, 0),
			Color = tur.Renk,
		})
		local mesh = Instance.new("SpecialMesh")
		mesh.MeshType = Enum.MeshType.Sphere
		mesh.Parent = kubbe
		table.insert(parcalar, kubbe)
		-- Susamlar
		for i = 1, 9 do
			local aci = rastgele:NextNumber(0, math.pi * 2)
			local f = rastgele:NextNumber(0.15, 0.75)
			local yukseklik = kubbeK * math.sqrt(1 - f * f)
			local x = math.cos(aci) * f * cap / 2
			local z = math.sin(aci) * f * cap / 2
			table.insert(
				parcalar,
				parca({
					Size = Vector3.new(0.32, 0.12, 0.18) * olcek,
					CFrame = altCFrame * CFrame.new(x, tabanK + yukseklik, z) * CFrame.Angles(0, aci, 0),
					Color = Color3.fromRGB(255, 248, 225),
				})
			)
		end
	elseif tur.Sekil == "kare" then
		-- Peynir: köşeleri taşan ince kare dilim
		table.insert(
			parcalar,
			parca({
				Size = Vector3.new(cap * 0.78, k, cap * 0.78),
				CFrame = altCFrame * donus * CFrame.new(0, k / 2, 0),
				Color = tur.Renk,
				Material = malzemeEnum(tur.Malzeme),
			})
		)
	elseif tur.Sekil == "dalga" then
		-- Marul: disk + kenardan sarkan yapraklar
		table.insert(parcalar, disk(altCFrame * CFrame.new(0, k / 2, 0), k, cap * 0.92, tur.Renk, malzemeEnum(tur.Malzeme)))
		local yaprakSayisi = 5
		for i = 1, yaprakSayisi do
			local aci = (i / yaprakSayisi) * math.pi * 2 + rastgele:NextNumber(-0.3, 0.3)
			local yaprak = parca({
				Size = Vector3.new(1.3, k * 0.7, 1.1) * Vector3.new(olcek, 1, olcek),
				CFrame = altCFrame
					* CFrame.Angles(0, aci, 0)
					* CFrame.new(cap * 0.47, k * 0.35, 0)
					* CFrame.Angles(0, 0, math.rad(-18)),
				Color = tur.Renk:Lerp(Color3.new(1, 1, 1), 0.12),
				Material = malzemeEnum(tur.Malzeme),
			})
			table.insert(parcalar, yaprak)
		end
	else
		local p = disk(altCFrame * CFrame.new(0, k / 2, 0), k, cap, tur.Renk, malzemeEnum(tur.Malzeme or "SmoothPlastic"))
		if tur.Altin then
			p.Reflectance = 0.25
			local parilti = Instance.new("Sparkles")
			parilti.SparkleColor = Color3.fromRGB(255, 230, 120)
			parilti.Parent = p
		end
		table.insert(parcalar, p)
	end

	for _, p in ipairs(parcalar) do
		burgerParcasiAyarla(p)
	end
	return parcalar, k
end

local function konfetiYap(tabak)
	local ek = Instance.new("Attachment")
	ek.Name = "KonfetiNoktasi"
	ek.Position = Vector3.new(0, 0, 0)
	ek.Parent = tabak

	local e = Instance.new("ParticleEmitter")
	e.Name = "Konfeti"
	e.Enabled = false
	e.Rate = 0
	e.Lifetime = NumberRange.new(1.4, 2.4)
	e.Speed = NumberRange.new(22, 34)
	e.SpreadAngle = Vector2.new(35, 35)
	e.Acceleration = Vector3.new(0, -35, 0)
	e.Drag = 1.5
	e.Rotation = NumberRange.new(0, 360)
	e.RotSpeed = NumberRange.new(-300, 300)
	e.Size = NumberSequence.new({
		NumberSequenceKeypoint.new(0, 0.5),
		NumberSequenceKeypoint.new(1, 0.25),
	})
	e.Color = ColorSequence.new({
		ColorSequenceKeypoint.new(0, Color3.fromRGB(255, 80, 80)),
		ColorSequenceKeypoint.new(0.25, Color3.fromRGB(255, 210, 60)),
		ColorSequenceKeypoint.new(0.5, Color3.fromRGB(90, 220, 110)),
		ColorSequenceKeypoint.new(0.75, Color3.fromRGB(80, 160, 255)),
		ColorSequenceKeypoint.new(1, Color3.fromRGB(220, 110, 255)),
	})
	e.LightEmission = 0.3
	-- Silindir X ekseninde olduğu için "Right" yönü yukarı bakar
	e.EmissionDirection = Enum.NormalId.Right
	e.Parent = ek
	return e
end

local function tabelaYap(model, cf, renk)
	local tutucu = parca({
		Name = "TabelaNoktasi",
		Size = Vector3.new(1, 1, 1),
		CFrame = cf,
		Transparency = 1,
		CanCollide = false,
		CanQuery = false,
		CanTouch = false,
		Parent = model,
	})
	local bb = Instance.new("BillboardGui")
	bb.Name = "Tabela"
	bb.Size = UDim2.new(9, 0, 2.6, 0)
	bb.MaxDistance = 90
	bb.LightInfluence = 0
	bb.Parent = tutucu

	local cerceve = Instance.new("Frame")
	cerceve.Size = UDim2.fromScale(1, 1)
	cerceve.BackgroundColor3 = Color3.fromRGB(35, 25, 20)
	cerceve.BackgroundTransparency = 0.15
	cerceve.Parent = bb
	local kose = Instance.new("UICorner")
	kose.CornerRadius = UDim.new(0.25, 0)
	kose.Parent = cerceve
	local kenar = Instance.new("UIStroke")
	kenar.Color = renk
	kenar.Thickness = 3
	kenar.Parent = cerceve

	local baslik = Instance.new("TextLabel")
	baslik.Name = "Baslik"
	baslik.BackgroundTransparency = 1
	baslik.Size = UDim2.fromScale(1, 0.55)
	baslik.Font = Enum.Font.FredokaOne
	baslik.TextScaled = true
	baslik.TextColor3 = Color3.new(1, 1, 1)
	baslik.Text = "🍔 Boş Tezgah"
	baslik.Parent = cerceve

	local alt = Instance.new("TextLabel")
	alt.Name = "Alt"
	alt.BackgroundTransparency = 1
	alt.Position = UDim2.fromScale(0, 0.55)
	alt.Size = UDim2.fromScale(1, 0.4)
	alt.Font = Enum.Font.FredokaOne
	alt.TextScaled = true
	alt.TextColor3 = renk
	alt.Text = "Gel burger katla!"
	alt.Parent = cerceve

	return baslik, alt
end

local function tezgahYap(i, adet, klasor)
	local aci = (i - 1) / adet * math.pi * 2
	local disari = Vector3.new(math.cos(aci), 0, math.sin(aci))
	local merkez = disari * Config.TezgahYaricapi
	local renk = TEZGAH_RENKLERI[(i - 1) % #TEZGAH_RENKLERI + 1]

	local model = Instance.new("Model")
	model.Name = "Tezgah" .. i

	-- Tezgahın ön yüzü (LookVector) oyuncunun durduğu yere, yani dışarı bakar
	local tezgahCF = CFrame.lookAt(merkez + YUKARI * (ZEMIN_Y + TEZGAH_YUKSEKLIK / 2), merkez + disari * 10 + YUKARI * (ZEMIN_Y + TEZGAH_YUKSEKLIK / 2))

	parca({
		Name = "Govde",
		Size = Vector3.new(9, TEZGAH_YUKSEKLIK, 6),
		CFrame = tezgahCF,
		Color = Color3.fromRGB(150, 95, 55),
		Material = Enum.Material.WoodPlanks,
		Parent = model,
	})
	-- Önde renkli şerit
	parca({
		Name = "Serit",
		Size = Vector3.new(9.05, 0.7, 0.2),
		CFrame = tezgahCF * CFrame.new(0, 0.6, -3.05),
		Color = renk,
		Parent = model,
	})
	parca({
		Name = "Ust",
		Size = Vector3.new(9.6, 0.3, 6.6),
		CFrame = tezgahCF * CFrame.new(0, TEZGAH_YUKSEKLIK / 2 + 0.15, 0),
		Color = Color3.fromRGB(245, 245, 240),
		Material = Enum.Material.Marble,
		Parent = model,
	})

	local tezgahUstY = ZEMIN_Y + TEZGAH_YUKSEKLIK + 0.3
	local tabakKalinlik = 0.3
	local tabakMerkez = CFrame.new(merkez + YUKARI * (tezgahUstY + tabakKalinlik / 2))
	local tabak = disk(tabakMerkez, tabakKalinlik, 6.6, Color3.fromRGB(255, 255, 255), Enum.Material.SmoothPlastic)
	tabak.Name = "Tabak"
	tabak.Parent = model
	local tabakKenar = disk(tabakMerkez * CFrame.new(0, -0.02, 0), tabakKalinlik * 0.9, 7.2, renk, Enum.Material.SmoothPlastic)
	tabakKenar.Name = "TabakKenar"
	tabakKenar.Parent = model

	local konfeti = konfetiYap(tabak)

	-- Oyuncunun durduğu renkli yuvarlak
	local oyuncuNokta = merkez + disari * 7
	local yer = disk(CFrame.new(oyuncuNokta + YUKARI * (ZEMIN_Y + 0.05)), 0.1, 5, renk, Enum.Material.Neon)
	yer.Name = "OyuncuYeri"
	yer.CanCollide = false
	yer.Transparency = 0.35
	yer.Parent = model

	local baslik, alt = tabelaYap(model, tezgahCF * CFrame.new(0, -0.1, -3.7), renk)

	local katlar = Instance.new("Folder")
	katlar.Name = "Katlar"
	katlar.Parent = model

	model.PrimaryPart = model:FindFirstChild("Govde")
	model.Parent = klasor

	return {
		Index = i,
		Model = model,
		Tabak = tabak,
		Katlar = katlar,
		Konfeti = konfeti,
		Renk = renk,
		-- Burgerin oturduğu yer: tabağın üst yüzü
		TabanCFrame = CFrame.new(merkez + YUKARI * (tezgahUstY + tabakKalinlik)),
		OyuncuCFrame = CFrame.lookAt(oyuncuNokta + YUKARI * (ZEMIN_Y + 3), merkez + YUKARI * (ZEMIN_Y + 3)),
		TabelaBaslik = baslik,
		TabelaAlt = alt,
		Sahip = nil,
	}
end

local function zeminYap(klasor)
	local karo = 8
	local adet = 14
	local bas = -(adet * karo) / 2 + karo / 2
	for x = 0, adet - 1 do
		for z = 0, adet - 1 do
			local kirmizi = (x + z) % 2 == 0
			parca({
				Name = "Karo",
				Size = Vector3.new(karo, ZEMIN_Y, karo),
				CFrame = CFrame.new(bas + x * karo, ZEMIN_Y / 2, bas + z * karo),
				Color = kirmizi and Color3.fromRGB(215, 60, 55) or Color3.fromRGB(250, 242, 225),
				Parent = klasor,
			})
		end
	end
end

local function heykelYap(klasor)
	local model = Instance.new("Model")
	model.Name = "DevBurgerHeykeli"

	local kaideK = 3
	local kaide = disk(CFrame.new(0, ZEMIN_Y + kaideK / 2, 0), kaideK, 18, Color3.fromRGB(245, 240, 235), Enum.Material.Marble)
	kaide.Name = "Kaide"
	kaide.Parent = model
	local serit = disk(CFrame.new(0, ZEMIN_Y + kaideK - 0.3, 0), 0.5, 18.4, Color3.fromRGB(255, 200, 40), Enum.Material.Foil)
	serit.Parent = model

	local olcek = 3
	local rastgele = Random.new(42)
	local cf = CFrame.new(0, ZEMIN_Y + kaideK, 0)
	local function ekle(tur)
		local parcalar, k = Dunya.KatYap(tur, cf, olcek, rastgele)
		for _, p in ipairs(parcalar) do
			p.CanCollide = true
			p.Parent = model
		end
		cf = cf * CFrame.new(0, k, 0)
	end
	local M = {}
	for _, m in ipairs(Config.Malzemeler) do
		M[m.Ad] = m
	end
	ekle(Config.AltEkmek)
	for _, ad in ipairs({ "Köfte", "Peynir", "Marul", "Domates", "Köfte", "Peynir", "Turşu" }) do
		if M[ad] then
			ekle(M[ad])
		end
	end
	ekle(Config.UstEkmek)
	local tepeY = cf.Position.Y

	-- Taç
	local altin = Color3.fromRGB(255, 200, 40)
	local tacY = tepeY - 0.6
	disk(CFrame.new(0, tacY + 0.9, 0), 1.8, 7, altin, Enum.Material.Foil).Parent = model
	for i = 1, 6 do
		local a = i / 6 * math.pi * 2
		local pos = Vector3.new(math.cos(a) * 3.1, tacY + 2.6, math.sin(a) * 3.1)
		parca({
			Size = Vector3.new(1.1, 2.4, 1.1),
			CFrame = CFrame.new(pos) * CFrame.Angles(0, -a, 0) * CFrame.Angles(0, math.rad(45), 0),
			Color = altin,
			Material = Enum.Material.Foil,
			Parent = model,
		})
		parca({
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(0.9, 0.9, 0.9),
			CFrame = CFrame.new(pos + Vector3.new(0, 1.5, 0)),
			Color = i % 2 == 0 and Color3.fromRGB(255, 40, 70) or Color3.fromRGB(40, 140, 255),
			Material = Enum.Material.Neon,
			Parent = model,
		})
	end

	-- Dev yazı
	local tutucu = parca({
		Name = "BaslikNoktasi",
		Size = Vector3.new(1, 1, 1),
		CFrame = CFrame.new(0, tacY + 10, 0),
		Transparency = 1,
		CanCollide = false,
		CanQuery = false,
		CanTouch = false,
		Parent = model,
	})
	local bb = Instance.new("BillboardGui")
	bb.Size = UDim2.new(46, 0, 9, 0)
	bb.MaxDistance = 500
	bb.LightInfluence = 0
	bb.Parent = tutucu
	local yazi = Instance.new("TextLabel")
	yazi.BackgroundTransparency = 1
	yazi.Size = UDim2.fromScale(1, 1)
	yazi.Font = Enum.Font.FredokaOne
	yazi.TextScaled = true
	yazi.Text = "🍔 BURGER İMPARATORU 👑"
	yazi.TextColor3 = Color3.new(1, 1, 1)
	yazi.Parent = bb
	local gradyan = Instance.new("UIGradient")
	gradyan.Color = ColorSequence.new(Color3.fromRGB(255, 230, 90), Color3.fromRGB(255, 120, 40))
	gradyan.Rotation = 90
	gradyan.Parent = yazi
	local kenar = Instance.new("UIStroke")
	kenar.Color = Color3.fromRGB(90, 30, 10)
	kenar.Thickness = 4
	kenar.Parent = yazi

	model.Parent = klasor
end

-- Bütün dünyayı kurar ve tezgah listesini döndürür
function Dunya.Kur()
	local Lighting = game:GetService("Lighting")
	Lighting.ClockTime = 14.5
	Lighting.Brightness = 2.5
	Lighting.OutdoorAmbient = Color3.fromRGB(150, 140, 135)

	local ana = Instance.new("Folder")
	ana.Name = "BurgerDunyasi"

	local zemin = Instance.new("Folder")
	zemin.Name = "Zemin"
	zeminYap(zemin)
	zemin.Parent = ana

	heykelYap(ana)
	ana.Parent = workspace

	local tezgahKlasoru = Instance.new("Folder")
	tezgahKlasoru.Name = "Tezgahlar"
	local tezgahlar = {}
	for i = 1, Config.TezgahSayisi do
		tezgahlar[i] = tezgahYap(i, Config.TezgahSayisi, tezgahKlasoru)
	end
	tezgahKlasoru.Parent = workspace

	local ucanlar = Instance.new("Folder")
	ucanlar.Name = "ServisEdilenler"
	ucanlar.Parent = workspace

	return tezgahlar, ucanlar
end

return Dunya
