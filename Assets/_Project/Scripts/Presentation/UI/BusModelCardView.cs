using System;
using BusTycoon.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>Galerideki tek otobüs kartı. Kilitliyse gri + "neden kilitli" metni gösterir (hedef verir).</summary>
    public sealed class BusModelCardView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text segmentLabel;
        [SerializeField] TMP_Text statsLabel;
        [SerializeField] TMP_Text priceLabel;
        [SerializeField] TMP_Text lockReasonLabel;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] GameObject newBadge;
        [SerializeField] Button buyButton;

        BusModelData model;

        public event Action<BusModelData> BuyClicked;

        void Awake() => buyButton.onClick.AddListener(() => BuyClicked?.Invoke(model));

        public void Bind(BusModelData busModel, float price, bool unlocked, string lockReason, bool canAfford, bool isNew)
        {
            model = busModel;
            if (icon != null) icon.sprite = busModel.Icon;

            nameLabel.text = busModel.DisplayName;
            segmentLabel.text = SegmentName(busModel.Segment);
            statsLabel.text =
                $"{busModel.Seats} koltuk · Konfor {UIFormat.Stars(busModel.Comfort)}\n" +
                $"{busModel.AverageSpeedKmh:0} km/s · {busModel.FuelLitersPer100Km:0} L/100 km\n" +
                $"Rahat menzil {UIFormat.Number(busModel.ComfortableRangeKm)} km · Bilet çarpanı ×{busModel.FareMultiplier:0.00}";
            priceLabel.text = UIFormat.Money(price);

            lockedOverlay.SetActive(!unlocked);
            lockReasonLabel.gameObject.SetActive(!unlocked);
            lockReasonLabel.text = lockReason;
            if (newBadge != null) newBadge.SetActive(isNew);

            buyButton.interactable = unlocked && canAfford;
        }

        public static string SegmentName(BusSegment segment) => segment switch
        {
            BusSegment.Minibus => "Minibüs",
            BusSegment.Midibus => "Midibüs",
            BusSegment.StandardCoach => "Şehirlerarası Standart",
            BusSegment.ComfortCoach => "Konfor",
            BusSegment.LuxuryCoach => "Lüks 2+1 VIP",
            BusSegment.DoubleDecker => "Çift Katlı",
            _ => segment.ToString()
        };
    }
}
