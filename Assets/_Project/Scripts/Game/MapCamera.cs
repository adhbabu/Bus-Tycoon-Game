using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Strateji oyunu kamerası: haritaya eğik bakar, kaydırma (sürükle / WASD), yakınlaştırma (tekerlek)
    /// ve bir noktaya yumuşak odaklanma. Girdi doğrudan okunmaz; <see cref="GameUI"/> IMGUI olaylarından
    /// çağırır. Böylece eski Input Manager / yeni Input System ayarından bağımsız çalışır.
    ///
    /// Hareket: hedefe yay benzeri (kritik sönümlü) geçiş; sürükleme bırakılınca harita hızıyla süzülür ve
    /// yavaşlar; yakınlaştıkça kamera eğilir (uzakta haritaya tepeden, yakında ufka doğru) ve görüş açısı daralır.
    /// </summary>
    public sealed class MapCamera : MonoBehaviour
    {
        const float FarPitch = 62f, NearPitch = 42f;   // uzakta tepeden, yakında sinematik
        const float FarFov = 40f, NearFov = 35f;
        const float MinDistance = 7f;
        const float MaxDistance = 140f;     // Avrupa haritasının tamamı görünür
        const float TiltDistance = 85f;     // eğim ve görüş açısı bu uzaklığa kadar değişir, ötesinde sabit
        const float UserSmooth = 0.11f;     // kullanıcı girdisi: çabuk tepki
        const float FocusSmooth = 0.42f;    // odaklanma (şehre/hatta): sinematik
        const float DriftSmooth = 1.6f;
        const float FlingDamping = 3.2f;    // süzülmenin sönümü (1/sn)

        Camera cam;
        Rect bounds;
        Vector3 target, targetGoal, targetVelocity;
        float distance = 60f, distanceGoal = 60f, distanceVelocity;
        float smoothTime = UserSmooth;
        float driftAngle;

        // Sürükleme hızı (dünya birimi/sn) ve bırakınca süzülme
        Vector3 dragVelocity, fling;
        float lastDragTime = -1f;
        bool dragging;

        public Camera Camera => cam;
        public float Distance => distance;
        /// <summary>0 = en yakın, 1 = en uzak (eğim, LOD ve etiketler için).</summary>
        public float Zoom01 => Mathf.InverseLerp(MinDistance, MaxDistance, distance);
        /// <summary>Ana menüde haritanın üzerinde yavaşça süzülür.</summary>
        public bool AutoDrift { get; set; }

        public void Init(Camera camera, Rect worldBoundsXZ)
        {
            cam = camera;
            bounds = worldBoundsXZ;
            target = targetGoal = new Vector3(bounds.center.x, 0f, bounds.center.y);
            Apply();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); // sekme arka plandan dönünce sıçramasın

            if (AutoDrift)
            {
                driftAngle += dt * 0.05f;
                targetGoal = new Vector3(bounds.center.x + Mathf.Cos(driftAngle) * bounds.width * 0.18f, 0f,
                    bounds.center.y + Mathf.Sin(driftAngle * 1.3f) * bounds.height * 0.15f);
                distanceGoal = 52f;
                smoothTime = DriftSmooth;
                fling = Vector3.zero;
            }

            // Bırakılan sürükleme: harita parmağın hızıyla kaymaya devam eder, üstel yavaşlar
            if (!dragging && fling.sqrMagnitude > 1e-4f)
            {
                Vector3 step = fling * dt;
                targetGoal += step;
                target += step;
                fling *= Mathf.Exp(-FlingDamping * dt);
                if (Clamp()) fling = Vector3.zero; // kenara çarpınca dur
            }

            target = Vector3.SmoothDamp(target, targetGoal, ref targetVelocity, smoothTime, Mathf.Infinity, dt);
            distance = Mathf.SmoothDamp(distance, distanceGoal, ref distanceVelocity, smoothTime, Mathf.Infinity, dt);
            // Odak geçişi bitince yeniden çabuk tepkiye dön
            if (smoothTime > UserSmooth && !AutoDrift && (target - targetGoal).sqrMagnitude < 0.01f &&
                Mathf.Abs(distance - distanceGoal) < 0.05f)
                smoothTime = UserSmooth;
            Apply();
        }

        void Apply()
        {
            float t = Mathf.Pow(Mathf.InverseLerp(MinDistance, TiltDistance, distance), 0.7f);
            Quaternion rotation = Quaternion.Euler(Mathf.Lerp(NearPitch, FarPitch, t), 0f, 0f);
            cam.fieldOfView = Mathf.Lerp(NearFov, FarFov, t);
            cam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>Klavye ile kaydırma; hız yakınlaştırmaya göre ölçeklenir.</summary>
        public void PanByAxis(Vector2 axis, float deltaTime)
        {
            fling = Vector3.zero;
            smoothTime = UserSmooth;
            targetGoal += new Vector3(axis.x, 0f, axis.y) * (distance * 0.9f * deltaTime);
            Clamp();
        }

        /// <summary>Parmak/fare basıldı: süzülme durur, harita parmağa yapışır.</summary>
        public void BeginDrag()
        {
            dragging = true;
            fling = dragVelocity = Vector3.zero;
            lastDragTime = Time.unscaledTime;
        }

        /// <summary>Bırakıldı: son hareketin hızıyla süzülür (son 0,12 sn'de hareket yoksa durur).</summary>
        public void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            bool recent = Time.unscaledTime - lastDragTime < 0.12f;
            fling = recent ? Vector3.ClampMagnitude(dragVelocity, distance * 4f) : Vector3.zero;
        }

        /// <summary>Fareyle sürükleme: imlecin altındaki harita noktası imleçle birlikte hareket eder.</summary>
        public void DragPan(Vector2 previousScreen, Vector2 currentScreen)
        {
            if (!TryGetGroundPoint(previousScreen, out Vector3 a) || !TryGetGroundPoint(currentScreen, out Vector3 b)) return;
            Vector3 delta = a - b;
            delta.y = 0f;
            targetGoal += delta;
            target += delta;
            smoothTime = UserSmooth;
            Clamp();

            // Hız: kareler arası süreye göre, yumuşatılmış (tek bir titrek kare süzülmeyi bozmasın)
            float now = Time.unscaledTime;
            float elapsed = Mathf.Max(now - lastDragTime, 1f / 120f);
            dragVelocity = Vector3.Lerp(dragVelocity, delta / elapsed, 0.45f);
            lastDragTime = now;
        }

        /// <summary>Pozitif = uzaklaş. İmleç yönüne doğru yakınlaşır.</summary>
        public void Zoom(float amount, Vector2 screenPosition)
        {
            smoothTime = UserSmooth;
            float before = distanceGoal;
            distanceGoal = Mathf.Clamp(distanceGoal * (1f + amount * 0.08f), MinDistance, MaxDistance);

            if (distanceGoal < before && TryGetGroundPoint(screenPosition, out Vector3 point))
            {
                float pull = 1f - distanceGoal / before;
                targetGoal = Vector3.Lerp(targetGoal, new Vector3(point.x, 0f, point.z), pull);
                Clamp();
            }
        }

        /// <summary>Çift dokunuş: dokunulan yere doğru belirgin yakınlaşma (sinematik geçişle).</summary>
        public void ZoomInAt(Vector2 screenPosition)
        {
            if (!TryGetGroundPoint(screenPosition, out Vector3 point)) return;
            fling = Vector3.zero;
            smoothTime = FocusSmooth * 0.8f;
            distanceGoal = Mathf.Clamp(distanceGoal * 0.55f, MinDistance, MaxDistance);
            targetGoal = Vector3.Lerp(targetGoal, new Vector3(point.x, 0f, point.z), 0.7f);
            Clamp();
        }

        public void FocusOn(Vector3 worldPoint, float? newDistance = null)
        {
            fling = Vector3.zero;
            smoothTime = FocusSmooth;
            targetGoal = new Vector3(worldPoint.x, 0f, worldPoint.z);
            if (newDistance.HasValue) distanceGoal = Mathf.Clamp(newDistance.Value, MinDistance, MaxDistance);
            Clamp();
        }

        public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>Hedefi harita sınırına çeker; sınıra değdiyse true.</summary>
        bool Clamp()
        {
            Vector3 before = targetGoal;
            targetGoal.x = Mathf.Clamp(targetGoal.x, bounds.xMin, bounds.xMax);
            targetGoal.z = Mathf.Clamp(targetGoal.z, bounds.yMin, bounds.yMax);
            return targetGoal != before;
        }
    }
}
