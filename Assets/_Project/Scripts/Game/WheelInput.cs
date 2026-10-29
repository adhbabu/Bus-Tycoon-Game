using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Tekerlek olayının kaynağını ayırt eder: fare tekerleği yakınlaştırır, dizüstü touchpad'inde iki parmakla
    /// kaydırma haritayı kaydırır, iki parmakla sıkıştırma yakınlaştırır.
    /// Web'de sınıflandırma tarayıcı olayından yapılır (Assets/Plugins/WebGL/BusTycoonInput.jslib); diğer
    /// platformlarda IMGUI olayından tahmin edilir (yatay ya da kesirli kayma = touchpad, Ctrl = sıkıştırma).
    /// </summary>
    public static class WheelInput
    {
        public enum Kind { None, Mouse, Touchpad, Pinch }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BusTycoon_InitWheel();
        [DllImport("__Internal")] static extern int BusTycoon_WheelKind();
        [DllImport("__Internal")] static extern float BusTycoon_TakeWheelDeltaX();
        [DllImport("__Internal")] static extern float BusTycoon_TakeWheelDeltaY();
        static bool initialized;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>Tekerlek türünü tarayıcı söyler; Unity'nin kendi tekerlek olayı yalnız fare tekerleği için kullanılır.</summary>
        public static bool BrowserClassifies => true;
#else
        public static bool BrowserClassifies => false;
#endif

        /// <summary>Tarayıcının bildirdiği son tekerlek türü (kareler arasında korunur).</summary>
        public static Kind LastWebKind { get; private set; }

        /// <summary>
        /// Her kare başında çağrılır. Web'de son tekerlek olaylarının türünü ve toplam kaymasını (ekran pikseli,
        /// aşağı/sağa pozitif) verir; başka platformlarda ya da olay yoksa false.
        /// </summary>
        public static bool TakeWebGesture(out Kind kind, out Vector2 pixels)
        {
            kind = Kind.None;
            pixels = Vector2.zero;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!initialized)
            {
                BusTycoon_InitWheel();
                initialized = true;
            }
            pixels = new Vector2(BusTycoon_TakeWheelDeltaX(), BusTycoon_TakeWheelDeltaY());
            kind = (Kind)Mathf.Clamp(BusTycoon_WheelKind(), 0, 3);
#endif
            LastWebKind = kind;
            return pixels != Vector2.zero && (kind == Kind.Touchpad || kind == Kind.Pinch);
        }

        /// <summary>Web dışı platformlar için IMGUI tekerlek olayından tahmin; touchpad kayması ekran pikseline çevrilir.</summary>
        public static Kind Classify(Event e, out Vector2 pixels)
        {
            pixels = e.delta * 8f;
            if (e.control || e.command) return Kind.Pinch;
            bool fractional = Mathf.Abs(e.delta.y - Mathf.Round(e.delta.y)) > 0.001f;
            return Mathf.Abs(e.delta.x) > 0.001f || fractional ? Kind.Touchpad : Kind.Mouse;
        }
    }
}
