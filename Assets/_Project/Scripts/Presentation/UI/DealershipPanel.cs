using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Otobüs galerisi. Tüm segmentler alttan üste tek listede görünür; kilitliler de gösterilir ki
    /// oyuncu "sırada ne var, ne gerekiyor" bilgisini her zaman görsün (progression motivasyonu).
    /// "2. el" seçeneği alt segmentte ucuz başlangıç sağlar; daha yıpranmış araç = daha çok bakım/arıza.
    /// </summary>
    public sealed class DealershipPanel : UIScreen
    {
        [SerializeField] BusModelCardView cardPrefab;
        [SerializeField] Transform cardRoot;
        [SerializeField] Toggle usedToggle;
        [SerializeField] TMP_Text cashLabel;
        [SerializeField] TMP_Text feedbackLabel;
        [SerializeField] TMP_Text nextGoalLabel;
        [SerializeField] Button closeButton;
        [Tooltip("2. el araçların durumu (0..1).")]
        [SerializeField, Range(0.3f, 0.95f)] float usedCondition = 0.6f;

        readonly List<BusModelCardView> cards = new List<BusModelCardView>();
        readonly HashSet<string> seenModelIds = new HashSet<string>();

        float Condition => usedToggle != null && usedToggle.isOn ? usedCondition : 1f;

        protected override void OnInitialized()
        {
            closeButton.onClick.AddListener(() => Flow.Back());
            if (usedToggle != null) usedToggle.onValueChanged.AddListener(_ => Refresh());
        }

        protected override void OnShown()
        {
            if (cards.Count == 0) BuildCards();
            Session.Finance.CashChanged += HandleCashChanged;
            feedbackLabel.text = string.Empty;
            Refresh();
        }

        protected override void OnHidden()
        {
            Session.Finance.CashChanged -= HandleCashChanged;
            // Galeri bir kez görüldüyse açık modellerin "YENİ" rozeti kalkar.
            foreach (var model in Session.Catalog.SortedByTier)
                if (Session.Progression.IsModelUnlocked(model)) seenModelIds.Add(model.Id);
        }

        void HandleCashChanged(double _) => Refresh();

        void BuildCards()
        {
            foreach (var model in Session.Catalog.SortedByTier)
            {
                BusModelCardView card = Instantiate(cardPrefab, cardRoot);
                card.BuyClicked += Buy;
                cards.Add(card);
            }
        }

        void Refresh()
        {
            var fleet = Session.Fleet;
            var progression = Session.Progression;
            cashLabel.text = UIFormat.Money(Session.Finance.Cash);

            IReadOnlyList<BusModelData> models = Session.Catalog.SortedByTier;
            for (int i = 0; i < cards.Count && i < models.Count; i++)
            {
                BusModelData model = models[i];
                bool unlocked = progression.IsModelUnlocked(model);
                float price = fleet.GetPurchasePrice(model, Condition);
                cards[i].Bind(model, price, unlocked, progression.GetLockReason(model),
                    Session.Finance.CanAfford(price), unlocked && !seenModelIds.Contains(model.Id));
            }

            BusModelData next = progression.GetNextLockedModel();
            nextGoalLabel.text = next == null
                ? "Tüm modeller açık."
                : $"Sıradaki hedef: {next.DisplayName} — {progression.GetLockReason(next)}";
        }

        void Buy(BusModelData model)
        {
            var result = Session.Fleet.TryPurchase(model, Condition, out BusInstance bus);
            feedbackLabel.text = result.Message;
            if (result) Refresh();
        }
    }
}
