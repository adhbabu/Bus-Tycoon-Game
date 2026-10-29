using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Multiplayer;
using UnityEngine;

namespace BusTycoon.Game
{
    // Çok oyunculu: ana menüdeki "Çok Oyunculu" ekranı (oda kur / koda katıl / lobi), oyun içinde canlı sıralama kartı
    // ve yarış sonu penceresi. Oda mantığı RoomSession'da, oyuna bağlantı MultiplayerDirector'da.
    public sealed partial class GameUI
    {
        const string NamePrefKey = "mp_name";

        string mpName, mpCode = string.Empty;
        MatchMode mpMode = MatchMode.Blitz;
        RoomPlayer mpWinner;
        Vector2 mpScroll;
        bool inviteConsumed;

        MultiplayerDirector Multiplayer => MultiplayerDirector.Instance;
        RoomSession Room => Multiplayer != null ? Multiplayer.Room : null;
        /// <summary>Yarış sürüyor: hız ve gün süresi odaya bağlı, duraklatılamaz.</summary>
        bool RaceLocked => Multiplayer != null && Multiplayer.ClockLocked;

        Rect MultiplayerRect => CenteredBox(580f, 700f);

        void BindMultiplayer()
        {
            if (Multiplayer == null) return;
            Multiplayer.MatchStarted += OnMatchStarted;
            Multiplayer.MatchEnded += OnMatchEnded;
            Multiplayer.Notice += OnMultiplayerNotice;
        }

        void UnbindMultiplayer()
        {
            if (Multiplayer == null) return;
            Multiplayer.MatchStarted -= OnMatchStarted;
            Multiplayer.MatchEnded -= OnMatchEnded;
            Multiplayer.Notice -= OnMultiplayerNotice;
        }

        /// <summary>"?oda=KOD" bağlantısıyla gelen oyuncu doğrudan odaya katılma ekranını görür.</summary>
        void OpenInviteIfAny()
        {
            if (inviteConsumed) return;
            inviteConsumed = true;
            string code = MultiplayerDirector.InviteCode;
            if (string.IsNullOrEmpty(code) || Multiplayer == null) return;
            mpCode = code;
            OpenMultiplayer();
        }

        void OpenMultiplayer()
        {
            if (string.IsNullOrEmpty(mpName))
                mpName = PlayerPrefs.GetString(NamePrefKey, session.IsRunning ? session.State.companyName : "Oyuncu");
            mode = Mode.Multiplayer;
            modal = Modal.None;
            showMenuHelp = false;
        }

        void LeaveRoomToMenu()
        {
            Multiplayer?.Leave();
            mode = Mode.MainMenu;
        }

        // ------------------------------------------------------------------ menü ekranı

        void DrawMultiplayer()
        {
            var box = MultiplayerRect;
            uiRects.Add(box);
            GUILayout.BeginArea(box, s.Modal);
            mpScroll = BeginScroll(mpScroll);
            GUILayout.BeginHorizontal();
            LayoutIcon(UIIcon.Finish, 30f, 2f);
            Sp(8f);
            GUILayout.Label("Çok Oyunculu", s.H1);
            GUILayout.EndHorizontal();
            Divider();

            RoomSession room = Room;
            RoomPhase phase = room != null ? room.Phase : RoomPhase.Idle;
            switch (phase)
            {
                case RoomPhase.Connecting: DrawConnecting(room); break;
                case RoomPhase.Lobby: DrawLobby(room); break;
                default: DrawRoomChoice(room); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Oda kurma (mod seçimiyle) ya da kodla katılma.</summary>
        void DrawRoomChoice(RoomSession failed)
        {
            GUILayout.Label("Arkadaşlarınla aynı anda yarış: herkes kendi şirketini kurar, hedef kasaya ilk ulaşan kazanır.", s.Wrap);
            Sp(8f);
            GUILayout.Label("Adın", s.Dim);
            mpName = GUILayout.TextField(mpName ?? string.Empty, 18, s.Field, Ht(40f));
            Sp(10f);

            GUILayout.Label("Oda kur", s.H2);
            GUILayout.BeginHorizontal();
            ModeCard(MatchMode.Blitz, "Blitz", "$1.000.000 · hızlı (2x)");
            ModeCard(MatchMode.Normal, "Normal", "$10.000.000 · normal akış (1x)");
            GUILayout.EndHorizontal();
            Sp(4f);
            if (GUILayout.Button("ODA KUR", s.Primary, Ht(46f)))
                Defer(() =>
                {
                    RememberName();
                    Multiplayer.CreateRoom(mpName, mpMode);
                });

            Sp(14f);
            GUILayout.Label("Odaya katıl", s.H2);
            GUILayout.BeginHorizontal();
            mpCode = MatchRules.NormalizeCode(GUILayout.TextField(mpCode ?? string.Empty, 8, s.Field, Ht(44f)));
            if (EnabledButton(mpCode.Length == MatchRules.CodeLength, "KATIL", s.Primary, Wd(120f), Ht(44f)))
                Defer(() =>
                {
                    RememberName();
                    Multiplayer.JoinRoom(mpName, mpCode);
                });
            GUILayout.EndHorizontal();
            GUILayout.Label("Arkadaşının verdiği 5 karakterlik oda kodunu yaz.", s.WrapDim);

            if (failed != null && failed.Phase == RoomPhase.Closed && !string.IsNullOrEmpty(failed.Status))
            {
                Sp(8f);
                ColorLabel(failed.Status, UIStyles.Red, s.Wrap);
            }
            if (!WebRoomTransport.IsSupported && MultiplayerDirector.TransportFactory == MultiplayerDirector.WebFactory)
                ColorLabel("Çok oyunculu şimdilik web sürümünde oynanır.", UIStyles.Orange, s.Wrap);

            GUILayout.FlexibleSpace();
            Sp(10f);
            if (GUILayout.Button("Geri", s.Button, Ht(42f))) Defer(LeaveRoomToMenu);
        }

        void ModeCard(MatchMode mode, string title, string detail)
        {
            bool active = mpMode == mode;
            if (GUILayout.Button($"{title}\n{detail}", active ? s.TabActive : s.Tab, Ht(56f)))
                Defer(() => mpMode = mode);
        }

        void RememberName()
        {
            mpName = RoomMessage.CleanName(mpName);
            PlayerPrefs.SetString(NamePrefKey, mpName);
        }

        void DrawConnecting(RoomSession room)
        {
            GUILayout.Label(room.IsHost ? "Oda açılıyor…" : $"{room.Code} odasına bağlanılıyor…", s.H2);
            GUILayout.Label("Bağlantı birkaç saniye sürebilir.", s.WrapDim);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Vazgeç", s.Button, Ht(42f))) Defer(() => Multiplayer.Leave());
        }

        /// <summary>Lobi: oda kodu, mod ve hız (kurucu değiştirir), oyuncular, merkez şehir seçimi, başlat.</summary>
        void DrawLobby(RoomSession room)
        {
            GUILayout.Label("ODA KODU", s.StatLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label(room.Code, s.Title);
            GUILayout.FlexibleSpace();
            if (Application.platform == RuntimePlatform.WebGLPlayer && GUILayout.Button("Bağlantıyı kopyala", s.Tab, Wd(170f), Ht(36f)))
            {
                string link = InviteLink(room.Code);
                Defer(() =>
                {
                    WebClipboard.Copy(link);
                    Toast("Davet bağlantısı kopyalandı.", UIStyles.Green);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Arkadaşların Çok Oyunculu → Odaya katıl'a bu kodu yazsın" +
                            (Application.platform == RuntimePlatform.WebGLPlayer ? " ya da bağlantıyı açsın." : "."), s.WrapDim);
            Sp(8f);

            // Mod ve hız
            if (room.IsHost)
            {
                GUILayout.BeginHorizontal();
                foreach (MatchMode m in new[] { MatchMode.Blitz, MatchMode.Normal })
                {
                    MatchMode selected = m;
                    if (GUILayout.Button($"{MatchRules.Name(m)} · {Fmt.Compact(MatchRules.TargetCash(m))}", room.Mode == m ? s.TabActive : s.Tab, Ht(36f)))
                        Defer(() => Room?.SetMode(selected));
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hız", s.Dim, Wd(50f));
                foreach (int speed in new[] { 1, 2, 4 })
                {
                    int selected = speed;
                    if (GUILayout.Button($"{speed}x", room.Speed == speed ? s.SpeedButtonActive : s.SpeedButton, Ht(32f)))
                        Defer(() => Room?.SetSpeed(selected));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label($"{MatchRules.Name(room.Mode)} · hız {room.Speed}x", s.Bold);
            }
            ColorLabel($"Hedef: kasada {Fmt.Money(room.TargetCash)} — ilk ulaşan kazanır.", UIStyles.Gold, s.Bold);
            Sp(8f);

            // Oyuncular
            GUILayout.Label($"Oyuncular ({room.Players.Count}/{MatchRules.MaxPlayers})", s.H2);
            foreach (var player in room.Players)
            {
                GUILayout.BeginHorizontal(s.Card);
                LayoutIcon(player.IsHost ? UIIcon.Star : UIIcon.People, 18f, 2f);
                Sp(6f);
                GUILayout.Label(player.Name + (player.Id == room.LocalId ? " (sen)" : string.Empty), s.Bold);
                GUILayout.FlexibleSpace();
                CityData city = session.World.GetCity(player.CityId);
                if (city != null)
                {
                    LayoutCityIcon(city, 20f, 1f);
                    Sp(4f);
                    GUILayout.Label(city.DisplayName, s.Label);
                }
                else ColorLabel("şehir seçiyor…", UIStyles.DimText, s.Label);
                GUILayout.EndHorizontal();
            }
            Sp(8f);

            // Merkez şehir
            GUILayout.Label("Merkez şehrin", s.H2);
            RoomPlayer local = room.Local;
            for (int row = 0; row < 2; row++)
            {
                GUILayout.BeginHorizontal();
                for (int col = 0; col < 3; col++)
                {
                    CityData city = session.World.GetCity(QuickStartCityIds[row * 3 + col]);
                    if (city == null) continue;
                    bool chosen = local != null && local.CityId == city.Id;
                    string id = city.Id;
                    if (GUILayout.Button(city.DisplayName, chosen ? s.TabActive : s.Button, Ht(34f))) Defer(() => Room?.SetCity(id));
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();
            Sp(10f);
            if (room.IsHost)
            {
                if (!room.CanStart)
                    GUILayout.Label(room.Players.Count < 2 ? "Başlatmak için en az bir arkadaşın katılmalı." : "Herkes merkez şehrini seçince başlatabilirsin.", s.WrapDim);
                if (EnabledButton(room.CanStart, "YARIŞI BAŞLAT", s.Primary, Ht(48f))) Defer(() => Room?.Start());
            }
            else
            {
                ColorLabel(local != null && string.IsNullOrEmpty(local.CityId) ? "Merkez şehrini seç; sonra kurucu yarışı başlatır." : "Kurucunun yarışı başlatması bekleniyor…", UIStyles.DimText, s.Wrap);
            }
            if (GUILayout.Button("Odadan çık", s.Button, Ht(40f))) Defer(() => Multiplayer.Leave());
        }

        static string InviteLink(string code)
        {
            string url = Application.absoluteURL ?? string.Empty;
            int query = url.IndexOf('?');
            if (query >= 0) url = url.Substring(0, query);
            int hash = url.IndexOf('#');
            if (hash >= 0) url = url.Substring(0, hash);
            return url + "?oda=" + code;
        }

        // ------------------------------------------------------------------ oyun içi

        void OnMatchStarted()
        {
            RoomSession room = Room;
            mpWinner = null;
            session.SecondsPerDay = 4f; // herkes aynı gün süresiyle
            Toast($"Yarış başladı! {MatchRules.Name(room.Mode)}: kasada {Fmt.Money(room.TargetCash)} biriktiren ilk oyuncu kazanır.", UIStyles.Gold);
            CityData hq = session.Headquarters;
            if (hq != null) OpenPlannerNow(hq);
        }

        void OnMatchEnded(RoomPlayer winner)
        {
            mpWinner = winner;
            if (StationBusy) Stations.ExitImmediate();
            modal = Modal.MatchEnd;
        }

        void OnMultiplayerNotice(string text) => Toast(text, UIStyles.Blue);

        /// <summary>Canlı sıralama kartı (haritanın sol üstü): mod, hedef, oyuncular kasaya göre ve hedefe ilerleme.</summary>
        void DrawRaceWidget()
        {
            RoomSession room = Room;
            if (room == null || !(room.Phase == RoomPhase.Playing || room.Phase == RoomPhase.Ended)) return;
            List<RoomPlayer> standings = room.Standings();
            float rowHeight = 30f * u, width = (compact ? 270f : 320f) * u;
            float height = 44f * u + rowHeight * standings.Count + 8f * u;
            var rect = new Rect(ContentLeft + Margin, SafeTop + TopBarHeight + Margin, width, height);
            if (panel != Panel.None && rect.xMax > PanelRect.x - Margin) return;
            uiRects.Add(rect);
            GUI.Box(rect, GUIContent.none, s.Panel);

            float pad = 14f * u;
            UIIcons.Draw(new Rect(rect.x + pad, rect.y + 11f * u, 20f * u, 20f * u), UIIcon.Finish);
            string title = room.Phase == RoomPhase.Ended ? "YARIŞ BİTTİ" : $"{MatchRules.Name(room.Mode).ToUpperInvariant()} · HEDEF {Fmt.Compact(room.TargetCash)}";
            GUI.Label(new Rect(rect.x + pad + 26f * u, rect.y + 12f * u, width - pad * 2f - 26f * u, 20f * u), title, s.StatLabel);

            float y = rect.y + 40f * u;
            for (int i = 0; i < standings.Count; i++)
            {
                RoomPlayer p = standings[i];
                bool me = p.Id == room.LocalId;
                Color color = p.Bankrupt ? UIStyles.Red : !p.Connected ? UIStyles.DimText : me ? UIStyles.Gold : UIStyles.Text;
                Color previous = GUI.contentColor;
                GUI.contentColor = color;
                GUI.Label(new Rect(rect.x + pad, y, 22f * u, 20f * u), $"{i + 1}.", s.Bold);
                string name = p.Name + (p.Bankrupt ? " (iflas)" : !p.Connected ? " (ayrıldı)" : string.Empty);
                GUI.Label(new Rect(rect.x + pad + 22f * u, y, width * 0.5f, 20f * u), name, me ? s.Bold : s.Label);
                GUI.Label(new Rect(rect.xMax - pad - 110f * u, y, 110f * u, 20f * u), Fmt.Compact(p.Cash), s.Right);
                GUI.contentColor = previous;
                DrawBar(new Rect(rect.x + pad + 22f * u, y + 20f * u, width - pad * 2f - 22f * u, 4f * u),
                    Mathf.Clamp01((float)(p.Cash / room.TargetCash)), me ? UIStyles.Gold : UIStyles.Blue);
                y += rowHeight;
            }
        }

        /// <summary>Yarış sonu: kazanan, sıralama; oyun tek başına sürebilir.</summary>
        void DrawMatchEndModal()
        {
            RoomSession room = Room;
            bool won = mpWinner != null && room != null && mpWinner.Id == room.LocalId;
            ColorLabel(won ? "KAZANDIN!" : $"{(mpWinner != null ? mpWinner.Name : "Rakibin")} kazandı", won ? UIStyles.Gold : UIStyles.Orange, s.H1);
            Divider();
            if (room != null)
            {
                GUILayout.Label($"{MatchRules.Name(room.Mode)} yarışı: kasada {Fmt.Money(room.TargetCash)}'a ilk ulaşan " +
                                $"{(mpWinner != null ? mpWinner.Name : "?")} oldu ({(mpWinner != null ? mpWinner.Day : 0)}. gün).", s.Wrap);
                Sp(8f);
                List<RoomPlayer> standings = room.Standings();
                for (int i = 0; i < standings.Count; i++)
                {
                    RoomPlayer p = standings[i];
                    KeyValue($"{i + 1}. {p.Name}{(p.Id == room.LocalId ? " (sen)" : string.Empty)}", Fmt.Money(p.Cash),
                        p.Id == mpWinner?.Id ? UIStyles.Gold : (Color?)null);
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Oynamaya Devam Et", s.Primary, Ht(46f))) Defer(() => modal = Modal.None);
            if (GUILayout.Button("Ana Menü", s.Button, Ht(40f)))
                Defer(() =>
                {
                    Multiplayer?.Leave();
                    EnterMainMenu();
                });
        }
    }
}
