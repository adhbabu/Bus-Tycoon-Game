-- BURGER İMPARATORU - Oyuncunun dükkanını arsasına kurar
-- Geliştirme aldıkça dükkan büyür: tezgah ve bant -> makineler -> duvarlar -> çatı ve neon tabela
-- -> masalar -> çiçek ve ışıklar -> çatıda dev burger.

local ReplicatedStorage = game:GetService("ReplicatedStorage")
local Ortak = ReplicatedStorage:WaitForChild("Ortak")
local Config = require(Ortak:WaitForChild("Config"))
local Gorsel = require(Ortak:WaitForChild("Gorsel"))
local Ekonomi = require(Ortak:WaitForChild("Ekonomi"))

local Dukkan = {}

local Y = Config.Yerlesim
local TEPE = Y.TezgahYuksekligi
local METAL = Color3.fromRGB(150, 155, 165)
local KOYU_METAL = Color3.fromRGB(70, 72, 80)
local BANT = Color3.fromRGB(45, 45, 50)
local AHSAP = Color3.fromRGB(150, 98, 58)
local BEYAZ = Color3.fromRGB(245, 245, 240)

-- Arsanın üst yüzünün ortası (-Z = yola bakan ön)
function Dukkan.Cerceve(arsa)
	local taban = arsa:FindFirstChild("Taban")
	return taban.CFrame * CFrame.new(0, taban.Size.Y / 2, 0)
end

local function yazi(ebeveyn, metin, renk, oz)
	local l = Instance.new("TextLabel")
	l.BackgroundTransparency = 1
	l.Size = UDim2.fromScale(1, 1)
	l.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
	l.TextScaled = true
	l.Text = metin
	l.TextColor3 = renk or Color3.new(1, 1, 1)
	for k, v in pairs(oz or {}) do
		l[k] = v
	end
	l.Parent = ebeveyn
	return l
end

function Dukkan.Kur(arsa, veri)
	local eski = arsa:FindFirstChild("Dukkan")
	if eski then
		eski:Destroy()
	end
	local klasor = Instance.new("Folder")
	klasor.Name = "Dukkan"
	local F = Dukkan.Cerceve(arsa)

	local function blok(ad, boyut, merkez, renk, malzeme, oz)
		local p = Gorsel.Parca({
			Name = ad,
			Size = boyut,
			CFrame = F * merkez,
			Color = renk,
			Material = malzeme or Enum.Material.SmoothPlastic,
			CanCollide = true,
		})
		for k, v in pairs(oz or {}) do
			p[k] = v
		end
		p.Parent = klasor
		return p
	end
	local function dikDisk(ad, kalinlik, cap, merkez, renk, malzeme)
		local p = Gorsel.Disk(F * merkez, kalinlik, cap, renk, malzeme)
		p.Name = ad
		p.CanCollide = true
		p.Parent = klasor
		return p
	end
	local function tabelaYazisi(p, yuz, ust, alt, arkaRenk, parlak)
		local sg = Instance.new("SurfaceGui")
		sg.Face = yuz
		sg.LightInfluence = parlak and 0 or 0.5
		sg.Brightness = parlak and 2 or 1
		sg.SizingMode = Enum.SurfaceGuiSizingMode.PixelsPerStud
		sg.PixelsPerStud = 30
		sg.Parent = p
		local f = Instance.new("Frame")
		f.Size = UDim2.fromScale(1, 1)
		f.BackgroundColor3 = arkaRenk
		f.Parent = sg
		yazi(f, ust, Color3.new(1, 1, 1), { Size = UDim2.fromScale(0.94, 0.62), Position = UDim2.fromScale(0.03, 0.04) })
		if alt then
			yazi(f, alt, Color3.fromRGB(255, 240, 200), { Size = UDim2.fromScale(0.9, 0.28), Position = UDim2.fromScale(0.05, 0.68) })
		end
		local cizgi = Instance.new("UIStroke")
		cizgi.Color = Color3.new(1, 1, 1)
		cizgi.Thickness = 6
		cizgi.ApplyStrokeMode = Enum.ApplyStrokeMode.Border
		cizgi.Parent = f
		return sg
	end

	if not veri then
		-- Boş arsa: küçük bir tabela
		arsa:SetAttribute("Sahip", 0)
		arsa:SetAttribute("Makine", 0)
		arsa:SetAttribute("Un", 1)
		arsa:SetAttribute("Oturma", false)
		local p = Y.TabelaYeri
		for _, x in ipairs({ -3.5, 3.5 }) do
			blok("Direk", Vector3.new(0.5, 6, 0.5), CFrame.new(p.X + x, 3, p.Z), AHSAP, Enum.Material.Wood)
		end
		local pano = blok("Pano", Vector3.new(9, 3.5, 0.4), CFrame.new(p.X, 5.5, p.Z), Color3.fromRGB(90, 90, 95), Enum.Material.Wood)
		tabelaYazisi(pano, Enum.NormalId.Front, "🍔 BOŞ ARSA", "Bir oyuncu bekliyor", Color3.fromRGB(90, 90, 95))
		tabelaYazisi(pano, Enum.NormalId.Back, "🍔 BOŞ ARSA", "Bir oyuncu bekliyor", Color3.fromRGB(90, 90, 95))
		klasor.Parent = arsa
		Dukkan.ButonlariKur(arsa, nil)
		return
	end

	local stil = veri.Stil
	local renk = stil.Renk
	local acik = renk:Lerp(Color3.new(1, 1, 1), 0.6)
	local g = veri.Gelistirme
	local function var(anahtar)
		return (g[anahtar] or 0) > 0
	end
	local makineSayisi = math.clamp(g.Makine or 0, 0, #Y.MakineX)

	arsa:SetAttribute("Sahip", veri.UserId)
	arsa:SetAttribute("Renk", renk)
	arsa:SetAttribute("Logo", stil.Logo)
	arsa:SetAttribute("Ad", stil.Ad)
	arsa:SetAttribute("Makine", makineSayisi)
	arsa:SetAttribute("Un", Ekonomi.UnCarpani(g))
	arsa:SetAttribute("Oturma", var("Oturma"))

	local bm, bb = Y.BinaMerkez, Y.BinaBoyu
	local onZ = bm.Z - bb.Z / 2 -- binanın ön duvarı
	local arkaZ = bm.Z + bb.Z / 2
	local solX, sagX = bm.X - bb.X / 2, bm.X + bb.X / 2

	-- Zemin (dükkan renginde, kenarı beyaz)
	blok("Zemin", Vector3.new(bb.X, 0.3, bb.Z), CFrame.new(bm.X, 0.15, bm.Z), acik, Enum.Material.CeramicTiles)
	blok("ZeminKenar", Vector3.new(bb.X + 1, 0.2, bb.Z + 1), CFrame.new(bm.X, 0.1, bm.Z), BEYAZ, Enum.Material.SmoothPlastic)
	-- Önde kaldırım taşı (oyuncunun durduğu yer)
	blok("OnAvlu", Vector3.new(bb.X, 0.25, 15), CFrame.new(0, 0.125, onZ - 7.5), Color3.fromRGB(215, 210, 200), Enum.Material.Pavement)

	-- Ana tezgah (KATLA kutusu burada)
	local at = Y.AnaTezgah
	blok("AnaTezgahGovde", Vector3.new(10, TEPE - 0.3, 5), CFrame.new(at.X, (TEPE - 0.3) / 2, at.Z), AHSAP, Enum.Material.WoodPlanks)
	blok("AnaTezgahSerit", Vector3.new(10.05, 0.7, 0.2), CFrame.new(at.X, 1.6, at.Z - 2.55), renk)
	blok("AnaTezgahUst", Vector3.new(10.6, 0.3, 5.6), CFrame.new(at.X, TEPE - 0.15, at.Z), BEYAZ, Enum.Material.Marble)
	-- Oyuncunun durduğu renkli halka
	local oy = Y.OyuncuYeri
	local yer = dikDisk("OyuncuYeri", 0.1, 5, CFrame.new(oy.X, 0.3, oy.Z), renk, Enum.Material.Neon)
	yer.CanCollide = false
	yer.Transparency = 0.4

	-- Teslim bandı: ana tezgahtan satış penceresine
	local s = Y.SatisNoktasi
	local bantBas, bantSon = at.X + 5, s.X - 3
	local bantOrta, bantBoy = (bantBas + bantSon) / 2, bantSon - bantBas
	blok("TeslimBandiGovde", Vector3.new(bantBoy, TEPE - 0.3, 3.4), CFrame.new(bantOrta, (TEPE - 0.3) / 2, at.Z), METAL, Enum.Material.Metal)
	blok("TeslimBandi", Vector3.new(bantBoy, 0.3, 3), CFrame.new(bantOrta, TEPE - 0.15, at.Z), BANT, Enum.Material.Fabric)

	-- Satış penceresi tezgahı + tente
	blok("SatisGovde", Vector3.new(6, TEPE - 0.3, 4), CFrame.new(s.X, (TEPE - 0.3) / 2, s.Z), AHSAP, Enum.Material.WoodPlanks)
	blok("SatisUst", Vector3.new(6.6, 0.3, 4.6), CFrame.new(s.X, TEPE - 0.15, s.Z), BEYAZ, Enum.Material.Marble)
	for _, x in ipairs({ -2.9, 2.9 }) do
		blok("TenteDirek", Vector3.new(0.3, 5, 0.3), CFrame.new(s.X + x, TEPE + 2.5, s.Z - 2.1), BEYAZ, Enum.Material.Metal)
	end
	local tente = blok("Tente", Vector3.new(7, 0.3, 5), CFrame.new(s.X, TEPE + 5.2, s.Z) * CFrame.Angles(math.rad(-12), 0, 0), renk)
	tabelaYazisi(tente, Enum.NormalId.Front, "SATIŞ", nil, renk)

	-- Ön tabela: logo + dükkan adı (iki yüzü de yazılı)
	local tp = Y.TabelaYeri
	for _, x in ipairs({ -8, 8 }) do
		blok("TabelaDirek", Vector3.new(0.7, 11.5, 0.7), CFrame.new(tp.X + x, 5.75, tp.Z), KOYU_METAL, Enum.Material.Metal)
	end
	local pano = blok("Tabela", Vector3.new(18, 6, 0.6), CFrame.new(tp.X, 9, tp.Z), renk)
	blok("TabelaCerceve", Vector3.new(18.8, 6.8, 0.4), CFrame.new(tp.X, 9, tp.Z), BEYAZ, Enum.Material.SmoothPlastic)
	local ust = stil.Logo .. " " .. stil.Ad
	local alt = "Sahibi: " .. veri.SahipAdi
	tabelaYazisi(pano, Enum.NormalId.Front, ust, alt, renk)
	tabelaYazisi(pano, Enum.NormalId.Back, ust, alt, renk)

	-- Paketleme makineleri
	for i = 1, makineSayisi do
		local x = Y.MakineX[i]
		local z = Y.MakineZ
		local m = Instance.new("Model")
		m.Name = "Makine" .. i
		local function mb(ad, boyut, merkez, rnk, mal)
			local p = blok(ad, boyut, merkez, rnk, mal)
			p.Parent = m
			return p
		end
		mb("Govde", Vector3.new(5, TEPE - 0.3, 4.4), CFrame.new(x, (TEPE - 0.3) / 2, z), METAL, Enum.Material.Metal)
		mb("Serit", Vector3.new(5.05, 0.6, 4.45), CFrame.new(x, 1.4, z), renk)
		mb("Platform", Vector3.new(5.4, 0.3, 4.8), CFrame.new(x, TEPE - 0.15, z), KOYU_METAL, Enum.Material.DiamondPlate)
		for _, dx in ipairs({ -2.5, 2.5 }) do
			mb("PresDirek", Vector3.new(0.5, 5.4, 0.5), CFrame.new(x + dx, TEPE + 2.7, z + 1.8), KOYU_METAL, Enum.Material.Metal)
		end
		mb("PresUst", Vector3.new(5.5, 0.7, 0.7), CFrame.new(x, TEPE + 5.4, z + 1.8), KOYU_METAL, Enum.Material.Metal)
		local kol = mb("Kol", Vector3.new(3.4, 0.7, 3.4), CFrame.new(x, TEPE + 4.4, z - 0.2), renk, Enum.Material.Metal)
		kol.CanCollide = false
		local isik = mb("Isik", Vector3.new(0.8, 0.8, 0.8), CFrame.new(x, TEPE + 6.1, z + 1.8), Color3.fromRGB(90, 255, 120), Enum.Material.Neon)
		isik.Shape = Enum.PartType.Ball
		-- makineden ana banda kısa bant
		local bas, son = z - 2.4, Y.BantZ + 1.5
		mb("KisaBant", Vector3.new(3, 0.3, bas - son), CFrame.new(x, TEPE - 0.15, (bas + son) / 2), BANT, Enum.Material.Fabric)
		mb("KisaBantGovde", Vector3.new(3.2, TEPE - 0.3, bas - son), CFrame.new(x, (TEPE - 0.3) / 2, (bas + son) / 2), METAL, Enum.Material.Metal)
		m.Parent = klasor
	end

	-- Ana bant (ilk makineyle gelir): makinelerin önünden satış penceresine
	if makineSayisi > 0 then
		local bx, bz = Y.BantX, Y.BantZ
		local x0 = Y.MakineX[1] - 3
		local yatayBoy = bx + 1.5 - x0
		blok("AnaBantGovde", Vector3.new(yatayBoy, TEPE - 0.3, 3.2), CFrame.new(x0 + yatayBoy / 2, (TEPE - 0.3) / 2, bz), METAL, Enum.Material.Metal)
		blok("AnaBant", Vector3.new(yatayBoy, 0.3, 3), CFrame.new(x0 + yatayBoy / 2, TEPE - 0.15, bz), BANT, Enum.Material.Fabric)
		local z1 = s.Z + 2.3
		local dikeyBoy = bz - 1.5 - z1
		blok("DonusBantGovde", Vector3.new(3.2, TEPE - 0.3, dikeyBoy), CFrame.new(bx, (TEPE - 0.3) / 2, z1 + dikeyBoy / 2), METAL, Enum.Material.Metal)
		blok("DonusBant", Vector3.new(3, 0.3, dikeyBoy), CFrame.new(bx, TEPE - 0.15, z1 + dikeyBoy / 2), BANT, Enum.Material.Fabric)
	end

	-- Duvarlar ve camlar
	if var("Duvar") then
		local H = bb.Y
		local CAM_UST = H - 4 -- camların üst kenarı
		local duvarRenk = renk:Lerp(Color3.new(1, 1, 1), 0.35)
		local cam = Color3.fromRGB(170, 215, 240)
		local function duvar(x0, x1, z, y0, y1)
			if x1 - x0 < 0.05 or y1 - y0 < 0.05 then
				return
			end
			blok("Duvar", Vector3.new(x1 - x0, y1 - y0, 1), CFrame.new((x0 + x1) / 2, (y0 + y1) / 2, z), duvarRenk, Enum.Material.Brick)
		end
		local function camPanel(x0, x1, z, y0, y1)
			blok("Cam", Vector3.new(x1 - x0, y1 - y0, 0.3), CFrame.new((x0 + x1) / 2, (y0 + y1) / 2, z), cam, Enum.Material.Glass, {
				Transparency = 0.55,
				Reflectance = 0.15,
			})
			-- beyaz cam çerçeveleri (dışarıdan bakınca vitrin gibi)
			local adet = math.max(1, math.floor((x1 - x0) / 6 + 0.5))
			for i = 1, adet - 1 do
				local x = x0 + (x1 - x0) * i / adet
				blok("CamCerceve", Vector3.new(0.35, y1 - y0, 0.5), CFrame.new(x, (y0 + y1) / 2, z - 0.1), BEYAZ, Enum.Material.SmoothPlastic)
			end
			blok("CamCerceve", Vector3.new(x1 - x0, 0.35, 0.5), CFrame.new((x0 + x1) / 2, y1, z - 0.1), BEYAZ, Enum.Material.SmoothPlastic)
			blok("CamCerceve", Vector3.new(x1 - x0, 0.35, 0.5), CFrame.new((x0 + x1) / 2, y0, z - 0.1), BEYAZ, Enum.Material.SmoothPlastic)
		end
		-- ön duvar: [sol cam] [kapı] [orta] [satış penceresi] [sağ cam]
		local fz = onZ + 0.5
		local kapi0, kapi1 = -3, 3
		local pen0, pen1 = Y.BantX - 2, Y.BantX + 2
		-- alt bant (cam altı) ve üst bant (cam üstü) ile camlı bölümler
		local function camliBolum(x0, x1)
			duvar(x0, x1, fz, 0, 3)
			camPanel(x0, x1, fz, 3, CAM_UST)
			duvar(x0, x1, fz, CAM_UST, H)
		end
		duvar(solX, solX + 2, fz, 0, H)
		camliBolum(solX + 2, kapi0 - 1)
		duvar(kapi0 - 1, kapi0, fz, 0, H)
		duvar(kapi0, kapi1, fz, 9, H) -- kapının üstü
		duvar(kapi1, kapi1 + 1, fz, 0, H)
		camliBolum(kapi1 + 1, pen0)
		duvar(pen0, pen1, fz, 0, TEPE - 0.3) -- pencerenin altı
		duvar(pen0, pen1, fz, 7.5, H) -- pencerenin üstü
		camliBolum(pen1, sagX - 2)
		duvar(sagX - 2, sagX, fz, 0, H)
		-- arka duvar
		blok("Duvar", Vector3.new(bb.X, H, 1), CFrame.new(bm.X, H / 2, arkaZ - 0.5), duvarRenk, Enum.Material.Brick)
		-- yan duvarlar (ortası camlı)
		for _, x in ipairs({ solX + 0.5, sagX - 0.5 }) do
			local z0, z1 = onZ + 1, arkaZ - 1
			local c0, c1 = onZ + 7, arkaZ - 7
			blok("Duvar", Vector3.new(1, H, c0 - z0), CFrame.new(x, H / 2, (z0 + c0) / 2), duvarRenk, Enum.Material.Brick)
			blok("Duvar", Vector3.new(1, H, z1 - c1), CFrame.new(x, H / 2, (c1 + z1) / 2), duvarRenk, Enum.Material.Brick)
			blok("Duvar", Vector3.new(1, 3, c1 - c0), CFrame.new(x, 1.5, (c0 + c1) / 2), duvarRenk, Enum.Material.Brick)
			blok("Duvar", Vector3.new(1, H - CAM_UST, c1 - c0), CFrame.new(x, (CAM_UST + H) / 2, (c0 + c1) / 2), duvarRenk, Enum.Material.Brick)
			blok("Cam", Vector3.new(0.3, CAM_UST - 3, c1 - c0), CFrame.new(x, (3 + CAM_UST) / 2, (c0 + c1) / 2), cam, Enum.Material.Glass, { Transparency = 0.55, Reflectance = 0.15 })
		end
		-- beyaz üst şerit
		blok("UstSerit", Vector3.new(bb.X + 0.6, 0.8, 1.4), CFrame.new(bm.X, H - 0.4, fz), BEYAZ)
		-- içeride, arka duvarda menü panosu (logo ve menü: her dilde aynı)
		local menu = blok("MenuPano", Vector3.new(26, 6, 0.4), CFrame.new(bm.X, H - 4.5, arkaZ - 1.2), Color3.fromRGB(45, 38, 36), Enum.Material.SmoothPlastic)
		tabelaYazisi(menu, Enum.NormalId.Front, stil.Logo .. "  🍔  🍟  🥤  " .. stil.Logo, nil, Color3.fromRGB(45, 38, 36), true)
		blok("MenuCerceve", Vector3.new(27, 7, 0.3), CFrame.new(bm.X, H - 4.5, arkaZ - 1.05), renk, Enum.Material.SmoothPlastic)
	end

	-- Çatı ve neon tabela
	if var("Cati") then
		local H = bb.Y
		blok("Cati", Vector3.new(bb.X + 2, 1, bb.Z + 2), CFrame.new(bm.X, H + 0.5, bm.Z), Color3.fromRGB(90, 90, 98), Enum.Material.Concrete)
		blok("CatiKenar", Vector3.new(bb.X + 2.4, 1.4, 0.6), CFrame.new(bm.X, H + 1.2, onZ - 1), renk)
		-- çatının yan ve arka kenarları (bina daha toplu görünsün)
		blok("CatiKenar", Vector3.new(bb.X + 2.4, 1.4, 0.6), CFrame.new(bm.X, H + 1.2, arkaZ + 1), renk)
		blok("CatiKenar", Vector3.new(0.6, 1.4, bb.Z + 2.6), CFrame.new(solX - 1, H + 1.2, bm.Z), renk)
		blok("CatiKenar", Vector3.new(0.6, 1.4, bb.Z + 2.6), CFrame.new(sagX + 1, H + 1.2, bm.Z), renk)
		-- çatıda klima kutuları
		for _, x in ipairs({ -bb.X / 2 + 7, bb.X / 2 - 7 }) do
			blok("Klima", Vector3.new(5, 2.6, 4), CFrame.new(bm.X + x, H + 2.3, arkaZ - 6), Color3.fromRGB(200, 202, 208), Enum.Material.Metal)
			dikDisk("KlimaPervane", 0.3, 3, CFrame.new(bm.X + x, H + 3.7, arkaZ - 6), Color3.fromRGB(70, 72, 80), Enum.Material.DiamondPlate)
		end
		-- tavan ışıkları
		for _, x in ipairs({ -bb.X / 4, bb.X / 4 }) do
			local lamba = blok("TavanIsigi", Vector3.new(8, 0.3, 2), CFrame.new(bm.X + x, H - 0.2, bm.Z), Color3.fromRGB(255, 245, 220), Enum.Material.Neon)
			lamba.CanCollide = false
			local tl = Instance.new("SurfaceLight")
			tl.Face = Enum.NormalId.Bottom
			tl.Range = 18
			tl.Angle = 120
			tl.Brightness = 1.2
			tl.Color = Color3.fromRGB(255, 240, 210)
			tl.Parent = lamba
		end
		local neon = blok("NeonTabela", Vector3.new(36, 7, 0.8), CFrame.new(0, H + 5, onZ - 0.6), renk:Lerp(Color3.new(0, 0, 0), 0.25))
		tabelaYazisi(neon, Enum.NormalId.Front, stil.Logo .. " " .. stil.Ad, nil, renk:Lerp(Color3.new(0, 0, 0), 0.25), true)
		local serit = blok("NeonSerit", Vector3.new(36.4, 0.4, 0.4), CFrame.new(0, H + 1.4, onZ - 1.1), renk, Enum.Material.Neon)
		serit.CanCollide = false
		local isik = Instance.new("SurfaceLight")
		isik.Face = Enum.NormalId.Front
		isik.Range = 14
		isik.Brightness = 2
		isik.Color = renk
		isik.Parent = neon
	end

	-- Oturma alanı
	if var("Oturma") then
		for i, p in ipairs(Y.Masalar) do
			local m = CFrame.new(p.X, 0, p.Z)
			dikDisk("MasaAyak", 2.6, 0.5, m * CFrame.new(0, 1.3, 0), BEYAZ, Enum.Material.Metal)
			dikDisk("Masa", 0.25, 4.4, m * CFrame.new(0, 2.7, 0), BEYAZ, Enum.Material.SmoothPlastic)
			for k = 0, 3 do
				local a = k * math.pi / 2 + math.rad(45)
				blok("Sandalye", Vector3.new(1.6, 1.6, 1.6), m * CFrame.Angles(0, a, 0) * CFrame.new(0, 0.8, 3.4), i % 2 == 0 and renk or acik)
			end
			dikDisk("SemsiyeDirek", 7, 0.25, m * CFrame.new(0, 4.2, 0), BEYAZ, Enum.Material.Metal)
			dikDisk("Semsiye", 0.4, 7.5, m * CFrame.new(0, 7.6, 0), renk, Enum.Material.Fabric)
			dikDisk("SemsiyeTepe", 0.8, 3, m * CFrame.new(0, 8.1, 0), BEYAZ, Enum.Material.Fabric)
		end
	end

	-- Çiçek ve ışıklar
	if var("Dekor") then
		local cicekler = { Color3.fromRGB(255, 90, 120), Color3.fromRGB(255, 210, 60), Color3.fromRGB(180, 110, 255), Color3.fromRGB(255, 150, 60) }
		for _, aralik in ipairs({ { solX + 2.5, -5 }, { 5, Y.BantX - 3 } }) do
			local x0, x1 = aralik[1], aralik[2]
			blok("Saksi", Vector3.new(x1 - x0, 1.2, 1.4), CFrame.new((x0 + x1) / 2, 0.6, onZ - 0.9), Color3.fromRGB(150, 95, 60), Enum.Material.WoodPlanks)
			for x = x0 + 0.8, x1 - 0.8, 1.6 do
				local c = blok("Cicek", Vector3.new(1, 1, 1), CFrame.new(x, 1.6, onZ - 0.9), cicekler[math.floor(x) % #cicekler + 1])
				c.Shape = Enum.PartType.Ball
				c.CanCollide = false
			end
		end
		-- kapıya kırmızı halı
		blok("Hali", Vector3.new(5, 0.1, 9), CFrame.new(0, 0.3, onZ - 4.5), Color3.fromRGB(190, 30, 40), Enum.Material.Fabric, { CanCollide = false })
		-- ışık zinciri (çatının önünde)
		local renkler = { Color3.fromRGB(255, 80, 80), Color3.fromRGB(255, 220, 80), Color3.fromRGB(80, 200, 255), Color3.fromRGB(120, 255, 120) }
		local k = 0
		for x = solX + 1, sagX - 1, 2.5 do
			k += 1
			local y = bb.Y - 0.9 - math.abs(math.sin(k * 0.6)) * 0.6
			local ampul = blok("Ampul", Vector3.new(0.6, 0.6, 0.6), CFrame.new(x, y, onZ - 1.2), renkler[k % #renkler + 1], Enum.Material.Neon)
			ampul.Shape = Enum.PartType.Ball
			ampul.CanCollide = false
		end
	end

	-- Çalışanlar
	local kiyafet = {
		Asci = { Gomlek = Color3.fromRGB(245, 245, 245), Pantolon = Color3.fromRGB(60, 60, 70), Sapka = "sef", Yuz = "😋" },
		Kasiyer = { Gomlek = renk, Pantolon = Color3.fromRGB(45, 45, 55), Sapka = "kep", SapkaRenk = renk, Onluk = true, Yuz = "😊" },
		Teknisyen = { Gomlek = Color3.fromRGB(70, 95, 130), Pantolon = Color3.fromRGB(45, 55, 75), Sapka = "kep", SapkaRenk = Color3.fromRGB(255, 200, 40), Yuz = "🤓" },
		Temizlikci = { Gomlek = Color3.fromRGB(110, 185, 120), Pantolon = Color3.fromRGB(50, 60, 55), Yuz = "😄" },
	}
	for anahtar, yer in pairs(Y.Calisanlar) do
		if var(anahtar) then
			local konum = Vector3.new(yer.Yer.X, 0.3, yer.Yer.Z)
			local cf = F * CFrame.lookAt(konum, konum + yer.Bakis)
			local oz = kiyafet[anahtar]
			oz.Ten = Gorsel.Tenler[(#anahtar + (veri.UserId % 5)) % #Gorsel.Tenler + 1]
			local insan = Gorsel.Insan(cf, oz)
			local m = Instance.new("Model")
			m.Name = "Calisan_" .. anahtar
			for _, p in ipairs(insan.Liste) do
				p.CanCollide = false
				p.Parent = m
			end
			if anahtar == "Temizlikci" then
				-- süpürge
				local sap = Gorsel.Parca({ Name = "Supurge", Size = Vector3.new(0.25, 4.2, 0.25), CFrame = cf * CFrame.new(1.9, 2.1, -0.6) * CFrame.Angles(math.rad(-15), 0, math.rad(8)), Color = Color3.fromRGB(150, 100, 55), Material = Enum.Material.Wood })
				sap.Parent = m
				local fir = Gorsel.Parca({ Name = "SupurgeUcu", Size = Vector3.new(1.4, 0.6, 0.5), CFrame = cf * CFrame.new(2.2, 0.3, -1.1), Color = Color3.fromRGB(230, 200, 90), Material = Enum.Material.Fabric })
				fir.Parent = m
			end
			m:SetAttribute("Rol", anahtar)
			m.PrimaryPart = insan.Govde
			m.Parent = klasor
		end
	end

	-- Çatıda dev burger
	if var("DevTabela") and var("Cati") then
		local tabanY = bb.Y + 1
		dikDisk("DevBurgerKaide", 1.2, 17, CFrame.new(0, tabanY + 0.6, bm.Z + 2), renk, Enum.Material.SmoothPlastic)
		local parcalar = Gorsel.Burger(F * CFrame.new(0, tabanY + 1.2, bm.Z + 2), 3.2, false, false)
		for _, p in ipairs(parcalar) do
			p.CanCollide = true
			p.Parent = klasor
		end
	end

	klasor.Parent = arsa
	Dukkan.ButonlariKur(arsa, veri)
end

-- Yerdeki satın alma butonları (tycoon gibi: üstüne bas, geliştirmeyi al).
-- Her satın almadan sonra yeniden kurulur: en üst seviyesine gelen geliştirmenin butonu kaybolur,
-- fiyat yazıları güncellenir. Üstüne basılmasını sunucu dinler (BurgerSunucu), rengini oyuncunun
-- ekranı paraya göre boyar (yeşil: alabilirsin, kırmızı: paran yetmiyor, gri: kilitli).
function Dukkan.ButonlariKur(arsa, veri)
	local eski = arsa:FindFirstChild("Butonlar")
	if eski then
		eski:Destroy()
	end
	if not veri then
		return
	end
	local klasor = Instance.new("Folder")
	klasor.Name = "Butonlar"
	local F = Dukkan.Cerceve(arsa)
	local g = veri.Gelistirme or {}
	for yon, liste in ipairs({ Config.Butonlar.Sol, Config.Butonlar.Sag }) do
		local x = (yon == 1 and -1 or 1) * Y.ButonX
		for i, anahtar in ipairs(liste) do
			local gb = Ekonomi.GelistirmeBul(anahtar)
			local z = Y.ButonZ[i]
			local seviye = g[anahtar] or 0
			local fiyat = gb and Ekonomi.Fiyat(anahtar, seviye)
			if gb and z and fiyat then -- fiyat yoksa en üst seviyede: buton yok
				local taban = Gorsel.Parca({
					Name = "ButonTaban",
					Size = Vector3.new(5.2, 0.4, 5.2),
					CFrame = F * CFrame.new(x, 0.2, z),
					Color = Color3.fromRGB(60, 62, 70),
					Material = Enum.Material.DiamondPlate,
					CanCollide = true,
				})
				taban.Parent = klasor
				local b = Gorsel.Parca({
					Name = "SatinAlButonu",
					Size = Vector3.new(4, 0.5, 4),
					CFrame = F * CFrame.new(x, 0.45, z),
					Color = Color3.fromRGB(130, 135, 150),
					Material = Enum.Material.Neon,
					CanCollide = false,
				})
				b.CanTouch = true
				b:SetAttribute("Gelistirme", anahtar)
				b:SetAttribute("Sahip", veri.UserId)
				local bb = Instance.new("BillboardGui")
				bb.Name = "Etiket"
				bb.Size = UDim2.new(8, 0, 3.4, 0)
				bb.StudsOffset = Vector3.new(0, 3.4, 0)
				bb.MaxDistance = 70
				bb.LightInfluence = 0
				bb.Parent = b
				yazi(bb, gb.Ikon, Color3.new(1, 1, 1), { Name = "Ikon", Size = UDim2.fromScale(0.22, 0.5) })
				yazi(bb, gb.Ad, Color3.new(1, 1, 1), {
					Name = "Ad",
					Size = UDim2.fromScale(0.78, 0.5),
					Position = UDim2.fromScale(0.22, 0),
					TextStrokeTransparency = 0.2,
				})
				local alt
				if not Ekonomi.KilidiAcik(g, anahtar) then
					local gerekli = Ekonomi.GelistirmeBul(gb.Gerekli or "")
					alt = string.format("🔒 Önce: %s", gerekli and gerekli.Ad or "?")
				elseif gb.MaxSeviye > 1 then
					alt = string.format("💰 %s  •  Sv %d/%d", Ekonomi.Kisalt(fiyat), seviye, gb.MaxSeviye)
				else
					alt = "💰 " .. Ekonomi.Kisalt(fiyat)
				end
				yazi(bb, alt, Color3.fromRGB(255, 225, 90), {
					Name = "Fiyat",
					Size = UDim2.fromScale(1, 0.45),
					Position = UDim2.fromScale(0, 0.52),
					TextStrokeTransparency = 0.2,
				})
				b.Parent = klasor
			end
		end
	end
	klasor.Parent = arsa
end

-- Oyuncunun ışınlanacağı yer: tezgaha bakarak
function Dukkan.OyuncuCFrame(arsa)
	local F = Dukkan.Cerceve(arsa)
	local p = Y.OyuncuYeri
	local a = Y.AnaTezgah
	local bas = F * Vector3.new(p.X, 3, p.Z)
	local hedef = F * Vector3.new(a.X, 3, a.Z)
	return CFrame.lookAt(bas, hedef)
end

return Dukkan
