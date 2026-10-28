using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>Oyundaki tüm otobüs modellerinin listesi (galeri / bayi içeriği).</summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/Fleet/Bus Catalog", fileName = "BusCatalog")]
    public sealed class BusCatalog : ScriptableObject
    {
        [SerializeField] List<BusModelData> models = new List<BusModelData>();

        [NonSerialized] Dictionary<string, BusModelData> lookup;
        [NonSerialized] List<BusModelData> sorted;

        public IReadOnlyList<BusModelData> Models => models;

        /// <summary>Kademe (segment), sonra fiyata göre sıralı liste — galeri ekranının sırası.</summary>
        public IReadOnlyList<BusModelData> SortedByTier
        {
            get
            {
                if (sorted == null)
                {
                    sorted = new List<BusModelData>(models.Count);
                    foreach (var model in models) if (model != null) sorted.Add(model);
                    sorted.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.Price.CompareTo(b.Price));
                }
                return sorted;
            }
        }

        public BusModelData GetById(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, BusModelData>(StringComparer.Ordinal);
                foreach (var model in models)
                    if (model != null && !string.IsNullOrEmpty(model.Id)) lookup[model.Id] = model;
            }
            return lookup.TryGetValue(modelId, out var result) ? result : null;
        }

        /// <summary>
        /// Bir sonraki kademedeki en ucuz model. UI'da "Yükselt" önerisi ve
        /// progression ipucu ("Sonraki hedef: Toros 46 – Seviye 5") için kullanılır.
        /// </summary>
        public BusModelData GetNextTierModel(BusModelData current)
        {
            int currentTier = current != null ? current.Tier : -1;
            BusModelData best = null;
            foreach (var model in SortedByTier)
            {
                if (model.Tier <= currentTier) continue;
                if (best == null || model.Tier < best.Tier) best = model;
            }
            return best;
        }

        internal void SetModels(IEnumerable<BusModelData> values)
        {
            models = new List<BusModelData>(values);
            InvalidateCaches();
        }

        void InvalidateCaches()
        {
            lookup = null;
            sorted = null;
        }

        void OnEnable() => InvalidateCaches();
        void OnValidate() => InvalidateCaches();
    }
}
