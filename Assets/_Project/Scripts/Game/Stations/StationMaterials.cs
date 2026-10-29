using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Durak modellerinin katman malzemeleri (<see cref="MeshLayer"/>). Pencereler ve ışıklı parçalar ışıksızdır
    /// (Sprites/Default, her derlemede var): gündüz camda gökyüzü, gece sıcak ışık görünür (<see cref="SetNight"/>).
    /// </summary>
    public static class StationMaterials
    {
        static readonly Color WindowDay = new Color(0.6f, 0.73f, 0.86f);
        static readonly Color WindowNight = new Color(1f, 0.84f, 0.5f);
        static Material window, glow, glass;
        static float night = -1f;

        public static Material Window => window != null ? window : window = Create("StationWindow", WindowDay, 2990);
        public static Material Glow => glow != null ? glow : glow = Create("StationGlow", new Color(0.82f, 0.82f, 0.82f), 2991);
        public static Material Glass => glass != null ? glass : glass = Create("StationGlass", new Color(0.78f, 0.9f, 1f, 0.32f), 3005);

        static Material Create(string name, Color color, int queue) =>
            new Material(MaterialFactory.LineMaterial) { name = name, color = color, renderQueue = queue };

        /// <summary>Mesh'in alt mesh sırasına göre malzeme dizisi.</summary>
        public static Material[] For(MeshLayer[] layers, Material accent)
        {
            var materials = new Material[Mathf.Max(1, layers.Length)];
            materials[0] = StationPalette.Material;
            for (int i = 0; i < layers.Length; i++)
            {
                switch (layers[i])
                {
                    case MeshLayer.Lit: materials[i] = StationPalette.Material; break;
                    case MeshLayer.Window: materials[i] = Window; break;
                    case MeshLayer.Glow: materials[i] = Glow; break;
                    case MeshLayer.Glass: materials[i] = Glass; break;
                    case MeshLayer.Accent: materials[i] = accent != null ? accent : StationPalette.Material; break;
                }
            }
            return materials;
        }

        /// <summary>0 gündüz … 1 gece: pencereler ve lambalar yanar.</summary>
        public static void SetNight(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - night) < 0.004f) return;
            night = value;
            float lit = Mathf.Clamp01((value - 0.1f) / 0.5f);
            Window.color = Color.Lerp(WindowDay, WindowNight, lit);
            Glow.color = Color.Lerp(new Color(0.82f, 0.82f, 0.82f), Color.white, lit);
        }
    }
}
