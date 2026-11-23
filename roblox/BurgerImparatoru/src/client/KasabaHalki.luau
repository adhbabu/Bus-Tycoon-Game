-- BURGER İMPARATORU - Kasaba halkı (sadece bu oyuncunun ekranında)
-- Meydanda ve kaldırımlarda gezen, durup konuşan insanlar; heykelin önünde huss dansı yapanlar;
-- meydandaki NPC'lerin el sallaması.

local RunService = game:GetService("RunService")
local Workspace = game:GetService("Workspace")
local Dil = require(game:GetService("ReplicatedStorage"):WaitForChild("Ortak"):WaitForChild("Dil"))

local KasabaHalki = {}

local Config, Gorsel
local klasor
local rastgele = Random.new()
local gezenler = {}
local dansedenler = {}
local npcKayit = setmetatable({}, { __mode = "k" })
local zaman = 0
local GORUS = 320

local GOMLEKLER = {
	Color3.fromRGB(230, 70, 70), Color3.fromRGB(70, 130, 230), Color3.fromRGB(250, 200, 60),
	Color3.fromRGB(90, 190, 110), Color3.fromRGB(170, 100, 220), Color3.fromRGB(245, 140, 60),
	Color3.fromRGB(240, 240, 240), Color3.fromRGB(240, 120, 170), Color3.fromRGB(60, 190, 190),
}
local PANTOLONLAR = { Color3.fromRGB(40, 60, 110), Color3.fromRGB(50, 50, 55), Color3.fromRGB(110, 85, 60), Color3.fromRGB(30, 90, 70) }
local YUZLER = { "🙂", "😀", "😋", "🤩", "😎", "🥳", "😊", "🤗", "😄", "😜" }

local function sec(liste)
	return liste[rastgele:NextInteger(1, #liste)]
end

-- Kare bir döngü üzerinde, d mesafesindeki nokta ve yön
local function karedeNokta(R, d, y)
	local L = 8 * R
	d = d % L
	local kenar = math.floor(d / (2 * R))
	local t = d - kenar * 2 * R
	if kenar == 0 then
		return Vector3.new(-R + t, y, -R), Vector3.new(1, 0, 0)
	elseif kenar == 1 then
		return Vector3.new(R, y, -R + t), Vector3.new(0, 0, 1)
	elseif kenar == 2 then
		return Vector3.new(R - t, y, R), Vector3.new(-1, 0, 0)
	else
		return Vector3.new(-R, y, R - t), Vector3.new(0, 0, -1)
	end
end

local function balonYap(bas)
	local bb = Instance.new("BillboardGui")
	bb.Size = UDim2.new(8, 0, 1.7, 0)
	bb.StudsOffset = Vector3.new(0, 2.3, 0)
	bb.MaxDistance = 70
	bb.LightInfluence = 0
	bb.Enabled = false
	bb.Parent = bas
	local f = Instance.new("Frame")
	f.AnchorPoint = Vector2.new(0.5, 0.5)
	f.Position = UDim2.fromScale(0.5, 0.5)
	f.Size = UDim2.fromScale(1, 1)
	f.BackgroundColor3 = Color3.new(1, 1, 1)
	f.Parent = bb
	local k = Instance.new("UICorner")
	k.CornerRadius = UDim.new(0.4, 0)
	k.Parent = f
	local t = Instance.new("TextLabel")
	t.Name = "Yazi"
	t.BackgroundTransparency = 1
	t.Size = UDim2.fromScale(0.92, 0.8)
	t.Position = UDim2.fromScale(0.04, 0.1)
	t.TextScaled = true
	t.FontFace = Font.fromEnum(Enum.Font.FredokaOne)
	t.TextColor3 = Color3.fromRGB(45, 30, 20)
	t.Parent = f
	return bb, t
end

local function insanYap(cf)
	local insan = Gorsel.Insan(cf, {
		Gomlek = sec(GOMLEKLER),
		Pantolon = sec(PANTOLONLAR),
		Ten = sec(Gorsel.Tenler),
		Yuz = sec(YUZLER),
		Sapka = rastgele:NextNumber() < 0.35 and "kep" or nil,
		SapkaRenk = sec(GOMLEKLER),
	})
	for _, p in ipairs(insan.Liste) do
		p.CanQuery = false
		p.Parent = klasor
	end
	return insan
end

local function konus(kisi, metin, sure)
	kisi.BalonYazi.Text = metin
	kisi.Balon.Enabled = true
	kisi.BalonSure = sure or 3
end

local function guncelle(dt)
	zaman += dt
	local kamera = Workspace.CurrentCamera
	local kameraYeri = kamera and kamera.CFrame.Position or Vector3.zero
	if kameraYeri.Magnitude > GORUS then
		return -- meydandan uzaktayken kimseyi oynatma
	end

	for _, g in ipairs(gezenler) do
		-- konuşma balonu
		if g.BalonSure then
			g.BalonSure -= dt
			if g.BalonSure <= 0 then
				g.BalonSure = nil
				g.Balon.Enabled = false
			end
		end
		g.Konus -= dt
		if g.Konus <= 0 then
			g.Konus = rastgele:NextNumber(7, 16)
			g.Durak = rastgele:NextNumber(1.5, 3)
			konus(g, Dil.T(sec(Config.KasabaSozleri)), 3)
		end
		local yurudu = false
		if g.Durak and g.Durak > 0 then
			g.Durak -= dt
		else
			g.D += g.Yon * g.Hiz * dt
			g.Faz += dt * 10
			yurudu = true
		end
		local p, ileri = karedeNokta(g.R, g.D, g.Y)
		ileri *= g.Yon
		local cf = CFrame.lookAt(p, p + ileri)
		Gorsel.InsanTasi(g.Insan, cf, yurudu and math.sin(g.Faz) * 0.6 or 0, false)
	end

	-- huss dansı: kollar yukarı aşağı, hafif zıplama
	for i, d in ipairs(dansedenler) do
		local vurus = zaman * 4.2 + i * 0.7
		local yukari = math.abs(math.sin(vurus))
		local cf = d.Cf * CFrame.new(0, yukari * 0.45, 0) * CFrame.Angles(0, math.sin(zaman * 1.3 + i) * 0.25, 0)
		Gorsel.InsanTasi(d.Insan, cf, 0, false, math.rad(150) * yukari + 0.2)
		if d.BalonSure then
			d.BalonSure -= dt
			if d.BalonSure <= 0 then
				d.BalonSure = nil
				d.Balon.Enabled = false
			end
		end
		d.Konus -= dt
		if d.Konus <= 0 then
			d.Konus = rastgele:NextNumber(5, 11)
			konus(d, "HUSS! HUSS! HUSS!", 2)
		end
	end

	-- meydandaki NPC'ler arada el sallar
	local harita = Workspace:FindFirstChild("Harita")
	local npcler = harita and harita:FindFirstChild("NPCler")
	if npcler then
		for i, m in ipairs(npcler:GetChildren()) do
			if m:IsA("Model") and m:GetAttribute("Rol") == "NPC" then
				local k = npcKayit[m]
				if not k then
					local govde = m:FindFirstChild("Govde")
					if govde then
						k = { Taban = govde.CFrame * CFrame.new(0, -3, 0), Sag = m:FindFirstChild("SagKol") }
						npcKayit[m] = k
					end
				end
				if k and k.Sag then
					local dongu = (zaman + i * 1.7) % 5
					local aci = 0
					if dongu < 1.2 then
						aci = math.rad(165) + math.sin(zaman * 14) * 0.35
					end
					local omuz = k.Taban * CFrame.new(0, 3.8, 0)
					k.Sag.CFrame = omuz * CFrame.new(1.5, 0, 0) * CFrame.Angles(aci, 0, 0) * CFrame.new(0, -0.8, 0)
				end
			end
		end
	end
end

function KasabaHalki.Baslat(config, gorsel)
	Config = config
	Gorsel = gorsel
	klasor = Instance.new("Folder")
	klasor.Name = "KasabaHalki"
	klasor.Parent = Workspace

	-- gezenler: yarısı meydanda, yarısı kaldırımda
	for i = 1, Config.KasabaHalkiSayisi do
		local meydanda = i % 2 == 0
		local R = meydanda and rastgele:NextNumber(36, 42) or rastgele:NextNumber(68, 70)
		local g = {
			R = R,
			Y = meydanda and 0.4 or 0.5,
			D = rastgele:NextNumber(0, 8 * R),
			Yon = rastgele:NextNumber() < 0.5 and -1 or 1,
			Hiz = rastgele:NextNumber(4, 7),
			Faz = rastgele:NextNumber(0, 6),
			Konus = rastgele:NextNumber(1, 12),
		}
		local p = karedeNokta(g.R, g.D, g.Y)
		g.Insan = insanYap(CFrame.new(p))
		g.Balon, g.BalonYazi = balonYap(g.Insan.Bas)
		table.insert(gezenler, g)
	end

	-- dans edenler: heykelin önünde halka
	for i = 1, 6 do
		local a = i / 6 * math.pi * 2
		local p = Vector3.new(math.cos(a) * 13, 0.4, math.sin(a) * 13)
		local cf = CFrame.lookAt(p, p + Vector3.new(math.cos(a), 0, math.sin(a)))
		local d = { Cf = cf, Insan = insanYap(cf), Konus = rastgele:NextNumber(0, 6) }
		d.Balon, d.BalonYazi = balonYap(d.Insan.Bas)
		table.insert(dansedenler, d)
	end

	RunService.RenderStepped:Connect(guncelle)
end

return KasabaHalki
