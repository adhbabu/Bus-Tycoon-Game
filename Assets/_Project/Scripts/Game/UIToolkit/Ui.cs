using System;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    public enum UiWeight { Regular, Bold, Black }

    /// <summary>
    /// Yeni arayüz için küçük kurucu yardımcılar: öğeyi sınıflarıyla oluşturup ebeveyne ekler. Yazı tipi kalınlığı
    /// C#'tan verilir (Inter Medium / Bold / ExtraBold; Resources/BusTycoon/Fonts). Değer yazan yardımcılar yalnız
    /// değişince yazar: yeniden çizim ve bellek ayırma olmasın.
    /// </summary>
    public static class Ui
    {
        static Font regular, bold, black;

        static Font FontFor(UiWeight weight)
        {
            switch (weight)
            {
                case UiWeight.Bold: return bold != null ? bold : bold = Resources.Load<Font>(DefaultContent.HeavyFontResourcePath);
                case UiWeight.Black: return black != null ? black : black = Resources.Load<Font>(DefaultContent.DisplayFontResourcePath);
                default: return regular != null ? regular : regular = Resources.Load<Font>(DefaultContent.RegularFontResourcePath);
            }
        }

        public static void Weight(VisualElement element, UiWeight weight)
        {
            Font font = FontFor(weight);
            if (font != null) element.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        public static void AddClasses(VisualElement element, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (string name in classes.Split(' '))
                if (name.Length > 0) element.AddToClassList(name);
        }

        public static VisualElement El(string classes, VisualElement parent = null)
        {
            var element = new VisualElement();
            AddClasses(element, classes);
            parent?.Add(element);
            return element;
        }

        public static Label Text(string classes, string text, UiWeight weight = UiWeight.Regular, VisualElement parent = null)
        {
            var label = new Label(text);
            AddClasses(label, classes);
            Weight(label, weight);
            label.pickingMode = PickingMode.Ignore;
            parent?.Add(label);
            return label;
        }

        /// <summary>Düğme: odak almaz (klavye kısayolları haritada kalsın), tıklanınca kısa ses çalar.</summary>
        public static Button Button(string classes, string text, Action onClick, VisualElement parent = null, UiWeight weight = UiWeight.Bold)
        {
            var button = new Button(() =>
            {
                GameAudio.Play(Sfx.Click);
                onClick?.Invoke();
            })
            {
                text = text ?? string.Empty,
                focusable = false
            };
            AddClasses(button, classes);
            Weight(button, weight);
            parent?.Add(button);
            return button;
        }

        public static VisualElement Icon(UIIcon icon, string classes, VisualElement parent = null)
        {
            var element = El(string.IsNullOrEmpty(classes) ? "bt-icon" : classes, parent);
            element.pickingMode = PickingMode.Ignore;
            SetIcon(element, icon);
            return element;
        }

        public static void SetIcon(VisualElement element, UIIcon icon)
        {
            Sprite sprite = UiSprites.Get(icon);
            if (sprite != null) element.style.backgroundImage = new StyleBackground(sprite);
        }

        public static void SetText(TextElement element, string text)
        {
            if (element.text != text) element.text = text;
        }

        public static void Show(VisualElement element, bool show)
        {
            DisplayStyle display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display != display) element.style.display = display;
        }

        public static void Toggle(VisualElement element, string className, bool on)
        {
            if (element.ClassListContains(className) != on) element.EnableInClassList(className, on);
        }

        /// <summary>Konum ve boyut (tasarım birimi); yalnız değişince yazılır.</summary>
        public static void Place(VisualElement element, float left, float top, float width, float height)
        {
            IStyle style = element.style;
            if (Differs(style.left, left)) style.left = left;
            if (Differs(style.top, top)) style.top = top;
            if (Differs(style.width, width)) style.width = width;
            if (Differs(style.height, height)) style.height = height;
        }

        /// <summary>Yalnız konum (tasarım birimi; boyut içerikten gelir); yalnız değişince yazılır.</summary>
        public static void Move(VisualElement element, float left, float top)
        {
            IStyle style = element.style;
            if (Differs(style.left, left)) style.left = left;
            if (Differs(style.top, top)) style.top = top;
        }

        static bool Differs(StyleLength current, float value) =>
            current.keyword != StyleKeyword.Undefined || Mathf.Abs(current.value.value - value) > 0.05f;

        /// <summary>Yatay doluluk (0..1): genişlik yerine ölçekle; yerleşim yeniden hesaplanmaz.</summary>
        public static void Fill(VisualElement element, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            Vector3 current = element.style.scale.value.value;
            if (element.style.scale.keyword == StyleKeyword.Undefined && Mathf.Abs(current.x - fraction) < 0.001f) return;
            element.style.scale = new Scale(new Vector3(fraction, 1f, 1f));
        }
    }
}
