using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Arayüz girdisinin büyük kısmı IMGUI olaylarından gelir (tek parmak dokunuş = fare). IMGUI'nin vermediği
    /// iki şey burada okunur: iki parmak (pinch) konumları ve Android geri tuşu. Projede eski Input Manager
    /// veya yeni Input System hangisi etkinse o kullanılır; ikisi de yoksa özellik sessizce devre dışı kalır.
    /// </summary>
    public static class TouchInput
    {
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
