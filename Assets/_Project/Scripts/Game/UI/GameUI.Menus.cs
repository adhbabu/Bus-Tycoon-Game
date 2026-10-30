using System;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Game
{
    // Ana menü, yeni oyun (başlangıç şehri), duraklatma/iflas/zafer pencereleri, kayıt-yükleme, yardım.
    public sealed partial class GameUI
    {
        static readonly string[] QuickStartCityIds = { "TR_ISTANBUL", "TR_ANKARA", "TR_IZMIR", "TR_BURSA", "TR_ANTALYA", "TR_KONYA" };

        string companyName = "Anadolu Turizm";
        CityData startCity;
        string startCityInfo;
        string startDifficulty;
        Color startDifficultyColor;
        bool showMenuHelp;
        string latestSaveSlot;
        string latestSaveLabel;
        Vector2 helpScroll, newGameScroll;
        Texture2D mascot;
        bool mascotLoaded;

        // ------------------------------------------------------------------ ana menü

        void EnterMainMenu()
        {
            if (Stations != null) Stations.ExitImmediate();
            mode = Mode.MainMenu;
            modal = Modal.None;
            panel = Panel.None;
            showMenuHelp = false;
            startCity = null;
            mapCamera.AutoDrift = true;
            // Yarışta saat durmaz (kurucunun saati herkesi bekletir); oyuncu menüden "Oyuna Dön" ile döner.
            if (session.IsRunning && session.Clock != null && !RaceLocked) session.Clock.SetSpeed(0);
            RefreshLatestSave();
        }

        void RefreshLatestSave()
        {
            DateTime? manual = SaveSystem.GetLastWriteTimeUtc(SaveSlot);
            DateTime? auto = SaveSystem.GetLastWriteTimeUtc(AutosaveSlot);
            latestSaveSlot = null;
            DateTime? latest = null;
            if (manual.HasValue) { latestSaveSlot = SaveSlot; latest = manual; }
            if (auto.HasValue && (!latest.HasValue || auto.Value > latest.Value)) { latestSaveSlot = AutosaveSlot; latest = auto; }
            latestSaveLabel = latest.HasValue ? latest.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : null;
        }

        /// <summary>Ana menü kutusu içeriğe göre boylanır; yardım açıkken uzun metin için tam boy.</summary>
        Rect MainMenuRect
        {
            get
            {
                if (showMenuHelp) return CenteredBox(560f, 600f);
                int buttons = 4 + (session.IsRunning && !session.IsBankrupt ? 1 : 0) + (CanQuit ? 1 : 0);
                if (phone) return CenteredBox(840f, 92f + 54f * buttons + 40f); // yatay telefon: solda başlık, sağda düğmeler
                return CenteredBox(560f, (compact ? 300f : 335f) + 56f * buttons);
            }
        }

        Rect NewGameRect => new Rect(SafeLeft + Margin, SafeTop + Margin,
            Mathf.Min(470f * u, SafeRight - SafeLeft - 2f * Margin), H - SafeTop - SafeBottom - 2f * Margin);

        Rect ModalRect => CenteredBox(480f, 470f);
        Vector2 modalScroll;
        Modal scrolledModal;

        /// <summary>Güvenli alanın ortasında, ekrana sığacak şekilde küçültülen kutu.</summary>
        Rect CenteredBox(float widthUnits, float heightUnits)
        {
            float width = Mathf.Min(widthUnits * u, SafeRight - SafeLeft - 2f * Margin);
            float height = Mathf.Min(heightUnits * u, H - SafeTop - SafeBottom - 2f * Margin);
            return new Rect(SafeLeft + (SafeRight - SafeLeft - width) * 0.5f, SafeTop + (H - SafeTop - SafeBottom - height) * 0.5f, width, height);
        }

        // iOS'ta uygulama kendini kapatmaz (Apple kuralı); tarayıcıda oyun sekmeyi kapatamaz.
        static bool CanQuit => Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;

        void DrawMainMenu()
        {
            var box = MainMenuRect;
            uiRects.Add(box);
            GUILayout.BeginArea(box, s.Modal);

            if (phone && !showMenuHelp)
            {
                // Yatay telefon: ekran kısa, geniş; başlık solda, düğmeler sağda (kaydırmasız sığar)
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(Wd(300f));
                GUILayout.FlexibleSpace();
                DrawMascot(70f);
                ColorLabel("Otobüs İmparatoru", UIStyles.Orange, s.Big);
                GUILayout.Label("Bus Tycoon · otobüs şirketini kur, büyüt!", s.WrapDim);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Harita: Natural Earth · Şehirler: GeoNames · Simgeler: Twemoji · Yazı tipi: Inter", s.WrapDim);
                GUILayout.EndVertical();
                Sp(16f);
                GUILayout.BeginVertical();
                DrawMainMenuButtons();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }

            DrawMascot(compact ? 78f : 108f);
            GUILayout.Label("Otobüs İmparatoru", s.Title);
            GUILayout.Label("Bus Tycoon · otobüs şirketini kur, büyüt!", s.Center);
            Sp(compact ? 12f : 20f);

            if (showMenuHelp)
            {
                helpScroll = BeginScroll(helpScroll);
                DrawHelpContent();
                GUILayout.EndScrollView();
                if (GUILayout.Button("Geri", s.Button, Ht(40f))) Defer(() => showMenuHelp = false);
            }
            else
            {
                DrawMainMenuButtons();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Harita: Natural Earth · Şehirler: GeoNames (CC BY 4.0) · Simgeler: Twemoji (CC BY 4.0) · Yazı tipi: Inter (SIL OFL)", s.WrapDim);
            GUILayout.EndArea();
        }

        void DrawMainMenuButtons()
        {
            if (session.IsRunning && !session.IsBankrupt &&
                GUILayout.Button($"Oyuna Dön  ({session.State.companyName})", s.Primary, Ht(50f)))
                Defer(ResumeFromMenu);

            if (GUILayout.Button("Yeni Oyun", session.IsRunning ? s.Button : s.Primary, Ht(50f)))
                Defer(BeginNewGame);

            if (GUILayout.Button("Çok Oyunculu  (Blitz · Normal)", s.Button, Ht(46f)))
                Defer(OpenMultiplayer);

            string continueText = latestSaveLabel != null ? $"Kayıttan Devam Et  ({latestSaveLabel})" : "Kayıttan Devam Et";
            if (EnabledButton(latestSaveSlot != null, continueText, s.Button, Ht(46f)))
                Defer(() => LoadSlot(latestSaveSlot));

            if (GUILayout.Button("Nasıl Oynanır?", s.Button, Ht(46f))) Defer(() => showMenuHelp = true);
            if (CanQuit && GUILayout.Button("Çıkış", s.Button, Ht(46f))) Defer(QuitGame);
            Sp(4f);
            DrawSoundToggle();
        }

        void DrawSoundToggle()
        {
            if (GameAudio.Instance == null) return;
            bool on = GUILayout.Toggle(GameAudio.Enabled, "Ses efektleri (tıklama, korna, para, uyarılar)", s.Toggle);
            if (on == GameAudio.Enabled) return;
            GameAudio.Enabled = on;
            GameAudio.Play(Sfx.Notify); // açılınca örnek ses
        }

        /// <summary>Ana menüdeki güler yüzlü otobüs; yolda gidiyormuş gibi hafifçe zıplar.</summary>
        void DrawMascot(float heightUnits)
        {
            if (!mascotLoaded)
            {
                mascotLoaded = true;
                mascot = Resources.Load<Texture2D>(DefaultContent.MascotResourcePath);
            }
            if (mascot == null)
            {
                Sp(10f);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(10f, heightUnits * u, GUILayout.ExpandWidth(true), Ht(heightUnits));
            if (Event.current.type != EventType.Repaint) return;
            float bounce = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 4f * u;
            rect.y -= bounce;
            GUI.DrawTexture(rect, mascot, ScaleMode.ScaleToFit);
        }

        void ResumeFromMenu()
        {
            mode = Mode.Playing;
            mapCamera.AutoDrift = false;
            CityData hq = session.Headquarters;
            if (hq != null) mapCamera.FocusOn(world.GetCityPosition(hq), 34f);
            session.Clock.SetSpeed(RaceLocked ? Room.Speed : 1);
        }

        void BeginNewGame()
        {
            if (Multiplayer != null && Multiplayer.InMatch) Multiplayer.Leave(); // tek başına yeni oyun: odadan çık
            mode = Mode.NewGame;
            modal = Modal.None;
            panel = Panel.None;
            startCity = null;
            mapCamera.AutoDrift = false;
            mapCamera.FocusOn(Vector3.zero, 70f);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ yeni oyun

        void DrawNewGame()
        {
            var rect = NewGameRect;
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            newGameScroll = BeginScroll(newGameScroll);

            GUILayout.Label("Yeni Şirket Kur", s.H1);
            Divider();
            GUILayout.Label("Şirket adı", s.Dim);
            companyName = TextInput("company", companyName, 28, "Şirket adı", 40f);
            Sp(10f);
            GUILayout.Label("Haritadan merkez ofisinizin bulunacağı şehre tıklayın. O şehrin ülkesi ücretsiz açılır; diğer ülkeler sonradan lisansla açılır.", s.Wrap);
            Sp(6f);

            GUILayout.Label("Hızlı seçim", s.H2);
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    CityData city = session.World.GetCity(QuickStartCityIds[row * 3 + col]);
                    if (city == null) continue;
                    if (GUILayout.Button(city.DisplayName, city == startCity ? s.TabActive : s.Button, Ht(34f)))
                    {
                        CityData selected = city;
                        Defer(() => SelectStartCity(selected));
                    }
                }
                GUILayout.EndHorizontal();
            }

            Sp(10f);
            if (startCity != null)
            {
                GUILayout.BeginVertical(s.Card);
                GUILayout.BeginHorizontal();
                LayoutCityIcon(startCity, 28f, 0f);
                Sp(6f);
                GUILayout.Label($"{startCity.DisplayName}, {startCity.Region.DisplayName}", s.H2);
                LayoutIcon(UIIcons.Flag(startCity.Region.Id), 22f, 3f);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label(startCityInfo, s.Wrap);
                ColorLabel(startDifficulty, startDifficultyColor, s.Bold);
                GUILayout.EndVertical();
            }
            else
            {
                ColorLabel("Henüz şehir seçilmedi.", UIStyles.DimText, s.Label);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(s.Card);
            GUILayout.Label("Başlangıç sermayesi", s.Dim);
            GUILayout.Label($"{Fmt.Money(session.ProgressionConfig.StartingCash)} nakit + 2. el {session.ProgressionConfig.StartingBus.DisplayName} minibüs", s.Wrap);
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Geri", s.Button, Wd(110f), Ht(46f))) Defer(EnterMainMenu);
            if (EnabledButton(startCity != null, "ŞİRKETİ KUR", s.Primary, Ht(46f))) Defer(StartCompany);
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void SelectStartCity(CityData city)
        {
            startCity = city;
            mapCamera.FocusOn(world.GetCityPosition(city), 40f);

            // Hat, açık ülke içindeki HERHANGİ iki şehir arasında açılabilir; asıl belirleyici ülke pazarıdır.
            long market = 0;
            int cities = 0, big = 0;
            foreach (var other in session.World.Cities)
            {
                if (other.Region != city.Region) continue;
                cities++;
                market += other.Population;
                if (other.Population >= CityData.LargeCityPopulation) big++;
            }

            startCityInfo = $"Nüfus {Fmt.Population(city.Population)}\n" +
                            $"{city.Region.DisplayName}: {cities} şehir, {big} büyük şehir, toplam pazar {Fmt.Population(market)} kişi\n" +
                            "Ülke içinde istediğiniz iki şehir arasında hat açabilirsiniz; merkez ofis haritada altın renkle görünür.";

            if (market > 30_000_000) { startDifficulty = "Kolay başlangıç: büyük ülke pazarı"; startDifficultyColor = UIStyles.Green; }
            else if (market > 5_000_000) { startDifficulty = "Orta zorluk: orta büyüklükte pazar"; startDifficultyColor = UIStyles.Yellow; }
            else { startDifficulty = "Zor başlangıç: küçük ülke pazarı"; startDifficultyColor = UIStyles.Orange; }
        }

        void StartCompany()
        {
            if (startCity == null) return;
            string name = string.IsNullOrWhiteSpace(companyName) ? "Anadolu Turizm" : companyName.Trim();
            CityData hq = startCity;
            GUIUtility.keyboardControl = 0;

            session.StartNewGame(name, hq); // → OnSessionStarted
            Toast($"{name} kuruldu! İlk iş: {hq.DisplayName} çıkışlı bir hat açın.", UIStyles.Gold);
            OpenPlannerNow(hq);
        }

        // ------------------------------------------------------------------ kayıt / yükleme

        void SaveTo(string slot)
        {
            if (!session.IsRunning) return;
            if (session.SaveGame(slot)) Toast("Oyun kaydedildi.", UIStyles.Green);
            else Toast("Kayıt başarısız! Ayrıntı için konsola bakın.", UIStyles.Red);
            RefreshLatestSave();
        }

        void LoadSlot(string slot)
        {
            if (slot == null) return;
            if (Multiplayer != null && Multiplayer.InMatch) Multiplayer.Leave();
            if (session.TryLoadGame(slot)) Toast("Kayıt yüklendi.", UIStyles.Green); // → OnSessionStarted
            else Toast("Kayıt yüklenemedi.", UIStyles.Red);
        }

        // ------------------------------------------------------------------ modallar

        void OpenPauseMenu()
        {
            if (mode != Mode.Playing || modal != Modal.None) return;
            speedBeforePause = Mathf.Max(1, session.Clock.Speed);
            if (!RaceLocked) session.Clock.SetSpeed(0); // yarışta oyun durmaz
            RefreshLatestSave();
            modal = Modal.Pause;
        }

        void ClosePauseMenu()
        {
            modal = Modal.None;
            if (!session.IsBankrupt && !RaceLocked) session.Clock.SetSpeed(speedBeforePause);
        }

        void AskConfirm(string title, string text, Action onConfirm)
        {
            confirmTitle = title;
            confirmText = text;
            confirmAction = onConfirm;
            modal = Modal.Confirm;
        }

        void DrawModal()
        {
            var full = new Rect(0f, 0f, W, H);
            uiRects.Add(full);
            if (Event.current.type == EventType.Repaint) DrawRect(full, new Color(0.02f, 0.03f, 0.1f, 0.55f));

            var box = ModalRect;
            if (modal != scrolledModal)
            {
                scrolledModal = modal;
                modalScroll = Vector2.zero;
            }
            GUILayout.BeginArea(box, s.Modal);
            // Telefonda kutu ekrana sığmayabilir: içerik parmakla kaydırılır
            if (phone) modalScroll = BeginScroll(modalScroll);
            switch (modal)
            {
                case Modal.Pause: DrawPauseModal(); break;
                case Modal.GameOver: DrawGameOverModal(); break;
                case Modal.Victory: DrawVictoryModal(); break;
                case Modal.Confirm: DrawConfirmModal(); break;
                case Modal.MatchEnd: DrawMatchEndModal(); break;
            }
            if (phone) GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawPauseModal()
        {
            bool race = RaceLocked;
            GUILayout.Label(race ? "Menü (yarış sürüyor)" : "Oyun Duraklatıldı", s.H1);
            Divider();
            if (GUILayout.Button("Devam Et", s.Primary, Ht(46f))) Defer(ClosePauseMenu);
            if (GUILayout.Button("Kaydet", s.Button, Ht(40f))) Defer(() => SaveTo(SaveSlot));
            if (compact && GUILayout.Button("Günlük (son olaylar)", s.Button, Ht(40f)))
                Defer(() => { ClosePauseMenu(); OpenPanelNow(Panel.Log); });
            if (phone && GUILayout.Button("Nasıl Oynanır? (yardım)", s.Button, Ht(40f)))
                Defer(() => { ClosePauseMenu(); OpenPanelNow(Panel.Help); });
            if (race)
            {
                if (GUILayout.Button("Yarıştan Çık (tek başına devam)", s.Danger, Ht(40f)))
                    Defer(() => AskConfirm("Yarıştan çık", "Odadan ayrılırsınız; şirketiniz tek başına oyunda kalır.", () => Multiplayer?.Leave()));
            }
            else
            {
                if (EnabledButton(latestSaveSlot != null, "Son Kaydı Yükle", s.Button, Ht(40f)))
                    Defer(() => LoadSlot(latestSaveSlot));

                Sp(8f);
                GUILayout.Label("Gün süresi (1x hızda)", s.Dim);
                GUILayout.BeginHorizontal();
                foreach (float seconds in new[] { 2f, 4f, 8f })
                {
                    bool active = Mathf.Approximately(session.SecondsPerDay, seconds);
                    if (GUILayout.Button($"{seconds:0} sn", active ? s.TabActive : s.Tab, Ht(32f)))
                        session.SecondsPerDay = seconds;
                }
                GUILayout.EndHorizontal();
            }

            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle != null)
            {
                Sp(4f);
                bool on = GUILayout.Toggle(cycle.Enabled, "Gece-gündüz döngüsü (gece şehir ışıkları ve farlar)", s.Toggle);
                if (on != cycle.Enabled) cycle.SetEnabled(on);
            }
            DrawSoundToggle();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ana Menü", s.Button, Ht(40f))) Defer(EnterMainMenu);
            if (CanQuit && GUILayout.Button("Oyundan Çık", s.Danger, Ht(40f))) Defer(() => { session.SaveGame(AutosaveSlot); QuitGame(); });
        }

        void DrawGameOverModal()
        {
            ColorLabel("İFLAS ETTİNİZ", UIStyles.Red, s.H1);
            Divider();
            GUILayout.Label($"Kasanız {session.ProgressionConfig.BankruptcyGraceDays} gün boyunca eksi kaldı ve alacaklılar şirkete el koydu.", s.Wrap);
            Sp(8f);
            KeyValue("Dayanılan süre", $"{session.State.currentDay} gün");
            KeyValue("Taşınan yolcu", Fmt.Num(session.State.totalPassengers));
            KeyValue("Ulaşılan seviye", session.Progression.Level.ToString());
            Sp(8f);
            GUILayout.Label("İpucu: Zarar eden hatları kapatın, atıl otobüsleri satın ve fiyatı 'ideal bölge'de tutun.", s.WrapDim);
            GUILayout.FlexibleSpace();
            if (EnabledButton(latestSaveSlot != null, "Son Kaydı Yükle", s.Button, Ht(42f)))
                Defer(() => LoadSlot(latestSaveSlot));
            if (GUILayout.Button("Yeni Oyun", s.Primary, Ht(46f))) Defer(BeginNewGame);
            if (GUILayout.Button("Ana Menü", s.Button, Ht(40f))) Defer(EnterMainMenu);
        }

        void DrawVictoryModal()
        {
            ColorLabel("TEBRİKLER!", UIStyles.Gold, s.H1);
            Divider();
            GUILayout.Label($"{session.State.companyName} kasasında {Fmt.Money(GoalTracker.VictoryCash)}'ı aştı. Artık bir Otobüs İmparatorusunuz!", s.Wrap);
            Sp(8f);
            KeyValue("Süre", $"{session.State.currentDay} gün");
            KeyValue("Taşınan yolcu", Fmt.Num(session.State.totalPassengers));
            KeyValue("Hat / Otobüs", $"{session.Routes.Routes.Count} / {session.Fleet.Buses.Count}");
            KeyValue("İtibar", $"{session.Progression.Reputation:0}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Oynamaya Devam Et", s.Primary, Ht(46f))) Defer(() => modal = Modal.None);
            if (GUILayout.Button("Ana Menü", s.Button, Ht(40f))) Defer(EnterMainMenu);
        }

        void DrawConfirmModal()
        {
            GUILayout.Label(confirmTitle, s.H1);
            Divider();
            GUILayout.Label(confirmText, s.Wrap);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Vazgeç", s.Button, Ht(44f))) Defer(() => modal = Modal.None);
            if (GUILayout.Button("Onayla", s.Danger, Ht(44f)))
            {
                Action action = confirmAction;
                Defer(() =>
                {
                    modal = Modal.None;
                    action?.Invoke();
                });
            }
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ yardım

        void DrawHelpContent()
        {
            HelpSection("Amaç",
                "Otobüs şirketinizi büyütün: şehirleri hatlarla bağlayın, bilet fiyatını doğru ayarlayın, filonuzu " +
                $"minibüsten VIP otobüse yükseltin. Kasada {Fmt.Money(GoalTracker.VictoryCash)}'a ulaşınca oyunu kazanırsınız.");
            HelpSection("Hat açmak",
                "Yeni Hat (N) ile planlayıcıyı açın, kalkış ve varış şehrine tıklayın ya da bir durağa girip 'Buradan hat aç'a " +
                "basın. Teklif kartı mesafe, tahmini talep, önerilen bilet fiyatı ve otobüs sayısını gösterir. Planlayıcı en kârlı " +
                "hedefleri de önerir.");
            HelpSection("Duraklar",
                "Haritada bir durağa tıklayınca içine girersiniz. Durak şehrin büyüklüğüne göre küçük durak, otogar, büyük otogar " +
                "ya da metropol terminalidir; yolcu trafiğiniz arttıkça içi kalabalıklaşır. Hat işlettiğiniz duraklara kafe, simit " +
                "büfesi, dijital pano gibi geliştirmeler yapın: her biri durakta görünür ve o duraktan geçen hatların talebini artırır. " +
                "İçeride sürükleyerek döndürün, tekerlekle yakınlaştırın.");
            HelpSection("Bilet fiyatı (oyunun kalbi)",
                "Fiyat yükseldikçe yolcu azalır. Çok ucuzsa otobüsler dolar ama kâr kaçar; çok pahalıysa koltuklar boş kalır. " +
                "Fiyat panelindeki yeşil bant 'ideal bölge'dir. Otobüs sayısı değişince ideal fiyat da değişir! " +
                "Tahminler pazar araştırmanız kadar isabetlidir; 'Dün' satırındaki gerçek sonuçlara bakın.");
            HelpSection("Filo ve ilerleme",
                "Yolcu taşıdıkça seviye atlarsınız: yeni otobüsler, daha fazla hat ve yeni ülkeler açılır. " +
                "Minibüs uzun hatlarda yolcu kaybeder; 300 km üstü hatlara midibüs veya şehirlerarası otobüs koyun. " +
                "Lüks modeller ayrıca itibar ister: adil fiyat ve konforlu araç itibarı artırır. " +
                "Otobüsler yıprandıkça bakımı pahalılaşır ve arıza yapar. Filo panelindeki 'Bakım asistanı' yıpranan " +
                "otobüsleri revizyona sokar, arızalananı hemen tamir edip hattına gönderir; 'Hepsini onar ve hatlara gönder' " +
                "bütün filoyu onarır ve garajdakileri hatlarına yollar.");
            HelpSection("Kontroller",
                "Sol tık: seç · Sürükle veya WASD/ok tuşları: kaydır · Tekerlek veya Q/E: yakınlaştır · Sağ tık: seçimi iptal\n" +
                "Boşluk: duraklat · 1/2/3: hız · N: yeni hat · R: hatlar · F: filo · G: galeri · K: finans · B: bölgeler · " +
                "J: görevler · F5: kaydet · Esc: geri / menü");
            HelpSection("Çok oyunculu",
                "Ana menüde Çok Oyunculu → Oda kur: arkadaşların 5 karakterlik oda kodunla katılır. Herkes kendi şirketini kurar, " +
                "günler aynı anda ilerler. Blitz'de $1.000.000, Normal'de $10.000.000 kasaya ilk ulaşan kazanır.");
            HelpSection("Dikkat",
                "Kasa 30 gün üst üste eksi kalırsa iflas edersiniz. Garajda bekleyen otobüsler de sigorta öder.");
        }

        void HelpSection(string title, string body)
        {
            GUILayout.Label(title, s.H2);
            GUILayout.Label(body, s.Wrap);
            Sp(8f);
        }
    }
}
