using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzün (UI Toolkit) kökü: belge ve panel ayarları kodla kurulur (sahne ya da asset gerektirmez). Tema
    /// (Resources/BusTycoon/UI/Theme.tss) ve stiller (App.uss) Resources'tan yüklenir. Arayüz kalıcı bir öğe ağacıdır:
    /// her kare yeniden kurulmaz, yalnız değişen değerler yazılır (IMGUI'de her olayda bütün arayüz baştan kuruluyordu).
    /// <para>Ölçek IMGUI ile aynı birimdir (<c>u</c>): masaüstünde ekran yüksekliği 1000 birim. Böylece geçiş sürerken
    /// yeni parçalar eskilerle aynı yeri kaplar.</para>
    /// <para>Varsayılan arayüz budur. Eski arayüz (IMGUI) bir süre daha <c>?arayuz=eski</c> ile açılabilir.</para>
    /// </summary>
    public sealed class UiHost : MonoBehaviour
    {
        public const string ThemeResourcePath = "BusTycoon/UI/Theme";
        public const string StylesResourcePath = "BusTycoon/UI/App";

        UIDocument document;
        PanelSettings settings;
        float appliedScale = -1f;

        /// <summary>Yeni arayüz açılsın mı: varsayılan evet; <c>?arayuz=eski</c> (ya da <c>-arayuz eski</c>) eski arayüzü açar.</summary>
        public static bool Requested
        {
            get
            {
                string value = (LaunchOptions.Get("arayuz") ?? LaunchOptions.Get("ui") ?? string.Empty).Trim().ToLowerInvariant();
                return !(value == "eski" || value == "old" || value == "0");
            }
        }

        /// <summary>Bütün ekranı kaplayan kök; kendisi tıklanmaz (haritaya geçer), yalnız içindeki kartlar tıklanır.</summary>
        public VisualElement Root { get; private set; }

        public static UiHost Create(Transform parent)
        {
            var go = new GameObject("UiHost");
            go.transform.SetParent(parent, false);
            go.SetActive(false); // belge, panel ayarları verilmeden etkinleşmesin
            var host = go.AddComponent<UiHost>();
            host.Build();
            go.SetActive(true);
            host.Root = host.document.rootVisualElement;
            host.Root.pickingMode = PickingMode.Ignore;
            host.Root.AddToClassList("bt-root");
            UiDiagnostics.Install(host.Root);
            var styles = Resources.Load<StyleSheet>(StylesResourcePath);
            if (styles != null) host.Root.styleSheets.Add(styles);
            else Debug.LogWarning($"[Bus Tycoon] Arayüz stilleri bulunamadı: Resources/{StylesResourcePath}.uss");
            return host;
        }

        void Build()
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "BusTycoonPanel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (settings.themeStyleSheet == null) Debug.LogWarning($"[Bus Tycoon] Arayüz teması bulunamadı: Resources/{ThemeResourcePath}.tss");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.sortingOrder = 10;
            // Deney (?uix=vb4096 vb.): köşe tamponu sayfa boyu
            string experiments = LaunchOptions.Get("uix") ?? string.Empty;
            foreach (string item in experiments.Split(','))
                if (item.StartsWith("vb") && int.TryParse(item.Substring(2), out int budget)) settings.vertexBudget = budget;
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
        }

        /// <summary>Tasarım biriminin piksel karşılığı (IMGUI'nin <c>u</c>'su); değişince panel ölçeği güncellenir.</summary>
        public void SetScale(float unit)
        {
            if (settings == null || Mathf.Abs(unit - appliedScale) < 0.001f) return;
            appliedScale = unit;
            settings.scale = unit;
        }

        /// <summary>Kök sınıfları (ör. "phone", "compact"): stiller cihaza göre değişir.</summary>
        public void SetClass(string className, bool on)
        {
            if (Root != null && Root.ClassListContains(className) != on) Root.EnableInClassList(className, on);
        }

        /// <summary>Yeni arayüzde bir yazı alanı odakta mı (klavye haritayı kaydırmasın).</summary>
        public bool TextInputFocused
        {
            get
            {
                // Odak yazı alanının içindeki giriş öğesinde olabilir: üst öğelere bakılır
                for (var element = Root?.panel?.focusController?.focusedElement as VisualElement; element != null; element = element.parent)
                    if (element is TextField) return true;
                return false;
            }
        }

        /// <summary>IMGUI koordinatındaki nokta (sol üst köşe başlangıç) yeni arayüzün tıklanabilir bir öğesinin üstünde mi.</summary>
        public bool IsOverUi(Vector2 guiPoint)
        {
            IPanel panel = Root?.panel;
            if (panel == null) return false;
            Vector2 screen = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
            Vector2 local = RuntimePanelUtils.ScreenToPanel(panel, screen);
            VisualElement picked = panel.Pick(local);
            return picked != null && picked != Root;
        }

        void OnDestroy()
        {
            if (settings != null) Destroy(settings);
            UiSprites.Clear();
        }
    }

    /// <summary>İkon atlasındaki (UIIcons) ikonların UI Toolkit için Sprite'ları (bir kez oluşturulur).</summary>
    public static class UiSprites
    {
        const int Columns = 8;
        const float Cell = 80f, Icon = 72f;
        static readonly Dictionary<UIIcon, Sprite> sprites = new Dictionary<UIIcon, Sprite>();

        public static Sprite Get(UIIcon icon)
        {
            if (sprites.TryGetValue(icon, out Sprite sprite) && sprite != null) return sprite;
            Texture2D atlas = UIIcons.Atlas;
            if (atlas == null) return null;
            int index = (int)icon;
            float pad = (Cell - Icon) / 2f;
            float x = (index % Columns) * Cell + pad;
            float yFromTop = (index / Columns) * Cell + pad;
            var rect = new Rect(x, atlas.height - yFromTop - Icon, Icon, Icon);
            sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = icon.ToString();
            sprites[icon] = sprite;
            return sprite;
        }

        public static void Clear()
        {
            foreach (var sprite in sprites.Values) if (sprite != null) Object.Destroy(sprite);
            sprites.Clear();
        }
    }
}
