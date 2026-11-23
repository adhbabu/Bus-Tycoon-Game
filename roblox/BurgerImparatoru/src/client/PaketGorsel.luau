-- BURGER İMPARATORU - Kutu görselleri (sadece bu oyuncunun ekranında)
-- Tezgahtaki kutunun katlanması, paketin banttan satış penceresine gitmesi,
-- makinelerin presi ve bantta akan kutular. Sunucu sadece "şu oldu" der, çizimi burası yapar.

local TweenService = game:GetService("TweenService")
local Workspace = game:GetService("Workspace")

local PaketGorsel = {}

local Config, Gorsel
local Y
local klasor
local arsalar = {} -- sıra -> durum
local EN_FAZLA_BANTTA = 22 -- bir dükkanın bantlarında aynı anda en fazla kaç kutu görünsün
local GORUS_UZAKLIGI = 260 -- bundan uzaktaki dükkanların makine kutularını çizme

local KATLAMA = TweenInfo.new(0.17, Enum.EasingStyle.Quad, Enum.EasingDirection.Out)
local DUSME = TweenInfo.new(0.28, Enum.EasingStyle.Back, Enum.EasingDirection.Out)

local function cerceve(st)
	local taban = st.Model:FindFirstChild("Taban")
	if not taban then
		return nil
	end
	return taban.CFrame * CFrame.new(0, taban.Size.Y / 2, 0)
end

local function stilOku(st, altin)
	return {
		Renk = st.Model:GetAttribute("Renk") or Color3.fromRGB(220, 55, 50),
		Logo = st.Model:GetAttribute("Logo") or "🍔",
		Altin = altin,
	}
end

local function sahipli(st)
	local s = st.Model:GetAttribute("Sahip")
	return type(s) == "number" and s ~= 0
end

local function kutuTabani(st)
	local F = cerceve(st)
	if not F then
		return nil
	end
	local a = Y.AnaTezgah
	return F * CFrame.new(a.X, Y.TezgahYuksekligi, a.Z)
end

local function temizle(st)
	if st.Kutu then
		for _, p in pairs(st.Kutu) do
			p:Destroy()
		end
	end
	if st.Burger then
		for _, p in ipairs(st.Burger) do
			p:Destroy()
		end
	end
	st.Kutu = nil
	st.Burger = nil
end

-- Tezgaha yeni açık kutu (ve içinde burger) koyar
local function yeniKutu(st, altin, asama, gecikme)
	temizle(st)
	if not sahipli(st) then
		return
	end
	local taban = kutuTabani(st)
	if not taban then
		return
	end
	asama = asama or 0
	st.Asama = asama
	st.Altin = altin and true or false
	local o = Config.BurgerOlcegi
	local kutu = Gorsel.KutuYap(taban, stilOku(st, st.Altin), o)
	local poz = Gorsel.KutuPozu(taban, asama, o)
	local burger = Gorsel.Burger(taban * CFrame.new(0, Gorsel.KartonKalinligi * o, 0), o, st.Altin, false)
	local hepsi = {}
	for ad, p in pairs(kutu) do
		p.CFrame = poz[ad]
		table.insert(hepsi, p)
	end
	for _, p in ipairs(burger) do
		table.insert(hepsi, p)
	end
	local bilgi = gecikme and TweenInfo.new(0.28, Enum.EasingStyle.Back, Enum.EasingDirection.Out, 0, false, gecikme) or DUSME
	for _, p in ipairs(hepsi) do
		local hedef = p.CFrame
		p.CFrame = hedef + Vector3.new(0, 5, 0)
		p.Parent = klasor
		TweenService:Create(p, bilgi, { CFrame = hedef }):Play()
	end
	st.Kutu = kutu
	st.Burger = burger
end

-- Parçaları bir yoldan geçirir (her noktaya sırayla), sonra yok eder.
-- basTaban: kutunun şu anki alt yüzü; noktalar: kutunun alt yüzünün gideceği yerler
local function yoldanGotur(parcalar, basTaban, noktalar, hiz, bitince)
	if #parcalar == 0 then
		return
	end
	local goreli = {}
	for _, p in ipairs(parcalar) do
		goreli[p] = basTaban:ToObjectSpace(p.CFrame)
	end
	task.spawn(function()
		local simdiki = basTaban
		for _, hedefCf in ipairs(noktalar) do
			local mesafe = (hedefCf.Position - simdiki.Position).Magnitude
			local sure = math.max(0.05, mesafe / hiz)
			local bilgi = TweenInfo.new(sure, Enum.EasingStyle.Linear)
			for _, p in ipairs(parcalar) do
				if p.Parent then
					TweenService:Create(p, bilgi, { CFrame = hedefCf * goreli[p] }):Play()
				end
			end
			task.wait(sure)
			simdiki = hedefCf
		end
		-- satış penceresinde küçülüp kaybolsun
		for _, p in ipairs(parcalar) do
			if p.Parent then
				TweenService:Create(p, TweenInfo.new(0.15), { Size = p.Size * 0.2, Transparency = 1 }):Play()
			end
		end
		task.wait(0.16)
		for _, p in ipairs(parcalar) do
			p:Destroy()
		end
		if bitince then
			bitince()
		end
	end)
end

local function satisNoktasi(F)
	local s = Y.SatisNoktasi
	return F * CFrame.new(s.X, Y.TezgahYuksekligi, s.Z)
end

----------------------------------------------------------------------
-- Dışarıya açık fonksiyonlar
----------------------------------------------------------------------

-- KATLA: kutuyu yeni aşamaya katlar; paketlendiyse paketi banda koyup yeni kutu getirir
function PaketGorsel.Katla(sira, asama, paketlendi, altin, siradakiAltin)
	local st = arsalar[sira]
	if not st or not sahipli(st) then
		return
	end
	if not st.Kutu or st.Altin ~= (altin and true or false) then
		yeniKutu(st, altin, math.max(0, asama - 1))
	end
	local taban = kutuTabani(st)
	if not taban or not st.Kutu then
		return
	end
	local poz = Gorsel.KutuPozu(taban, asama, Config.BurgerOlcegi)
	for ad, p in pairs(st.Kutu) do
		TweenService:Create(p, KATLAMA, { CFrame = poz[ad] }):Play()
	end
	st.Asama = asama
	if not paketlendi then
		return
	end

	-- Kapak kapandı: açık kutuyu kapalı pakete çevir, bantta satış penceresine gönder
	local eskiKutu, eskiBurger = st.Kutu, st.Burger
	st.Kutu, st.Burger = nil, nil
	local stil = stilOku(st, altin)
	task.delay(0.2, function()
		for _, p in pairs(eskiKutu) do
			p:Destroy()
		end
		for _, p in ipairs(eskiBurger) do
			p:Destroy()
		end
		local F = cerceve(st)
		if not F or not sahipli(st) then
			return
		end
		local paket = Gorsel.KapaliKutu(taban, stil, Config.BurgerOlcegi)
		for _, p in ipairs(paket) do
			p.Parent = klasor
		end
		yoldanGotur(paket, taban, { satisNoktasi(F) }, 18, function()
			if PaketGorsel.SatisaGeldi then
				PaketGorsel.SatisaGeldi(sira, stil)
			end
		end)
	end)
	yeniKutu(st, siradakiAltin, 0, 0.3)
end

-- Kutu doğru stilde/aşamada mı? Değilse düzelt (ör. sunucudan gelen durumla eşitle)
function PaketGorsel.Esitle(sira, asama, altin)
	local st = arsalar[sira]
	if not st or not sahipli(st) then
		return
	end
	if not st.Kutu or st.Altin ~= (altin and true or false) or st.Asama ~= asama then
		yeniKutu(st, altin, asama)
	end
end

-- Makine bir paket yaptı
function PaketGorsel.Uretim(sira, makine, altin)
	local st = arsalar[sira]
	if not st or not sahipli(st) then
		return
	end
	if (st.Model:GetAttribute("Makine") or 0) < makine then
		return
	end
	local F = cerceve(st)
	if not F then
		return
	end
	local kamera = Workspace.CurrentCamera
	if kamera and (kamera.CFrame.Position - F.Position).Magnitude > GORUS_UZAKLIGI then
		return
	end

	-- pres aşağı inip kalksın
	local dukkan = st.Model:FindFirstChild("Dukkan")
	local m = dukkan and dukkan:FindFirstChild("Makine" .. makine)
	local kol = m and m:FindFirstChild("Kol")
	if kol and not st.PresCalisiyor[makine] then
		st.PresCalisiyor[makine] = true
		local ust = kol.CFrame
		local alt = ust - Vector3.new(0, 1.3, 0)
		local inis = TweenService:Create(kol, TweenInfo.new(0.1, Enum.EasingStyle.Quad, Enum.EasingDirection.In), { CFrame = alt })
		inis:Play()
		inis.Completed:Connect(function()
			if not kol.Parent then
				st.PresCalisiyor[makine] = false -- bu arada dükkan yeniden kuruldu
				return
			end
			local kalkis = TweenService:Create(kol, TweenInfo.new(0.16, Enum.EasingStyle.Quad, Enum.EasingDirection.Out), { CFrame = ust })
			kalkis:Play()
			kalkis.Completed:Connect(function()
				st.PresCalisiyor[makine] = false
			end)
		end)
	end

	if st.Bantta >= EN_FAZLA_BANTTA then
		return
	end
	st.Bantta += 1
	local x = Y.MakineX[makine]
	local tepe = Y.TezgahYuksekligi
	local bas = F * CFrame.new(x, tepe, Y.MakineZ - 0.4)
	local paket = Gorsel.KapaliKutu(bas, stilOku(st, altin), Config.BantKutuOlcegi)
	for _, p in ipairs(paket) do
		p.Parent = klasor
	end
	local noktalar = {
		F * CFrame.new(x, tepe, Y.BantZ),
		F * CFrame.new(Y.BantX, tepe, Y.BantZ),
		satisNoktasi(F),
	}
	local stil = stilOku(st, altin)
	yoldanGotur(paket, bas, noktalar, 11, function()
		st.Bantta -= 1
		if PaketGorsel.SatisaGeldi then
			PaketGorsel.SatisaGeldi(sira, stil)
		end
	end)
end

function PaketGorsel.Baslat(config, gorsel)
	Config = config
	Gorsel = gorsel
	Y = Config.Yerlesim
	klasor = Instance.new("Folder")
	klasor.Name = "BurgerGorselleri"
	klasor.Parent = Workspace

	local harita = Workspace:WaitForChild("Harita")
	local arsaKlasoru = harita:WaitForChild("Arsalar")
	for i = 1, Config.ArsaSayisi do
		local model = arsaKlasoru:WaitForChild("Arsa" .. i, 10)
		if model then
			local st = { Sira = i, Model = model, Asama = 0, Altin = false, Bantta = 0, PresCalisiyor = {} }
			arsalar[i] = st
			local function yenile()
				if sahipli(st) then
					yeniKutu(st, st.Altin, st.Asama)
				else
					temizle(st)
				end
			end
			model:GetAttributeChangedSignal("Sahip"):Connect(function()
				st.Asama = 0
				st.Altin = false
				yenile()
			end)
			model:GetAttributeChangedSignal("Renk"):Connect(yenile)
			model:GetAttributeChangedSignal("Logo"):Connect(yenile)
			yenile()
		end
	end
end

return PaketGorsel
