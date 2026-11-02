using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde ana menü: maskot, başlık, düğmeler (Oyuna Dön, Yeni Oyun, Çok Oyunculu, Kayıttan Devam Et,
    /// Nasıl Oynanır?, Çıkış), ses ve görüntü kalitesi. Arkada harita kendi kendine süzülür.
    /// İçerik menüye girince, ayar değişince ya da kayıt durumu değişince kurulur. Telefonda başlık solda, düğmeler sağda.
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkMenu, tkMenuCard, tkMenuMascot;
        bool tkMenuDirty = true;
        int tkMenuSignature = int.MinValue;

        void BuildToolkitMenuLayer()
        {
            tkMenu = Ui.El("bt-menu-layer", toolkit.Root);
            tkMenu.pickingMode = PickingMode.Ignore;
            tkMenuCard = Ui.El("bt-menu", tkMenu);
            tkMenu.style.display = DisplayStyle.None;
        }

        int MenuSignature()
        {
            int signature = (session.IsRunning && !session.IsBankrupt ? 1 : 0) + (showMenuHelp ? 2 : 0) + (phone ? 4 : 0);
            signature = signature * 31 + (latestSaveLabel?.GetHashCode() ?? 0);
            signature = signature * 31 + (GameAudio.Enabled ? 1 : 0);
            signature = signature * 31 + (GraphicsQuality.IsAuto ? 7 : (int)GraphicsQuality.Current);
            signature = signature * 31 + (session.State?.companyName?.GetHashCode() ?? 0);
            return signature;
        }

        void UpdateToolkitMenu()
        {
            bool show = mode == Mode.MainMenu;
            Ui.Show(tkMenu, show);
            if (!show) return;
            tkMenu.SetEnabled(modal == Modal.None);
            float k = 1f / u;
            Rect box = MainMenuRect;
            Ui.Place(tkMenuCard, box.x * k, box.y * k, box.width * k, box.height * k);

            // Maskot yolda gidiyormuş gibi hafifçe zıplar
            if (tkMenuMascot != null)
                tkMenuMascot.style.translate = new Translate(0f, -Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 4f);

            int signature = MenuSignature();
            if (!tkMenuDirty && signature == tkMenuSignature) return;
            tkMenuDirty = false;
            tkMenuSignature = signature;
            RebuildToolkitMenu();
        }

        void RebuildToolkitMenu()
        {
            tkMenuCard.Clear();
            tkMenuMascot = null;
            Ui.Toggle(tkMenuCard, "is-wide", phone && !showMenuHelp);
            if (phone && !showMenuHelp)
            {
                // Yatay telefon: ekran kısa, geniş; başlık solda, düğmeler sağda
                VisualElement left = Ui.El("bt-menu__brand", tkMenuCard);
                MenuBrand(left, 70f);
                Ui.El("bt-grow", left);
                QualityTabs(left);
                Credits(left);
                VisualElement right = Ui.El("bt-menu__buttons", tkMenuCard);
                MenuButtons(right, false);
                return;
            }

            MenuBrand(tkMenuCard, compact ? 78f : 108f);
            if (showMenuHelp)
            {
                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scroll.AddToClassList("bt-menu__help");
                tkMenuCard.Add(scroll);
                BuildHelp(scroll.contentContainer);
                ModalButton(tkMenuCard, "Geri", () => { showMenuHelp = false; tkMenuDirty = true; });
                return;
            }
            MenuButtons(tkMenuCard, true);
            Ui.El("bt-grow", tkMenuCard);
            Credits(tkMenuCard);
        }

        void MenuBrand(VisualElement parent, float mascotHeight)
        {
            if (!mascotLoaded)
            {
                mascotLoaded = true;
                mascot = Resources.Load<Texture2D>(DefaultContent.MascotResourcePath);
            }
            if (mascot != null)
            {
                tkMenuMascot = Ui.El("bt-menu__mascot", parent);
                tkMenuMascot.usageHints = UsageHints.DynamicTransform; // her kare zıplar
                tkMenuMascot.style.height = mascotHeight;
                tkMenuMascot.style.backgroundImage = new StyleBackground(mascot);
            }
            Ui.Text("bt-menu__title", "Otobüs İmparatoru", UiWeight.Black, parent);
            Ui.Text("bt-menu__subtitle", "Bus Tycoon · otobüs şirketini kur, büyüt!", UiWeight.Regular, parent);
        }

        void MenuButtons(VisualElement parent, bool withQuality)
        {
            bool running = session.IsRunning && !session.IsBankrupt;
            if (running) ModalButton(parent, $"Oyuna Dön  ({session.State.companyName})", ResumeFromMenu, "is-primary bt-modal__button--main");
            ModalButton(parent, "Yeni Oyun", BeginNewGame, running ? null : "is-primary bt-modal__button--main");
            ModalButton(parent, "Çok Oyunculu  (Blitz · Normal)", OpenMultiplayer);
            string continueText = latestSaveLabel != null ? $"Kayıttan Devam Et  ({latestSaveLabel})" : "Kayıttan Devam Et";
            ModalButton(parent, continueText, () => LoadSlot(latestSaveSlot), enabled: latestSaveSlot != null);
            ModalButton(parent, "Nasıl Oynanır?", () => { showMenuHelp = true; tkMenuDirty = true; });
            if (CanQuit) ModalButton(parent, "Çıkış", QuitGame);
            if (GameAudio.Instance != null)
            {
                Button sound = Ui.Button("bt-check", string.Empty, () =>
                {
                    GameAudio.Enabled = !GameAudio.Enabled;
                    if (GameAudio.Enabled) GameAudio.Play(Sfx.Notify);
                    tkMenuDirty = true;
                }, parent);
                Ui.Toggle(sound, "is-on", GameAudio.Enabled);
                Ui.El("bt-check__box", sound);
                Ui.Text("bt-check__label", "Ses efektleri (tıklama, korna, para, uyarılar)", UiWeight.Regular, sound);
            }
            if (withQuality) QualityTabs(parent);
        }

        void QualityTabs(VisualElement parent)
        {
            string current = GraphicsQuality.Label(GraphicsQuality.Current);
            Ui.Text("bt-caps bt-modal__caption", GraphicsQuality.IsAuto ? $"GÖRÜNTÜ KALİTESİ · OTOMATİK: {current.ToUpperInvariant()}" : "GÖRÜNTÜ KALİTESİ", UiWeight.Bold, parent);
            VisualElement tabs = Ui.El("bt-tabs-inline", parent);
            foreach (QualityLevel? choice in QualityChoices)
            {
                bool active = choice.HasValue ? !GraphicsQuality.IsAuto && GraphicsQuality.Current == choice.Value : GraphicsQuality.IsAuto;
                QualityLevel? target = choice;
                Button tab = Ui.Button("bt-tab", choice.HasValue ? GraphicsQuality.Label(choice.Value) : "Otomatik", () =>
                {
                    if (!active) GraphicsQuality.Select(target);
                    tkMenuDirty = true;
                }, tabs);
                Ui.Toggle(tab, "is-active", active);
            }
        }

        static void Credits(VisualElement parent) =>
            Ui.Text("bt-menu__credits", "Harita: Natural Earth · Şehirler: GeoNames (CC BY 4.0) · Simgeler: Twemoji (CC BY 4.0) · Yazı tipi: Inter (SIL OFL)", UiWeight.Regular, parent);
    }
}
