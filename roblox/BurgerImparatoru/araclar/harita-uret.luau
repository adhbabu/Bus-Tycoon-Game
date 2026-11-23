-- Burger Kasabası haritasını harita/Harita.rbxm dosyasına yazar (Lune ile çalışır).
-- Kullanım (roblox/BurgerImparatoru klasöründe):  lune run araclar/harita-uret.luau
local fs = require("@lune/fs")
local luau = require("@lune/luau")
local roblox = require("@lune/roblox")

-- Lune'da Roblox'taki Random yok; aynı tohumla hep aynı haritayı veren basit bir tane
local Random = {}
function Random.new(seed)
	local state = seed or 12345
	local r = {}
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

local function yukle(yol)
	local env = {
		Instance = roblox.Instance, Vector3 = roblox.Vector3, Vector2 = roblox.Vector2, CFrame = roblox.CFrame,
		Color3 = roblox.Color3, Enum = roblox.Enum, UDim = roblox.UDim, UDim2 = roblox.UDim2,
		NumberRange = roblox.NumberRange, NumberSequence = roblox.NumberSequence,
		NumberSequenceKeypoint = roblox.NumberSequenceKeypoint, ColorSequence = roblox.ColorSequence,
		ColorSequenceKeypoint = roblox.ColorSequenceKeypoint, Font = roblox.Font, Random = Random,
		math = math, string = string, table = table, pairs = pairs, ipairs = ipairs, type = type,
		tostring = tostring, tonumber = tonumber, error = error, assert = assert, pcall = pcall, print = print,
	}
	return luau.load(fs.readFile(yol), { debugName = yol, environment = env })()
end

local Config = yukle("src/shared/Config.luau")
local Gorsel = yukle("src/shared/Gorsel.luau")
local HaritaUretici = yukle("src/server/HaritaUretici.luau")

local harita = HaritaUretici.Uret(Config, Gorsel)
local sayi = 0
for _, d in ipairs(harita:GetDescendants()) do
	if d:IsA("BasePart") then
		sayi += 1
	end
end
fs.writeDir("harita")
fs.writeFile("harita/Harita.rbxm", roblox.serializeModel({ harita }))
print(string.format("Harita yazıldı: harita/Harita.rbxm (%d parça)", sayi))
