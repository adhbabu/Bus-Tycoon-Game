using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Tarayıcıda ekranın fiziksel ölçüleri (Assets/Plugins/WebGL/BusTycoonInput.jslib): tuval pikseli başına CSS
    /// pikseli (iOS'ta nokta) ve Safari'nin güvenli alan payları (çentik/ada, ana ekran çubuğu). Unity WebGL'de
    /// <see cref="Screen.safeArea"/> tüm ekranı verir; arayüz bu payları ayrıca uygular. Kare başına bir kez okunur.
    /// Web dışında ölçek 0 (bilinmiyor), paylar 0'dır.
    /// </summary>
    public static class WebScreen
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern float BusTycoon_CanvasScale();
        [DllImport("__Internal")] static extern float BusTycoon_SafeInset(int side);
#endif

        static int frame = -1;
        static float scale, left, right, top, bottom;

        /// <summary>Testler için: tarayıcı ölçeği ve payları (piksel) elle verilir.</summary>
        public static void Simulate(float pixelsPerCssPixel, float insetLeft, float insetRight, float insetTop, float insetBottom)
        {
            frame = int.MaxValue;
            scale = pixelsPerCssPixel;
            left = insetLeft;
            right = insetRight;
            top = insetTop;
            bottom = insetBottom;
        }

        /// <summary>Tuval pikseli / CSS pikseli; bilinmiyorsa 0.</summary>
        public static float PixelsPerCssPixel
        {
            get
            {
                Refresh();
                return scale;
            }
        }

        public static float InsetLeft { get { Refresh(); return left; } }
        public static float InsetRight { get { Refresh(); return right; } }
        public static float InsetTop { get { Refresh(); return top; } }
        public static float InsetBottom { get { Refresh(); return bottom; } }

        static void Refresh()
        {
            if (frame == Time.frameCount || frame == int.MaxValue) return;
            frame = Time.frameCount;
#if UNITY_WEBGL && !UNITY_EDITOR
            scale = BusTycoon_CanvasScale();
            left = BusTycoon_SafeInset(0);
            right = BusTycoon_SafeInset(1);
            top = BusTycoon_SafeInset(2);
            bottom = BusTycoon_SafeInset(3);
#endif
        }
    }
}
