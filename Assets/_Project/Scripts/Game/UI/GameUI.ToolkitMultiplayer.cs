using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde çok oyunculu: ana menüdeki ekran (oda kur / koda katıl / lobi), oyun içinde canlı sıralama kartı
    /// ve yarış sonu penceresi (<see cref="BuildMatchEndModal"/>). Ekran yalnız oda durumu değişince kurulur. Yazı
    /// alanları bu kurulumu tetiklemez: yazarken odak kaybolmaz.
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkMp, tkMpCard, tkMpBody, tkMpFooter;
        ScrollView tkMpScroll;
        Button tkMpJoin;
        int tkMpSignature = int.MinValue;
        bool tkMpDirty = true;

        VisualElement tkRaceCard;
        int tkRaceSignature = int.MinValue;

        bool ToolkitDrawsMultiplayer => NewUi && mode == Mode.Multiplayer;

        void BuildToolkitMultiplayerLayer()
        {
            tkMp = Ui.El("bt-menu-layer", toolkit.Root);
            tkMp.pickingMode = PickingMode.Ignore;
            tkMpCard = Ui.El("bt-menu bt-mp", tkMp);
            VisualElement header = Ui.El("bt-row bt-mp__header", tkMpCard);
            Ui.Icon(UIIcon.Finish, "bt-mp__icon", header);
            Ui.Text("bt-sheet__title", "Çok Oyunculu", UiWeight.Black, header);
            tkMpScroll = new ScrollView(ScrollViewMode.Vertical);
            tkMpScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            tkMpScroll.AddToClassList("bt-sheet__scroll");
            tkMpCard.Add(tkMpScroll);
            tkMpBody = tkMpScroll.contentContainer;
            tkMpFooter = Ui.El("bt-mp__footer", tkMpCard);
            tkMp.style.display = DisplayStyle.None;
        }

        void UpdateToolkitMultiplayer()
        {
            bool show = ToolkitDrawsMultiplayer;
            Ui.Show(tkMp, show);
            if (!show)
            {
                tkMpDirty = true;
                return;
            }
            tkMp.SetEnabled(modal == Modal.None);
            float k = 1f / u;
            Rect box = MultiplayerRect;
            Ui.Place(tkMpCard, box.x * k, box.y * k, box.width * k, box.height * k);

            int signature = MultiplayerSignature();
            if (!tkMpDirty && signature == tkMpSignature) return;
            tkMpDirty = false;
            tkMpSignature = signature;
            RebuildToolkitMultiplayer();
        }

        static int Hash(string text) => text?.GetHashCode() ?? 0;

        /// <summary>Ekranı değiştiren oda durumu (yazı alanları hariç).</summary>
        int MultiplayerSignature()
        {
            RoomSession room = Room;
            RoomPhase phase = room != null ? room.Phase : RoomPhase.Idle;
            int h = (int)phase * 31 + (mobile ? 1 : 0);
            switch (phase)
            {
                case RoomPhase.Connecting:
                    h = h * 31 + (room.IsHost ? 1 : 0);
                    h = h * 31 + Hash(room.Code);
                    break;
                case RoomPhase.Lobby:
                    h = h * 31 + Hash(room.Code);
                    h = h * 31 + (room.IsHost ? 1 : 0) + (room.CanStart ? 2 : 0);
                    h = h * 31 + (int)room.Mode * 7 + room.Speed;
                    h = h * 31 + (Entitlements.Owns(ContentPacks.Europe) ? 1 : 0);
                    foreach (RoomPlayer player in room.Players)
                    {
                        h = h * 31 + Hash(player.Id);
                        h = h * 31 + Hash(player.Name);
                        h = h * 31 + Hash(player.CityId);
                        h = h * 31 + (player.IsHost ? 1 : 0);
                    }
                    break;
                default:
                    h = h * 31 + (int)mpMode;
                    if (room != null && room.Phase == RoomPhase.Closed) h = h * 31 + Hash(room.Status);
                    break;
            }
            return h;
        }

        void RebuildToolkitMultiplayer()
        {
            tkMpBody.Clear();
            tkMpFooter.Clear();
            tkMpJoin = null;
            RoomSession room = Room;
            switch (room != null ? room.Phase : RoomPhase.Idle)
            {
                case RoomPhase.Connecting: BuildConnecting(room); break;
                case RoomPhase.Lobby: BuildLobby(room); break;
                default: BuildRoomChoice(room); break;
            }
        }

        /// <summary>Oda kurma (mod seçimiyle) ya da kodla katılma.</summary>
        void BuildRoomChoice(RoomSession failed)
        {
            VisualElement body = tkMpBody;
            Ui.Text("bt-item__text", "Arkadaşlarınla aynı anda yarış: herkes kendi şirketini kurar, hedef kasaya ilk ulaşan kazanır.", UiWeight.Regular, body);

            Ui.Text("bt-caps bt-modal__caption", "ADIN", UiWeight.Bold, body);
            TkTextInput(body, "mp_name", mpName, 18, "Adın (diğer oyuncular görür)", value => mpName = value);

            Section(body, "Oda kur");
            VisualElement modes = Ui.El("bt-row bt-mp__modes", body);
            ModeChoice(modes, MatchMode.Blitz, "Blitz", "$1.000.000 · hızlı (2x)");
            ModeChoice(modes, MatchMode.Normal, "Normal", "$10.000.000 · normal akış (1x)");
            ModalButton(body, "ODA KUR", () =>
            {
                RememberName();
                Multiplayer.CreateRoom(mpName, mpMode);
            }, "is-primary bt-modal__button--main");

            Section(body, "Odaya katıl");
            VisualElement join = Ui.El("bt-row bt-mp__join", body);
            VisualElement code = TkTextInput(join, "mp_code", mpCode, 8, "Oda kodu (5 karakter)", value =>
            {
                mpCode = MatchRules.NormalizeCode(value);
                tkMpJoin?.SetEnabled(mpCode.Length == MatchRules.CodeLength);
            });
            code.AddToClassList("bt-grow");
            tkMpJoin = ModalButton(join, "KATIL", () =>
            {
                RememberName();
                Multiplayer.JoinRoom(mpName, mpCode);
            }, "is-primary bt-mp__join-button", enabled: mpCode.Length == MatchRules.CodeLength);
            Ui.Text("bt-item__text", "Arkadaşının verdiği 5 karakterlik oda kodunu yaz ya da gönderdiği bağlantıyı aç.", UiWeight.Regular, body);

            if (failed != null && failed.Phase == RoomPhase.Closed && !string.IsNullOrEmpty(failed.Status))
                Ui.Text("bt-modal__text is-down bt-mp__status", failed.Status, UiWeight.Bold, body);
            if (!WebRoomTransport.IsSupported && MultiplayerDirector.TransportFactory == MultiplayerDirector.WebFactory)
                Ui.Text("bt-modal__text is-warning-text bt-mp__status", "Çok oyunculu şimdilik web sürümünde oynanır.", UiWeight.Bold, body);

            ModalButton(tkMpFooter, "Geri", LeaveRoomToMenu);
        }

        void ModeChoice(VisualElement parent, MatchMode choice, string title, string detail)
        {
            Button card = Ui.Button("bt-mode", string.Empty, () =>
            {
                mpMode = choice;
                tkMpDirty = true;
            }, parent);
            Ui.Toggle(card, "is-active", mpMode == choice);
            Ui.Text("bt-mode__title", title, UiWeight.Black, card);
            Ui.Text("bt-mode__text", detail, UiWeight.Regular, card);
        }

        void BuildConnecting(RoomSession room)
        {
            Ui.Text("bt-modal__title", room.IsHost ? "Oda açılıyor…" : $"{room.Code} odasına bağlanılıyor…", UiWeight.Black, tkMpBody);
            Ui.Text("bt-item__text", "Bağlantı birkaç saniye sürebilir.", UiWeight.Regular, tkMpBody);
            ModalButton(tkMpFooter, "Vazgeç", () => Multiplayer.Leave());
        }

        /// <summary>Lobi: oda kodu ve davet, mod ve hız (kurucu değiştirir), oyuncular, merkez şehir seçimi, başlat.</summary>
        void BuildLobby(RoomSession room)
        {
            VisualElement body = tkMpBody;
            VisualElement codeCard = Ui.El("bt-item is-current", body);
            Ui.Text("bt-caps", "ODA KODU", UiWeight.Bold, codeCard);
            VisualElement codeRow = Ui.El("bt-row", codeCard);
            Ui.Text("bt-mp__code bt-grow", room.Code, UiWeight.Black, codeRow);
            InviteButton(codeRow, room.Code);
            Ui.Text("bt-item__text", "Arkadaşların Çok Oyunculu → Odaya katıl'a bu kodu yazsın ya da bağlantıyı açsın.", UiWeight.Regular, codeCard);
            if (room.IsHost)
                Ui.Text("bt-item__text is-warning-text", "Odayı sen kurdun: yarış bitene kadar bu oyun açık kalsın (kapatırsan oda kapanır).", UiWeight.Bold, codeCard);

            if (room.IsHost)
            {
                Ui.Text("bt-caps bt-modal__caption", "MOD", UiWeight.Bold, body);
                VisualElement modes = Ui.El("bt-tabs-inline", body);
                foreach (MatchMode m in new[] { MatchMode.Blitz, MatchMode.Normal })
                {
                    MatchMode selected = m;
                    Button tab = Ui.Button("bt-tab", $"{MatchRules.Name(m)} · {Fmt.Compact(MatchRules.TargetCash(m))}", () => Room?.SetMode(selected), modes);
                    Ui.Toggle(tab, "is-active", room.Mode == m);
                }
                Ui.Text("bt-caps bt-modal__caption", "HIZ", UiWeight.Bold, body);
                VisualElement speeds = Ui.El("bt-tabs-inline", body);
                foreach (int speed in new[] { 1, 2, 4 })
                {
                    int selected = speed;
                    Button tab = Ui.Button("bt-tab", $"{speed}x", () => Room?.SetSpeed(selected), speeds);
                    Ui.Toggle(tab, "is-active", room.Speed == speed);
                }
            }
            else
            {
                Ui.Text("bt-item__title", $"{MatchRules.Name(room.Mode)} · hız {room.Speed}x", UiWeight.Bold, body);
            }
            Ui.Text("bt-item__title is-gold bt-mp__target", $"Hedef: kasada {Fmt.Money(room.TargetCash)} — ilk ulaşan kazanır.", UiWeight.Bold, body);

            Section(body, $"Oyuncular ({room.Players.Count}/{MatchRules.MaxPlayers})");
            foreach (RoomPlayer player in room.Players)
            {
                VisualElement row = Ui.El("bt-item bt-row bt-mp__player", body);
                Ui.Icon(player.IsHost ? UIIcon.Star : UIIcon.People, "bt-item__icon", row);
                Ui.Text("bt-item__title bt-grow", player.Name + (player.Id == room.LocalId ? " (sen)" : string.Empty), UiWeight.Bold, row);
                CityData city = session.World.GetCity(player.CityId);
                if (city != null)
                {
                    VisualElement icon = Ui.El("bt-item__icon", row);
                    Sprite sprite = UICityIcons.Sprite(city);
                    if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
                    Ui.Text("bt-item__text", city.DisplayName, UiWeight.Bold, row);
                }
                else Ui.Text("bt-item__text", "şehir seçiyor…", UiWeight.Regular, row);
            }

            Section(body, "Merkez şehrin");
            RoomPlayer local = room.Local;
            VisualElement cities = Ui.El("bt-tabs-inline", body);
            // Avrupa Paketi varsa Avrupa başkentleri de (paket web'de herkeste var: odadaki herkes aynı haritada)
            var ids = new List<string>(QuickStartCityIds);
            if (Entitlements.Owns(ContentPacks.Europe)) ids.AddRange(EuropeStartCityIds);
            foreach (string id in ids)
            {
                CityData city = session.World.GetCity(id);
                if (city == null) continue;
                string target = city.Id;
                Button tab = Ui.Button("bt-tab", city.DisplayName, () => Room?.SetCity(target), cities);
                Ui.Toggle(tab, "is-active", local != null && local.CityId == city.Id);
            }

            if (room.IsHost)
            {
                if (!room.CanStart)
                    Ui.Text("bt-item__text", room.Players.Count < 2 ? "Başlatmak için en az bir arkadaşın katılmalı." : "Herkes merkez şehrini seçince başlatabilirsin.", UiWeight.Regular, tkMpFooter);
                ModalButton(tkMpFooter, "YARIŞI BAŞLAT", () => Room?.Start(), "is-primary bt-modal__button--main", enabled: room.CanStart);
            }
            else
            {
                Ui.Text("bt-item__text", local != null && string.IsNullOrEmpty(local.CityId) ? "Merkez şehrini seç; sonra kurucu yarışı başlatır." : "Kurucunun yarışı başlatması bekleniyor…", UiWeight.Regular, tkMpFooter);
            }
            ModalButton(tkMpFooter, "Odadan çık", () => Multiplayer.Leave());
        }

        /// <summary>Davet bağlantısı: web'de tarayıcı paylaşır (telefonda paylaşım menüsü, bilgisayarda panoya), diğerinde Unity panosu.</summary>
        void InviteButton(VisualElement parent, string code)
        {
            string link = InviteLink(code);
            bool web = WebTap.Available;
            Button button = Ui.Button("bt-tab is-active bt-mp__invite", web && mobile ? "Bağlantıyı paylaş" : "Bağlantıyı kopyala", () =>
            {
                if (web) return; // web'de dokunuşu tarayıcı alır
                GUIUtility.systemCopyBuffer = link;
                Toast("Davet bağlantısı kopyalandı.", UIStyles.Green);
            }, parent);
            if (web)
                TkShareZone(button, "mp_invite", link, $"Otobüs İmparatoru'nda yarışalım! Oda kodu: {code}", result =>
                    Toast(result == "copied" ? "Davet bağlantısı kopyalandı." : result == "shared" ? "Davet gönderildi." : "Bağlantıyı kopyalayıp arkadaşına gönder.",
                        UIStyles.Green));
        }

        // ------------------------------------------------------------------ oyun içi: canlı sıralama

        void BuildToolkitRace()
        {
            tkRaceCard = Ui.El("bt-race-card", tkHud);
            tkRaceCard.style.display = DisplayStyle.None;
        }

        void UpdateToolkitRace(float k)
        {
            RoomSession room = Room;
            bool show = !StationUi && room != null && (room.Phase == RoomPhase.Playing || room.Phase == RoomPhase.Ended);
            float width = (compact ? 270f : 320f) * u;
            float left = ContentLeft + Margin, top = SafeTop + TopBarHeight + Margin;
            if (show && panel != Panel.None && left + width > PanelRect.x - Margin) show = false;
            Ui.Show(tkRaceCard, show);
            if (!show)
            {
                tkRaceSignature = int.MinValue;
                return;
            }
            float height = 44f * u + 30f * u * room.Players.Count + 8f * u;
            Ui.Place(tkRaceCard, left * k, top * k, width * k, height * k);
            tkCovered.Add(new Rect(left, top, width, height));

            int signature = (int)room.Phase * 31 + (int)room.Mode;
            foreach (RoomPlayer player in room.Players)
            {
                signature = signature * 31 + Hash(player.Id);
                signature = signature * 31 + Hash(player.Name);
                signature = signature * 31 + (int)(player.Cash / 1000.0);
                signature = signature * 31 + (player.Bankrupt ? 1 : 0) + (player.Connected ? 2 : 0);
            }
            if (signature == tkRaceSignature) return;
            tkRaceSignature = signature;

            tkRaceCard.Clear();
            VisualElement header = Ui.El("bt-row bt-race-card__header", tkRaceCard);
            Ui.Icon(UIIcon.Finish, "bt-icon bt-race-card__icon", header);
            Ui.Text("bt-caps", room.Phase == RoomPhase.Ended ? "YARIŞ BİTTİ" : $"{MatchRules.Name(room.Mode).ToUpperInvariant()} · HEDEF {Fmt.Compact(room.TargetCash)}", UiWeight.Bold, header);
            List<RoomPlayer> standings = room.Standings();
            for (int i = 0; i < standings.Count; i++)
            {
                RoomPlayer p = standings[i];
                bool me = p.Id == room.LocalId;
                string tone = p.Bankrupt ? "is-down" : !p.Connected ? "bt-dim" : me ? "is-gold" : null;
                VisualElement row = Ui.El("bt-race-card__row", tkRaceCard);
                VisualElement line = Ui.El("bt-row", row);
                Label rank = Ui.Text("bt-race-card__rank", $"{i + 1}.", UiWeight.Bold, line);
                Label name = Ui.Text("bt-race-card__name bt-grow", p.Name + (p.Bankrupt ? " (iflas)" : !p.Connected ? " (ayrıldı)" : string.Empty), me ? UiWeight.Black : UiWeight.Bold, line);
                Label cash = Ui.Text("bt-race-card__cash", Fmt.Compact(p.Cash), UiWeight.Bold, line);
                if (tone != null)
                {
                    rank.AddToClassList(tone);
                    name.AddToClassList(tone);
                    cash.AddToClassList(tone);
                }
                VisualElement bar = Ui.El("bt-bar bt-race-card__bar", row);
                VisualElement fill = Ui.El(me ? "bt-bar__fill bt-bar__fill--gold" : "bt-bar__fill bt-bar__fill--blue", bar);
                Ui.Fill(fill, Mathf.Clamp01((float)(p.Cash / room.TargetCash)));
            }
        }

        // ------------------------------------------------------------------ yarış sonu penceresi

        void BuildMatchEndModal(VisualElement body)
        {
            RoomSession room = Room;
            bool won = mpWinner != null && room != null && mpWinner.Id == room.LocalId;
            ModalTitle(body, won ? "KAZANDIN!" : $"{(mpWinner != null ? mpWinner.Name : "Rakibin")} kazandı", won ? "is-gold" : "is-warning-text");
            if (room != null)
            {
                Ui.Text("bt-modal__text", $"{MatchRules.Name(room.Mode)} yarışı: kasada {Fmt.Money(room.TargetCash)}'a ilk ulaşan " +
                                          $"{(mpWinner != null ? mpWinner.Name : "?")} oldu ({(mpWinner != null ? mpWinner.Day : 0)}. gün).", UiWeight.Regular, body);
                VisualElement table = Ui.El("bt-item", body);
                List<RoomPlayer> standings = room.Standings();
                for (int i = 0; i < standings.Count; i++)
                {
                    RoomPlayer p = standings[i];
                    KeyValue(table, $"{i + 1}. {p.Name}{(p.Id == room.LocalId ? " (sen)" : string.Empty)}", Fmt.Money(p.Cash),
                        p.Id == mpWinner?.Id ? "is-gold" : null);
                }
            }
            Ui.El("bt-modal__spacer", body);
            ModalButton(body, "Oynamaya Devam Et", () => modal = Modal.None, "is-primary bt-modal__button--main");
            ModalButton(body, "Ana Menü", () =>
            {
                Multiplayer?.Leave();
                EnterMainMenu();
            });
        }
    }
}
