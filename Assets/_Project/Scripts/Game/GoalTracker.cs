using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Economy;

namespace BusTycoon.Game
{
    /// <summary>
    /// Oyuncuya yön veren görev zinciri: her görev bir öğretici adım + para ödülüdür. Son görev
    /// (<see cref="VictoryCash"/> kasa) "kazanma" anıdır; oyun sonrasında serbest devam eder.
    /// Tamamlanan görevler <c>CompanyState.completedGoalIds</c> içinde kaydedilir.
    /// </summary>
    public sealed class GoalTracker
    {
        /// <summary>Oyunu kazanmak için kasada olması gereken para.</summary>
        public const long VictoryCash = 10_000_000;

        public sealed class Goal
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Hint;
            public readonly float Reward;
            public readonly bool IsVictory;
            internal readonly Func<GameSession, bool> Condition;

            public Goal(string id, string title, string hint, float reward, Func<GameSession, bool> condition, bool isVictory = false)
            {
                Id = id;
                Title = title;
                Hint = hint;
                Reward = reward;
                Condition = condition;
                IsVictory = isVictory;
            }
        }

        readonly GameSession session;
        readonly List<Goal> goals = new List<Goal>();
        bool priceAdjusted;

        public event Action<Goal> Completed;

        public GoalTracker(GameSession session)
        {
            this.session = session;

            Add("first_route", "İlk hattını aç", "Yeni Hat (N) ile kalkış ve varış şehrine tıklayın ya da bir durağa girip 'Buradan hat aç'a basın.", 3000,
                s => s.Routes.Routes.Count >= 1);
            Add("first_price", "Bilet fiyatını ayarla", "Hat panelinde kaydırıcıyla fiyatı değiştirip 'Uygula'ya basın.", 1000,
                _ => priceAdjusted);
            Add("second_bus", "Filoyu büyüt: 2 otobüs", "Galeri'den (G) bir otobüs alın.", 3000,
                s => s.Fleet.Buses.Count >= 2);
            Add("profit_500", "Günde $500 net kâr", "Doluluğu yüksek, fiyatı ideal bölgede hatlar kurun.", 5000,
                s => LastNet(s) >= 500);
            Add("station", "Durağını güzelleştir", "Haritada hattınızın geçtiği bir durağa tıklayıp içine girin ve bir geliştirme yapın.", 5000,
                s => s.State.stations.Count > 0);
            Add("level_3", "Şirket seviyesi 3", "Yolcu taşıdıkça XP kazanırsınız.", 8000,
                s => s.Progression.Level >= 3);
            Add("routes_4", "4 hat işlet", "Seviye atladıkça hat limitiniz artar.", 10000,
                s => s.Routes.Routes.Count >= 4);
            Add("coach", "İlk şehirlerarası otobüs", "Seviye 5'te Toros 46 açılır; uzun hatlarda çok daha kârlıdır.", 20000,
                s => HasTier(s, 2));
            Add("rep_50", "İtibar 50", "Adil fiyat + konforlu araç = memnun yolcu.", 15000,
                s => s.Progression.Reputation >= 50f);
            Add("profit_5000", "Günde $5.000 net kâr", "Ana arterlere (İstanbul–Ankara, İstanbul–İzmir...) büyük otobüs koyun.", 25000,
                s => LastNet(s) >= 5000);
            Add("abroad", "Yurt dışına açıl", "Bölgeler (B) panelinden bir ülke lisansı alın.", 30000,
                s => s.State.unlockedRegionIds.Count >= 2);
            Add("vip", "Lüks segment: VIP otobüs", "Seviye 12 ve itibar 55 gerekir.", 50000,
                s => HasTier(s, 4));
            Add("millionaire", "İlk milyon: $1.000.000 kasa", "Yolun yarısı bile değil: asıl hedef 10 milyon!", 0,
                s => s.Finance.Cash >= 1_000_000);
            Add("profit_25000", "Günde $25.000 net kâr", "Büyük şehirleri VIP ve çift katlı otobüslerle bağlayın.", 100000,
                s => LastNet(s) >= 25000);
            Add("tycoon", "Otobüs İmparatoru: $10.000.000 kasa", "Büyük hedef! Ulaştığınızda oyunu kazanırsınız.", 0,
                s => s.Finance.Cash >= VictoryCash, isVictory: true);
        }

        public IReadOnlyList<Goal> Goals => goals;

        public Goal Current
        {
            get
            {
                foreach (var goal in goals)
                    if (!IsCompleted(goal)) return goal;
                return null;
            }
        }

        public int CompletedCount
        {
            get
            {
                int count = 0;
                foreach (var goal in goals)
                    if (IsCompleted(goal)) count++;
                return count;
            }
        }

        public bool IsCompleted(Goal goal) => session.State != null && session.State.completedGoalIds.Contains(goal.Id);

        public void ResetSessionFlags() => priceAdjusted = false;

        public void NotifyPriceAdjusted()
        {
            priceAdjusted = true;
            Evaluate();
        }

        public void Evaluate()
        {
            if (!session.IsRunning || session.IsBankrupt) return;
            foreach (var goal in goals)
            {
                if (IsCompleted(goal) || !goal.Condition(session)) continue;
                session.State.completedGoalIds.Add(goal.Id);
                if (goal.Reward > 0f) session.Finance.Earn(goal.Reward, LedgerCategory.Other, "goal:" + goal.Id);
                Completed?.Invoke(goal);
            }
        }

        void Add(string id, string title, string hint, float reward, Func<GameSession, bool> condition, bool isVictory = false) =>
            goals.Add(new Goal(id, title, hint, reward, condition, isVictory));

        static double LastNet(GameSession s)
        {
            var history = s.State.financeHistory;
            return history.Count == 0 ? 0 : history[history.Count - 1].Net;
        }

        static bool HasTier(GameSession s, int tier)
        {
            foreach (var bus in s.Fleet.Buses)
                if (bus.Model != null && bus.Model.Tier >= tier) return true;
            return false;
        }
    }
}
