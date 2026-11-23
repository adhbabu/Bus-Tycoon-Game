-- BURGER İMPARATORU - Burger Kasabası haritası
-- Ortada meydan ve dev burger heykeli, etrafında yol, yolun dışında 8 dükkan arsası,
-- köşelerde parklar, en dışta ağaçlar, tepeler ve şehir binaları.
-- Hiçbir şey "require" etmez: Config ve Gorsel dışarıdan verilir. Böylece hem oyunda
-- (harita yoksa) hem de derleme sırasında (Lune ile .rbxm dosyasına) çalışır.

local HaritaUretici = {}

local YOL_IC = 48 -- meydanın kenarı
local YOL_DIS = 66 -- yolun dış kenarı
local KALDIRIM = 72 -- kaldırımın dış kenarı (arsalar buradan başlar)
local ARSA_MERKEZ = 102 -- arsanın merkezi (meydanın ortasına uzaklık)
local ARSA_KAYMA = 40 -- bir kenardaki iki arsa bu kadar sağa/sola kayık
local HARITA = 360 -- yarım genişlik

function HaritaUretici.Uret(Config, Gorsel)
	local rastgele = Random.new(2026)
	local parca = Gorsel.Parca
	local disk = Gorsel.Disk

	local harita = Instance.new("Model")
	harita.Name = "Harita"

	local function klasor(ad, ebeveyn)
		local f = Instance.new("Folder")
		f.Name = ad
		f.Parent = ebeveyn or harita
		return f
	end

	local function blok(ebeveyn, ad, boyut, cf, renk, malzeme, oz)
		local p = parca({
			Name = ad,
			Size = boyut,
			CFrame = cf,
			Color = renk,
			Material = malzeme or Enum.Material.SmoothPlastic,
			CanCollide = true,
		})
		for k, v in pairs(oz or {}) do
			p[k] = v
		end
		p.Parent = ebeveyn
		return p
	end

	local function top(ebeveyn, ad, cap, cf, renk, malzeme)
		local p = parca({
			Name = ad,
			Shape = Enum.PartType.Ball,
			Size = Vector3.new(cap, cap, cap),
			CFrame = cf,
			Color = renk,
			Material = malzeme or Enum.Material.Grass,
			CanCollide = true,
		})
		p.Parent = ebeveyn
		return p
	end

	------------------------------------------------------------------
	-- Zemin
	------------------------------------------------------------------
	local zemin = klasor("Zemin")
	blok(zemin, "Cimen", Vector3.new(HARITA * 2, 4, HARITA * 2), CFrame.new(0, -2, 0), Color3.fromRGB(96, 168, 72), Enum.Material.Grass)
	-- Haritadan düşmesinler
	for _, d in ipairs({ { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } }) do
		local boy = d[1] ~= 0 and Vector3.new(2, 80, HARITA * 2) or Vector3.new(HARITA * 2, 80, 2)
		blok(zemin, "GorunmezDuvar", boy, CFrame.new(d[1] * (HARITA - 1), 40, d[2] * (HARITA - 1)), Color3.new(1, 1, 1), nil, { Transparency = 1 })
	end

	------------------------------------------------------------------
	-- Meydan
	------------------------------------------------------------------
	local meydan = klasor("Meydan")
	blok(meydan, "MeydanTasi", Vector3.new(YOL_IC * 2, 0.4, YOL_IC * 2), CFrame.new(0, 0.2, 0), Color3.fromRGB(214, 198, 172), Enum.Material.Cobblestone)
	local halka = disk(CFrame.new(0, 0.45, 0), 0.1, 44, Color3.fromRGB(180, 82, 70), Enum.Material.Brick)
	halka.CanCollide = true
	halka.Parent = meydan

	-- Dev burger heykeli
	local heykel = Instance.new("Model")
	heykel.Name = "DevBurgerHeykeli"
	heykel.Parent = meydan
	local kaide = disk(CFrame.new(0, 2, 0), 3.6, 20, Color3.fromRGB(245, 240, 232), Enum.Material.Marble)
	kaide.CanCollide = true
	kaide.Parent = heykel
	local serit = disk(CFrame.new(0, 3.45, 0), 0.6, 20.4, Color3.fromRGB(255, 200, 40), Enum.Material.Foil)
	serit.Parent = heykel
	local burgerParcalari, burgerBoy = Gorsel.Burger(CFrame.new(0, 3.8, 0), 3.4, false, false)
	for _, p in ipairs(burgerParcalari) do
		p.CanCollide = true
		p.Parent = heykel
	end
	local tepe = 3.8 + burgerBoy
	local altinRenk = Color3.fromRGB(255, 200, 40)
	local tac = disk(CFrame.new(0, tepe + 0.3, 0), 2, 7.5, altinRenk, Enum.Material.Foil)
	tac.Parent = heykel
	for i = 1, 6 do
		local a = i / 6 * math.pi * 2
		local p = Vector3.new(math.cos(a) * 3.2, tepe + 2.4, math.sin(a) * 3.2)
		blok(heykel, "TacDikeni", Vector3.new(1.2, 2.6, 1.2), CFrame.new(p) * CFrame.Angles(0, -a + math.rad(45), 0), altinRenk, Enum.Material.Foil)
		top(heykel, "Mucevher", 1, CFrame.new(p + Vector3.new(0, 1.7, 0)), i % 2 == 0 and Color3.fromRGB(255, 40, 70) or Color3.fromRGB(40, 140, 255), Enum.Material.Neon)
	end
	local baslikTutucu = blok(heykel, "BaslikNoktasi", Vector3.new(1, 1, 1), CFrame.new(0, tepe + 12, 0), Color3.new(1, 1, 1), nil, {
		Transparency = 1,
		CanCollide = false,
		CanQuery = false,
	})
	local bb = Instance.new("BillboardGui")
	bb.Size = UDim2.new(54, 0, 9, 0)
	bb.MaxDistance = 600
	bb.LightInfluence = 0
	bb.Parent = baslikTutucu
	local baslik = Instance.new("TextLabel")
	baslik.BackgroundTransparency = 1
	baslik.Size = UDim2.fromScale(1, 1)
	baslik.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
	baslik.TextScaled = true
	baslik.Text = "🍔 BURGER İMPARATORU 👑"
	baslik.TextColor3 = Color3.fromRGB(255, 225, 90)
	baslik.Parent = bb
	local bc = Instance.new("UIStroke")
	bc.Color = Color3.fromRGB(90, 30, 10)
	bc.Thickness = 4
	bc.Parent = baslik

	-- Fıskiyeler
	for _, k in ipairs({ { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } }) do
		local m = Vector3.new(k[1] * 30, 0, k[2] * 30)
		local havuz = disk(CFrame.new(m + Vector3.new(0, 0.9, 0)), 1.4, 13, Color3.fromRGB(190, 190, 195), Enum.Material.Concrete)
		havuz.CanCollide = true
		havuz.Parent = meydan
		local su = disk(CFrame.new(m + Vector3.new(0, 1.62, 0)), 0.1, 11.6, Color3.fromRGB(70, 160, 230), Enum.Material.Glass)
		su.Transparency = 0.25
		su.Reflectance = 0.2
		su.Parent = meydan
		local sutun = disk(CFrame.new(m + Vector3.new(0, 3, 0)), 4, 1.4, Color3.fromRGB(200, 200, 205), Enum.Material.Marble)
		sutun.Parent = meydan
		local fiskiye = Instance.new("ParticleEmitter")
		fiskiye.Texture = "rbxasset://textures/particles/sparkles_main.dds"
		fiskiye.Rate = 40
		fiskiye.Lifetime = NumberRange.new(0.8, 1.2)
		fiskiye.Speed = NumberRange.new(12, 16)
		fiskiye.SpreadAngle = Vector2.new(12, 12)
		fiskiye.Acceleration = Vector3.new(0, -30, 0)
		fiskiye.Size = NumberSequence.new(0.5, 0.2)
		fiskiye.Color = ColorSequence.new(Color3.fromRGB(180, 225, 255))
		fiskiye.Transparency = NumberSequence.new(0.2, 1)
		fiskiye.EmissionDirection = Enum.NormalId.Right -- silindir yattığı için "sağ" yukarı bakar
		fiskiye.Parent = sutun
	end

	-- Banklar ve saksılar
	local function bank(cf)
		local m = Instance.new("Model")
		m.Name = "Bank"
		blok(m, "Oturak", Vector3.new(6, 0.4, 2), cf * CFrame.new(0, 1.6, 0), Color3.fromRGB(150, 95, 55), Enum.Material.Wood)
		blok(m, "Sirt", Vector3.new(6, 1.6, 0.3), cf * CFrame.new(0, 2.6, 0.85), Color3.fromRGB(150, 95, 55), Enum.Material.Wood)
		for _, x in ipairs({ -2.5, 2.5 }) do
			blok(m, "Ayak", Vector3.new(0.4, 1.4, 1.8), cf * CFrame.new(x, 0.7, 0), Color3.fromRGB(50, 50, 55), Enum.Material.Metal)
		end
		return m
	end
	local function saksi(ebeveyn, p)
		local s = disk(CFrame.new(p + Vector3.new(0, 1, 0)), 2, 5, Color3.fromRGB(200, 120, 80), Enum.Material.Brick)
		s.CanCollide = true
		s.Parent = ebeveyn
		local renkler = { Color3.fromRGB(255, 90, 120), Color3.fromRGB(255, 210, 60), Color3.fromRGB(180, 110, 255), Color3.fromRGB(255, 140, 50) }
		for i = 1, 5 do
			local a = i / 5 * math.pi * 2
			top(ebeveyn, "Cicek", 1.2, CFrame.new(p + Vector3.new(math.cos(a) * 1.3, 2.4, math.sin(a) * 1.3)), renkler[(i - 1) % #renkler + 1], Enum.Material.SmoothPlastic)
		end
		top(ebeveyn, "Yaprak", 3, CFrame.new(p + Vector3.new(0, 2.2, 0)), Color3.fromRGB(70, 150, 60))
	end
	for _, k in ipairs({ { 0, -1 }, { 0, 1 }, { -1, 0 }, { 1, 0 } }) do
		local yon = Vector3.new(k[1], 0, k[2])
		local yan = Vector3.new(-k[2], 0, k[1])
		for _, s in ipairs({ -1, 1 }) do
			local p = yon * 16 + yan * s * 7
			bank(CFrame.lookAt(p, p + yon)).Parent = meydan
			saksi(meydan, yon * 16 + yan * s * 14)
		end
	end

	------------------------------------------------------------------
	-- Yol, kaldırım, yaya geçidi, lambalar
	------------------------------------------------------------------
	local yol = klasor("Yol")
	local asfalt = Color3.fromRGB(58, 60, 66)
	local yolEn = YOL_DIS - YOL_IC
	local orta = (YOL_IC + YOL_DIS) / 2
	-- dört kenar: kuzey/güney tam boy, doğu/batı aradaki kısım
	blok(yol, "Asfalt", Vector3.new(YOL_DIS * 2, 0.3, yolEn), CFrame.new(0, 0.15, -orta), asfalt, Enum.Material.Asphalt)
	blok(yol, "Asfalt", Vector3.new(YOL_DIS * 2, 0.3, yolEn), CFrame.new(0, 0.15, orta), asfalt, Enum.Material.Asphalt)
	blok(yol, "Asfalt", Vector3.new(yolEn, 0.3, YOL_IC * 2), CFrame.new(-orta, 0.15, 0), asfalt, Enum.Material.Asphalt)
	blok(yol, "Asfalt", Vector3.new(yolEn, 0.3, YOL_IC * 2), CFrame.new(orta, 0.15, 0), asfalt, Enum.Material.Asphalt)
	-- kesik orta çizgiler
	local beyaz = Color3.fromRGB(240, 240, 235)
	for z = -orta + 4, orta - 4, 9 do
		for _, x in ipairs({ -orta, orta }) do
			blok(yol, "Cizgi", Vector3.new(0.5, 0.05, 4.5), CFrame.new(x, 0.32, z), beyaz, nil, { CanCollide = false })
		end
	end
	for x = -orta + 4, orta - 4, 9 do
		for _, z in ipairs({ -orta, orta }) do
			blok(yol, "Cizgi", Vector3.new(4.5, 0.05, 0.5), CFrame.new(x, 0.32, z), beyaz, nil, { CanCollide = false })
		end
	end
	-- dış kaldırım
	local kaldirimRenk = Color3.fromRGB(198, 198, 202)
	local kEn = KALDIRIM - YOL_DIS
	local kOrta = (KALDIRIM + YOL_DIS) / 2
	blok(yol, "Kaldirim", Vector3.new(KALDIRIM * 2, 0.5, kEn), CFrame.new(0, 0.25, -kOrta), kaldirimRenk, Enum.Material.Concrete)
	blok(yol, "Kaldirim", Vector3.new(KALDIRIM * 2, 0.5, kEn), CFrame.new(0, 0.25, kOrta), kaldirimRenk, Enum.Material.Concrete)
	blok(yol, "Kaldirim", Vector3.new(kEn, 0.5, YOL_DIS * 2), CFrame.new(-kOrta, 0.25, 0), kaldirimRenk, Enum.Material.Concrete)
	blok(yol, "Kaldirim", Vector3.new(kEn, 0.5, YOL_DIS * 2), CFrame.new(kOrta, 0.25, 0), kaldirimRenk, Enum.Material.Concrete)
	-- yaya geçitleri (meydandan her arsaya)
	for _, k in ipairs({ { 0, -1 }, { 0, 1 }, { -1, 0 }, { 1, 0 } }) do
		local yon = Vector3.new(k[1], 0, k[2])
		local yan = Vector3.new(-k[2], 0, k[1])
		for _, s in ipairs({ -ARSA_KAYMA, ARSA_KAYMA }) do
			for i = -3, 3 do
				local p = yon * orta + yan * (s + i * 1.6) + Vector3.new(0, 0.33, 0)
				blok(yol, "YayaGecidi", Vector3.new(1, 0.05, yolEn - 2), CFrame.lookAt(p, p + yon), beyaz, nil, { CanCollide = false })
			end
		end
	end
	-- sokak lambaları
	local function lamba(p, bakis)
		local m = Instance.new("Model")
		m.Name = "Lamba"
		local cf = CFrame.lookAt(p, p + bakis)
		local direk = disk(cf * CFrame.new(0, 6, 0), 12, 0.6, Color3.fromRGB(40, 42, 48), Enum.Material.Metal)
		direk.CanCollide = true
		direk.Parent = m
		blok(m, "Kol", Vector3.new(0.4, 0.4, 3), cf * CFrame.new(0, 11.8, -1.3), Color3.fromRGB(40, 42, 48), Enum.Material.Metal)
		local bas = blok(m, "Ampul", Vector3.new(1.4, 0.6, 1.4), cf * CFrame.new(0, 11.4, -2.6), Color3.fromRGB(255, 236, 180), Enum.Material.Neon, { CanCollide = false })
		local isik = Instance.new("PointLight")
		isik.Range = 20
		isik.Brightness = 1.2
		isik.Color = Color3.fromRGB(255, 225, 170)
		isik.Parent = bas
		return m
	end
	for _, k in ipairs({ { 0, -1 }, { 0, 1 }, { -1, 0 }, { 1, 0 } }) do
		local yon = Vector3.new(k[1], 0, k[2])
		local yan = Vector3.new(-k[2], 0, k[1])
		for t = -60, 60, 30 do
			local p = yon * (YOL_DIS + 1.2) + yan * t + Vector3.new(0, 0.5, 0)
			lamba(p, -yon).Parent = yol
		end
	end

	------------------------------------------------------------------
	-- Ağaçlar
	------------------------------------------------------------------
	local agaclar = klasor("Agaclar")
	local yesiller = { Color3.fromRGB(70, 150, 60), Color3.fromRGB(90, 170, 70), Color3.fromRGB(60, 135, 75), Color3.fromRGB(110, 180, 70) }
	local function agac(p, boy)
		boy = boy or rastgele:NextNumber(0.8, 1.3)
		local m = Instance.new("Model")
		m.Name = "Agac"
		local govde = disk(CFrame.new(p + Vector3.new(0, 4 * boy, 0)), 8 * boy, 1.4 * boy, Color3.fromRGB(110, 75, 45), Enum.Material.Wood)
		govde.CanCollide = true
		govde.Parent = m
		local renk = yesiller[rastgele:NextInteger(1, #yesiller)]
		top(m, "Yaprak", 8 * boy, CFrame.new(p + Vector3.new(0, 9 * boy, 0)), renk)
		top(m, "Yaprak", 6 * boy, CFrame.new(p + Vector3.new(2.2 * boy, 8 * boy, 1 * boy)), renk:Lerp(Color3.new(1, 1, 1), 0.08))
		top(m, "Yaprak", 6 * boy, CFrame.new(p + Vector3.new(-1.8 * boy, 8.4 * boy, -1.4 * boy)), renk:Lerp(Color3.new(0, 0, 0), 0.08))
		m.Parent = agaclar
		return m
	end
	-- meydanın köşelerinde ağaçlar
	for _, k in ipairs({ { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } }) do
		agac(Vector3.new(k[1] * 41, 0.4, k[2] * 41), 1)
		agac(Vector3.new(k[1] * 41, 0.4, k[2] * 18), 0.9)
		agac(Vector3.new(k[1] * 18, 0.4, k[2] * 41), 0.9)
	end
	-- köşe parkları (arsaların arasındaki köşeler)
	local parklar = klasor("Parklar")
	for _, k in ipairs({ { 1, 1 }, { -1, 1 }, { 1, -1 }, { -1, -1 } }) do
		local merkez = Vector3.new(k[1] * 110, 0, k[2] * 110)
		local cimen = blok(parklar, "ParkCimeni", Vector3.new(66, 0.3, 66), CFrame.new(merkez + Vector3.new(0, 0.15, 0)), Color3.fromRGB(110, 185, 80), Enum.Material.Grass)
		cimen.CanCollide = true
		local yolCf = CFrame.lookAt(merkez, Vector3.new(0, 0, 0))
		blok(parklar, "ParkYolu", Vector3.new(4, 0.32, 70), yolCf * CFrame.new(0, 0.2, 0), Color3.fromRGB(225, 205, 160), Enum.Material.Sand)
		if k[1] == 1 and k[2] == -1 then
			-- göl
			local gol = disk(CFrame.new(merkez + Vector3.new(8, 0.35, -8)), 0.2, 34, Color3.fromRGB(60, 150, 220), Enum.Material.Glass)
			gol.Transparency = 0.15
			gol.Reflectance = 0.25
			gol.Parent = parklar
			local kiyi = disk(CFrame.new(merkez + Vector3.new(8, 0.3, -8)), 0.2, 38, Color3.fromRGB(230, 210, 160), Enum.Material.Sand)
			kiyi.Parent = parklar
		end
		for _ = 1, 9 do
			local p = merkez + Vector3.new(rastgele:NextNumber(-28, 28), 0.3, rastgele:NextNumber(-28, 28))
			if (p - merkez).Magnitude > 8 then
				agac(p)
			end
		end
		bank(CFrame.lookAt(merkez + Vector3.new(-6, 0.3, 0), merkez + Vector3.new(-12, 0.3, 0))).Parent = parklar
		saksi(parklar, merkez + Vector3.new(6, 0.3, 6))
	end
	-- dış halka: arsaların arkasında sık ağaçlar
	for i = 1, 70 do
		local a = i / 70 * math.pi * 2 + rastgele:NextNumber(-0.03, 0.03)
		local r = rastgele:NextNumber(150, 175)
		local x, z = math.cos(a) * r, math.sin(a) * r
		-- kare haritada köşelere doğru daha uzağa
		local olcek = 1 / math.max(math.abs(math.cos(a)), math.abs(math.sin(a)))
		olcek = math.min(olcek, 1.25)
		agac(Vector3.new(x * olcek, 0, z * olcek))
	end

	------------------------------------------------------------------
	-- Uzaktaki şehir binaları ve tepeler
	------------------------------------------------------------------
	local sehir = klasor("Sehir")
	local pasteller = {
		Color3.fromRGB(240, 190, 170), Color3.fromRGB(170, 205, 235), Color3.fromRGB(245, 225, 160),
		Color3.fromRGB(190, 225, 180), Color3.fromRGB(220, 190, 235), Color3.fromRGB(235, 235, 225),
	}
	local cam = Color3.fromRGB(150, 200, 235)
	for i = 1, 22 do
		local a = i / 22 * math.pi * 2 + 0.07
		local r = rastgele:NextNumber(225, 265)
		local p = Vector3.new(math.cos(a) * r, 0, math.sin(a) * r)
		local en, boy, derin = rastgele:NextNumber(18, 30), rastgele:NextNumber(22, 60), rastgele:NextNumber(16, 24)
		local cf = CFrame.lookAt(p, Vector3.new(0, 0, 0))
		local m = Instance.new("Model")
		m.Name = "Bina"
		blok(m, "Govde", Vector3.new(en, boy, derin), cf * CFrame.new(0, boy / 2, 0), pasteller[rastgele:NextInteger(1, #pasteller)], Enum.Material.Concrete)
		blok(m, "CatiKenari", Vector3.new(en + 1, 1, derin + 1), cf * CFrame.new(0, boy + 0.5, 0), Color3.fromRGB(120, 120, 130), Enum.Material.Concrete)
		local kat = 0
		for y = 6, boy - 4, 7 do
			kat += 1
			blok(m, "Pencere", Vector3.new(en - 4, 2.6, 0.2), cf * CFrame.new(0, y, -derin / 2 - 0.1), cam, Enum.Material.Glass, { Reflectance = 0.3 })
		end
		m.Parent = sehir
	end
	local tepeler = klasor("Tepeler")
	for i = 1, 26 do
		local a = i / 26 * math.pi * 2
		local cap = rastgele:NextNumber(70, 130)
		local r = rastgele:NextNumber(300, 345)
		local p = Vector3.new(math.cos(a) * r, -cap * 0.28, math.sin(a) * r)
		top(tepeler, "Tepe", cap, CFrame.new(p), Color3.fromRGB(85, 155, 65):Lerp(Color3.fromRGB(120, 175, 80), rastgele:NextNumber()))
	end

	------------------------------------------------------------------
	-- Arsalar
	------------------------------------------------------------------
	local arsalar = klasor("Arsalar")
	local boy = Config.Yerlesim.ArsaBoyu
	local sira = 0
	for _, k in ipairs({ { 0, -1 }, { 1, 0 }, { 0, 1 }, { -1, 0 } }) do
		local yon = Vector3.new(k[1], 0, k[2])
		local yan = Vector3.new(-k[2], 0, k[1])
		for _, s in ipairs({ -ARSA_KAYMA, ARSA_KAYMA }) do
			sira += 1
			local merkez = yon * ARSA_MERKEZ + yan * s
			-- arsanın önü (-Z) meydana bakar
			local cf = CFrame.lookAt(merkez, merkez - yon)
			local m = Instance.new("Model")
			m.Name = "Arsa" .. sira
			local taban = blok(m, "Taban", boy, cf * CFrame.new(0, 0, 0), Color3.fromRGB(125, 190, 95), Enum.Material.Grass)
			taban.Locked = true
			-- yanlarda ve arkada çit (çalı)
			local cali = Color3.fromRGB(60, 130, 55)
			blok(m, "Cit", Vector3.new(2, 3, boy.Z), cf * CFrame.new(-boy.X / 2 + 1, 2, 0), cali, Enum.Material.Grass)
			blok(m, "Cit", Vector3.new(2, 3, boy.Z), cf * CFrame.new(boy.X / 2 - 1, 2, 0), cali, Enum.Material.Grass)
			blok(m, "Cit", Vector3.new(boy.X, 3, 2), cf * CFrame.new(0, 2, boy.Z / 2 - 1), cali, Enum.Material.Grass)
			m:SetAttribute("Sira", sira)
			m:SetAttribute("Sahip", 0)
			m.PrimaryPart = taban
			m.Parent = arsalar
		end
	end

	------------------------------------------------------------------
	-- Başlangıç noktası (meydanda)
	------------------------------------------------------------------
	local spawn = Instance.new("SpawnLocation")
	spawn.Name = "Baslangic"
	spawn.Anchored = true
	spawn.Neutral = true
	spawn.Duration = 0
	spawn.Size = Vector3.new(10, 0.4, 10)
	spawn.CFrame = CFrame.new(0, 0.6, 32)
	spawn.Color = Color3.fromRGB(255, 200, 50)
	spawn.Material = Enum.Material.Neon
	spawn.Transparency = 0.5
	spawn.TopSurface = Enum.SurfaceType.Smooth
	spawn.Parent = harita

	return harita
end

return HaritaUretici
