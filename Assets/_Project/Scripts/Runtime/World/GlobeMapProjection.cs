using System;
using UnityEngine;

namespace BusTycoon.World
{
    /// <summary>
    /// 3D küre (dünya) projeksiyonu. Enlem 0 / boylam 0 noktası kürenin -Z yüzüne,
    /// yani +Z yönüne bakan varsayılan kameraya dönüktür.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/World/Globe Projection", fileName = "GlobeProjection")]
    public sealed class GlobeMapProjection : MapProjection
    {
        [SerializeField, Min(0.1f)] float radius = 50f;
        [SerializeField] Vector3 center = Vector3.zero;

        public float Radius => radius;

        public override Vector3 GeoToWorld(GeoCoordinate geo)
        {
            double lat = geo.latitude * Math.PI / 180.0;
            double lon = geo.longitude * Math.PI / 180.0;
            var direction = new Vector3(
                (float)(Math.Cos(lat) * Math.Sin(lon)),
                (float)Math.Sin(lat),
                (float)(-Math.Cos(lat) * Math.Cos(lon)));
            return center + direction * radius;
        }

        public override GeoCoordinate WorldToGeo(Vector3 worldPosition)
        {
            Vector3 d = (worldPosition - center).normalized;
            double lat = Math.Asin(Mathf.Clamp(d.y, -1f, 1f)) * 180.0 / Math.PI;
            double lon = Math.Atan2(d.x, -d.z) * 180.0 / Math.PI;
            return new GeoCoordinate(lat, lon);
        }

        public override Vector3 SurfaceNormal(GeoCoordinate geo) => (GeoToWorld(geo) - center).normalized;
    }
}
