using BusTycoon.Core;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// "Sadece Play'e bas" girişi: sahnede bir <see cref="GameSession"/> yoksa oyunun tamamını kodla kurar —
    /// içerik (93 şehir, 8 otobüs, ayarlar), kamera, ışık, 3D harita ve arayüz. Prefab, sahne veya asset
    /// bağlamak gerekmez; boş bir sahnede (veya Unity'nin varsayılan SampleScene'inde) çalışır.
    /// Kendi sahnenizi GameSession ile kurduysanız bu sınıf devre dışı kalır.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindFirstObjectByType<GameSession>() != null) return;

            var csv = Resources.Load<TextAsset>(DefaultContent.CitiesResourcePath);
            if (csv == null)
            {
                Debug.LogError($"[Bus Tycoon] Resources/{DefaultContent.CitiesResourcePath}.csv bulunamadı.");
                return;
            }

            ConfigurePlatform();
            GameUI.ProfileOverride = GameUI.ParseProfile(LaunchOptions.Get("profil") ?? LaunchOptions.Get("profile"));
            GameContent content = DefaultContent.Build(csv.text);
            var mapTexture = Resources.Load<Texture2D>(DefaultContent.MapTextureResourcePath);

            var root = new GameObject("Bus Tycoon");

            // Oturum: GameObject pasifken yapılandır ki Awake asset'leri hazır bulsun.
            var sessionObject = new GameObject("GameSession");
            sessionObject.transform.SetParent(root.transform, false);
            sessionObject.SetActive(false);
            var session = sessionObject.AddComponent<GameSession>();
            session.Configure(content.World, content.Economy, content.Progression, content.Catalog);
            sessionObject.SetActive(true);

            var world = new GameObject("World").AddComponent<WorldRenderer>();
            world.transform.SetParent(root.transform, false);
            world.Init(session, content.Projection, mapTexture);

            Camera camera = SetupCamera(world.SeaColor);
            SetupLighting();

            var mapCamera = camera.gameObject.AddComponent<MapCamera>();
            mapCamera.Init(camera, world.MapBoundsXZ);

            var ui = new GameObject("GameUI").AddComponent<GameUI>();
            ui.transform.SetParent(root.transform, false);
            ui.Init(session, world, mapCamera);

            if (LaunchOptions.Has("vitrin") || LaunchOptions.Has("showcase"))
                new GameObject(ShowcaseDirector.ObjectName).AddComponent<ShowcaseDirector>().Init(session, ui);
        }

        /// <summary>Mobil: yatay ekran, 60 FPS, ekran kararmasın (simülasyon izlenirken). Tarayıcı kendi kare hızını kullanır.</summary>
        static void ConfigurePlatform()
        {
            bool web = Application.platform == RuntimePlatform.WebGLPlayer;
            if (!web) Application.targetFrameRate = 60;
            if (!Application.isMobilePlatform || web) return;

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        static Camera SetupCamera(Color background)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            return camera;
        }

        static void SetupLighting()
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                sun = light;
                break;
            }

            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = Quaternion.Euler(62f, -35f, 0f);
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = Application.isMobilePlatform ? LightShadows.Hard : LightShadows.Soft;
            sun.shadowStrength = 0.6f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.fog = false;
        }
    }
}
