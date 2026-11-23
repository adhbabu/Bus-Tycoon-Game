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
if process.args[3] == "ses" then
	for _, d in ipairs(game_real:GetDescendants()) do
		if d.Name == "Config" and d:IsA("ModuleScript") then
			d.Source = string.gsub(d.Source, 'Config.HussSesId = ""', 'Config.HussSesId = "rbxassetid://123"')
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
		local child = real:FindFirstChild(key)
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
		local ok, err = pcall(function()
			v[key] = unwrap(value)
		end)
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
		return newSignal("PropChanged:" .. prop)
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
		local tw = {}
		function tw:Play()
			for k, val in pairs(goals) do
				obj[k] = val -- validates property + type
			end
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
		return { __tweeninfo = true, Time = time or 1, EasingStyle = style, EasingDirection = dir }
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

local function senaryo()
print("== Sunucu başlıyor")
runScript(findScript(game:GetService("ServerScriptService"), "BurgerSunucu"))
task.wait(0.3)

local function makePlayer(name, userId)
	local plr = wrap(roblox.Instance.new("Player"))
	plr.Name = name
	plr.DisplayName = name
	plr.UserId = userId
	local gui = wrap(roblox.Instance.new("PlayerGui"))
	gui.Parent = plr
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

-- Player 1 (local)
local p1 = makePlayer("Burgerci", 1001)
LOCAL_PLAYER = p1
Players.LocalPlayer = p1
Players.PlayerAdded:Fire(p1)
makeCharacter(p1)
task.wait(0.2)

print("== İstemci başlıyor")
runScript(findScript(game:GetService("StarterPlayer"), "BurgerEkran"))
task.wait(0.4)

-- Player 2 (other)
local p2 = makePlayer("Arkadas", 1002)
Players.PlayerAdded:Fire(p2)
makeCharacter(p2)
task.wait(0.2)

local gui = p1.PlayerGui:WaitForChild("BurgerArayuz")
local alt = gui.Alt
local katla = alt.Katla
local servis = alt.Servis
local dukkanBtn = alt.Dukkan
local ust = gui.Ust

local function stat(plr, n)
	return plr.leaderstats[n].Value
end

local function heartbeat(seconds)
	local steps = math.floor(seconds / 0.05)
	for _ = 1, steps do
		RunService.Heartbeat:FireSync(0.05)
		RunService.RenderStepped:FireSync(0.05)
		task.wait(0.001)
	end
end

print("== Katlama testi")
for i = 1, 12 do
	katla.Activated:Fire()
	task.wait(0.1)
end
task.wait(0.5)
local station = Workspace.Tezgahlar.Tezgah1
print("Tezgah1 parça sayısı:", #unwrap(station.Katlar):GetChildren(), "Kat (leaderstats):", stat(p1, "Kat"))
assert(stat(p1, "Kat") == 12, "12 kat bekleniyordu, gelen " .. stat(p1, "Kat"))
print("Tabela:", station:FindFirstChild("TabelaNoktasi").Tabela:FindFirstChildOfClass("Frame").Alt.Text)

-- Hile koruması: arka arkaya 20 istek → en fazla 4 geçmeli
local remotes = game:GetService("ReplicatedStorage").Uzaklar
local onceKat = stat(p1, "Kat")
for _ = 1, 20 do
	remotes.Katla.OnServerEvent:Fire(p1)
end
task.wait(0.3)
local hile = stat(p1, "Kat") - onceKat
print("Spam ile geçen kat:", hile)
assert(hile <= 5, "rate limit çalışmıyor")

print("== Servis testi")
task.wait(0.2)
servis.Activated:Fire()
task.wait(1.8)
print("Para:", stat(p1, "Para"), "Sunucuya göre yeni burger parça sayısı:", #unwrap(station.Katlar):GetChildren())
assert(stat(p1, "Para") > 0, "servis para vermedi")
print("Ekrandaki para:", ust:FindFirstChildOfClass("Frame") and "ok")

print("== Dükkan testi")
dukkanBtn.Activated:Fire()
task.wait(0.1)
local panel = gui.DukkanPaneli
assert(panel.Visible == true, "dükkan açılmadı")
-- Para ver ve tüm geliştirmeleri satın almayı dene
local paraOnce = stat(p1, "Para")
remotes.SatinAl.OnServerEvent:Fire(p1, "HizliEller")
remotes.SatinAl.OnServerEvent:Fire(p1, "YokBoyle")
remotes.SatinAl.OnServerEvent:Fire(p1, 12345)
task.wait(0.2)
print("Para önce/sonra:", paraOnce, stat(p1, "Para"))

-- Çok para kazanmak için birçok burger yap
for tur = 1, 6 do
	for _ = 1, 30 do
		katla.Activated:Fire()
		task.wait(0.095)
	end
	-- dolu tabakta katla → uyarı
	katla.Activated:Fire()
	task.wait(0.1)
	servis.Activated:Fire()
	task.wait(0.7)
	for _, anahtar in ipairs({ "HizliEller", "Lezzet", "OtoAsci", "DevTabak" }) do
		remotes.SatinAl.OnServerEvent:Fire(p1, anahtar)
	end
	task.wait(0.2)
	print(string.format("  tur %d: Para=%d Kat=%d Unvan=%s", tur, stat(p1, "Para"), stat(p1, "Kat"), stat(p1, "Unvan")))
end

-- Dükkan butonlarına tıkla (UI yolu)
for _, satir in ipairs(unwrap(panel):GetChildren()) do
	if satir:IsA("Frame") then
		for _, c in ipairs(satir:GetChildren()) do
			if c:IsA("TextButton") then
				wrap(c).Activated:Fire()
			end
		end
	end
end
task.wait(0.3)
gui.DukkanPaneli:FindFirstChildOfClass("TextButton").Activated:Fire() -- kapat
task.wait(0.1)

print("== Çırak Aşçı (otomatik) testi")
-- sunucu tarafında oto aşçı alınmış mı?
local katOnce = stat(p1, "Kat")
heartbeat(8)
task.wait(0.3)
print("Oto ile eklenen kat:", stat(p1, "Kat") - katOnce)

print("== Klavye kısayolları")
for name, b in pairs(BOUND_ACTIONS) do
	print("  bağlı:", name)
end
local beginState = roblox.Enum.UserInputState.Begin
BOUND_ACTIONS.BurgerDukkan.fn("BurgerDukkan", beginState)
BOUND_ACTIONS.BurgerDukkan.fn("BurgerDukkan", beginState)
BOUND_ACTIONS.BurgerServis.fn("BurgerServis", beginState)
task.wait(0.8)
BOUND_ACTIONS.BurgerKatla.fn("BurgerKatla", beginState)
task.wait(0.3)

print("== Ses düğmesi")
ust.Ses.Activated:Fire()
task.wait(0.05)
ust.Ses.Activated:Fire()

print("== Diğer oyuncu katlıyor")
for _ = 1, 5 do
	remotes.Katla.OnServerEvent:Fire(p2)
	task.wait(0.1)
end
remotes.ServisEt.OnServerEvent:Fire(p2)
task.wait(1.6)

print("== Karakter yeniden doğuyor")
makeCharacter(p1)
task.wait(0.3)

print("== Oyuncu 2 çıkıyor")
p2.Parent = nil
Players.PlayerRemoving:Fire(p2)
task.wait(0.3)
assert(Workspace.Tezgahlar.Tezgah2.TabelaNoktasi.Tabela:FindFirstChildOfClass("Frame").Baslik.Text == "🍔 Boş Tezgah", "tezgah boşalmadı")

print("== Ekrandaki yazılar")
for _, root in ipairs({ gui.Ust, gui.Alt }) do
	for _, d in ipairs(unwrap(root):GetDescendants()) do
		if (d:IsA("TextLabel") or d:IsA("TextButton")) and d.Text ~= "" and d.Visible then
			print("  [" .. d.Parent.Name .. "] " .. d.Text)
		end
	end
end
print("== Kapanış")
for _, fn in ipairs(CLOSE_HANDLERS) do
	local ok, e = xpcall(fn, debug.traceback)
	if not ok then
		reportError(e)
	end
end
p1.Parent = nil
Players.PlayerRemoving:Fire(p1)
task.wait(0.5)

end
local okS, errS = xpcall(senaryo, debug.traceback)
if not okS then reportError("SENARYO: " .. tostring(errS)) end
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
pcall(function() local g = Players:FindFirstChild("Burgerci") end)

print("== Kayıt")
for k, v in pairs(DATASTORE) do
	print("  " .. k, "Para=" .. tostring(v.Para), "ToplamKat=" .. tostring(v.ToplamKat), "Rekor=" .. tostring(v.Rekor))
	for gk, gv in pairs(v.Gelistirme) do
		print("    " .. gk .. "=" .. gv)
	end
end

print("== İstatistik")
print("  tween:", STATS.tweens, "ses:", STATS.sounds, "konfeti:", STATS.emits, "ışınlama:", STATS.pivots)
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
