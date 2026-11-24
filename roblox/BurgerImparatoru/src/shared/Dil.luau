-- BURGER İMPARATORU - Diller (Türkçe + İngilizce)
-- Oyundaki bütün yazılar kodda Türkçe yazılır. Bu sözlük her Türkçe yazının İngilizcesini tutar.
-- Roblox dili Türkçe olan oyuncular Türkçe, diğer herkes İngilizce görür (Config.Dil ile zorlanabilir).
--
-- Yeni bir yazı eklersen buraya İngilizcesini de ekle:  ["Türkçe yazı"] = "English text",
-- %s, %d, %.1f gibi yerlere isim ve sayılar gelir; İngilizcede de AYNI SIRADA ve aynı sayıda olmalı.
-- "%%" yüzde işaretinin kendisidir.

local Dil = {}
Dil.Kod = "tr" -- "tr" ya da "en"

local EN = {
	------------------------------------------------------------------
	-- Renkler, kategoriler, geliştirmeler (Config)
	------------------------------------------------------------------
	["Kırmızı"] = "Red",
	["Turuncu"] = "Orange",
	["Sarı"] = "Yellow",
	["Yeşil"] = "Green",
	["Turkuaz"] = "Turquoise",
	["Mavi"] = "Blue",
	["Mor"] = "Purple",
	["Pembe"] = "Pink",
	["Siyah"] = "Black",
	["Beyaz"] = "White",

	["Mutfak"] = "Kitchen",
	["Çalışanlar"] = "Staff",
	["Makineler"] = "Machines",
	["Dükkan"] = "Shop",
	["Yeni Şube"] = "New Branch",

	["Hızlı Eller"] = "Fast Hands",
	["Her tıkta 2 katlama"] = "2 folds per tap",
	["Gizli Sos"] = "Secret Sauce",
	["Her paket daha çok para eder"] = "Every package earns more cash",
	["Altın Şans"] = "Golden Luck",
	["ALTIN BURGER daha sık çıkar"] = "GOLDEN BURGERS show up more often",
	["Aşçı"] = "Cook",
	["Tezgahında senin yerine katlar"] = "Folds at your counter for you",
	["Kasiyer"] = "Cashier",
	["Müşterilere güler yüz. Ün +%20"] = "Smiles at customers. Fame +20%",
	["Teknisyen"] = "Technician",
	["Makineler %25 daha kısa sürede paketler"] = "Machines pack 25% faster",
	["Temizlikçi"] = "Cleaner",
	["Pırıl pırıl dükkan. Ün +%15"] = "A sparkling clean shop. Fame +15%",
	["Paketleme Makinesi"] = "Packing Machine",
	["Sen dururken burger paketler"] = "Packs burgers while you chill",
	["Makine Hızı"] = "Machine Speed",
	["Bütün makineler daha hızlı"] = "All machines work faster",
	["Duvarlar ve Camlar"] = "Walls & Windows",
	["Gerçek bir dükkan! Ün +%15"] = "A real shop! Fame +15%",
	["Çatı ve Neon Tabela"] = "Roof & Neon Sign",
	["Logon gece gündüz parlar. Ün +%20"] = "Your logo glows day and night. Fame +20%",
	["Oturma Alanı"] = "Seating Area",
	["Şemsiyeli masalar. Ün +%20"] = "Tables with umbrellas. Fame +20%",
	["Çiçek ve Işıklar"] = "Flowers & Lights",
	["Saksılar, ışık zinciri. Ün +%25"] = "Flower pots and string lights. Fame +25%",
	["Çatıda Dev Burger"] = "Giant Rooftop Burger",
	["Herkes dükkanını uzaktan görür. Ün +%50"] = "Everyone can see your shop from afar. Fame +50%",

	------------------------------------------------------------------
	-- NPC'ler, kasaba halkı, unvanlar (Config)
	------------------------------------------------------------------
	["Hüsnü Usta"] = "Chef Husnu",
	["📜 Görevler"] = "📜 Quests",
	["Hakem Hülya"] = "Referee Hulya",
	["⏱️ Huss Yarışması"] = "⏱️ Huss Contest",
	["Hediyeci Hasan"] = "Hasan the Gift Guy",
	["🎁 Bedava Hediye"] = "🎁 Free Gift",
	["Konuş"] = "Talk",
	["Yarış!"] = "Compete!",
	["Hediyeni Al"] = "Get Your Gift",

	["Bu kasabanın en iyi burgeri kimde?"] = "Who makes the best burger in town?",
	["İmparator Burger yemeden gitmem!"] = "Not leaving without an Emperor Burger!",
	["Altın burger gören var mı? ✨"] = "Has anyone seen a golden burger? ✨",
	["Ketçap mı hardal mı? İkisi! 😋"] = "Ketchup or mustard? Both! 😋",
	["Bugün 3 burger yedim, hâlâ açım!"] = "I ate 3 burgers today and I'm still hungry!",
	["Şu dev balon da ne öyle? 🎈"] = "What's that giant balloon? 🎈",
	["Hüsnü Usta görev veriyormuş!"] = "Chef Husnu is giving out quests!",
	["Huss yarışmasında rekor kıracağım!"] = "I'm gonna break the Huss Contest record!",
	["Kasabanın yeni imparatoru kim olacak? 👑"] = "Who will be the town's new emperor? 👑",
	["Patatesler de bedava mı? 🍟"] = "Are the fries free too? 🍟",
	["Kuyruk çok uzun ama değer!"] = "The line is long but so worth it!",

	["Çırak"] = "Apprentice",
	["Şef"] = "Chef",
	["Usta Şef"] = "Master Chef",
	["Burger Lordu"] = "Burger Lord",
	["Burger Sultanı"] = "Burger Sultan",
	["BURGER İMPARATORU"] = "BURGER EMPEROR",

	------------------------------------------------------------------
	-- Görevler ve geliştirme etkileri (Ekonomi)
	------------------------------------------------------------------
	["KATLA'ya %s kez bas"] = "Press FOLD %s times",
	["Elle %s paket yap"] = "Pack %s burgers by hand",
	["%d altın burger bul ✨"] = "Find golden burgers: %d ✨",
	["%s para kazan"] = "Earn %s cash",
	["Tık başına %d katlama"] = "%d folds per tap",
	["Paket parası x%.1f"] = "Package value x%.1f",
	["Altın burger %%%d"] = "Golden burger %d%%",
	["%d / %d makine"] = "%d / %d machines",
	["%.1f sn'de 1 paket"] = "1 package every %.1fs",
	["Henüz yok"] = "None yet",
	["%.1f sn'de 1 katlama"] = "1 fold every %.1fs",
	["Alındı ✓"] = "Owned ✓",
	["Makine süresi -%25"] = "Machine time -25%",
	["Ün +%%%d"] = "Fame +%d%%",

	------------------------------------------------------------------
	-- Sunucudan gelen duyurular
	------------------------------------------------------------------
	["Bütün arsalar dolu! Biri çıkınca dükkanın açılacak."] = "All plots are full! Your shop opens when someone leaves.",
	["🏙️ %s %d. şubesini açtı!"] = "🏙️ %s opened branch #%d!",
	["Dükkan adı en fazla %d harf olabilir."] = "Shop names can have at most %d letters.",
	['İsim kontrol edilemedi, şimdilik "%s" yazdık.'] = 'Couldn\'t check the name, using "%s" for now.',
	["Bu isim Roblox kurallarına uymuyor, bazı harfler gizlendi."] = "That name breaks Roblox rules, some letters were hidden.",
	["📜 Görev tamam! +%s para"] = "📜 Quest complete! +%s cash",
	["Hakem Hülya: Biraz dinlen! %d saniye sonra tekrar gel."] = "Referee Hulya: Take a break! Come back in %d seconds.",
	["🏆 %s Huss Yarışmasında %d tık! Kasaba rekoru!"] = "🏆 %s got %d taps in the Huss Contest! Town record!",
	["Hediyeci Hasan: Sıradaki hediye %d:%02d sonra! 🎁"] = "Hasan the Gift Guy: Next gift in %d:%02d! 🎁",
	["🎁 Hediye: %d saniye boyunca 2 KAT PARA! 🔥"] = "🎁 Gift: DOUBLE CASH for %d seconds! 🔥",
	["🎁 Hediye: +%s para!"] = "🎁 Gift: +%s cash!",
	["⏰ ALTIN SAAT! %d saniye boyunca bütün paketler 2 kat para! 🔥"] = "⏰ GOLDEN HOUR! Every package earns double cash for %d seconds! 🔥",

	------------------------------------------------------------------
	-- Harita ve dükkan tabelaları, oyuncuların başındaki unvan
	------------------------------------------------------------------
	["🍔 BURGER KASABASI 🍔"] = "🍔 BURGER TOWN 🍔",
	["🍔 BURGER İMPARATORU 👑"] = "🍔 BURGER EMPEROR 👑",
	["🍦 DONDURMA"] = "🍦 ICE CREAM",
	["🌭 SOSİSLİ"] = "🌭 HOT DOGS",
	["KETÇAP"] = "KETCHUP",
	["HARDAL"] = "MUSTARD",
	["🍔 BOŞ ARSA"] = "🍔 EMPTY PLOT",
	["Bir oyuncu bekliyor"] = "Waiting for a player",
	["SATIŞ"] = "PICK UP",
	["Sahibi: %s"] = "Owner: %s",
	["👑 %s  🏙️%d"] = "👑 %s  🏙️%d",
	["👑 %s"] = "👑 %s",

	------------------------------------------------------------------
	-- Ekran (butonlar, menüler, yazılar)
	------------------------------------------------------------------
	["Para"] = "Cash",
	["Paket"] = "Packed",
	["Makineler / sn"] = "Machines / s",
	["%d makine / sn"] = "%d machines / s",
	["Makine yok"] = "No machines",
	["Paket • 🏙️ x%.1f"] = "Packed • 🏙️ x%.1f",
	["KATLA!"] = "FOLD!",
	["BEKLE"] = "WAIT",
	["Geliştir!"] = "Upgrade!",
	["🛒 DÜKKAN"] = "🛒 SHOP",
	["Paran yetmiyor! 🍔"] = "Not enough cash! 🍔",
	["Biraz daha burger paketle."] = "Pack a few more burgers.",
	["Yeni Şube Aç"] = "Open New Branch",
	["Yeni şube açınca paran ve bütün geliştirmelerin sıfırlanır, dükkanın baştan kurulur. Ama paket sayın ve unvanın kalır, her şube bütün paralarını kalıcı olarak %%%d artırır!"] = "A new branch resets your cash and all upgrades, and your shop is rebuilt from scratch. But you keep your package count and title, and every branch boosts all your cash by %d%% forever!",
	["%s  •  Sv %d/%d"] = "%s  •  Lv %d/%d",
	["Sv %d/%d"] = "Lv %d/%d",
	["💰 %s  •  Sv %d/%d"] = "💰 %s  •  Lv %d/%d",
	["Sv"] = "Lv",
	["🔒 Önce: %s"] = "🔒 Needs: %s",
	["✓ TAMAM"] = "✓ MAXED",
	["Yeni Şube Aç  •  Şu an %d. şube"] = "Open New Branch  •  Now branch #%d",
	["Şimdiki para çarpanı x%.1f"] = "Current cash multiplier x%.1f",
	["Yeni şubede x%.1f"] = "New branch: x%.1f",
	["Emin misin? Bas!"] = "Sure? Tap again!",
	["Yeni şube için 💰 %s lazım"] = "You need 💰 %s for a new branch",
	["🎨 DÜKKANINI KUR"] = "🎨 BUILD YOUR SHOP",
	["1) Logonu seç"] = "1) Pick your logo",
	["2) Dükkan rengini seç"] = "2) Pick your shop color",
	["3) Dükkanının adı"] = "3) Name your shop",
	["Örn: Mega Burger"] = "e.g. Mega Burger",
	["✅ DÜKKANI AÇ!"] = "✅ OPEN SHOP!",
	["Dükkan adını herkes görür, kibar bir isim seç 🙂"] = "Everyone can see your shop name, keep it nice 🙂",
	["Dükkanın açıldı! KATLA'ya bas."] = "Your shop is open! Press FOLD.",
	["🎁 ÖDÜLÜ AL"] = "🎁 CLAIM REWARD",
	["Tamam"] = "OK",
	["📜 Görev %d"] = "📜 Quest %d",
	["Ödül: 💰 %s"] = "Reward: 💰 %s",
	["Aferin evlat! Görevini bitirdin. 👏\nİşte ödülün: 💰 %s"] = "Well done, kid! You finished the quest. 👏\nHere's your reward: 💰 %s",
	["Merhaba evlat! Sana bir görevim var:\n%s (%s/%s)\nBitirirsen sana 💰 %s veririm!"] = "Hey kid! I've got a quest for you:\n%s (%s/%s)\nFinish it and I'll give you 💰 %s!",
	["🔥 2 KAT PARA %s"] = "🔥 DOUBLE CASH %s",
	["⏰ ALTIN SAAT x2 %s"] = "⏰ GOLDEN HOUR x2 %s",
	["🎁 Hediye %s"] = "🎁 Gift %s",
	["🎁 Hediye hazır! Meydana gel"] = "🎁 Gift ready! Come to the plaza",
	["Hazır ol! KATLA'ya hızlı hızlı bas!"] = "Get ready! Tap FOLD as fast as you can!",
	["⏱️ %.1f  •  %d TIK"] = "⏱️ %.1f  •  %d TAPS",
	["⏱️ BİTTİ  •  %d TIK"] = "⏱️ DONE  •  %d TAPS",
	["Hakem sayıyor..."] = "The referee is counting...",
	["👑 %s  •  %s / %s paket → %s"] = "👑 %s  •  %s / %s packed → %s",
	["👑 %s  •  EN TEPEDESİN!"] = "👑 %s  •  YOU'RE AT THE TOP!",
	["💾 Studio testinde kayıt kapalı (oyunu yayınlayınca açılır)"] = "💾 Saving is off in Studio tests (it turns on when you publish)",
	["💾 Kayıt yüklenemedi, bu seferki ilerlemen kaydedilmeyecek"] = "💾 Couldn't load your save, progress this time won't be saved",
	["Bütün arsalar dolu!"] = "All plots are full!",
	["Biri çıkınca dükkanın açılacak."] = "Your shop opens when someone leaves.",
	["KOMBO x%d"] = "COMBO x%d",
	["🏙️ %d. ŞUBE AÇILDI!"] = "🏙️ BRANCH #%d OPENED!",
	["Bütün paralar artık x%.1f"] = "All cash is now x%.1f",
	["KATLA'ya bas: 3 katlamada burger paketlenir!"] = "Press FOLD: 3 folds and the burger is packed!",
	["✨ ALTIN BURGER! ✨"] = "✨ GOLDEN BURGER! ✨",
	["👑 YENİ UNVAN: %s!"] = "👑 NEW TITLE: %s!",
	["Burger İmparatorluğuna bir adım daha!"] = "One step closer to the Burger Empire!",
	["👑 %s artık %s!"] = "👑 %s is now: %s!",
	["⏱️ HUSS YARIŞMASI!"] = "⏱️ HUSS CONTEST!",
	["Rekorun: %d tık"] = "Your record: %d taps",
	["🏁 %d TIK  •  +%s 💰"] = "🏁 %d TAPS  •  +%s 💰",
	["🏆 YENİ REKOR!"] = "🏆 NEW RECORD!",

	------------------------------------------------------------------
	-- Robux mağazası
	------------------------------------------------------------------
	["💎 ROBUX MAĞAZASI"] = "💎 ROBUX SHOP",
	["⭐ Geçişler: bir kere al, hep senin"] = "⭐ Passes: buy once, yours forever",
	["🛍️ Ürünler: istediğin kadar al"] = "🛍️ Items: buy as many as you like",
	["Yakında"] = "Soon",
	["✓ SENDE"] = "✓ OWNED",
	["🔁 Otomatik katlama AÇIK"] = "🔁 Auto fold ON",
	["🔁 Otomatik katlama KAPALI"] = "🔁 Auto fold OFF",
	["🎉 %s alındı! Teşekkürler!"] = "🎉 %s purchased! Thank you!",
	["💵 +%s para!"] = "💵 +%s cash!",
	["🔥 %d dakika 2 KAT PARA!"] = "🔥 DOUBLE CASH for %d minutes!",
	["⏰ %s herkese ALTIN SAAT hediye etti! %d saniye bütün paketler 2 kat! 🔥"] = "⏰ %s gifted everyone a GOLDEN HOUR! Every package earns double for %d seconds! 🔥",
	["2 Kat Para"] = "2x Cash",
	["Bütün paketler hep 2 kat para eder"] = "Every package always earns double cash",
	["+%25 para, başında ve sohbette VIP yazısı"] = "+25% cash, VIP tag over your head and in chat",
	["Altın Şans x2"] = "Golden Luck x2",
	["ALTIN BURGER 2 kat daha sık çıkar"] = "GOLDEN BURGERS show up twice as often",
	["Otomatik Katlama"] = "Auto Fold",
	["Açınca KATLA'ya senin yerine basar"] = "Turn it on and it presses FOLD for you",
	["Para Kesesi"] = "Cash Pouch",
	["10 dakikalık kazancın kadar para"] = "Cash equal to 10 minutes of your earnings",
	["Para Çuvalı"] = "Cash Sack",
	["45 dakikalık kazancın kadar para"] = "Cash equal to 45 minutes of your earnings",
	["Para Kasası"] = "Cash Vault",
	["3 saatlik kazancın kadar para"] = "Cash equal to 3 hours of your earnings",
	["15 Dakika 2 Kat Para"] = "15 Min Double Cash",
	["15 dakika boyunca bütün paketler 2 kat"] = "Every package earns double for 15 minutes",
	["Herkese Altın Saat"] = "Golden Hour for Everyone",
	["Bütün sunucuya hemen Altın Saat başlat"] = "Start a Golden Hour for the whole server now",

	------------------------------------------------------------------
	-- Ödüller, kodlar, arkadaşlar, grup, sıralama
	------------------------------------------------------------------
	["🎉 ÖDÜLLER"] = "🎉 REWARDS",
	["Günlük ödül"] = "Daily reward",
	["Kod gir"] = "Enter a code",
	["Arkadaşlarınla oyna"] = "Play with friends",
	["Grubumuza katıl"] = "Join our group",
	["Kodu buraya yaz"] = "Type the code here",
	["KULLAN"] = "REDEEM",
	["DAVET ET"] = "INVITE",
	["AL!"] = "CLAIM!",
	["Günlük ödül: %d. gün"] = "Daily reward: day %d",
	["💰 %s  •  Her gün gel, ödül büyüsün!"] = "💰 %s  •  Come back daily, it grows!",
	["Her arkadaş +%%%d para (şu an +%%%d)"] = "Each friend +%d%% cash (now +%d%%)",
	["👥 ARKADAŞ +%%%d"] = "👥 FRIENDS +%d%%",
	["✓ Gruptasın: +%%%d para"] = "✓ You're in the group: +%d%% cash",
	["Oyun sayfasındaki gruba katıl, sonra tekrar gir: +%%%d para"] = "Join the group on the game page, then rejoin: +%d%% cash",
	["📅 Günlük ödülün hazır!"] = "📅 Your daily reward is ready!",
	["🎉 butonuna bas"] = "Tap the 🎉 button",
	["Şu an davet gönderilemiyor"] = "Can't send invites right now",
	["📅 %d. gün ödülü: +%s para!"] = "📅 Day %d reward: +%s cash!",
	["🎟️ Böyle bir kod yok."] = "🎟️ That code doesn't exist.",
	["🎟️ Bu kodu zaten kullandın."] = "🎟️ You already used this code.",
	["🎟️ %s kodu: +%s para!"] = "🎟️ Code %s: +%s cash!",
	["🏆 DÜNYA SIRALAMASI"] = "🏆 WORLD LEADERBOARD",
	["En çok paket yapanlar"] = "Most burgers packed",
}
Dil.EN = EN

-- "tr-tr", "TR", "en-us" gibi bir dil kodundan dili seçer
function Dil.Ayarla(kod)
	local k = string.lower(string.sub(tostring(kod or ""), 1, 2))
	Dil.Kod = (k == "tr") and "tr" or "en"
end

local function karsilik(metin)
	if Dil.Kod == "en" and type(metin) == "string" then
		return EN[metin] or metin
	end
	return metin
end

-- T("Türkçe yazı") ya da T("biçim %s %d", ...): seçili dildeki yazıyı verir.
-- Argümanlar da sözlükte varsa (ör. bir unvan adı) onlar da çevrilir.
function Dil.T(metin, ...)
	local s = karsilik(metin)
	local n = select("#", ...)
	if n == 0 then
		return s
	end
	local args = { ... }
	for i = 1, n do
		args[i] = karsilik(args[i])
	end
	return string.format(s, table.unpack(args, 1, n))
end

----------------------------------------------------------------------
-- Hazır yazıları çevirme (haritadaki tabelalar, sunucunun yazdığı isimler)
-- Tam eşleşme yoksa "%s", "%d" içeren kalıplara bakılır: "Sahibi: Ali" -> "Owner: Ali"
----------------------------------------------------------------------
local kaliplar = nil
local onbellek = {}
local onbellekSayisi = 0

local function kacir(s)
	return (string.gsub(s, "[%^%$%(%)%%%.%[%]%*%+%-%?]", "%%%0"))
end

local function kaliplariHazirla()
	kaliplar = {}
	for tr, en in pairs(EN) do
		if string.find(tr, "%%[%-%d%.]*[sdf]") then
			local desen, i = "^", 1
			while true do
				local a, b, _, tur = string.find(tr, "%%([%-%d%.]*)([sdf%%])", i)
				if not a then
					break
				end
				desen ..= kacir(string.sub(tr, i, a - 1))
				if tur == "s" then
					desen ..= "(.-)"
				elseif tur == "d" then
					desen ..= "(%-?%d+)"
				elseif tur == "f" then
					desen ..= "(%-?%d+%.?%d*)"
				else
					desen ..= "%%"
				end
				i = b + 1
			end
			desen ..= kacir(string.sub(tr, i)) .. "$"
			local sablon = string.gsub(en, "%%([%-%d%.]*)([sdf%%])", function(_, tur)
				return tur == "%" and "%%" or "%s"
			end)
			table.insert(kaliplar, { Desen = desen, Sablon = sablon, Uzunluk = #tr })
		end
	end
	-- uzun (daha belirgin) kalıplar önce denenir
	table.sort(kaliplar, function(x, y)
		return x.Uzunluk > y.Uzunluk
	end)
end

-- Hazır bir yazının İngilizcesi; çevrilecek bir şey yoksa nil
function Dil.Cevir(metin)
	if Dil.Kod ~= "en" or type(metin) ~= "string" or metin == "" then
		return nil
	end
	local d = EN[metin]
	if d then
		return d
	end
	local o = onbellek[metin]
	if o ~= nil then
		return o or nil
	end
	if not kaliplar then
		kaliplariHazirla()
	end
	local sonuc = false
	for _, k in ipairs(kaliplar) do
		local yakalar = { string.match(metin, k.Desen) }
		if #yakalar > 0 then
			for j, y in ipairs(yakalar) do
				yakalar[j] = EN[y] or y
			end
			local ok, s = pcall(string.format, k.Sablon, table.unpack(yakalar))
			if ok then
				sonuc = s
				break
			end
		end
	end
	if onbellekSayisi > 400 then
		onbellek = {}
		onbellekSayisi = 0
	end
	onbellek[metin] = sonuc
	onbellekSayisi += 1
	return sonuc or nil
end

-- Bir klasördeki (ör. Workspace) bütün yazıları çevirir ve sonradan eklenenleri/değişenleri izler.
-- Sadece oyuncunun ekranında çalışır; sunucudaki asıl yazılar Türkçe kalır.
-- Türkçe modda Roblox'un kendi otomatik çevirisi Türkçe yazılara karışmasın diye kapatılır
-- (İngilizce modda açık kalır: Roblox İngilizce yazıları başka dillere de çevirebilir).
function Dil.DunyayiCevir(kok)
	if Dil.Kod ~= "en" then
		local function kapat(o)
			if o:IsA("LayerCollector") then
				o.AutoLocalize = false
			end
		end
		for _, o in ipairs(kok:GetDescendants()) do
			kapat(o)
		end
		kok.DescendantAdded:Connect(kapat)
		return
	end
	local function ozellik(o, ad)
		local function uygula()
			local y = Dil.Cevir(o[ad])
			if y and y ~= o[ad] then
				o[ad] = y
			end
		end
		uygula()
		o:GetPropertyChangedSignal(ad):Connect(uygula)
	end
	local function bagla(o)
		if o:IsA("TextLabel") or o:IsA("TextButton") then
			ozellik(o, "Text")
		elseif o:IsA("ProximityPrompt") then
			ozellik(o, "ActionText")
			ozellik(o, "ObjectText")
		end
	end
	for _, o in ipairs(kok:GetDescendants()) do
		bagla(o)
	end
	kok.DescendantAdded:Connect(bagla)
end

return Dil
