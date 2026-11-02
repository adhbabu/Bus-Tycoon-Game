using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde yazı girişi ve tarayıcı dokunuşları. Bilgisayarda UI Toolkit yazı alanı (TextField) kullanılır.
    /// Telefon ve tablette Unity klavye açamaz: alan bir dokunma kutusudur, dokununca tarayıcının yazı penceresi açılır.
    /// Davet bağlantısını paylaşma da tarayıcıda dokunuşun içinde yapılmalıdır (WebTap).
    /// IMGUI her Repaint'te dokunma alanlarını bildirir. Yeni arayüzün alanları da aynı karede, öğenin ekrandaki
    /// yerinden bildirilir. Sonuçlar her karede alınır.
    /// </summary>
    public sealed partial class GameUI
    {
        sealed class TapZone
        {
            public VisualElement Element;
            public int Kind;
            public string A, B;
            public Action<string> Result;
        }

        readonly Dictionary<string, TapZone> tkTapZones = new Dictionary<string, TapZone>();

        /// <summary>
        /// Yazı alanı. <paramref name="changed"/> her değişiklikte çağrılır.
        /// Dokunmatik web'de dokunma kutusu çizilir; yazılan metin tarayıcı penceresinden gelir.
        /// </summary>
        VisualElement TkTextInput(VisualElement parent, string id, string value, int maxLength, string title, Action<string> changed)
        {
            value ??= string.Empty;
            if (mobile && WebTap.Available)
            {
                VisualElement box = Ui.El("bt-field bt-field--tap", parent);
                Label text = Ui.Text("bt-field__text", string.Empty, UiWeight.Bold, box);
                var zone = new TapZone { Element = box, Kind = WebTap.PromptKind, A = title };
                void Show(string current)
                {
                    zone.B = current;
                    text.text = current.Length > 0 ? current : "Yazmak için dokun";
                    Ui.Toggle(text, "is-placeholder", current.Length == 0);
                }
                Show(value);
                zone.Result = typed =>
                {
                    typed = (typed ?? string.Empty).Trim();
                    if (typed.Length > maxLength) typed = typed.Substring(0, maxLength);
                    Show(typed);
                    changed(typed);
                };
                tkTapZones[id] = zone;
                return box;
            }

            var field = new TextField { maxLength = maxLength, value = value };
            field.AddToClassList("bt-field");
            Ui.Weight(field, UiWeight.Bold);
            field.RegisterValueChangedCallback(evt => changed(evt.newValue ?? string.Empty));
            parent.Add(field);
            return field;
        }

        /// <summary>Düğmeyi tarayıcı paylaşım alanı yap (web'de dokunuş tarayıcıya gider, sonucu <paramref name="result"/> alır).</summary>
        void TkShareZone(Button button, string id, string link, string message, Action<string> result)
        {
            tkTapZones[id] = new TapZone { Element = button, Kind = WebTap.ShareKind, A = link, B = message, Result = result };
        }

        /// <summary>Her kare: tarayıcıdan gelen sonuçlar.</summary>
        void PumpToolkitTaps()
        {
            if (tkTapZones.Count == 0) return;
            foreach (var pair in tkTapZones)
            {
                if (pair.Value.Element.panel == null || !WebTap.TryTake(pair.Key, out string value)) continue;
                pair.Value.Result?.Invoke(value);
                break; // sonuç arayüzü yeniden kurabilir; kalanlar sonraki karede
            }
        }

        /// <summary>IMGUI Repaint'inde: ekranda görünen dokunma alanlarını tarayıcıya bildir.</summary>
        void ReportToolkitTapZones()
        {
            if (toolkit == null || tkTapZones.Count == 0 || !WebTap.Available) return;
            List<string> stale = null;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            foreach (var pair in tkTapZones)
            {
                VisualElement element = pair.Value.Element;
                if (element.panel == null)
                {
                    (stale ??= new List<string>()).Add(pair.Key);
                    continue;
                }
                if (!OnScreen(element)) continue;
                Rect bounds = element.worldBound;
                WebTap.Zone(pair.Key, pair.Value.Kind, new Rect(bounds.x * u, bounds.y * u, bounds.width * u, bounds.height * u),
                    pair.Value.A, pair.Value.B);
            }
            GUI.matrix = previous;
            if (stale != null) foreach (string id in stale) tkTapZones.Remove(id);
        }

        /// <summary>Öğe ve üstleri görünür ve etkin mi (gizli ya da pencerenin arkasında kalan alan dokunuş almaz).</summary>
        static bool OnScreen(VisualElement element)
        {
            if (!element.enabledInHierarchy) return false;
            for (VisualElement e = element; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden) return false;
            Rect bounds = element.worldBound;
            return bounds.width > 1f && bounds.height > 1f && !float.IsNaN(bounds.x);
        }
    }
}
