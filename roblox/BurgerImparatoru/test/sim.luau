-- Burger İmparatoru smoke test: runs the real server + client scripts from the built .rbxlx
-- inside Lune, with real Roblox instances (property names validated against the reflection
-- database) and fake engine services (signals, tweens, remotes, datastore).
local fs = require("@lune/fs")
local roblox = require("@lune/roblox")
local luau = require("@lune/luau")
local task = require("@lune/task")
local process = require("@lune/process")

local placePath = process.args[1]
local DATASTORE_MODE = process.args[2] or "ok" -- "ok" | "studio"
local game_real = roblox.deserializePlace(fs.readFile(placePath))
-- testte Altın Saat beklemesin
for _, d in ipairs(game_real:GetDescendants()) do
	if d.Name == "Config" and d:IsA("ModuleScript") then
		d.Source = string.gsub(d.Source, "Config.AltinSaat = { EnAz = 300, EnCok = 480,", "Config.AltinSaat = { EnAz = 2, EnCok = 3,")
	end
end
-- dil: "tr" (varsayılan) ya da "en" argümanı; foto modu İngilizce çeker
local DIL = DATASTORE_MODE == "foto" and "en" or "tr"
for _, a in ipairs(process.args) do
	if a == "en" or a == "tr" then
		DIL = a
	end
end
for _, d in ipairs(game_real:GetDescendants()) do
	if d.Name == "Config" and d:IsA("ModuleScript") then
		d.Source = string.gsub(d.Source, 'Config.Dil = "otomatik"', 'Config.Dil = "' .. DIL .. '"')
	end
end
if process.args[3] == "ses" then
	for _, d in ipairs(game_real:GetDescendants()) do
		if d.Name == "Config" and d:IsA("ModuleScript") then
			d.Source = string.gsub(d.Source, 'Config.HussSesId = "[^"]*"', 'Config.HussSesId = "rbxassetid://123"')
		end
	end
end

local ERRORS = {}
local LOG = {}
local function logf(...)
	table.insert(LOG, string.format(...))
end

local function TB(e)
	local ok, tb = pcall(debug.traceback, tostring(e), 2)
	return ok and tb or tostring(e)
end

local function reportError(e)
	table.insert(ERRORS, tostring(e))
	print("!! SCRIPT ERROR: " .. tostring(e))
end

----------------------------------------------------------------------
-- Signals
----------------------------------------------------------------------
local function newSignal(name)
	local s = { conns = {}, name = name }
	function s:Connect(fn)
		local c = { fn = fn, Connected = true }
		function c:Disconnect()
			c.Connected = false
		end
		table.insert(s.conns, c)
		return c
	end
	function s:Once(fn)
		local c
		c = s:Connect(function(...)
			c:Disconnect()
			fn(...)
		end)
		return c
	end
	function s:Fire(...)
		for _, c in ipairs(s.conns) do
			if c.Connected then
				local args = table.pack(...)
				task.spawn(function()
					local ok, e = xpcall(c.fn, TB, table.unpack(args, 1, args.n))
					if not ok then
						reportError(e)
					end
				end)
			end
		end
	end
	function s:FireSync(...)
		for _, c in ipairs(s.conns) do
			if c.Connected then
				local ok, e = xpcall(c.fn, TB, ...)
				if not ok then
					reportError(e)
				end
			end
		end
	end
	return s
end

----------------------------------------------------------------------
-- Proxy wrapping of real instances
----------------------------------------------------------------------
local nextId = 0
local proxies = {} -- id -> proxy
local realOf = setmetatable({}, { __mode = "k" }) -- proxy -> real
local proxyExtra = setmetatable({}, { __mode = "k" }) -- proxy -> extra
local extra = {} -- id -> { signals = {}, props = {}, methods = {} }

local wrap, unwrap

local EVENT_NAMES = {
	Activated = true,
	OnServerEvent = true,
	OnClientEvent = true,
	CharacterAdded = true,
	PlayerAdded = true,
	PlayerRemoving = true,
	Heartbeat = true,
	RenderStepped = true,
	Changed = true,
	MouseButton1Click = true,
	ChildAdded = true,
	DescendantAdded = true,
	Triggered = true,
}

-- Runtime-only properties that exist in Roblox but are not in the serialized DOM
local RUNTIME_PROPS = {
	Character = true,
	DisplayName = true,
	UserId = true,
	LocalPlayer = true,
	CurrentCamera = true,
	ViewportSize = true,
	KeyboardEnabled = true,
	TouchEnabled = true,
	TimePosition = true,
	IsPlaying = true,
}

local customMethods = {} -- className or "*" -> name -> function(proxy, ...)

local function idOf(real)
	local id = real:GetAttribute("__simid")
	if id == nil then
		nextId += 1
		id = nextId
		real:SetAttribute("__simid", id)
	end
	return id
end

local function findCustom(real, key)
	local cls = real.ClassName
	if customMethods[cls] and customMethods[cls][key] then
		return customMethods[cls][key]
	end
	-- inheritance-ish checks
	for k, tbl in pairs(customMethods) do
		if k ~= "*" and k ~= cls and tbl[key] and real:IsA(k) then
			return tbl[key]
		end
	end
	return customMethods["*"][key]
end

wrap = function(v)
	if typeof(v) ~= "Instance" then
		return v
	end
	local id = idOf(v)
	local p = proxies[id]
	if p then
		return p
	end
	p = newproxy(true)
	local mt = getmetatable(p)
	extra[id] = { signals = {}, props = {} }
	local ex = extra[id]
	mt.__index = function(_, key)
		local real = v
		local custom = findCustom(real, key)
		if custom then
			return custom
		end
		if EVENT_NAMES[key] then
			ex.signals[key] = ex.signals[key] or newSignal(key)
			return ex.signals[key]
		end
		if ex.props[key] ~= nil then
			return ex.props[key]
		end
		local ok, val = pcall(function()
			return real[key]
		end)
		if ok and val ~= nil then
			if type(val) == "function" then
				return function(_, ...)
					local args = table.pack(...)
					for i = 1, args.n do
						args[i] = unwrap(args[i])
					end
					local res = table.pack(val(real, table.unpack(args, 1, args.n)))
					for i = 1, res.n do
						if type(res[i]) == "table" then
							local t = {}
							for j, x in ipairs(res[i]) do
								t[j] = wrap(x)
							end
							res[i] = t
						else
							res[i] = wrap(res[i])
						end
					end
					return table.unpack(res, 1, res.n)
				end
			end
			return wrap(val)
		end
		if not ok and string.find(tostring(val), "destroyed") then
			-- Roblox: silinmiş bir parçanın özellikleri okunabilir, Parent'ı nil olur
			return nil
		end
		local okC, child = pcall(function()
			return real:FindFirstChild(key)
		end)
		if not okC then
			return nil -- silinmiş parça: Roblox'ta özellikler nil/varsayılan döner
		end
		if child then
			return wrap(child)
		end
		if RUNTIME_PROPS[key] then
			return nil
		end
		if ok and val == nil then
			return nil -- valid property that is nil (e.g. Parent of a new instance)
		end
		error(string.format("%s is not a valid member of %s \"%s\" (%s)", tostring(key), real.ClassName, real.Name, tostring(val)), 2)
	end
	mt.__newindex = function(_, key, value)
		if RUNTIME_PROPS[key] then
			ex.props[key] = value
			return
		end
		local okEski, eski = pcall(function()
			return v[key]
		end)
		local ok, err = pcall(function()
			v[key] = unwrap(value)
		end)
		if ok then
			-- GetPropertyChangedSignal ve DescendantAdded (Roblox gibi)
			if ex.propSig and ex.propSig[key] and (not okEski or eski ~= unwrap(value)) then
				ex.propSig[key]:Fire()
			end
			if key == "Parent" and value ~= nil then
				local eklenen = nil
				local a = v.Parent
				while a do
					local e = extra[idOf(a)]
					local sig = e and e.signals.DescendantAdded
					if sig and #sig.conns > 0 then
						if not eklenen then
							eklenen = { v }
							for _, d in ipairs(v:GetDescendants()) do
								table.insert(eklenen, d)
							end
						end
						for _, d in ipairs(eklenen) do
							sig:Fire(wrap(d))
						end
					end
					a = a.Parent
				end
			end
		end
		if not ok and string.find(tostring(err), "destroyed") and key ~= "Parent" then
			return -- Roblox: silinmiş parçanın özelliği değiştirilebilir, hata vermez
		end
		if not ok then
			error(string.format("Cannot set %s.%s = %s: %s", v.ClassName, tostring(key), tostring(value), tostring(err)), 2)
		end
	end
	mt.__tostring = function()
		return v.Name
	end
	mt.__eq = function(a, b)
		return rawequal(a, b)
	end
	proxies[id] = p
	realOf[p] = v
	proxyExtra[p] = ex
	return p
end

unwrap = function(v)
	if type(v) == "userdata" and realOf[v] then
		return realOf[v]
	end
	return v
end

local function isProxy(v)
	return type(v) == "userdata" and realOf[v] ~= nil
end

----------------------------------------------------------------------
-- Custom engine behavior
----------------------------------------------------------------------
local LOCAL_PLAYER = nil
local CLOSE_HANDLERS = {}
local BOUND_ACTIONS = {}
local CAMERA = nil
local STATS = { tweens = 0, sounds = 0, emits = 0, pivots = 0, systemMessages = {} }
local DATASTORE = {}

customMethods["*"] = {
	WaitForChild = function(self, name, timeout)
		local real = unwrap(self)
		local t0 = os.clock()
		while true do
			local c = real:FindFirstChild(name)
			if c then
				return wrap(c)
			end
			if os.clock() - t0 > (timeout or 5) then
				if timeout then
					return nil
				end
				error("Infinite yield: WaitForChild(" .. name .. ") on " .. real:GetFullName())
			end
			task.wait(0.01)
		end
	end,
	GetPropertyChangedSignal = function(self, prop)
		local ex = proxyExtra[self]
		ex.propSig = ex.propSig or {}
		ex.propSig[prop] = ex.propSig[prop] or newSignal("PropChanged:" .. prop)
		return ex.propSig[prop]
	end,
	GetAttributeChangedSignal = function(self, ad)
		local ex = proxyExtra[self]
		ex.attr = ex.attr or {}
		ex.attr[ad] = ex.attr[ad] or newSignal("Attr:" .. ad)
		return ex.attr[ad]
	end,
	SetAttribute = function(self, ad, deger)
		local real = unwrap(self)
		local eski = real:GetAttribute(ad)
		real:SetAttribute(ad, deger)
		local ex = proxyExtra[self]
		if eski ~= deger and ex.attr and ex.attr[ad] then
			ex.attr[ad]:Fire()
		end
	end,
}
customMethods.Workspace = {
	GetServerTimeNow = function()
		return os.clock()
	end,
}
customMethods.Terrain = {
	FillBall = function(self, merkez, yaricap, malzeme)
		assert(typeof(merkez) == "Vector3" and type(yaricap) == "number" and typeof(malzeme) == "EnumItem", "FillBall args")
		STATS.arazi = (STATS.arazi or 0) + 1
	end,
	FillCylinder = function(self, cf, boy, yaricap, malzeme)
		assert(typeof(cf) == "CFrame" and type(boy) == "number" and type(yaricap) == "number" and typeof(malzeme) == "EnumItem", "FillCylinder args")
		STATS.arazi = (STATS.arazi or 0) + 1
	end,
}
customMethods.TextService = {
	FilterStringAsync = function(self, metin, userId)
		assert(type(metin) == "string" and type(userId) == "number", "FilterStringAsync args")
		return {
			GetNonChatStringForBroadcastAsync = function()
				-- fake filter: hide a "bad" test word like Roblox does with hashtags
				return (string.gsub(metin, "kotu", "####"))
			end,
		}
	end,
}
customMethods.Model = {
	PivotTo = function(self, cf)
		assert(typeof(cf) == "CFrame", "PivotTo expects CFrame")
		STATS.pivots += 1
	end,
}
customMethods.Sound = {
	Play = function(self)
		local real = unwrap(self)
		assert(real.SoundId ~= "", "Sound played without SoundId")
		STATS.sounds += 1
	end,
	Stop = function() end,
}
customMethods.ParticleEmitter = {
	Emit = function(self, n)
		assert(type(n) == "number", "Emit expects number")
		STATS.emits += 1
	end,
}
customMethods.RemoteEvent = {
	FireServer = function(self, ...)
		self.OnServerEvent:Fire(LOCAL_PLAYER, ...)
	end,
	FireClient = function(self, player, ...)
		assert(isProxy(player), "FireClient needs a player")
		if player == LOCAL_PLAYER then
			self.OnClientEvent:Fire(...)
		end
	end,
	FireAllClients = function(self, ...)
		self.OnClientEvent:Fire(...)
	end,
}
customMethods.TextChannel = {
	DisplaySystemMessage = function(self, msg)
		table.insert(STATS.systemMessages, msg)
	end,
}
customMethods.DataModel = {
	BindToClose = function(self, fn)
		table.insert(CLOSE_HANDLERS, fn)
	end,
}
customMethods.Players = {
	GetPlayers = function(self)
		local list = {}
		for _, c in ipairs(unwrap(self):GetChildren()) do
			if c:IsA("Player") then
				table.insert(list, wrap(c))
			end
		end
		return list
	end,
}
customMethods.RunService = {
	IsStudio = function()
		return true
	end,
}
customMethods.ContextActionService = {
	BindAction = function(self, name, fn, touch, ...)
		BOUND_ACTIONS[name] = { fn = fn, keys = { ... } }
	end,
}
customMethods.Debris = {
	AddItem = function() end,
}
customMethods.DataStoreService = {
	GetDataStore = function(self, name)
		return {
			GetAsync = function(_, key)
				if DATASTORE_MODE == "studio" then
					error("502: API Services rejected request with error. Error code: 0 Reason: StudioAccessToApisNotAllowed.")
				end
				task.wait(0.01)
				return DATASTORE[key]
			end,
			SetAsync = function(_, key, value, userIds)
				if DATASTORE_MODE == "studio" then
					error("StudioAccessToApisNotAllowed")
				end
				assert(type(userIds) == "table")
				DATASTORE[key] = value
			end,
		}
	end,
}
customMethods.TweenService = {
	Create = function(self, obj, info, goals)
		assert(isProxy(obj), "Tween target must be an instance")
		assert(type(info) == "table" and info.__tweeninfo, "TweenInfo expected")
		STATS.tweens += 1
		local tw = { Completed = newSignal("Completed") }
		function tw:Play()
			for k, val in pairs(goals) do
				obj[k] = val -- validates property + type
			end
			task.delay((info.Time or 0) + (info.DelayTime or 0), function()
				tw.Completed:Fire(roblox.Enum.PlaybackState.Completed)
			end)
		end
		function tw:Cancel() end
		return tw
	end,
}

----------------------------------------------------------------------
-- Globals for scripts
----------------------------------------------------------------------
local Random = {}
function Random.new(seed)
	local r = {}
	local state = seed or os.clock() * 1e6
	local function nxt()
		state = (state * 1103515245 + 12345) % 2147483648
		return state / 2147483648
	end
	function r:NextNumber(a, b)
		a = a or 0
		b = b or 1
		return a + (b - a) * nxt()
	end
	function r:NextInteger(a, b)
		return math.floor(a + (b - a + 1) * nxt())
	end
	return r
end

local TweenInfo = {
	new = function(time, style, dir, repeatCount, reverses, delay)
		return { __tweeninfo = true, Time = time or 1, EasingStyle = style, EasingDirection = dir, DelayTime = delay or 0 }
	end,
}

local safeTask = {
	wait = task.wait,
	spawn = function(fn, ...)
		local args = table.pack(...)
		return task.spawn(function()
			local ok, e = xpcall(fn, TB, table.unpack(args, 1, args.n))
			if not ok then
				reportError(e)
			end
		end)
	end,
	delay = function(t, fn, ...)
		local args = table.pack(...)
		return task.delay(t, function()
			local ok, e = xpcall(fn, TB, table.unpack(args, 1, args.n))
			if not ok then
				reportError(e)
			end
		end)
	end,
	defer = task.defer,
	cancel = task.cancel,
}

local game = wrap(game_real)
local Workspace = game:GetService("Workspace")

local moduleCache = {}
local runScript
local _G_BUILTINS

local function makeEnv(scriptProxy)
	local env = {
		game = game,
		workspace = Workspace,
		script = scriptProxy,
		Instance = {
			new = function(cls, parent)
				local inst = roblox.Instance.new(cls)
				local p = wrap(inst)
				if parent then
					p.Parent = parent
				end
				return p
			end,
		},
		Vector3 = roblox.Vector3,
		Font = roblox.Font,
		Vector2 = roblox.Vector2,
		CFrame = roblox.CFrame,
		Color3 = roblox.Color3,
		UDim = roblox.UDim,
		UDim2 = roblox.UDim2,
		Enum = roblox.Enum,
		NumberRange = roblox.NumberRange,
		NumberSequence = roblox.NumberSequence,
		NumberSequenceKeypoint = roblox.NumberSequenceKeypoint,
		ColorSequence = roblox.ColorSequence,
		ColorSequenceKeypoint = roblox.ColorSequenceKeypoint,
		Random = Random,
		TweenInfo = TweenInfo,
		task = safeTask,
		warn = function(...)
			logf("WARN: %s", table.concat({ ... }, " "))
		end,
		print = function(...)
			local t = {}
			for i, x in ipairs({ ... }) do
				t[i] = tostring(x)
			end
			logf("PRINT: %s", table.concat(t, " "))
		end,
		require = function(m)
			return runScript(m)
		end,
		typeof = typeof,
	}
	return setmetatable(env, { __index = _G_BUILTINS })
end

_G_BUILTINS = {
	newproxy = newproxy,
	math = math,
	string = string,
	table = table,
	os = os,
	pairs = pairs,
	ipairs = ipairs,
	next = next,
	type = type,
	tostring = tostring,
	tonumber = tonumber,
	pcall = pcall,
	xpcall = xpcall,
	error = error,
	assert = assert,
	select = select,
	setmetatable = setmetatable,
	getmetatable = getmetatable,
	rawequal = rawequal,
	rawget = rawget,
	rawset = rawset,
	unpack = table.unpack,
	debug = debug,
	utf8 = utf8,
	coroutine = coroutine,
	bit32 = bit32,
}

runScript = function(scriptProxy)
	local real = unwrap(scriptProxy)
	if real:IsA("ModuleScript") then
		local id = idOf(real)
		if moduleCache[id] ~= nil then
			return moduleCache[id]
		end
		local fn = luau.load(real.Source, { debugName = real:GetFullName(), environment = makeEnv(scriptProxy) })
		local result = fn()
		moduleCache[id] = result
		return result
	end
	local fn = luau.load(real.Source, { debugName = real:GetFullName(), environment = makeEnv(scriptProxy) })
	safeTask.spawn(fn)
end

----------------------------------------------------------------------
-- Scenario
----------------------------------------------------------------------
local function findScript(root, name)
	for _, d in ipairs(unwrap(root):GetDescendants()) do
		if d.Name == name then
			return wrap(d)
		end
	end
	error("script not found: " .. name)
end

local Players = game:GetService("Players")
local RunService = game:GetService("RunService")

-- Camera for client
CAMERA = wrap(roblox.Instance.new("Camera"))
CAMERA.ViewportSize = roblox.Vector2.new(1280, 720)
CAMERA.Parent = Workspace
Workspace.CurrentCamera = CAMERA
game:GetService("UserInputService").KeyboardEnabled = true

-- "foto" modu: 4 oyunculu dolu bir kasaba kurar, birkaç saniye oynatır ve bütün sahneyi
-- (parçalar + tabela yazıları) çalıştırıldığı klasördeki sahne.json'a yazar (magaza/kaynak). Tanıtım resimleri bundan çizilir.
local function fotoSenaryo()
	local serde = require("@lune/serde")
	-- tepeler oyun başında araziye dönüşür; fotoğraf için önce yerlerini al
	local tepeler = {}
	for _, t in ipairs(unwrap(Workspace.Harita.Tepeler):GetChildren()) do
		table.insert(tepeler, { t.CFrame.Position.X, t.CFrame.Position.Y, t.CFrame.Position.Z, t.Size.X, t.Color.R, t.Color.G, t.Color.B })
	end
	runScript(findScript(game:GetService("ServerScriptService"), "BurgerSunucu"))
	task.wait(0.4)
	local Cfg = runScript(findScript(game:GetService("ReplicatedStorage"), "Config"))
	local Gor = runScript(findScript(game:GetService("ReplicatedStorage"), "Gorsel"))
	local harita = Workspace:WaitForChild("Harita")
	local function hepsi()
		local g = {}
		for _, x in ipairs(Cfg.Gelistirmeler) do
			g[x.Anahtar] = x.MaxSeviye
		end
		return g
	end
	DATASTORE["oyuncu_1001"] = { Surum = 3, Para = 5e9, ToplamPaket = 160000, Sube = 2, Gelistirme = hepsi(), Stil = { Logo = 3, Renk = 6, Ad = "Huss Burger" } }
	DATASTORE["oyuncu_1002"] = { Surum = 3, Para = 1e6, ToplamPaket = 9000, Sube = 0, Stil = { Logo = 2, Renk = 1, Ad = "Kral Burger" },
		Gelistirme = { Duvar = 1, Cati = 1, Oturma = 1, Dekor = 1, DevTabela = 1, Makine = 4, Asci = 3, Kasiyer = 1, Temizlikci = 1, HizliEller = 1 } }
	DATASTORE["oyuncu_1003"] = { Surum = 3, Para = 1e5, ToplamPaket = 1200, Sube = 0, Stil = { Logo = 5, Renk = 4, Ad = "Ejder Burger" },
		Gelistirme = { Duvar = 1, Cati = 1, Makine = 2, Asci = 1, Kasiyer = 1 } }
	DATASTORE["oyuncu_1004"] = { Surum = 3, Para = 5000, ToplamPaket = 80, Sube = 0, Stil = { Logo = 8, Renk = 8, Ad = "Elmas Burger" },
		Gelistirme = { Makine = 1, Asci = 1 } }
	local oyuncular = {}
	for i, ad in ipairs({ "Burgerci", "Kral", "Ejder", "Elmas" }) do
		local plr = wrap(roblox.Instance.new("Player"))
		plr.Name = ad
		plr.DisplayName = ad
		plr.UserId = 1000 + i
		local g = wrap(roblox.Instance.new("PlayerGui"))
		g.Parent = plr
		plr.Parent = Players
		if i == 1 then
			LOCAL_PLAYER = plr
			Players.LocalPlayer = plr
		end
		Players.PlayerAdded:Fire(plr)
		oyuncular[i] = plr
	end
	task.wait(0.6)
	runScript(findScript(game:GetService("StarterPlayer"), "BurgerEkran"))
	task.wait(1)
	-- kasaba canlansın: müşteriler gelsin, bantlar dolsun
	for _ = 1, 260 do
		RunService.Heartbeat:FireSync(0.05)
		RunService.RenderStepped:FireSync(0.05)
		task.wait(0.05)
	end
	-- oyuncunun kutusu açık dursun ki burger görünsün (1 katlama)
	local remotes = game:GetService("ReplicatedStorage").Uzaklar
	task.wait(0.4)

	-- oyuncu karakterleri (fotoğrafta görünsünler): tezgahın önünde katlayan biri
	local insanlar = {}
	local function arsaCf(n)
		local t = unwrap(harita.Arsalar["Arsa" .. n].Taban)
		return t.CFrame * roblox.CFrame.new(0, t.Size.Y / 2, 0)
	end
	local F1 = arsaCf(1)
	local oy = Cfg.Yerlesim.OyuncuYeri
	local at = Cfg.Yerlesim.AnaTezgah
	local p = (F1 * roblox.CFrame.new(oy.X, 0.3, oy.Z + 4)).Position
	local h = (F1 * roblox.CFrame.new(at.X, 0.3, at.Z)).Position
	local kahraman = Gor.Insan(roblox.CFrame.lookAt(p, roblox.Vector3.new(h.X, p.Y, h.Z)), {
		Gomlek = roblox.Color3.fromRGB(255, 200, 40), Pantolon = roblox.Color3.fromRGB(40, 60, 120),
		Ten = Gor.Tenler[2], Yuz = "😆", Sapka = "kep", SapkaRenk = roblox.Color3.fromRGB(60, 120, 230),
	})
	Gor.InsanTasi(kahraman, roblox.CFrame.lookAt(p, roblox.Vector3.new(h.X, p.Y, h.Z)), 0, false, 1.4)
	local kk = wrap(roblox.Instance.new("Folder"))
	kk.Name = "Kahraman"
	kk.Parent = Workspace
	for _, x in ipairs(kahraman.Liste) do
		x.Parent = kk
	end

	-- dışa aktar
	local parcalar, guiler = {}, {}
	local sira = {}
	local function renk(c)
		return { c.R, c.G, c.B }
	end
	local function udim2(u)
		return { u.X.Scale, u.X.Offset, u.Y.Scale, u.Y.Offset }
	end
	local function elemanlar(kok)
		local liste = {}
		for _, c in ipairs(kok:GetChildren()) do
			if c:IsA("GuiObject") and c.Visible then
				local e = { tip = c.ClassName, pos = udim2(c.Position), size = udim2(c.Size), anchor = { c.AnchorPoint.X, c.AnchorPoint.Y },
					bg = renk(c.BackgroundColor3), bgA = 1 - c.BackgroundTransparency, rot = c.Rotation, z = c.ZIndex }
				local k = c:FindFirstChildOfClass("UICorner")
				if k then
					e.kose = { k.CornerRadius.Scale, k.CornerRadius.Offset }
				end
				local st = c:FindFirstChildOfClass("UIStroke")
				if st then
					e.cizgi = { renk(st.Color), st.Thickness, tostring(st.ApplyStrokeMode) }
				end
				if c:IsA("TextLabel") or c:IsA("TextButton") then
					e.yazi = c.Text
					e.yr = renk(c.TextColor3)
					e.yA = 1 - c.TextTransparency
					e.olcekli = c.TextScaled
					e.boy = c.TextSize
					e.hiza = tostring(c.TextXAlignment)
					e.kenar = 1 - c.TextStrokeTransparency
				end
				e.cocuk = elemanlar(c)
				table.insert(liste, e)
			end
		end
		return liste
	end
	for _, d in ipairs(unwrap(Workspace):GetDescendants()) do
		if d:IsA("BasePart") and d.Name ~= "GorunmezDuvar" and d.Name ~= "HumanoidRootPart" and d.Name ~= "Head" then
			local mesh = d:FindFirstChildOfClass("SpecialMesh")
			local sekil = d.ClassName == "WedgePart" and "Wedge" or "Block"
			if d:IsA("Part") then
				sekil = tostring(d.Shape):gsub("Enum.PartType.", "")
			end
			if mesh then
				sekil = tostring(mesh.MeshType):gsub("Enum.MeshType.", "")
			end
			local kayit = { cf = { d.CFrame:GetComponents() }, s = { d.Size.X, d.Size.Y, d.Size.Z }, c = renk(d.Color),
				m = tostring(d.Material):gsub("Enum.Material.", ""), a = 1 - d.Transparency, r = d.Reflectance, sh = sekil, n = d.Name }
			table.insert(parcalar, kayit)
			sira[d] = #parcalar
			for _, sg in ipairs(d:GetChildren()) do
				if sg:IsA("SurfaceGui") and sg.Enabled then
					table.insert(guiler, { tur = "yuzey", parca = #parcalar, yuz = tostring(sg.Face):gsub("Enum.NormalId.", ""),
						parlak = sg.LightInfluence < 0.2, el = elemanlar(sg) })
				elseif sg:IsA("BillboardGui") and sg.Enabled then
					local yer = d.CFrame.Position + sg.StudsOffset
					table.insert(guiler, { tur = "pano", yer = { yer.X, yer.Y, yer.Z }, size = udim2(sg.Size), el = elemanlar(sg) })
				end
			end
		end
	end
	fs.writeFile("sahne.json", serde.encode("json", { parcalar = parcalar, guiler = guiler, tepeler = tepeler }))
	print(string.format("sahne.json: %d parça, %d yazı", #parcalar, #guiler))
end

local function senaryo()
print("== Sunucu başlıyor")
local ConfigS = nil
runScript(findScript(game:GetService("ServerScriptService"), "BurgerSunucu"))
task.wait(0.4)
local harita = Workspace:WaitForChild("Harita")
local arsaSayisi = #unwrap(harita.Arsalar):GetChildren()
print("Arsa sayısı:", arsaSayisi)
assert(arsaSayisi == 8, "8 arsa bekleniyordu")
assert(harita.Arsalar.Arsa1.Dukkan:FindFirstChild("Pano") ~= nil, "boş arsada tabela olmalı")

local zengin = DATASTORE_MODE ~= "studio"
if zengin then
	DATASTORE["oyuncu_1001"] = { Surum = 3, Para = 50000000000, ToplamPaket = 10, Sube = 0, Gelistirme = {} }
end

local function makePlayer(name, userId)
	local plr = wrap(roblox.Instance.new("Player"))
	plr.Name = name
	plr.DisplayName = name
	plr.UserId = userId
	local g = wrap(roblox.Instance.new("PlayerGui"))
	g.Parent = plr
	plr.Parent = Players
	return plr
end
local function makeCharacter(plr)
	local char = wrap(roblox.Instance.new("Model"))
	char.Name = plr.Name
	local root = wrap(roblox.Instance.new("Part"))
	root.Name = "HumanoidRootPart"
	root.Parent = char
	local head = wrap(roblox.Instance.new("Part"))
	head.Name = "Head"
	head.Parent = char
	char.Parent = Workspace
	plr.Character = char
	plr.CharacterAdded:Fire(char)
	return char
end
local function stat(plr, n)
	return plr.leaderstats[n].Value
end
local DilModul = nil
local function DilT(metin, ...)
	DilModul = DilModul or runScript(findScript(game:GetService("ReplicatedStorage"), "Dil"))
	return DilModul.T(metin, ...)
end
local function butonBul(kok, metin)
	local aranan = DilT(metin)
	for _, d in ipairs(unwrap(kok):GetDescendants()) do
		if d:IsA("TextButton") and (d.Text == metin or d.Text == aranan) then
			return wrap(d)
		end
	end
	return nil
end
local function heartbeat(seconds)
	local steps = math.floor(seconds / 0.05)
	for _ = 1, steps do
		RunService.Heartbeat:FireSync(0.05)
		RunService.RenderStepped:FireSync(0.05)
		task.wait(0.05)
	end
end

local p1 = makePlayer("Burgerci", 1001)
LOCAL_PLAYER = p1
Players.LocalPlayer = p1
Players.PlayerAdded:Fire(p1)
makeCharacter(p1)
task.wait(0.3)

print("== İstemci başlıyor")
runScript(findScript(game:GetService("StarterPlayer"), "BurgerEkran"))
task.wait(1.0)
local gui = p1.PlayerGui:WaitForChild("BurgerArayuz")
local remotes = game:GetService("ReplicatedStorage").Uzaklar
local arsa1 = harita.Arsalar.Arsa1
assert(arsa1:GetAttribute("Sahip") == 1001, "oyuncu 1. arsayı almalı")

print("== Dükkanını kur ekranı")
assert(gui.StilPaneli.Visible == true, "ilk girişte stil ekranı açılmalı")
local Cfg = runScript(findScript(game:GetService("ReplicatedStorage"), "Config"))
butonBul(gui.StilPaneli, Cfg.Logolar[3]).Activated:Fire()
for _, d in ipairs(unwrap(gui.StilPaneli):GetDescendants()) do
	if d:IsA("TextButton") and d.Text == "" and d.BackgroundColor3 == Cfg.Renkler[6].Renk then
		wrap(d).Activated:Fire()
	end
end
local kutu = nil
for _, d in ipairs(unwrap(gui.StilPaneli):GetDescendants()) do
	if d:IsA("TextBox") then
		kutu = wrap(d)
	end
end
kutu.Text = "Huss Burger"
butonBul(gui.StilPaneli, "✅ DÜKKANI AÇ!").Activated:Fire()
task.wait(0.5)
print("Logo:", arsa1:GetAttribute("Logo"), "Ad:", arsa1:GetAttribute("Ad"), "Renk:", tostring(arsa1:GetAttribute("Renk")))
assert(arsa1:GetAttribute("Logo") == Cfg.Logolar[3], "logo uygulanmadı")
assert(arsa1:GetAttribute("Ad") == "Huss Burger", "ad uygulanmadı")
assert(arsa1:GetAttribute("Renk") == Cfg.Renkler[6].Renk, "renk uygulanmadı")
assert(gui.StilPaneli.Visible == false, "stil ekranı kapanmalı")

print("== Katlama")
local katlaB = gui.Alt.Katla
local p0 = stat(p1, "📦")
for _ = 1, 12 do
	katlaB.Activated:Fire()
	task.wait(0.1)
end
task.wait(0.8)
print("Paket:", stat(p1, "📦") - p0, "Para:", stat(p1, "💰"))
assert(stat(p1, "📦") - p0 == 4, "12 tıkta 4 paket bekleniyordu")
local gorsel = Workspace:FindFirstChild("BurgerGorselleri")
assert(gorsel and #unwrap(gorsel):GetChildren() > 0, "tezgahta kutu görseli yok")

print("== Görev, NPC'ler")
local npcler = harita:WaitForChild("NPCler")
assert(npcler:FindFirstChild("NPC_Gorev") and npcler:FindFirstChild("NPC_Yarisma") and npcler:FindFirstChild("NPC_Hediye"), "meydanda 3 NPC olmalı")
local function npcKonus(ad, kim)
	local prompt = npcler[ad].Govde:FindFirstChildOfClass("ProximityPrompt")
	assert(prompt, ad .. " için konuşma düğmesi yok")
	prompt.Triggered:Fire(kim or p1)
end
npcKonus("NPC_Gorev")
task.wait(0.3)
assert(gui.HusnuUsta.Visible == true, "Hüsnü Usta penceresi açılmalı")
print("Görev yazısı:", gui.Gorev:FindFirstChildOfClass("TextLabel").Text)
for _ = 1, 30 do
	katlaB.Activated:Fire()
	task.wait(0.1)
end
task.wait(0.4)
local paraGorevOnce = stat(p1, "💰")
assert(butonBul(gui.Gorev, "🎁 ÖDÜLÜ AL").Visible == true, "görev bitince ödül butonu çıkmalı")
butonBul(gui.Gorev, "🎁 ÖDÜLÜ AL").Activated:Fire()
task.wait(0.4)
print("Görev ödülü:", stat(p1, "💰") - paraGorevOnce, "Yeni görev:", gui.Gorev:FindFirstChildOfClass("TextLabel").Text)
assert(stat(p1, "💰") - paraGorevOnce >= 150, "görev ödülü verilmedi")

print("== Huss Yarışması")
npcKonus("NPC_Yarisma")
task.wait(3.2)
for _ = 1, 40 do
	katlaB.Activated:Fire()
	task.wait(0.1)
end
task.wait(9)
print("Yarışma sonucu:", gui.Yarisma:FindFirstChildOfClass("TextLabel").Text)
npcKonus("NPC_Yarisma") -- hemen tekrar: beklemeli
task.wait(0.2)

print("== Hediye")
local paraHediyeOnce = stat(p1, "💰")
npcKonus("NPC_Hediye")
task.wait(0.3)
print("Hediye:", stat(p1, "💰") - paraHediyeOnce, "2 kat:", tostring(p1:GetAttribute("IkiKatBitis")))
assert(stat(p1, "💰") > paraHediyeOnce or p1:GetAttribute("IkiKatBitis") ~= nil, "hediye verilmedi")
npcKonus("NPC_Hediye") -- tekrar: beklemeli
task.wait(0.2)
print("Altın Saat bitişi:", tostring(game:GetService("ReplicatedStorage"):GetAttribute("AltinSaatBitis")))
local halk = Workspace:FindFirstChild("KasabaHalki")
print("Kasaba halkı parçası:", halk and #unwrap(halk):GetChildren() or 0)
assert(halk and #unwrap(halk):GetChildren() > 50, "kasaba halkı yok")

local onceP = stat(p1, "📦")
for _ = 1, 20 do
	remotes.Katla.OnServerEvent:Fire(p1)
end
task.wait(0.5)
print("Spam ile geçen paket:", stat(p1, "📦") - onceP)
assert(stat(p1, "📦") - onceP <= 2, "hız sınırı çalışmıyor")

if zengin then
	print("== Her şeyi satın al")
	-- iki tur: kilitli olanlar (ör. Teknisyen makineden sonra açılır) ikinci turda alınır
	for _ = 1, 2 do
		for _, g in ipairs(Cfg.Gelistirmeler) do
			for _ = 1, g.MaxSeviye + 1 do
				remotes.SatinAl.OnServerEvent:Fire(p1, g.Anahtar)
			end
			task.wait(0.05)
		end
	end
	remotes.SatinAl.OnServerEvent:Fire(p1, "YokBoyle")
	remotes.SatinAl.OnServerEvent:Fire(p1, 42)
	task.wait(0.5)
	local d = arsa1.Dukkan
	for _, ad in ipairs({ "Duvar", "Cati", "NeonTabela", "Masa", "Ampul", "DevBurgerKaide", "Makine1", "Makine6", "AnaBant", "DonusBant",
		"Calisan_Asci", "Calisan_Kasiyer", "Calisan_Teknisyen", "Calisan_Temizlikci", "Supurge" }) do
		assert(d:FindFirstChild(ad, true) ~= nil, "dükkanda yok: " .. ad)
	end
	print("Makine sayısı (attribute):", arsa1:GetAttribute("Makine"), "Para:", stat(p1, "💰"))

	print("== Dükkan menüsü (sekmeler)")
	gui.Alt.Dukkan.Activated:Fire()
	task.wait(0.1)
	assert(gui.DukkanPaneli.Visible == true)
	for _, kat in ipairs(Cfg.Kategoriler) do
		butonBul(gui.DukkanPaneli, kat.Ikon .. " " .. DilT(kat.Ad)).Activated:Fire()
		task.wait(0.05)
	end
	butonBul(gui.DukkanPaneli, Cfg.Kategoriler[1].Ikon .. " " .. DilT(Cfg.Kategoriler[1].Ad)).Activated:Fire()
	for _, d2 in ipairs(unwrap(gui.DukkanPaneli):GetDescendants()) do
		if d2:IsA("TextButton") and d2.Parent and d2.Parent:IsA("Frame") and d2.Parent.Name == "Frame" then
			wrap(d2).Activated:Fire()
		end
	end
	task.wait(0.2)

	print("== Makineler, aşçı ve müşteriler çalışıyor")
	local pm = stat(p1, "📦")
	heartbeat(6)
	task.wait(0.6)
	local uretilen = stat(p1, "📦") - pm
	print("6 saniyede otomatik paket:", uretilen)
	assert(uretilen >= 24, "makineler üretmiyor")
	local mk = Workspace:FindFirstChild("Musteriler")
	local musteriParca = mk and #unwrap(mk):GetChildren() or 0
	print("Ekrandaki müşteri parçası:", musteriParca)
	assert(musteriParca > 0, "müşteri gelmedi")
	local bantta = 0
	for _, d2 in ipairs(unwrap(gorsel):GetChildren()) do
		if d2.Name == "PaketTepsi" then
			bantta += 1
		end
	end
	print("Ekrandaki bant kutusu:", bantta)

	-- görsel kontrol için dükkanın parçalarını yaz
	local serde = require("@lune/serde")
	local taban = unwrap(arsa1.Taban)
	local F = taban.CFrame * roblox.CFrame.new(0, taban.Size.Y / 2, 0)
	local parcalar = {}
	local function topla(kok)
		for _, d2 in ipairs(kok:GetDescendants()) do
			if d2:IsA("BasePart") and d2.Transparency < 0.95 then
				local cf = F:ToObjectSpace(d2.CFrame)
				local shape = "Block"
				if d2:IsA("Part") then
					shape = tostring(d2.Shape):gsub("Enum.PartType.", "")
				end
				local mesh = d2:FindFirstChildOfClass("SpecialMesh")
				if mesh then
					shape = "Ball"
				end
				if math.abs(cf.Position.X) < 40 and math.abs(cf.Position.Z) < 36 then
					table.insert(parcalar, { cf = { cf:GetComponents() }, size = { d2.Size.X, d2.Size.Y, d2.Size.Z }, color = { d2.Color.R, d2.Color.G, d2.Color.B }, shape = shape, alpha = 1 - d2.Transparency })
				end
			end
		end
	end
	topla(unwrap(arsa1))
	topla(unwrap(gorsel))
	topla(unwrap(mk))
	fs.writeFile("dukkan.json", serde.encode("json", { dukkan = parcalar }))

	print("== Yeni şube")
	gui.Alt.Dukkan.Activated:Fire()
	task.wait(0.05)
	gui.Alt.Dukkan.Activated:Fire()
	task.wait(0.05)
	butonBul(gui.DukkanPaneli, Cfg.Kategoriler[4].Ikon .. " " .. DilT(Cfg.Kategoriler[4].Ad)).Activated:Fire()
	local subeButonu = nil
	for _, d2 in ipairs(unwrap(gui.DukkanPaneli):GetDescendants()) do
		if d2:IsA("TextButton") and string.sub(d2.Text, 1, 4) == "💰" and d2.Parent.LayoutOrder == 100 then
			subeButonu = wrap(d2)
		end
	end
	assert(subeButonu, "şube butonu yok")
	subeButonu.Activated:Fire()
	task.wait(0.1)
	print("Onay yazısı:", subeButonu.Text)
	subeButonu.Activated:Fire()
	task.wait(0.6)
	print("Şube sonrası Para:", stat(p1, "💰"), "Duvar var mı:", arsa1.Dukkan:FindFirstChild("Duvar") ~= nil)
	assert(stat(p1, "💰") == 0, "şube parayı sıfırlamalı")
	assert(arsa1.Dukkan:FindFirstChild("Duvar") == nil, "şube dükkanı sıfırlamalı")
	assert(arsa1:GetAttribute("Logo") == Cfg.Logolar[3], "şube stili silmemeli")
end

print("== Geçersiz stil istekleri")
local function stilDene(istek)
	task.wait(1.6)
	remotes.StilSec.OnServerEvent:Fire(p1, istek)
	task.wait(0.1)
end
stilDene({ Logo = 99, Renk = 1, Ad = "x" })
stilDene({ Logo = 1, Renk = 1, Ad = string.rep("a", 30) })
stilDene("bozuk")
assert(arsa1:GetAttribute("Logo") == Cfg.Logolar[3], "geçersiz istek stili değiştirmemeli")
stilDene({ Logo = 2, Renk = 3, Ad = "kotu burger" })
task.wait(0.2)
print("Filtrelenmiş ad:", arsa1:GetAttribute("Ad"))
assert(arsa1:GetAttribute("Ad") == "#### burger", "ad filtreden geçmeli")
stilDene({ Logo = 3, Renk = 6, Ad = "  Huss Burger  " })
assert(arsa1:GetAttribute("Ad") == "Huss Burger", "boşluklar temizlenmeli")

print("== İkinci oyuncu")
local p2 = makePlayer("Arkadas", 1002)
Players.PlayerAdded:Fire(p2)
makeCharacter(p2)
task.wait(0.4)
local arsa2 = harita.Arsalar.Arsa2
assert(arsa2:GetAttribute("Sahip") == 1002, "ikinci oyuncu 2. arsayı almalı")
for _ = 1, 6 do
	remotes.Katla.OnServerEvent:Fire(p2)
	task.wait(0.1)
end
task.wait(0.5)
print("Oyuncu 2 paket:", stat(p2, "📦"), "Ad:", arsa2:GetAttribute("Ad"))
assert(stat(p2, "📦") == 2)
remotes.EvimeGit.OnServerEvent:Fire(p1)
makeCharacter(p1)
task.wait(0.3)
p2.Parent = nil
Players.PlayerRemoving:Fire(p2)
task.wait(0.4)
assert(arsa2:GetAttribute("Sahip") == 0, "çıkan oyuncunun arsası boşalmalı")
assert(arsa2.Dukkan:FindFirstChild("Pano") ~= nil, "boş arsa tabelası gelmeli")

print("== Ekrandaki yazılar")
for _, root in ipairs({ gui.Ust, gui.Alt }) do
	for _, d in ipairs(unwrap(root):GetDescendants()) do
		if (d:IsA("TextLabel") or d:IsA("TextButton")) and d.Text ~= "" and d.Visible then
			print("  [" .. d.Parent.Name .. "] " .. d.Text)
		end
	end
end

print("== Kapanış ve tekrar giriş")
for _, fn in ipairs(CLOSE_HANDLERS) do
	local ok, e = xpcall(fn, debug.traceback)
	if not ok then
		reportError(e)
	end
end
local paketOnce = stat(p1, "📦")
p1.Parent = nil
Players.PlayerRemoving:Fire(p1)
task.wait(0.4)
if zengin then
	local k = DATASTORE["oyuncu_1001"]
	assert(k and k.Stil and k.Stil.Ad == "Huss Burger" and k.Sube == 1, "kayıt eksik")
	local p1b = makePlayer("Burgerci", 1001)
	Players.PlayerAdded:Fire(p1b)
	task.wait(0.4)
	local a = nil
	for i = 1, 8 do
		if harita.Arsalar["Arsa" .. i]:GetAttribute("Sahip") == 1001 then
			a = harita.Arsalar["Arsa" .. i]
		end
	end
	assert(a and a:GetAttribute("Ad") == "Huss Burger", "tekrar girişte dükkan stili gelmeli")
	assert(stat(p1b, "📦") == paketOnce, "tekrar girişte paket sayısı gelmeli")
	print("Tekrar giriş tamam:", a.Name, a:GetAttribute("Ad"), stat(p1b, "📦"))
	p1b.Parent = nil
	Players.PlayerRemoving:Fire(p1b)
	task.wait(0.3)
end
end
local okS, errS = xpcall(DATASTORE_MODE == "foto" and fotoSenaryo or senaryo, debug.traceback)
if not okS then
	reportError("SENARYO: " .. tostring(errS))
end

-- İngilizce modda ekranda ya da haritada Türkçe yazı kalmış mı?
if DIL == "en" and DATASTORE_MODE ~= "foto" then
	print("== Dil kontrolü (en)")
	local EN = runScript(findScript(game:GetService("ReplicatedStorage"), "Dil")).EN
	local kalan, bakilan = {}, 0
	local function bak(yer, metin)
		if type(metin) ~= "string" or metin == "" then
			return
		end
		bakilan += 1
		local turkce = string.find(metin, "ğ") or string.find(metin, "ü") or string.find(metin, "ş") or string.find(metin, "ı")
			or string.find(metin, "ö") or string.find(metin, "ç") or string.find(metin, "İ") or string.find(metin, "Ğ")
			or string.find(metin, "Ü") or string.find(metin, "Ş") or string.find(metin, "Ö") or string.find(metin, "Ç")
		if turkce or (EN[metin] and EN[metin] ~= metin) then
			kalan[yer .. ": " .. metin] = true
		end
	end
	for _, d in ipairs(game_real:GetDescendants()) do
		local ilk = d:FindFirstAncestorOfClass("Workspace") or d:FindFirstAncestorOfClass("PlayerGui")
		if ilk then
			if d:IsA("TextLabel") or d:IsA("TextButton") then
				bak(d:GetFullName(), d.Text)
			elseif d:IsA("TextBox") then
				bak(d:GetFullName(), d.PlaceholderText)
			elseif d:IsA("ProximityPrompt") then
				bak(d:GetFullName(), d.ActionText)
				bak(d:GetFullName(), d.ObjectText)
			end
		end
	end
	for _, m in ipairs(STATS.systemMessages) do
		bak("sohbet", m)
	end
	local liste = {}
	for k in pairs(kalan) do
		table.insert(liste, k)
	end
	table.sort(liste)
	print(string.format("  %d yazıya bakıldı, %d tanesi çevrilmemiş", bakilan, #liste))
	for _, k in ipairs(liste) do
		print("  TÜRKÇE KALDI: " .. k)
	end
	if #liste > 0 then
		reportError(#liste .. " yazı İngilizceye çevrilmemiş")
	end
end
-- Ekran yazılarını göster
print("== Son ekran")
local function texts(root, depth)
	for _, d in ipairs(unwrap(root):GetDescendants()) do
		if d:IsA("TextLabel") or d:IsA("TextButton") then
			if d.Text ~= "" and d.Visible then
				print("  [" .. d.Parent.Name .. "/" .. d.ClassName .. "] " .. d.Text)
			end
		end
	end
end


print("== Kayıt")
for k, v in pairs(DATASTORE) do
	print("  " .. k, "Para=" .. tostring(v.Para), "ToplamPaket=" .. tostring(v.ToplamPaket), "YarismaRekor=" .. tostring(v.YarismaRekor), "GorevN=" .. tostring(v.GorevN), "SonHediye>0=" .. tostring((v.SonHediye or 0) > 0))
	for gk, gv in pairs(v.Gelistirme) do
		print("    " .. gk .. "=" .. gv)
	end
end

print("== İstatistik")
print("  tween:", STATS.tweens, "ses:", STATS.sounds, "konfeti:", STATS.emits, "ışınlama:", STATS.pivots, "arazi:", STATS.arazi)
for _, m in ipairs(STATS.systemMessages) do
	print("  sohbet:", m)
end
print("== Log")
for _, l in ipairs(LOG) do
	print("  " .. l)
end

if #ERRORS > 0 then
	print(("== %d HATA"):format(#ERRORS))
	process.exit(1)
end
print("== HATA YOK")
process.exit(0)
