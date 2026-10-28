using System;
using UnityEngine;

namespace BusTycoon.World
{
    /// <summary>
    /// XZ düzleminde Web-Mercator projeksiyonu. Harita dokusu (texture) aynı enlem/boylam
    /// sınırlarıyla kırpılmış olmalıdır (ör. Türkiye + Balkanlar).
    /// Harita yüksekliği, en-boy oranı bozulmasın diye genişlikten otomatik hesaplanır.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/World/Mercator Projection", fileName = "MercatorProjection")]
    public sealed class MercatorMapProjection : MapProjection
    {
        const double MaxMercatorLatitude = 85.05112878;

        [Header("Coğrafi sınırlar (harita dokusuyla birebir aynı olmalı)")]
        [SerializeField] double minLongitude = 19.0;
        [SerializeField] double maxLongitude = 46.0;
        [SerializeField] double minLatitude = 34.0;
        [SerializeField] double maxLatitude = 44.0;

        [Header("Unity yerleşimi")]
        [SerializeField, Min(1f)] float mapWidth = 100f;
        [SerializeField] Vector3 mapCenter = Vector3.zero;
        [SerializeField] float surfaceHeight = 0f;

        public float MapWidth => mapWidth;
        public float MapHeight => (float)(mapWidth * (MercY(maxLatitude) - MercY(minLatitude)) / LonRangeRad);
        public Vector3 MapCenter => mapCenter;

        double LonRangeRad => (maxLongitude - minLongitude) * Math.PI / 180.0;

        public override Vector3 GeoToWorld(GeoCoordinate geo)
        {
            double u = (geo.longitude - minLongitude) / (maxLongitude - minLongitude);
            double v = (MercY(geo.latitude) - MercY(minLatitude)) / (MercY(maxLatitude) - MercY(minLatitude));
            return mapCenter + new Vector3((float)(u - 0.5) * mapWidth, surfaceHeight, (float)(v - 0.5) * MapHeight);
        }

        public override GeoCoordinate WorldToGeo(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - mapCenter;
            double u = local.x / mapWidth + 0.5;
            double v = local.z / MapHeight + 0.5;

            double lon = minLongitude + u * (maxLongitude - minLongitude);
            double mercY = MercY(minLatitude) + v * (MercY(maxLatitude) - MercY(minLatitude));
            double lat = (2.0 * Math.Atan(Math.Exp(mercY)) - Math.PI / 2.0) * 180.0 / Math.PI;
            return new GeoCoordinate(lat, lon);
        }

        public override Vector3 SurfaceNormal(GeoCoordinate geo) => Vector3.up;

        static double MercY(double latitudeDeg)
        {
            double lat = Math.Max(-MaxMercatorLatitude, Math.Min(MaxMercatorLatitude, latitudeDeg)) * Math.PI / 180.0;
            return Math.Log(Math.Tan(Math.PI / 4.0 + lat / 2.0));
        }

        internal void Configure(double minLon, double maxLon, double minLat, double maxLat, float width)
        {
            minLongitude = minLon;
            maxLongitude = maxLon;
            minLatitude = minLat;
            maxLatitude = maxLat;
            mapWidth = width;
        }

        void OnValidate()
        {
            if (maxLongitude <= minLongitude) maxLongitude = minLongitude + 1.0;
            if (maxLatitude <= minLatitude) maxLatitude = minLatitude + 1.0;
            minLatitude = Math.Max(-MaxMercatorLatitude, minLatitude);
            maxLatitude = Math.Min(MaxMercatorLatitude, maxLatitude);
        }
    }
}
