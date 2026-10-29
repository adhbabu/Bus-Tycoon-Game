using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Arayüz girdisinin büyük kısmı IMGUI olaylarından gelir (tek parmak dokunuş = fare). IMGUI'nin vermediği
    /// iki şey burada okunur: iki parmak (pinch) konumları ve Android geri tuşu. Projede eski Input Manager
    /// veya yeni Input System hangisi etkinse o kullanılır; ikisi de yoksa özellik sessizce devre dışı kalır.
    /// </summary>
    public static class TouchInput
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int BusTycoon_IsTouchPrimary();
        [DllImport("__Internal")] static extern void BusTycoon_InitPointer();
        [DllImport("__Internal")] static extern int BusTycoon_LastPointerTouch();
        static int touchPrimary = -1;
        static bool pointerInitialized;
#endif

        /// <summary>
        /// Tarayıcıda birincil girdi dokunmatik mi (telefon, tablet)? iPadOS Safari kendini masaüstü Mac olarak
        /// tanıttığı için <see cref="Application.isMobilePlatform"/> iPad'de false döner; bu ona bakar.
        /// </summary>
        public static bool IsTouchPrimaryBrowser
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (touchPrimary < 0) touchPrimary = BusTycoon_IsTouchPrimary();
                return touchPrimary == 1;
#else
                return false;
#endif
            }
        }

        /// <summary>Son basış parmakla mı yapıldı (dokunmatik ekranlı bilgisayarda paneller parmakla kaysın)?</summary>
        public static bool LastPressWasTouch
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (!pointerInitialized)
                {
                    BusTycoon_InitPointer();
                    pointerInitialized = true;
                }
                return BusTycoon_LastPointerTouch() == 1;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.touchCount > 0;
#else
                return false;
#endif
            }
        }

        /// <summary>Basılı parmak sayısı; en az iki parmak varsa ilk ikisinin konumu ve bu karedeki hareketi.</summary>
        public static int Read(out Vector2 position0, out Vector2 position1, out Vector2 delta0, out Vector2 delta1)
        {
            position0 = position1 = delta0 = delta1 = Vector2.zero;
#if ENABLE_LEGACY_INPUT_MANAGER
            int count = Input.touchCount;
            if (count >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                position0 = a.position;
                position1 = b.position;
                delta0 = a.deltaPosition;
                delta1 = b.deltaPosition;
            }
            return count;
#elif BUSTYCOON_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen == null) return 0;
            int count = 0;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (count == 0)
                {
                    position0 = touch.position.ReadValue();
                    delta0 = touch.delta.ReadValue();
                }
                else if (count == 1)
                {
                    position1 = touch.position.ReadValue();
                    delta1 = touch.delta.ReadValue();
                }
                count++;
            }
            return count;
#else
            return 0;
#endif
        }

        /// <summary>Esc veya Android geri tuşu bu karede basıldı mı?</summary>
        public static bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif BUSTYCOON_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
