-- BURGER İMPARATORU - Müşteriler ve çalışanların hareketleri (sadece bu oyuncunun ekranında)
-- Müşteriler kaldırımdan gelir, satış penceresinde sıraya girer, paketini alıp kucağında taşır,
-- masa varsa bazen oturup yer, sonra gider. Dükkanın ünü ve makineleri arttıkça daha sık gelirler.

local RunService = game:GetService("RunService")
local Workspace = game:GetService("Workspace")

local Musteriler = {}

local Config, Gorsel, Y
local klasor
local arsalar = {}
local rastgele = Random.new()
local GORUS = 230 -- bundan uzaktaki dükkanlara müşteri gönderme

local GOMLEKLER = {
	Color3.fromRGB(230, 70, 70), Color3.fromRGB(70, 130, 230), Color3.fromRGB(250, 200, 60),
	Color3.fromRGB(90, 190, 110), Color3.fromRGB(170, 100, 220), Color3.fromRGB(245, 140, 60),
	Color3.fromRGB(240, 240, 240), Color3.fromRGB(60, 60, 70), Color3.fromRGB(240, 120, 170),
}
local PANTOLONLAR = {
	Color3.fromRGB(40, 60, 110), Color3.fromRGB(50, 50, 55), Color3.fromRGB(110, 85, 60),
	Color3.fromRGB(80, 80, 90), Color3.fromRGB(30, 90, 70),
}
local YUZLER = { "🙂", "😀", "😋", "🤩", "😎", "🥳", "😊", "🤗", "😄" }

local function cerceve(st)
	local taban = st.Model:FindFirstChild("Taban")
	if not taban then
		return nil
	end
	return taban.CFrame * CFrame.new(0, taban.Size.Y / 2, 0)
end

local function sahipli(st)
	local s = st.Model:GetAttribute("Sahip")
	return type(s) == "number" and s ~= 0
end

local function sec(liste)
	return liste[rastgele:NextInteger(1, #liste)]
end

local function musteriyiSil(m)
	for _, p in ipairs(m.Insan.Liste) do
		p:Destroy()
	end
	if m.Paket then
		for _, p in ipairs(m.Paket) do
			p:Destroy()
		end
	end
	m.Silindi = true
end

local function listedenCikar(liste, x)
	for i, v in ipairs(liste) do
		if v == x then
			table.remove(liste, i)
			return
		end
	end
end

local function kuyrukYeri(st, F, n)
	local s = Y.SatisNoktasi
	return F * Vector3.new(s.X, 0, s.Z - 4 - (n - 1) * Y.KuyrukAralik)
end

local function koltuklar(F)
	local liste = {}
	for _, masa in ipairs(Y.Masalar) do
		for k = 0, 3 do
			local a = k * math.pi / 2 + math.rad(45)
			local yer = F * (CFrame.new(masa.X, 0, masa.Z) * CFrame.Angles(0, a, 0) * CFrame.new(0, 0.6, 3.4))
			local masaMerkez = F * Vector3.new(masa.X, 0.6, masa.Z)
			table.insert(liste, CFrame.lookAt(yer.Position, Vector3.new(masaMerkez.X, yer.Position.Y, masaMerkez.Z)))
		end
	end
	return liste
end

local function musteriYap(st, F)
	local giris = Y.MusteriGiris
	local sx = rastgele:NextNumber(-28, 32)
	local m = {
		Arsa = st,
		Konum = F * Vector3.new(sx, 0, giris),
		Faz = rastgele:NextNumber(0, 6),
		Durum = "geliyor",
		Yol = { F * Vector3.new(Y.SatisNoktasi.X + rastgele:NextNumber(-4, 4), 0, -27) },
	}
	m.Insan = Gorsel.Insan(CFrame.new(m.Konum), {
		Gomlek = sec(GOMLEKLER),
		Pantolon = sec(PANTOLONLAR),
		Ten = sec(Gorsel.Tenler),
		Yuz = sec(YUZLER),
		Sapka = rastgele:NextNumber() < 0.35 and "kep" or nil,
		SapkaRenk = sec(GOMLEKLER),
	})
	for _, p in ipairs(m.Insan.Liste) do
		p.CanQuery = false
		p.Parent = klasor
	end
	table.insert(st.Liste, m)
	table.insert(st.Kuyruk, m)
end

-- Müşteri paketini alır
local function servisEt(st, m, stil)
	if m.Durum ~= "sirada" and m.Durum ~= "geliyor" then
		return
	end
	listedenCikar(st.Kuyruk, m)
	m.Durum = "aliyor"
	m.Bekle = 0.5
	m.Paket = Gorsel.KapaliKutu(CFrame.new(m.Konum), stil or {
		Renk = st.Model:GetAttribute("Renk") or Color3.fromRGB(220, 55, 50),
		Logo = st.Model:GetAttribute("Logo") or "🍔",
		Altin = false,
	}, 0.3)
	for _, p in ipairs(m.Paket) do
		p.CanQuery = false
		p.Parent = klasor
	end
	st.KasiyerSelam = 0.6
end

-- Bantta gelen bir paket satış penceresine ulaştı: sıradaki ilk müşteri alsın
function Musteriler.PaketGeldi(sira, stil)
	local st = arsalar[sira]
	if not st then
		return
	end
	local ilk = st.Kuyruk[1]
	if ilk and ilk.Durum == "sirada" then
		servisEt(st, ilk, stil)
	end
end

-- Aşçı tezgahta katladı: kolları hareket etsin
function Musteriler.AsciKatladi(sira)
	local st = arsalar[sira]
	if st then
		st.AsciHareket = 0.35
	end
end

local function cikisYolu(F, m)
	local s = Y.SatisNoktasi
	local yon = rastgele:NextNumber() < 0.5 and -1 or 1
	return {
		F * Vector3.new(s.X + 6, 0, -27),
		F * Vector3.new(s.X + 8, 0, Y.MusteriGiris),
		F * Vector3.new(yon * 48, 0, Y.MusteriGiris),
	}
end

local function yuru(m, hedef, dt)
	local fark = hedef - m.Konum
	fark = Vector3.new(fark.X, 0, fark.Z)
	local mesafe = fark.Magnitude
	local adim = Config.Musteri.Hiz * dt
	if mesafe <= adim or mesafe < 0.05 then
		m.Konum = Vector3.new(hedef.X, m.Konum.Y, hedef.Z)
		return true
	end
	local yon = fark.Unit
	m.Konum += yon * adim
	m.Yon = yon
	m.Faz += dt * 11
	return false
end

local function guncelle(dt)
	local kamera = Workspace.CurrentCamera
	local kameraYeri = kamera and kamera.CFrame.Position or Vector3.zero
	for _, st in pairs(arsalar) do
		local F = cerceve(st)
		if F then
			local yakin = (F.Position - kameraYeri).Magnitude < GORUS
			local acik = sahipli(st)

			-- yeni müşteri
			if acik and yakin then
				st.Sayac -= dt
				if st.Sayac <= 0 then
					local un = st.Model:GetAttribute("Un") or 1
					local makine = st.Model:GetAttribute("Makine") or 0
					local aralik = math.max(Config.Musteri.EnKisaAralik, Config.Musteri.TemelAralik / (un * (1 + makine * 0.35)))
					st.Sayac = aralik * rastgele:NextNumber(0.7, 1.3)
					if #st.Liste < Config.Musteri.EnFazla then
						musteriYap(st, F)
					end
				end
			elseif not acik and #st.Liste > 0 then
				for _, m in ipairs(st.Liste) do
					musteriyiSil(m)
				end
				st.Liste = {}
				st.Kuyruk = {}
				st.Koltuk = {}
			end

			-- müşterileri yürüt
			for i = #st.Liste, 1, -1 do
				local m = st.Liste[i]
				local yurudu = false
				if m.Durum == "geliyor" or m.Durum == "sirada" then
					local sira = table.find(st.Kuyruk, m) or 1
					local hedef
					if #m.Yol > 0 then
						hedef = m.Yol[1]
					else
						hedef = kuyrukYeri(st, F, sira)
					end
					local vardi = yuru(m, hedef, dt)
					yurudu = not vardi
					if vardi then
						if #m.Yol > 0 then
							table.remove(m.Yol, 1)
						else
							if m.Durum == "geliyor" then
								m.Durum = "sirada"
								m.Bekle = rastgele:NextNumber(1.5, 3.5)
							end
							m.Yon = (F * Vector3.new(0, 0, 1) - F.Position).Unit -- pencereye bak
							if sira == 1 then
								m.Bekle -= dt
								if m.Bekle <= 0 then
									servisEt(st, m, nil)
								end
							end
						end
					end
				elseif m.Durum == "aliyor" then
					m.Bekle -= dt
					if m.Bekle <= 0 then
						-- masa varsa bazen oturup yesin
						if st.Model:GetAttribute("Oturma") and rastgele:NextNumber() < 0.4 then
							local liste = koltuklar(F)
							for k, cf in ipairs(liste) do
								if not st.Koltuk[k] then
									st.Koltuk[k] = m
									m.Koltuk = k
									m.KoltukCf = cf
									m.Yol = { cf.Position - Vector3.new(0, cf.Position.Y - m.Konum.Y, 0) }
									m.Durum = "masaya"
									break
								end
							end
						end
						if m.Durum == "aliyor" then
							m.Durum = "gidiyor"
							m.Yol = cikisYolu(F, m)
						end
					end
				elseif m.Durum == "masaya" then
					if yuru(m, m.Yol[1], dt) then
						m.Durum = "oturuyor"
						m.Bekle = rastgele:NextNumber(4, 8)
					else
						yurudu = true
					end
				elseif m.Durum == "oturuyor" then
					m.Bekle -= dt
					if m.Bekle <= 0 then
						st.Koltuk[m.Koltuk] = nil
						m.Durum = "gidiyor"
						m.Yol = cikisYolu(F, m)
					end
				elseif m.Durum == "gidiyor" then
					if #m.Yol == 0 or yuru(m, m.Yol[1], dt) then
						if #m.Yol > 0 then
							table.remove(m.Yol, 1)
						end
						if #m.Yol == 0 then
							musteriyiSil(m)
							table.remove(st.Liste, i)
						end
					else
						yurudu = true
					end
				end

				if not m.Silindi then
					local yon = m.Yon or Vector3.new(0, 0, -1)
					local oturuyor = m.Durum == "oturuyor"
					local cf
					if oturuyor then
						cf = m.KoltukCf
					else
						cf = CFrame.lookAt(m.Konum, m.Konum + yon)
					end
					local salinim = yurudu and math.sin(m.Faz) * 0.6 or 0
					local tasiyor = m.Paket ~= nil and not oturuyor
					Gorsel.InsanTasi(m.Insan, cf, salinim, oturuyor, tasiyor and 1.25 or nil)
					if m.Paket then
						local kutuCf
						if oturuyor then
							-- paket masada
							local masaYonu = cf.LookVector
							kutuCf = CFrame.new(cf.Position + masaYonu * 1.6 + Vector3.new(0, 2.23, 0)) * (cf - cf.Position)
						else
							kutuCf = cf * CFrame.new(0, 2.55, -1.15)
						end
						local bas = m.Paket[1]
						local goreli = m.PaketGoreli
						if not goreli then
							goreli = {}
							for _, p in ipairs(m.Paket) do
								goreli[p] = bas.CFrame:ToObjectSpace(p.CFrame)
							end
							m.PaketGoreli = goreli
							m.PaketTabanFarki = bas.CFrame.Position.Y - m.Konum.Y
						end
						local tepsiCf = kutuCf * CFrame.new(0, m.PaketTabanFarki, 0)
						for _, p in ipairs(m.Paket) do
							p.CFrame = tepsiCf * goreli[p]
						end
					end
				end
			end

			-- çalışanlar: hafif kıpırdanma; aşçı katlarken, kasiyer müşteri alırken el sallar
			if yakin then
				st.ZamanCalisan = (st.ZamanCalisan or 0) + dt
				if st.AsciHareket then
					st.AsciHareket -= dt
					if st.AsciHareket <= 0 then
						st.AsciHareket = nil
					end
				end
				if st.KasiyerSelam then
					st.KasiyerSelam -= dt
					if st.KasiyerSelam <= 0 then
						st.KasiyerSelam = nil
					end
				end
				local dukkan = st.Model:FindFirstChild("Dukkan")
				if dukkan then
					for _, c in ipairs(dukkan:GetChildren()) do
						local rol = c:IsA("Model") and c:GetAttribute("Rol")
						if rol then
							local kayit = st.Calisanlar[c]
							if not kayit then
								local govde = c:FindFirstChild("Govde")
								if govde then
									kayit = {
										Taban = govde.CFrame * CFrame.new(0, -3, 0),
										SolKol = c:FindFirstChild("SolKol"),
										SagKol = c:FindFirstChild("SagKol"),
									}
									st.Calisanlar[c] = kayit
								end
							end
							if kayit and kayit.SolKol and kayit.SagKol then
								local t = st.ZamanCalisan
								local sol, sag
								if rol == "Asci" and st.AsciHareket then
									sol, sag = 1.2 + math.sin(t * 30) * 0.3, 1.2 - math.sin(t * 30) * 0.3
								elseif rol == "Kasiyer" and st.KasiyerSelam then
									sol, sag = 0.1, math.rad(170) + math.sin(t * 20) * 0.3
								elseif rol == "Temizlikci" then
									sol, sag = 0.9 + math.sin(t * 4) * 0.4, 0.9 + math.sin(t * 4) * 0.4
								else
									local k = math.sin(t * 2 + #rol) * 0.08
									sol, sag = k, -k
								end
								local omuz = kayit.Taban * CFrame.new(0, 3.8, 0)
								kayit.SolKol.CFrame = omuz * CFrame.new(-1.5, 0, 0) * CFrame.Angles(sol, 0, 0) * CFrame.new(0, -0.8, 0)
								kayit.SagKol.CFrame = omuz * CFrame.new(1.5, 0, 0) * CFrame.Angles(sag, 0, 0) * CFrame.new(0, -0.8, 0)
							end
						end
					end
				end
			end
		end
	end
end

function Musteriler.Baslat(config, gorsel)
	Config = config
	Gorsel = gorsel
	Y = Config.Yerlesim
	klasor = Instance.new("Folder")
	klasor.Name = "Musteriler"
	klasor.Parent = Workspace
	local harita = Workspace:WaitForChild("Harita")
	local arsaKlasoru = harita:WaitForChild("Arsalar")
	for i = 1, Config.ArsaSayisi do
		local model = arsaKlasoru:WaitForChild("Arsa" .. i, 10)
		if model then
			arsalar[i] = {
				Model = model,
				Liste = {},
				Kuyruk = {},
				Koltuk = {},
				Sayac = rastgele:NextNumber(0.5, 3),
				Calisanlar = setmetatable({}, { __mode = "k" }),
			}
		end
	end
	RunService.RenderStepped:Connect(guncelle)
end

return Musteriler
