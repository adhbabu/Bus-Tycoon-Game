using UnityEngine;

namespace BusTycoon.World
{
    /// <summary>
    /// Enlem/boylam ↔ Unity dünya koordinatı dönüşümü. ScriptableObject olduğu için
    /// düz harita (Mercator) ile 3D küre (Globe) arasında kod değiştirmeden asset üzerinden geçiş yapılır.
    /// </summary>
    public abstract class MapProjection : ScriptableObject
    {
        public abstract Vector3 GeoToWorld(GeoCoordinate geo);
        public abstract GeoCoordinate WorldToGeo(Vector3 worldPosition);

        /// <summary>Haritanın o noktadaki "yukarı" yönü (şehir işaretçilerini hizalamak için).</summary>
        public abstract Vector3 SurfaceNormal(GeoCoordinate geo);
    }
}
