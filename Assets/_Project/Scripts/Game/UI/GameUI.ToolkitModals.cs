using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde pencereler (oyun sırasında): duraklatma menüsü ve ayarlar, onay, iflas, zafer. Arka plan kararır ve
    /// tıklamayı tutar; kutu ortada belirir. İçerik pencere değişince ya da bir ayar değişince kurulur.
    /// Çok oyunculu maç sonu penceresi menülerle birlikte taşınacak (şimdilik IMGUI).
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkModalLayer, tkModalBox;
        ScrollView tkModalScroll;
        Modal tkBuiltModal = Modal.None;
        bool tkModalDirty;
        int tkModalOpenFrame = -1;

        bool ToolkitDrawsModal => NewUi && mode == Mode.Playing && modal != Modal.None && modal != Modal.MatchEnd;

        void BuildToolkitModalLayer()
        {
            tkModalLayer = Ui.El("bt-modal-layer", toolkit.Root);
            tkModalBox = Ui.El("bt-modal", tkModalLayer);
            tkModalScroll = new ScrollView(ScrollViewMode.Vertical);
            tkModalScroll.AddToClassList("bt-modal__scroll");
            tkModalBox.Add(tkModalScroll);
            tkModalLayer.style.display = DisplayStyle.None;
        }

        void UpdateToolkitModal()
        {
            bool show = ToolkitDrawsModal;
            Ui.Show(tkModalLayer, show);
            if (!show)
            {
                tkBuiltModal = Modal.None;
                return;
            }
            float k = 1f / u;
            Rect box = ModalRect;
            Ui.Place(tkModalBox, box.x * k, box.y * k, box.width * k, box.height * k);
            if (modal != tkBuiltModal)
            {
                tkModalLayer.RemoveFromClassList("is-open");
                tkModalOpenFrame = Time.frameCount + 1;
                tkModalScroll.scrollOffset = Vector2.zero;
                tkModalDirty = true;
            }
            else if (tkModalOpenFrame >= 0 && Time.frameCount >= tkModalOpenFrame)
            {
                tkModalOpenFrame = -1;
                tkModalLayer.AddToClassList("is-open");
            }
            if (!tkModalDirty) return;
            tkModalDirty = false;
            tkBuiltModal = modal;
            VisualElement body = tkModalScroll.contentContainer;
            body.Clear();
            switch (modal)
            {
                case Modal.Pause: BuildPauseModal(body); break;
                case Modal.GameOver: BuildGameOverModal(body); break;
                case Modal.Victory: BuildVictoryModal(body); break;
                case Modal.Confirm: BuildConfirmModal(body); break;
            }
        }

        static Label ModalTitle(VisualElement body, string text, string tone = null)
        {
            Label title = Ui.Text("bt-modal__title", text, UiWeight.Black, body);
            if (tone != null) title.AddToClassList(tone);
            return title;
        }

        static Button ModalButton(VisualElement body, string text, Action action, string variant = null, bool enabled = true)
        {
            Button button = Ui.Button("bt-btn bt-modal__button" + (variant != null ? " " + variant : string.Empty), text, action, body);
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>Onay kutusu satırı (ayar): kutu + yazı; tıklanınca değişir.</summary>
        Button CheckRow(VisualElement body, string label, bool on, Action toggle)
        {
            Button row = Ui.Button("bt-check", string.Empty, () =>
            {
                toggle();
                tkModalDirty = true;
            }, body);
            Ui.Toggle(row, "is-on", on);
            Ui.El("bt-check__box", row);
            Ui.Text("bt-check__label", label, UiWeight.Regular, row);
            return row;
        }

        void BuildPauseModal(VisualElement body)
        {
            bool race = RaceLocked;
            ModalTitle(body, race ? "Menü (yarış sürüyor)" : "Oyun Duraklatıldı");
            ModalButton(body, "Devam Et", ClosePauseMenu, "is-primary bt-modal__button--main");
            ModalButton(body, "Kaydet", () => SaveTo(SaveSlot));
            if (compact) ModalButton(body, "Günlük (son olaylar)", () => { ClosePauseMenu(); OpenPanelNow(Panel.Log); });
            if (phone) ModalButton(body, "Nasıl Oynanır? (yardım)", () => { ClosePauseMenu(); OpenPanelNow(Panel.Help); });
            if (race)
            {
                ModalButton(body, "Yarıştan Çık (tek başına devam)",
                    () => AskConfirm("Yarıştan çık", "Odadan ayrılırsınız; şirketiniz tek başına oyunda kalır.", () => Multiplayer?.Leave()), "is-danger");
            }
            else
            {
                ModalButton(body, "Son Kaydı Yükle", () => LoadSlot(latestSaveSlot), enabled: latestSaveSlot != null);
                Ui.Text("bt-caps bt-modal__caption", "GÜN SÜRESİ (1X HIZDA)", UiWeight.Bold, body);
                VisualElement days = Ui.El("bt-tabs-inline", body);
                foreach (float seconds in new[] { 2f, 4f, 8f })
                {
                    float value = seconds;
                    Button tab = Ui.Button("bt-tab", $"{seconds:0} sn", () => { session.SecondsPerDay = value; tkModalDirty = true; }, days);
                    Ui.Toggle(tab, "is-active", Mathf.Approximately(session.SecondsPerDay, seconds));
                }
            }

            Ui.Text("bt-caps bt-modal__caption", "AYARLAR", UiWeight.Bold, body);
            DayNightCycle cycle = DayNightCycle.Instance;
            if (cycle != null) CheckRow(body, "Gece-gündüz döngüsü (gece harita koyu tema)", cycle.Enabled, () => cycle.SetEnabled(!cycle.Enabled));
            if (GameAudio.Instance != null)
                CheckRow(body, "Ses efektleri (tıklama, korna, para, uyarılar)", GameAudio.Enabled, () =>
                {
                    GameAudio.Enabled = !GameAudio.Enabled;
                    if (GameAudio.Enabled) GameAudio.Play(Sfx.Notify); // açılınca örnek ses
                });
            CheckRow(body, "Gün sonu kazanç yazıları (hatların üstünde)", IncomeLabelsEnabled, () => IncomeLabelsEnabled = !IncomeLabelsEnabled);

            string current = GraphicsQuality.Label(GraphicsQuality.Current);
            Ui.Text("bt-caps bt-modal__caption", GraphicsQuality.IsAuto ? $"GÖRÜNTÜ KALİTESİ · OTOMATİK: {current.ToUpperInvariant()}" : "GÖRÜNTÜ KALİTESİ", UiWeight.Bold, body);
            VisualElement quality = Ui.El("bt-tabs-inline", body);
            foreach (QualityLevel? choice in QualityChoices)
            {
                bool active = choice.HasValue ? !GraphicsQuality.IsAuto && GraphicsQuality.Current == choice.Value : GraphicsQuality.IsAuto;
                QualityLevel? target = choice;
                Button tab = Ui.Button("bt-tab", choice.HasValue ? GraphicsQuality.Label(choice.Value) : "Otomatik", () =>
                {
                    if (!active) GraphicsQuality.Select(target);
                    tkModalDirty = true;
                }, quality);
                Ui.Toggle(tab, "is-active", active);
            }

            Ui.El("bt-modal__spacer", body);
            ModalButton(body, "Ana Menü", EnterMainMenu);
            if (CanQuit) ModalButton(body, "Oyundan Çık", () => { session.SaveGame(AutosaveSlot); QuitGame(); }, "is-danger");
        }

        void BuildGameOverModal(VisualElement body)
        {
            ModalTitle(body, "İFLAS ETTİNİZ", "is-down");
            Ui.Text("bt-modal__text", $"Kasanız {session.ProgressionConfig.BankruptcyGraceDays} gün boyunca eksi kaldı ve alacaklılar şirkete el koydu.", UiWeight.Regular, body);
            VisualElement stats = Ui.El("bt-item", body);
            KeyValue(stats, "Dayanılan süre", $"{session.State.currentDay} gün");
            KeyValue(stats, "Taşınan yolcu", Fmt.Num(session.State.totalPassengers));
            KeyValue(stats, "Ulaşılan seviye", session.Progression.Level.ToString());
            Ui.Text("bt-item__text", "İpucu: Zarar eden hatları kapatın, atıl otobüsleri satın ve fiyatı 'ideal bölge'de tutun.", UiWeight.Regular, body);
            Ui.El("bt-modal__spacer", body);
            ModalButton(body, "Son Kaydı Yükle", () => LoadSlot(latestSaveSlot), enabled: latestSaveSlot != null);
            ModalButton(body, "Yeni Oyun", BeginNewGame, "is-primary bt-modal__button--main");
            ModalButton(body, "Ana Menü", EnterMainMenu);
        }

        void BuildVictoryModal(VisualElement body)
        {
            ModalTitle(body, "TEBRİKLER!", "is-gold");
            Ui.Text("bt-modal__text", $"{session.State.companyName} kasasında {Fmt.Money(GoalTracker.VictoryCash)}'ı aştı. Artık bir Otobüs İmparatorusunuz!", UiWeight.Regular, body);
            VisualElement stats = Ui.El("bt-item", body);
            KeyValue(stats, "Süre", $"{session.State.currentDay} gün");
            KeyValue(stats, "Taşınan yolcu", Fmt.Num(session.State.totalPassengers));
            KeyValue(stats, "Hat / Otobüs", $"{session.Routes.Routes.Count} / {session.Fleet.Buses.Count}");
            KeyValue(stats, "İtibar", $"{session.Progression.Reputation:0}");
            Ui.El("bt-modal__spacer", body);
            ModalButton(body, "Oynamaya Devam Et", () => modal = Modal.None, "is-primary bt-modal__button--main");
            ModalButton(body, "Ana Menü", EnterMainMenu);
        }

        void BuildConfirmModal(VisualElement body)
        {
            ModalTitle(body, confirmTitle);
            Ui.Text("bt-modal__text", confirmText, UiWeight.Regular, body);
            Ui.El("bt-modal__spacer", body);
            VisualElement row = Ui.El("bt-actions bt-actions--fill", body);
            ModalButton(row, "Vazgeç", () => modal = Modal.None);
            Action action = confirmAction;
            ModalButton(row, "Onayla", () =>
            {
                modal = Modal.None;
                action?.Invoke();
            }, "is-danger");
        }
    }
}
