using System.Collections.Generic;
using BusTycoon.AI;
using UnityEngine;

namespace BusTycoon.Game
{
    // Yapay zekâ danışmanı (Y): OmniRoute ağ geçidi üzerinden şirket verisine bakıp öneri veren sohbet paneli.
    public sealed partial class GameUI
    {
        const string AiPrefEnabled = "bt.ai.enabled";
        const string AiPrefUrl = "bt.ai.url";
        const string AiPrefKey = "bt.ai.key";
        const string AiPrefModel = "bt.ai.model";
        const int AiMaxTokens = 600;
        const int AiHistoryLimit = 20;

        enum AiStatus { Unknown, Probing, Online, Offline }

        OmniRouteConfig aiConfig;
        readonly List<ChatMessage> aiHistory = new List<ChatMessage>();
        readonly List<string> aiModels = new List<string>();
        string aiQuestion = string.Empty;
        string aiStatusText = string.Empty;
        AiStatus aiStatus = AiStatus.Unknown;
        bool aiBusy;
        bool aiShowSettings;
        int aiRequestSerial;
        Vector2 advisorScroll;

        OmniRouteConfig AiConfig
        {
            get
            {
                if (aiConfig != null) return aiConfig;
                aiConfig = new OmniRouteConfig
                {
                    enabled = PlayerPrefs.GetInt(AiPrefEnabled, 0) == 1,
                    baseUrl = PlayerPrefs.GetString(AiPrefUrl, OmniRouteConfig.DefaultBaseUrl),
                    apiKey = PlayerPrefs.GetString(AiPrefKey, string.Empty),
                    model = PlayerPrefs.GetString(AiPrefModel, OmniRouteConfig.DefaultModel),
                };
                aiShowSettings = !aiConfig.enabled;
                return aiConfig;
            }
        }

        void SaveAiConfig()
        {
            var config = AiConfig;
            config.baseUrl = OmniRouteConfig.NormalizeBaseUrl(config.baseUrl);
            if (string.IsNullOrWhiteSpace(config.model)) config.model = OmniRouteConfig.DefaultModel;
            PlayerPrefs.SetInt(AiPrefEnabled, config.enabled ? 1 : 0);
            PlayerPrefs.SetString(AiPrefUrl, config.baseUrl);
            PlayerPrefs.SetString(AiPrefKey, config.apiKey ?? string.Empty);
            PlayerPrefs.SetString(AiPrefModel, config.model);
            PlayerPrefs.Save();
        }

        void OpenAdvisorNow()
        {
            OpenPanelNow(Panel.Advisor);
            if (panel == Panel.Advisor && AiConfig.enabled && aiStatus == AiStatus.Unknown) ProbeAi();
        }

        // ------------------------------------------------------------------ çizim

        void DrawAdvisor()
        {
            var config = AiConfig;
            PanelHeader("Yapay Zekâ Danışman", "OmniRoute üzerinden şirketinizi yorumlar");

            // Durum satırı + ayarlar düğmesi
            GUILayout.BeginHorizontal();
            switch (aiStatus)
            {
                case AiStatus.Online: Badge("Bağlı", s.BadgeSuccess); break;
                case AiStatus.Offline: Badge("Bağlantı yok", s.BadgeDanger); break;
                case AiStatus.Probing: Badge("Sınanıyor…", s.BadgeInfo); break;
                default: Badge(config.enabled ? "Hazır" : "Kapalı", s.BadgeWarning); break;
            }
            Sp(6f);
            GUILayout.Label(config.EffectiveModel, s.Small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(aiShowSettings ? "Ayarları gizle" : "Ayarlar", s.Tab, Wd(130f), Ht(32f)))
                Defer(() => aiShowSettings = !aiShowSettings);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(aiStatusText)) GUILayout.Label(aiStatusText, s.WrapDim);
            Sp(4f);

            advisorScroll = BeginScroll(advisorScroll);
            if (aiShowSettings) DrawAdvisorSettings(config);

            if (!config.enabled)
            {
                GUILayout.Label("Danışman kapalı. Bilgisayarınızda OmniRoute'u çalıştırıp yukarıdaki ayarlardan açın. " +
                                "Ayrıntılar: depo içindeki docs/05-OmniRoute-Yapay-Zeka.md", s.WrapDim);
                GUILayout.EndScrollView();
                return;
            }

            // Hızlı sorular
            GUILayout.Label("Hızlı sor", s.H2);
            foreach (string preset in AdvisorBrief.QuickQuestions)
            {
                string question = preset;
                if (EnabledButton(!aiBusy, preset, s.Button, Ht(34f))) Defer(() => AskAi(question));
            }
            Sp(6f);
            Divider();

            // Konuşma
            if (aiHistory.Count == 0)
                GUILayout.Label("Henüz soru sormadınız. Danışman her soruda kasanızı, hatlarınızı ve filonuzu görür; " +
                                "veri cihazınızdan yalnızca OmniRoute'a ve onun seçtiği modele gider.", s.WrapDim);
            foreach (var message in aiHistory)
            {
                GUILayout.BeginVertical(s.Card);
                bool mine = message.Role == ChatRole.User;
                GUILayout.Label(mine ? "Siz" : "Danışman", mine ? s.Small : s.CardTitle);
                GUILayout.Label(message.Content, mine ? s.WrapDim : s.Wrap);
                GUILayout.EndVertical();
                Sp(4f);
            }
            if (aiBusy) GUILayout.Label("Danışman düşünüyor…", s.WrapDim);
            GUILayout.EndScrollView();

            // Soru alanı (panelin altında sabit)
            Divider();
            aiQuestion = TextInput("ai-question", aiQuestion, 400, "Danışmana sorun", 38f);
            GUILayout.BeginHorizontal();
            if (EnabledButton(!aiBusy && aiQuestion.Trim().Length > 0, "Sor", s.Primary, Ht(38f)))
            {
                string question = aiQuestion.Trim();
                Defer(() =>
                {
                    aiQuestion = string.Empty;
                    GUIUtility.keyboardControl = 0;
                    AskAi(question);
                });
            }
            if (EnabledButton(aiHistory.Count > 0 && !aiBusy, "Temizle", s.Button, Wd(110f), Ht(38f)))
                Defer(() => aiHistory.Clear());
            GUILayout.EndHorizontal();
        }

        void DrawAdvisorSettings(OmniRouteConfig config)
        {
            GUILayout.BeginVertical(s.Card);
            GUILayout.Label("OmniRoute bağlantısı", s.CardTitle);
            GUILayout.Label("OmniRoute; Claude, GPT, Gemini ve ücretsiz modelleri tek adreste toplayan açık kaynak ağ geçididir. " +
                            "Paneli: http://localhost:20128", s.WrapDim);
            Sp(4f);

            GUILayout.Label("Adres (…/v1)", s.Small);
            config.baseUrl = TextInput("ai-url", config.baseUrl, 200, "OmniRoute adresi", 34f);
            GUILayout.Label("API anahtarı (panel → Endpoints)", s.Small);
            if (!mobile || !WebTap.Available) config.apiKey = GUILayout.PasswordField(config.apiKey ?? string.Empty, '•', 200, s.Field, Ht(34f));
            else config.apiKey = TextInput("ai-key", config.apiKey, 200, "OmniRoute API anahtarı", 34f);
            GUILayout.Label("Model (auto = OmniRoute seçsin)", s.Small);
            config.model = TextInput("ai-model", config.model, 120, "Model", 34f);

            if (aiModels.Count > 0)
            {
                GUILayout.Label($"Kullanılabilir modeller ({aiModels.Count}):", s.Small);
                GUILayout.BeginHorizontal();
                int shown = 0;
                foreach (string id in aiModels)
                {
                    if (shown == 4) break;
                    string modelId = id;
                    if (GUILayout.Button(modelId, config.model == modelId ? s.TabActive : s.Tab, Ht(28f)))
                        Defer(() => { AiConfig.model = modelId; SaveAiConfig(); });
                    shown++;
                }
                GUILayout.EndHorizontal();
            }
            Sp(6f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(config.enabled ? "Danışman AÇIK" : "Danışman KAPALI", config.enabled ? s.Primary : s.Button, Ht(38f)))
                Defer(() =>
                {
                    AiConfig.enabled = !AiConfig.enabled;
                    SaveAiConfig();
                    if (AiConfig.enabled) ProbeAi();
                    else { aiStatus = AiStatus.Unknown; aiStatusText = string.Empty; }
                });
            if (EnabledButton(aiStatus != AiStatus.Probing, "Kaydet ve sına", s.Button, Ht(38f)))
                Defer(() => { SaveAiConfig(); ProbeAi(); });
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            Sp(6f);
        }

        // ------------------------------------------------------------------ istekler

        void ProbeAi()
        {
            var config = AiConfig.Clone();
            config.baseUrl = OmniRouteConfig.NormalizeBaseUrl(config.baseUrl);
            aiStatus = AiStatus.Probing;
            aiStatusText = config.ModelsUrl;
            StartCoroutine(OmniRouteClient.ListModels(config,
                models =>
                {
                    aiModels.Clear();
                    aiModels.AddRange(models);
                    aiStatus = AiStatus.Online;
                    aiStatusText = models.Count > 0
                        ? $"OmniRoute çalışıyor · {models.Count} model"
                        : "OmniRoute çalışıyor ama model yok: panelden bir sağlayıcı bağlayın.";
                },
                error =>
                {
                    aiStatus = AiStatus.Offline;
                    aiStatusText = error;
                }));
        }

        void AskAi(string question)
        {
            if (aiBusy || string.IsNullOrWhiteSpace(question) || session == null) return;
            var config = AiConfig.Clone();
            if (!config.enabled) return;

            string summary = AdvisorBrief.BuildCompanySummary(session.State, new AdvisorContext
            {
                CurrencySymbol = Fmt.Symbol,
                MaxRoutes = session.IsRunning ? session.Progression.MaxRoutes : 0,
                VictoryCash = GoalTracker.VictoryCash,
                CityName = id => session.World?.GetCity(id)?.DisplayName ?? id,
                ModelName = id => session.Catalog != null ? session.Catalog.GetById(id)?.DisplayName ?? id : id,
            });
            var messages = AdvisorBrief.BuildMessages(summary, aiHistory, question);

            aiHistory.Add(new ChatMessage(ChatRole.User, question));
            TrimAiHistory();
            aiBusy = true;
            int serial = ++aiRequestSerial;
            advisorScroll.y = float.MaxValue; // en alta in

            StartCoroutine(OmniRouteClient.Chat(config, messages, AiMaxTokens,
                reply =>
                {
                    if (serial != aiRequestSerial) return;
                    aiBusy = false;
                    aiStatus = AiStatus.Online;
                    aiHistory.Add(new ChatMessage(ChatRole.Assistant, AdvisorBrief.CleanReply(reply)));
                    TrimAiHistory();
                    advisorScroll.y = float.MaxValue;
                    if (panel != Panel.Advisor && !Throttled("ai-reply", 10f)) Toast("Danışman yanıt verdi (Y).", UIStyles.Blue);
                },
                error =>
                {
                    if (serial != aiRequestSerial) return;
                    aiBusy = false;
                    aiStatus = AiStatus.Offline;
                    aiStatusText = error;
                    aiHistory.Add(new ChatMessage(ChatRole.Assistant, "Hata: " + error));
                    TrimAiHistory();
                }));
        }

        void TrimAiHistory()
        {
            while (aiHistory.Count > AiHistoryLimit) aiHistory.RemoveAt(0);
        }
    }
}
