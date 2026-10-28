using System;
using BusTycoon.Save;

namespace BusTycoon.Economy
{
    public enum LedgerCategory
    {
        TicketRevenue,
        BusSale,
        Fuel,
        DriverWages,
        Maintenance,
        Tolls,
        TerminalFees,
        PassengerService,
        Insurance,
        Repairs,
        BusPurchase,
        RouteLicense,
        RegionLicense,
        Other
    }

    [Serializable]
    public sealed class DailyFinanceSummary
    {
        public int day;
        public double income;
        public double expenses;
        public double cashAtClose;
        /// <summary>LedgerCategory sırasıyla kategori bazında net tutarlar (gelir +, gider −).</summary>
        public double[] byCategory;

        public double Net => income - expenses;
    }

    [Serializable]
    public struct TransactionRecord
    {
        public int day;
        public LedgerCategory category;
        public double amount;
        public string reference;
    }

    /// <summary>
    /// Kasa ve muhasebe. Günlük işletme kalemleri kategori bazında toplanır (hafif), büyük tek seferlik
    /// işlemler (otobüs alımı, lisans) ise ayrıca son işlemler listesine yazılır (UI akışı için).
    /// </summary>
    public sealed class FinanceSystem
    {
        public const int HistoryDays = 120;
        public const int RecentTransactionLimit = 50;
        static readonly int CategoryCount = Enum.GetValues(typeof(LedgerCategory)).Length;

        readonly CompanyState state;
        readonly double[] todayByCategory = new double[CategoryCount];
        double todayIncome;
        double todayExpenses;

        public event Action<double> CashChanged;
        public event Action<TransactionRecord> TransactionRecorded;

        public FinanceSystem(CompanyState state) => this.state = state;

        public double Cash => state.cash;
        public int DaysInDebt => state.daysInDebt;

        public bool CanAfford(double amount) => amount <= state.cash + 1e-6;

        /// <summary>İsteğe bağlı harcama: bakiye yetmezse yapılmaz.</summary>
        public bool TrySpend(double amount, LedgerCategory category, string reference = null)
        {
            if (amount < 0 || !CanAfford(amount)) return false;
            Record(-amount, category, reference, logTransaction: true);
            return true;
        }

        /// <summary>Zorunlu gider (yakıt, maaş, tamir): bakiye eksiye düşebilir.</summary>
        public void Charge(double amount, LedgerCategory category, string reference = null)
        {
            if (amount <= 0) return;
            Record(-amount, category, reference, logTransaction: category == LedgerCategory.Repairs);
        }

        public void Earn(double amount, LedgerCategory category, string reference = null)
        {
            if (amount <= 0) return;
            Record(amount, category, reference, logTransaction: category != LedgerCategory.TicketRevenue);
        }

        void Record(double signedAmount, LedgerCategory category, string reference, bool logTransaction)
        {
            state.cash += signedAmount;
            todayByCategory[(int)category] += signedAmount;
            if (signedAmount >= 0) todayIncome += signedAmount;
            else todayExpenses -= signedAmount;

            if (logTransaction)
            {
                var record = new TransactionRecord { day = state.currentDay, category = category, amount = signedAmount, reference = reference };
                state.recentTransactions.Add(record);
                if (state.recentTransactions.Count > RecentTransactionLimit) state.recentTransactions.RemoveAt(0);
                TransactionRecorded?.Invoke(record);
            }

            CashChanged?.Invoke(state.cash);
        }

        /// <summary>Gün sonu: günlük toplamları geçmişe yazar ve borç sayacını günceller.</summary>
        public DailyFinanceSummary CloseDay(int day)
        {
            var summary = new DailyFinanceSummary
            {
                day = day,
                income = todayIncome,
                expenses = todayExpenses,
                cashAtClose = state.cash,
                byCategory = (double[])todayByCategory.Clone()
            };

            state.financeHistory.Add(summary);
            if (state.financeHistory.Count > HistoryDays) state.financeHistory.RemoveAt(0);

            state.daysInDebt = state.cash < 0 ? state.daysInDebt + 1 : 0;

            Array.Clear(todayByCategory, 0, todayByCategory.Length);
            todayIncome = 0;
            todayExpenses = 0;
            return summary;
        }

        /// <summary>Son N günün net kârı (UI'daki "son 7 gün" göstergesi için).</summary>
        public double GetNetProfit(int lastDays)
        {
            double total = 0;
            var history = state.financeHistory;
            for (int i = Math.Max(0, history.Count - lastDays); i < history.Count; i++) total += history[i].Net;
            return total;
        }

        public static bool IsIncome(LedgerCategory category) =>
            category == LedgerCategory.TicketRevenue || category == LedgerCategory.BusSale;
    }
}
