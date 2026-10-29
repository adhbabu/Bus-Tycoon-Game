using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Stations;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// "Durağa girme": haritada bir durağa tıklayınca kamera durağa dalar, ekran kararır ve durağın içi
    /// (<see cref="StationScene"/>) kendi kamerası ve ışığıyla açılır; kamera sokaktan girişe doğru süzülüp salonu
    /// tepeden gösterir. İçeride sürükleyerek döndürülür, tekerlek/sıkıştırma ile yakınlaştırılır. Satın alınan
    /// geliştirme yerinde zıplayarak belirir, konfeti saçılır. Sahne haritadan çok uzakta kurulur; harita kamerası
    /// içerideyken kapatılır. Gece-gündüz ve oyun hızı (yolcular, trafik, saat kulesi) içeride de sürer.
    /// </summary>
    public sealed class StationView : MonoBehaviour
    {
        public enum Phase { Hidden, Diving, Inside, Leaving }

        const float DiveSeconds = 0.55f, IntroSeconds = 1.7f, LeaveSeconds = 0.32f, FadeOutSeconds = 0.4f;
        static readonly Vector3 StageOrigin = new Vector3(0f, 0f, 6000f);
        static readonly Color DaySky = new Color(0.63f, 0.8f, 0.95f), NightSky = new Color(0.05f, 0.08f, 0.17f);
        static readonly Color DayLight = new Color(1f, 0.96f, 0.9f), NightLight = new Color(0.55f, 0.65f, 1f);
        // Proje gamma renk uzayında: yere düşen ışık + ortam ≈ 1 olsun ki renkler (beyaz zemin) patlamasın.
        static readonly Color DayAmbient = new Color(0.47f, 0.49f, 0.54f), NightAmbient = new Color(0.2f, 0.23f, 0.34f);
        const float DayIntensity = 0.68f, NightIntensity = 0.28f;

        public static StationView Instance { get; private set; }

        public Phase State { get; private set; }
        public bool IsActive => State != Phase.Hidden;
        public bool IsInside => State == Phase.Inside;
        /// <summary>Ekranı karartan perdenin opaklığı (GameUI çizer).</summary>
        public float Fade { get; private set; }
        public CityData City { get; private set; }
        public StationScene Scene { get; private set; }
        public Camera Camera => stationCamera;
        /// <summary>Durağa girdikten sonraki giriş süzülmesi bitti mi (arayüz bundan sonra tam görünür).</summary>
        public float IntroProgress => State == Phase.Inside ? Mathf.Clamp01(timer / IntroSeconds) : 0f;

        GameSession session;
        MapCamera mapCamera;
        Light sun;
        Camera stationCamera;
        Light stationLight;
        Transform stage;
        float timer, savedMapDistance, savedShadowDistance, viewportShift;
        ShadowQuality savedShadows;
        bool swapped;

        // Kamera: odak çevresinde dönme (yaw), eğim (pitch), uzaklık
        float yaw = 28f, pitch = 38f, distance = 40f;
        float yawGoal, pitchGoal, distanceGoal;
        Vector3 focus, focusGoal;
        float focusHoldUntil = -1f;

        readonly List<(Transform Part, float Start)> pops = new List<(Transform, float)>();
        readonly List<Confetti> confetti = new List<Confetti>();
        static Mesh[] confettiMeshes;
        static MeshLayer[] confettiLayers;

        sealed class Confetti
        {
            public Transform Transform;
            public Vector3 Velocity, Spin;
            public float Age, Life;
        }

        public void Init(GameSession gameSession, MapCamera camera, Light sunLight)
        {
            Instance = this;
            session = gameSession;
            mapCamera = camera;
            sun = sunLight;

            stage = new GameObject("StationStage").transform;
            stage.SetParent(transform, false);
            stage.position = StageOrigin;
            stage.gameObject.SetActive(false);

            var cameraObject = new GameObject("StationCamera");
            cameraObject.transform.SetParent(transform, false);
            stationCamera = cameraObject.AddComponent<Camera>();
            stationCamera.clearFlags = CameraClearFlags.SolidColor;
            stationCamera.backgroundColor = DaySky;
            stationCamera.fieldOfView = 40f;
            stationCamera.nearClipPlane = 0.5f;
            stationCamera.farClipPlane = 600f;
            stationCamera.enabled = false;

            var lightObject = new GameObject("StationLight");
            lightObject.transform.SetParent(transform, false);
            stationLight = lightObject.AddComponent<Light>();
            stationLight.type = LightType.Directional;
            stationLight.shadows = Application.isMobilePlatform ? LightShadows.Hard : LightShadows.Soft;
            stationLight.shadowStrength = 0.55f;
            stationLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            stationLight.enabled = false;

            session.SessionStarted += ExitImmediate;
            session.DayCompleted += OnDayCompleted;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (session == null) return;
            session.SessionStarted -= ExitImmediate;
            session.DayCompleted -= OnDayCompleted;
            if (session.Stations != null) session.Stations.UpgradeBuilt -= OnUpgradeBuilt;
        }

        // ------------------------------------------------------------------ giriş / çıkış

        /// <summary>Durağa gir: kamera durağa dalar, ekran kararınca iç sahne açılır.</summary>
        public void Enter(CityData city)
        {
            if (city == null || State != Phase.Hidden || session.Stations == null) return;
            City = city;
            State = Phase.Diving;
            timer = 0f;
            savedMapDistance = mapCamera.Distance;
            mapCamera.FocusOn(WorldPosition(city), 7f);
            session.Stations.UpgradeBuilt -= OnUpgradeBuilt;
            session.Stations.UpgradeBuilt += OnUpgradeBuilt;
        }

        public void Exit()
        {
            if (State == Phase.Inside || State == Phase.Diving)
            {
                State = Phase.Leaving;
                timer = 0f;
            }
        }

        /// <summary>Perdesiz, hemen çık (yeni oyun, kayıt yükleme, ana menü).</summary>
        public void ExitImmediate()
        {
            if (State == Phase.Hidden) return;
            SwapToMap();
            State = Phase.Hidden;
            Fade = 0f;
            fadeOutTimer = -1f;
        }

        Vector3 WorldPosition(CityData city)
        {
            var world = FindFirstObjectByType<WorldRenderer>();
            return world != null ? world.GetCityPosition(city) : Vector3.zero;
        }

        void SwapToStation()
        {
            if (swapped) return;
            swapped = true;
            stage.gameObject.SetActive(true);
            Scene = new StationScene(stage, BuildInfo(City));
            Camera main = mapCamera.Camera;
            if (main != null) main.enabled = false;
            stationCamera.enabled = true;
            if (sun != null) sun.enabled = false;
            stationLight.enabled = true;
            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadows = QualitySettings.shadows;
            QualitySettings.shadowDistance = 150f;
            if (QualitySettings.shadows == ShadowQuality.Disable) QualitySettings.shadows = ShadowQuality.HardOnly;

            // Giriş: caddenin üstünden girişe bakış → salonun tepeden görünümü (kamera hep yolun ve arabaların üstünde)
            focusGoal = Scene.Focus;
            yawGoal = 28f;
            pitchGoal = 38f;
            distanceGoal = Scene.DefaultDistance;
            focus = Scene.Focus + new Vector3(-2f, 0.5f, -5f);
            yaw = -6f;
            pitch = 20f;
            distance = Scene.DefaultDistance * 0.55f;
            pops.Clear();
            ClearConfetti();
            ApplyCamera();
        }

        void SwapToMap()
        {
            if (!swapped) return;
            swapped = false;
            ClearConfetti();
            pops.Clear();
            Scene?.Destroy();
            Scene = null;
            stage.gameObject.SetActive(false);
            stationCamera.enabled = false;
            stationLight.enabled = false;
            Camera main = mapCamera.Camera;
            if (main != null) main.enabled = true;
            if (sun != null) sun.enabled = true;
            QualitySettings.shadowDistance = savedShadowDistance;
            QualitySettings.shadows = savedShadows;
            if (City != null) mapCamera.FocusOn(WorldPosition(City), Mathf.Max(18f, savedMapDistance));
        }

        /// <summary>Sahnenin girdileri: büyüklük, popülerlik, geliştirmeler, hatlar ve perondaki otobüsler.</summary>
        StationInfo BuildInfo(CityData city)
        {
            StationSystem stations = session.Stations;
            var info = new StationInfo
            {
                City = city,
                Tier = StationCatalog.TierOf(city),
                Popularity = stations.Popularity(city),
                CompanyName = session.State != null ? session.State.companyName : null,
                IsHeadquarters = city == session.Headquarters
            };
            foreach (var upgrade in StationCatalog.Upgrades)
                if (stations.Has(city, upgrade)) info.Upgrades.Add(upgrade.Id);

            var segments = new List<int>();
            var buses = new List<BusInstance>();
            foreach (var route in session.Routes.Routes)
            {
                if (route.cityAId != city.Id && route.cityBId != city.Id) continue;
                CityData other = session.World.GetCity(route.cityAId == city.Id ? route.cityBId : route.cityAId);
                if (other != null && !info.Destinations.Contains(other.DisplayName)) info.Destinations.Add(other.DisplayName);
                session.Fleet.GetBusesOnRoute(route.routeId, buses);
                foreach (var bus in buses)
                    if (bus.Model != null) segments.Add(Mathf.Clamp(bus.Model.Tier, 0, 5));
            }
            // Hattınız yoksa panoda ve peronlarda başka firmaların yakın şehirlere seferleri
            if (info.Destinations.Count < 4)
            {
                var nearby = new List<CityData>(session.World.Cities);
                nearby.RemoveAll(c => c == null || c == city);
                nearby.Sort((a, b) => session.World.GetRoadDistanceKm(city, a).CompareTo(session.World.GetRoadDistanceKm(city, b)));
                for (int i = 0; i < nearby.Count && info.Destinations.Count < 4; i++)
                    if (!info.Destinations.Contains(nearby[i].DisplayName)) info.Destinations.Add(nearby[i].DisplayName);
            }

            int bays = StationCatalog.Bays(info.Tier);
            bool operating = stations.IsOperating(city);
            int busy = Mathf.Clamp(Mathf.RoundToInt(bays * (operating ? 0.25f + 0.75f * info.Popularity.Traffic : 0.3f)), 1, bays);
            var random = new System.Random(city.Id.GetHashCode());
            for (int i = 0; i < busy; i++)
                info.BaySegments.Add(i < segments.Count ? segments[i] : new[] { 2, 2, 3, 1, 4, 5, 0 }[random.Next(7)]);

            info.CrowdSize = CrowdSize(info.Tier, info.Popularity);
            return info;
        }

        static int CrowdSize(StationTier tier, StationPopularity popularity) =>
            Mathf.RoundToInt(StationCatalog.People(tier) * (0.15f + 0.85f * popularity.Score / 100f));

        void OnDayCompleted(DayReport _)
        {
            if (Scene == null || City == null) return;
            StationPopularity popularity = session.Stations.Popularity(City);
            Scene.Info.Popularity = popularity;
            int size = CrowdSize(Scene.Info.Tier, popularity);
            if (Mathf.Abs(size - Scene.Info.CrowdSize) >= Mathf.Max(3, Scene.Info.CrowdSize / 6)) Scene.SetCrowdSize(size);
        }

        /// <summary>Satın alınan geliştirme yerinde zıplayarak belirir; kamera ona döner; konfeti saçılır.</summary>
        void OnUpgradeBuilt(CityData city, StationUpgrade upgrade)
        {
            if (Scene == null || city != City) return;
            Scene.Info.Popularity = session.Stations.Popularity(city);
            Scene.Info.CrowdSize = CrowdSize(Scene.Info.Tier, Scene.Info.Popularity);
            Scene.AddUpgrade(upgrade.Id);
            IReadOnlyList<Transform> parts = Scene.PartsOf(upgrade.Id);
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (Transform part in parts)
            {
                pops.Add((part, Time.unscaledTime + count * 0.06f));
                part.localScale = Vector3.one * 0.01f;
                center += stage.InverseTransformPoint(part.position);
                count++;
            }
            if (count == 0) return;
            center /= count;
            SpawnConfetti(center + Vector3.up * 3f);
            focusGoal = new Vector3(center.x, 1.5f, center.z);
            distanceGoal = Mathf.Min(distanceGoal, Scene.DefaultDistance * 0.75f);
            focusHoldUntil = Time.unscaledTime + 2.6f;
        }

        // ------------------------------------------------------------------ kamera kontrolü

        /// <summary>Sürükleme (ekran pikseli): yatay döndürür, dikey eğer.</summary>
        public void Orbit(Vector2 pixels)
        {
            if (State != Phase.Inside) return;
            float scale = 180f / Mathf.Max(400f, Screen.height);
            yawGoal += pixels.x * scale;
            pitchGoal = Mathf.Clamp(pitchGoal + pixels.y * scale * 0.7f, 18f, 72f);
        }

        /// <summary>Pozitif = uzaklaş.</summary>
        public void Zoom(float amount)
        {
            if (State != Phase.Inside || Scene == null) return;
            distanceGoal = Mathf.Clamp(distanceGoal * (1f + amount * 0.08f), Scene.MinDistance, Scene.MaxDistance);
        }

        public void ResetView()
        {
            if (Scene == null) return;
            yawGoal = 28f;
            pitchGoal = 38f;
            distanceGoal = Scene.DefaultDistance;
            focusGoal = Scene.Focus;
        }

        /// <summary>Sağdaki panelin kapladığı ekran oranı: sahne kalan alanın ortasında görünür (mercek kaydırma).</summary>
        public void SetViewportShift(float fraction) => viewportShift = Mathf.Clamp(fraction, 0f, 0.6f);

        // ------------------------------------------------------------------ kare döngüsü

        void Update()
        {
            if (State == Phase.Hidden) return;
            float dt = Time.unscaledDeltaTime;
            timer += dt;
            switch (State)
            {
                case Phase.Diving:
                    Fade = Smooth(0.08f, DiveSeconds, timer);
                    if (timer >= DiveSeconds)
                    {
                        SwapToStation();
                        State = Phase.Inside;
                        timer = 0f;
                    }
                    break;
                case Phase.Inside:
                    Fade = 1f - Smooth(0.05f, FadeOutSeconds + 0.05f, timer);
                    break;
                case Phase.Leaving:
                    Fade = Smooth(0f, LeaveSeconds, timer);
                    if (timer >= LeaveSeconds)
                    {
                        SwapToMap();
                        State = Phase.Hidden;
                        fadeOutTimer = 0f;
                    }
                    break;
            }

            if (Scene != null)
            {
                int speed = session.IsRunning && session.Clock != null ? session.Clock.Speed : 0;
                float gameDt = speed > 0 ? dt * Mathf.Sqrt(speed) : 0f;
                DayNightCycle cycle = DayNightCycle.Instance;
                float night = cycle != null ? cycle.Night01 : 0f;
                float hours = cycle != null ? cycle.TimeOfDay01 * 24f : 12f;
                Scene.Update(dt, gameDt, night, hours);
                AnimatePops();
                AnimateConfetti(dt);
            }
        }

        float fadeOutTimer = -1f;

        void LateUpdate()
        {
            // Haritaya dönüşte perde yavaşça açılır
            if (State == Phase.Hidden && fadeOutTimer >= 0f)
            {
                fadeOutTimer += Time.unscaledDeltaTime;
                Fade = 1f - Smooth(0f, FadeOutSeconds, fadeOutTimer);
                if (fadeOutTimer >= FadeOutSeconds)
                {
                    Fade = 0f;
                    fadeOutTimer = -1f;
                }
            }
            if (Scene == null) return;

            float dt = Time.unscaledDeltaTime;
            if (focusHoldUntil > 0f && Time.unscaledTime > focusHoldUntil)
            {
                focusHoldUntil = -1f;
                focusGoal = Scene.Focus;
            }
            float k = IntroProgress < 1f ? 1f - Mathf.Exp(-2.6f * dt) : 1f - Mathf.Exp(-8f * dt);
            yaw = Mathf.LerpAngle(yaw, yawGoal, k);
            pitch = Mathf.Lerp(pitch, pitchGoal, k);
            distance = Mathf.Lerp(distance, distanceGoal, k);
            focus = Vector3.Lerp(focus, focusGoal, k);
            ApplyCamera();
            ApplyLighting();
        }

        void ApplyCamera()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 local = focus - rotation * Vector3.forward * distance;
            stationCamera.transform.SetPositionAndRotation(stage.TransformPoint(local), rotation);
            stationCamera.ResetProjectionMatrix();
            if (viewportShift > 0.001f)
            {
                Matrix4x4 projection = stationCamera.projectionMatrix;
                projection[0, 2] = viewportShift;
                stationCamera.projectionMatrix = projection;
            }
            Scene?.UpdateCutaway(local);
        }

        /// <summary>İçerinin ışığı: gündüz sıcak güneş, gece mavi ay ışığı; ortam ışığını DayNightCycle'dan sonra ayarlar.</summary>
        void ApplyLighting()
        {
            DayNightCycle cycle = DayNightCycle.Instance;
            float night = cycle != null ? cycle.Night01 : 0f;
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(night - 0.45f) / 0.3f) * 0.5f;
            stationLight.color = Color.Lerp(Color.Lerp(DayLight, NightLight, night), new Color(1f, 0.62f, 0.4f), dusk);
            stationLight.intensity = Mathf.Lerp(DayIntensity, NightIntensity, night);
            RenderSettings.ambientLight = Color.Lerp(DayAmbient, NightAmbient, night);
            stationCamera.backgroundColor = Color.Lerp(Color.Lerp(DaySky, NightSky, night), new Color(0.98f, 0.62f, 0.45f), dusk);
        }

        // ------------------------------------------------------------------ satın alma efektleri

        void AnimatePops()
        {
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var (part, start) = pops[i];
                if (part == null)
                {
                    pops.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01((Time.unscaledTime - start) / 0.65f);
                // easeOutBack: hafif taşıp yerine oturur
                const float c1 = 1.9f, c3 = c1 + 1f;
                float u = t - 1f;
                float s = t <= 0f ? 0.01f : 1f + c3 * u * u * u + c1 * u * u;
                part.localScale = Vector3.one * Mathf.Max(0.01f, s);
                if (t >= 1f)
                {
                    part.localScale = Vector3.one;
                    pops.RemoveAt(i);
                }
            }
        }

        void SpawnConfetti(Vector3 local)
        {
            if (confettiMeshes == null)
            {
                Color[] colors = { new Color(1f, 0.57f, 0f), new Color(0.2f, 0.7f, 0.95f), new Color(1f, 0.85f, 0.2f), new Color(0.95f, 0.35f, 0.5f), new Color(0.4f, 0.85f, 0.4f), Color.white };
                confettiMeshes = new Mesh[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    var kit = new MeshKit();
                    kit.Box(Vector3.zero, new Vector3(0.28f, 0.04f, 0.18f), colors[i]);
                    confettiMeshes[i] = kit.Build("Confetti", out confettiLayers);
                }
            }
            var random = new System.Random();
            for (int i = 0; i < 36; i++)
            {
                var go = new GameObject("Confetti");
                go.transform.SetParent(stage, false);
                go.transform.localPosition = local + new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f);
                go.AddComponent<MeshFilter>().sharedMesh = confettiMeshes[i % confettiMeshes.Length];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = StationMaterials.For(confettiLayers, null);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f, speed = 2.5f + (float)random.NextDouble() * 4f;
                confetti.Add(new Confetti
                {
                    Transform = go.transform,
                    Velocity = new Vector3(Mathf.Cos(angle) * speed, 5f + (float)random.NextDouble() * 5f, Mathf.Sin(angle) * speed),
                    Spin = new Vector3((float)random.NextDouble() * 720f, (float)random.NextDouble() * 720f, 0f),
                    Life = 1.6f + (float)random.NextDouble() * 0.8f
                });
            }
        }

        void AnimateConfetti(float dt)
        {
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                Confetti c = confetti[i];
                c.Age += dt;
                if (c.Age >= c.Life || c.Transform == null)
                {
                    if (c.Transform != null) Destroy(c.Transform.gameObject);
                    confetti.RemoveAt(i);
                    continue;
                }
                c.Velocity += Vector3.down * (9f * dt);
                c.Velocity *= 1f - Mathf.Min(0.9f, 1.4f * dt); // hava direnci: yavaşça süzülsün
                c.Transform.localPosition += c.Velocity * dt;
                c.Transform.localRotation = Quaternion.Euler(c.Spin * c.Age);
                float fade = Mathf.Clamp01((c.Life - c.Age) / 0.4f);
                c.Transform.localScale = Vector3.one * fade;
            }
        }

        void ClearConfetti()
        {
            foreach (Confetti c in confetti)
                if (c.Transform != null) Destroy(c.Transform.gameObject);
            confetti.Clear();
        }

        static float Smooth(float from, float to, float t)
        {
            float x = Mathf.Clamp01((t - from) / Mathf.Max(0.0001f, to - from));
            return x * x * (3f - 2f * x);
        }
    }
}
