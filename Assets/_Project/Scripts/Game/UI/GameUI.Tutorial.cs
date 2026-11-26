using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Şehir içi başlangıcın 5 adımlı öğreticisi: 1) şehir seç (kurulumda biter), 2) galeriden dolmuş al, 3) iki durak
    /// seç, hat kur, 4) ilk sefer gününü bekle, 5) bilet fiyatı ve ilk kazanç. İçerik alanının sol altında küçük bir
    /// kart; ilgili düğme altın halkayla vurgulanır. Adım kayıtta saklanır (oyun kapanıp açılınca kaldığı yerden).
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkTutorial, tkTutorialDots;
        readonly System.Collections.Generic.List<VisualElement> tkTutorialDotList = new System.Collections.Generic.List<VisualElement>();
        Label tkTutorialKicker, tkTutorialTitle, tkTutorialText;
        Button tkTutorialAction;
        VisualElement tkTutorialTarget;
        int tkTutorialShown = -1;
        string tkTutorialSignature;

        const int TutorialSteps = 5;

        static readonly (string Title, string Text, string Action)[] TutorialContent =
        {
            ("Şehrini seçtin", "Merkezin hazır. Şimdi ilk aracını alalım.", "Devam"),
            ("Galeriden dolmuş al", "Galeri sekmesinden bir dolmuş (minibüs) satın al. Sermayen ilk araca ve hat ruhsatına yeter.", "Galeriye git"),
            ("İki durak seç, hat kur", "Haritada bir durağa dokun, sonra ikinci durağa. Hat sokaklardan en kısa yoldan gider; teklif uygunsa \"HATTI AÇ\".", "Haritaya git"),
            ("Kurulumu bekle ya da hızlandır", "Hat 3 günde kurulur, dolmuş da bayiden gelir (1 oyun günü = 60 sn). Beklemek istemezsen hat panelinde reklam izleyip hemen açabilirsin. İlk sefer gününün sonunda kazancın kasaya girer.", "Hattıma git"),
            ("Bilet fiyatı ve ilk kazanç", "Hattına dokun ve bilet fiyatına bak: önerilen fiyat en çok kârı getirir. Kazancın ana ekranda.", "Hatlarım"),
        };

        void BuildToolkitTutorial()
        {
            tkTutorial = Ui.El("bt-card bt-tutorial", tkHud);
            tkTutorial.name = "tutorial";
            VisualElement head = Ui.El("bt-row", tkTutorial);
            head.pickingMode = PickingMode.Ignore;
            tkTutorialKicker = Ui.Text("bt-caps is-gold-text", "", UiWeight.Black, head);
            Ui.El("bt-grow", head).pickingMode = PickingMode.Ignore;
            tkTutorialDots = Ui.El("bt-row bt-tutorial__dots", head);
            tkTutorialDots.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < TutorialSteps; i++)
            {
                VisualElement dot = Ui.El("bt-tutorial__dot", tkTutorialDots);
                dot.pickingMode = PickingMode.Ignore;
                tkTutorialDotList.Add(dot);
            }
            tkTutorialTitle = Ui.Text("bt-tutorial__title", "", UiWeight.Black, tkTutorial);
            tkTutorialText = Ui.Text("bt-tutorial__text", "", UiWeight.Regular, tkTutorial);
            VisualElement actions = Ui.El("bt-row bt-tutorial__actions", tkTutorial);
            Button skip = Ui.Button("bt-tutorial__skip", "Öğreticiyi geç", SkipTutorial, actions, UiWeight.Bold);
            skip.name = "tutorial-skip";
            Ui.El("bt-grow", actions).pickingMode = PickingMode.Ignore;
            tkTutorialAction = Ui.Button("bt-tutorial__go", "", TutorialAction, actions, UiWeight.Black);
            tkTutorialAction.name = "tutorial-go";
            tkTutorial.style.display = DisplayStyle.None;
        }

        bool TutorialActive => NewUi && mode == Mode.Playing && session.IsRunning && session.State.tutorialActive;

        /// <summary>Yeni şehir içi oyunda öğretici başlar (1. adım kurulumda tamamlandı).</summary>
        void StartTutorial()
        {
            session.State.tutorialActive = true;
            session.State.tutorialStep = 1;
            session.State.tutorialSawPrice = false;
        }

        void SkipTutorial()
        {
            session.State.tutorialActive = false;
            SetTutorialTarget(null);
        }

        void TutorialAction()
        {
            switch (session.State.tutorialStep)
            {
                case 1: SelectTab(Tab.Shop); break;
                case 2: SelectCityMap(true); SelectTab(Tab.Map); break;
                case 3:
                    // Kurulumdaki ilk dolmuş hattının paneli (reklamla hızlandırma orada)
                    foreach (var route in session.Routes.Routes)
                        if (route.IsUrban) { OpenRoutePanelNow(route, RouteTab.Price); return; }
                    SelectCityMap(true);
                    SelectTab(Tab.Map);
                    break;
                case 4: SelectTab(Tab.Routes); break;
                default: session.State.tutorialStep++; break;
            }
        }

        /// <summary>Adımın bitip bitmediği: oyunun durumundan okunur (oyuncu başka yoldan yapsa da sayılır).</summary>
        bool TutorialStepDone(int step)
        {
            var routes = session.Routes.Routes;
            switch (step)
            {
                case 0: return true;
                case 1: return session.Fleet.Buses.Count > 0;
                case 2:
                    foreach (var route in routes) if (route.IsUrban) return true;
                    return false;
                case 3:
                    foreach (var route in routes) if (route.IsUrban && !route.IsSettingUp && route.HasHistory) return true;
                    return false;
                case 4: return session.State.tutorialSawPrice;
                default: return true;
            }
        }

        void UpdateToolkitTutorial(Rect content, float k)
        {
            bool show = TutorialActive && !StationUi && modal == Modal.None;
            if (TutorialActive)
            {
                // Hat paneli bilet sekmesi açıldıysa son adım görülmüş sayılır
                if (session.State.tutorialStep == 4 && panel == Panel.Route && selectedRoute != null) session.State.tutorialSawPrice = true;
                while (session.State.tutorialStep < TutorialSteps && TutorialStepDone(session.State.tutorialStep))
                    session.State.tutorialStep++;
                if (session.State.tutorialStep >= TutorialSteps)
                {
                    session.State.tutorialActive = false;
                    show = false;
                    Toast("Öğretici tamam! Şimdi filonu büyüt: yeni dolmuşlar, yeni hatlar.", UIStyles.Gold, Sfx.Horn);
                }
            }
            Ui.Show(tkTutorial, show);
            if (!show)
            {
                SetTutorialTarget(null);
                tkTutorialShown = -1;
                return;
            }

            int step = session.State.tutorialStep;
            if (step != tkTutorialShown)
            {
                tkTutorialShown = step;
                var content0 = TutorialContent[Mathf.Clamp(step, 0, TutorialContent.Length - 1)];
                tkTutorialKicker.text = $"ÖĞRETİCİ · {step + 1}/{TutorialSteps}";
                tkTutorialTitle.text = content0.Title;
                tkTutorialText.text = content0.Text;
                tkTutorialAction.text = content0.Action;
                for (int i = 0; i < TutorialSteps; i++)
                {
                    VisualElement dot = tkTutorialDotList[i];
                    Ui.Toggle(dot, "is-done", i < step);
                    Ui.Toggle(dot, "is-current", i == step);
                }
            }

            // Telefonda tek satırlık şerit (adım, başlık, düğme): ekran kısa, büyük kart içeriği kapatıyordu
            Ui.Toggle(tkTutorial, "is-compact", phone);
            if (phone) tkTutorialKicker.text = $"{step + 1}/{TutorialSteps}";
            else if (!tkTutorialKicker.text.StartsWith("ÖĞRETİCİ")) tkTutorialKicker.text = $"ÖĞRETİCİ · {step + 1}/{TutorialSteps}";

            // Yer: içerik alanının sol altı (haritada kartın sol altı; ana ekranda en kârlı hatların altı)
            float width = Mathf.Min(phone ? 460f : 380f, content.width * k * (phone ? 0.62f : 0.36f));
            Ui.Place(tkTutorial, content.x * k + 12f, content.yMax * k - 12f - TutorialHeight(), width, TutorialHeight());
            tkCovered.Add(new Rect(content.x + 12f * u, content.yMax - (12f + TutorialHeight()) * u, width * u, TutorialHeight() * u));

            // Vurgu: adımın düğmesi (sekme ya da Harita düğmesi); zaten oradaysa vurgu yok
            VisualElement target = null;
            if (step == 1 && ActiveTab != Tab.Shop) target = TabButton("tab-shop");
            else if ((step == 2 || step == 3) && !(MapShown && CityMapShown)) target = tkMapDisc;
            else if (step == 4 && ActiveTab != Tab.Routes && panel != Panel.Route) target = TabButton("tab-routes");
            SetTutorialTarget(target);
            if (tkTutorialTarget != null) Ui.Toggle(tkTutorialTarget, "is-pulse", Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.6f);
        }

        float TutorialHeight() => phone ? 44f : 150f;

        VisualElement TabButton(string name)
        {
            foreach (var (button, _, _) in tkNav)
                if (button.name == name) return button;
            return null;
        }

        void SetTutorialTarget(VisualElement target)
        {
            if (tkTutorialTarget == target) return;
            if (tkTutorialTarget != null)
            {
                tkTutorialTarget.RemoveFromClassList("is-tutorial");
                tkTutorialTarget.RemoveFromClassList("is-pulse");
            }
            tkTutorialTarget = target;
            tkTutorialTarget?.AddToClassList("is-tutorial");
        }
    }
}
