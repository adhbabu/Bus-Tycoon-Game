using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Presentation.Map
{
    /// <summary>
    /// İki şehir arasındaki hattı büyük daire üzerinde örnekleyip yüzeyden yükselen bir yay olarak çizer.
    /// Hem düz (Mercator) hem küre (Globe) projeksiyonunda doğru görünür.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RouteLineView : MonoBehaviour
    {
        [SerializeField, Range(4, 128)] int segments = 32;
        [Tooltip("Yay yüksekliği = iki uç arasındaki dünya mesafesi × bu oran.")]
        [SerializeField, Range(0f, 0.5f)] float arcHeightRatio = 0.12f;
        [SerializeField, Min(0.001f)] float width = 0.12f;

        LineRenderer line;
        Vector3[] points;

        public string RouteId { get; private set; }

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = width;
        }

        public void Draw(string routeId, MapProjection projection, GeoCoordinate from, GeoCoordinate to)
        {
            RouteId = routeId;
            if (line == null) Awake();
            if (points == null || points.Length != segments + 1) points = new Vector3[segments + 1];

            Vector3 start = projection.GeoToWorld(from);
            Vector3 end = projection.GeoToWorld(to);
            float arcHeight = Vector3.Distance(start, end) * arcHeightRatio;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                GeoCoordinate geo = GeoCoordinate.Interpolate(from, to, t);
                float lift = 4f * t * (1f - t) * arcHeight; // parabol: uçlarda 0, ortada arcHeight
                points[i] = projection.GeoToWorld(geo) + projection.SurfaceNormal(geo) * lift;
            }

            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        public void SetColor(Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }
    }
}
