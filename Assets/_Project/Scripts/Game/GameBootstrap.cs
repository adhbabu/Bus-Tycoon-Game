using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// "Sadece Play'e bas" girişi: sahnede bir <see cref="GameSession"/> yoksa oyunun tamamını kodla kurar —
    /// içerik (221 şehir — Avrupa Paketi dahil —, otobüsler, ayarlar), kamera, ışık, 3D harita ve arayüz. Prefab, sahne veya asset
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
            // Harita dokuları JPEG/PNG baytından çözülür (MapTextures): web indirmesi ~13 MB küçük
            Texture2D mapTexture = MapTextures.LoadMap();
            Texture2D outerTexture = MapTextures.LoadOuter();
            Texture2D waterMask = MapTextures.LoadWaterMask();
            if (mapTexture != null)
                Debug.Log($"[Bus Tycoon] Harita dokusu {mapTexture.width}x{mapTexture.height}, {mapTexture.mipmapCount} mip, " +
                          $"deniz maskesi {(waterMask != null ? "var" : "yok")}");

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
            world.Init(session, content.Projection, mapTexture, outerTexture, LoadRoads(), waterMask);

            Camera camera = SetupCamera(world.SeaColor);
            Light sun = SetupLighting();
            GraphicsQuality.Init(sun); // gölgeler, harita efektleri, web çözünürlüğü (cihaza göre ya da menüden)
            new GameObject("DayNightCycle").AddComponent<DayNightCycle>().Init(session, sun, world);

            var mapCamera = camera.gameObject.AddComponent<MapCamera>();
            mapCamera.Init(camera, world.MapBoundsXZ);

            // Yakın plan harita döşemeleri (yakınlaşınca indirilir; kaliteye göre)
            var tiles = new GameObject("MapTiles").AddComponent<MapTiles>();
            tiles.transform.SetParent(world.transform, false);
            tiles.Init(world, mapCamera);

            // Durağa girme: içerisi haritadan uzakta kendi kamerası ve ışığıyla kurulur
            var stations = new GameObject("StationView").AddComponent<StationView>();
            stations.transform.SetParent(root.transform, false);
            stations.Init(session, mapCamera, sun);

            // Çok oyunculu oda (yarış başlayınca şirketi kurar, saati odaya bağlar)
            var multiplayer = new GameObject("Multiplayer").AddComponent<MultiplayerDirector>();
            multiplayer.transform.SetParent(root.transform, false);
            multiplayer.Init(session);

            // Ses efektleri (menüden açılıp kapatılır)
            var audio = new GameObject("GameAudio").AddComponent<GameAudio>();
            audio.transform.SetParent(root.transform, false);
            audio.Init();

            var ui = new GameObject("GameUI").AddComponent<GameUI>();
            ui.transform.SetParent(root.transform, false);
            ui.Init(session, world, mapCamera);

            if (LaunchOptions.Has("vitrin") || LaunchOptions.Has("showcase"))
                new GameObject(ShowcaseDirector.ObjectName).AddComponent<ShowcaseDirector>().Init(session, ui);
        }

        /// <summary>Karayolu ağı; yoksa ya da okunamazsa hatlar yay olarak çizilir (oyun etkilenmez).</summary>
        static RoadNetwork LoadRoads()
        {
            var asset = Resources.Load<TextAsset>(DefaultContent.RoadsResourcePath);
            if (asset == null) return null;
            try
            {
                return RoadNetwork.Parse(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bus Tycoon] Yol ağı okunamadı, hatlar yay olarak çizilecek: {e.Message}");
                return null;
            }
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
            // Işık efekti (bloom vb.) yok: HDR ara tamponu gereksiz; kapalıyken 4x MSAA tamponu yarı bellek kullanır
            camera.allowHDR = false;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;
            return camera;
        }

        static Light SetupLighting()
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
            sun.shadows = LightShadows.Soft; // kaliteye göre GraphicsQuality ayarlar
            sun.shadowStrength = 0.6f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.fog = false; // ufuk pusu haritadayken WorldRenderer'da açılır (kaliteye göre)
            RenderSettings.fogMode = FogMode.Linear;
            return sun;
        }
    }
}
