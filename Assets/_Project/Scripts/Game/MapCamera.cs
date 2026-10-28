using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Strateji oyunu kamerası: haritaya eğik bakar, kaydırma (sürükle / WASD), yakınlaştırma (tekerlek)
    /// ve bir noktaya yumuşak odaklanma. Girdi doğrudan okunmaz; <see cref="GameUI"/> IMGUI olaylarından
    /// çağırır. Böylece eski Input Manager / yeni Input System ayarından bağımsız çalışır.
    /// </summary>
    public sealed class MapCamera : MonoBehaviour
    {
        const float Pitch = 56f;
        const float MinDistance = 7f;
        const float MaxDistance = 85f;

        Camera cam;
        Rect bounds;
        Vector3 target, targetGoal;
        float distance = 60f, distanceGoal = 60f;
        float driftAngle;

        public Camera Camera => cam;
        public float Distance => distance;
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
            float dt = Time.unscaledDeltaTime;

            if (AutoDrift)
            {
                driftAngle += dt * 0.05f;
                targetGoal = new Vector3(bounds.center.x + Mathf.Cos(driftAngle) * bounds.width * 0.18f, 0f,
                    bounds.center.y + Mathf.Sin(driftAngle * 1.3f) * bounds.height * 0.15f);
                distanceGoal = 52f;
            }

            float k = 1f - Mathf.Exp(-(AutoDrift ? 1.5f : 10f) * dt);
            target = Vector3.Lerp(target, targetGoal, k);
            distance = Mathf.Lerp(distance, distanceGoal, k);
            Apply();
        }

        void Apply()
        {
            Quaternion rotation = Quaternion.Euler(Pitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }

        /// <summary>Klavye ile kaydırma; hız yakınlaştırmaya göre ölçeklenir.</summary>
        public void PanByAxis(Vector2 axis, float deltaTime)
        {
            targetGoal += new Vector3(axis.x, 0f, axis.y) * (distance * 0.9f * deltaTime);
            Clamp();
        }

        /// <summary>Fareyle sürükleme: imlecin altındaki harita noktası imleçle birlikte hareket eder.</summary>
        public void DragPan(Vector2 previousScreen, Vector2 currentScreen)
        {
            if (!TryGetGroundPoint(previousScreen, out Vector3 a) || !TryGetGroundPoint(currentScreen, out Vector3 b)) return;
            Vector3 delta = a - b;
            delta.y = 0f;
            targetGoal += delta;
            target += delta;
            Clamp();
        }

        /// <summary>Pozitif = uzaklaş. İmleç yönüne doğru yakınlaşır.</summary>
        public void Zoom(float amount, Vector2 screenPosition)
        {
            float before = distanceGoal;
            distanceGoal = Mathf.Clamp(distanceGoal * (1f + amount * 0.08f), MinDistance, MaxDistance);

            if (distanceGoal < before && TryGetGroundPoint(screenPosition, out Vector3 point))
            {
                float pull = 1f - distanceGoal / before;
                targetGoal = Vector3.Lerp(targetGoal, new Vector3(point.x, 0f, point.z), pull);
                Clamp();
            }
        }

        public void FocusOn(Vector3 worldPoint, float? newDistance = null)
        {
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

        void Clamp()
        {
            targetGoal.x = Mathf.Clamp(targetGoal.x, bounds.xMin, bounds.xMax);
            targetGoal.z = Mathf.Clamp(targetGoal.z, bounds.yMin, bounds.yMax);
        }
    }
}
