using System;
using BusTycoon.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BusTycoon.Presentation.Map
{
    public enum CityNodeState { Normal, Origin, Destination, Connected, Locked }

    /// <summary>
    /// Haritadaki şehir işaretçisi. Tıklama/hover, EventSystem + kameradaki PhysicsRaycaster ile gelir;
    /// böylece hem eski Input Manager hem yeni Input System ile çalışır.
    /// Prefab'da bir Collider (ör. SphereCollider) bulunmalıdır; aksi halde tıklama algılanmaz.
    /// </summary>
    public sealed class CityNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
        static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in

        [SerializeField] Renderer markerRenderer;
        [SerializeField] TMP_Text label;
        [Tooltip("MapTier'e (0 = metropol .. 3 = küçük) göre işaretçi ölçeği.")]
        [SerializeField] float[] scaleByTier = { 1.6f, 1.25f, 1f, 0.75f };

        [Header("Renkler")]
        [SerializeField] Color normalColor = new Color(0.85f, 0.85f, 0.9f);
        [SerializeField] Color hoverColor = Color.white;
        [SerializeField] Color originColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] Color destinationColor = new Color(1f, 0.6f, 0.15f);
        [SerializeField] Color connectedColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color lockedColor = new Color(0.45f, 0.45f, 0.45f);

        MaterialPropertyBlock propertyBlock;
        CityNodeState state;
        bool hovered;

        public CityData City { get; private set; }
        public CityNodeState State => state;

        public event Action<CityNodeView> Clicked;
        public event Action<CityNodeView> HoverEntered;
        public event Action<CityNodeView> HoverExited;

        public void Bind(CityData city, Vector3 position, Vector3 surfaceUp)
        {
            City = city;
            name = $"City_{city.Id}";
            transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, surfaceUp));

            int tier = Mathf.Clamp(city.MapTier, 0, scaleByTier.Length - 1);
            transform.localScale = Vector3.one * scaleByTier[tier];

            if (label != null)
            {
                label.text = city.DisplayName;
                label.fontStyle = city.IsCapital ? FontStyles.Bold : FontStyles.Normal;
            }
            ApplyVisual();
        }

        public void SetState(CityNodeState newState)
        {
            if (state == newState) return;
            state = newState;
            ApplyVisual();
        }

        public void SetLabelVisible(bool visible)
        {
            if (label != null) label.gameObject.SetActive(visible);
        }

        void ApplyVisual()
        {
            if (markerRenderer == null) return;
            propertyBlock ??= new MaterialPropertyBlock();

            Color color = state switch
            {
                CityNodeState.Origin => originColor,
                CityNodeState.Destination => destinationColor,
                CityNodeState.Connected => connectedColor,
                CityNodeState.Locked => lockedColor,
                _ => normalColor
            };
            if (hovered && state != CityNodeState.Locked) color = Color.Lerp(color, hoverColor, 0.5f);

            markerRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            markerRenderer.SetPropertyBlock(propertyBlock);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            ApplyVisual();
            HoverEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            ApplyVisual();
            HoverExited?.Invoke(this);
        }
    }
}
