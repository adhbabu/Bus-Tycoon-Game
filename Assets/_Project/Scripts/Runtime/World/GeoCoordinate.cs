using System;
using UnityEngine;

namespace BusTycoon.World
{
    /// <summary>
    /// WGS84 enlem/boylam (derece). Gerçek dünya konumlarının tek kaynağıdır;
    /// Unity dünya koordinatları her zaman bir <see cref="MapProjection"/> üzerinden türetilir.
    /// </summary>
    [Serializable]
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        public const double EarthRadiusKm = 6371.0088;
        const double DegToRad = Math.PI / 180.0;
        const double RadToDeg = 180.0 / Math.PI;

        [Tooltip("Enlem, -90 (Güney) .. 90 (Kuzey)")]
        public double latitude;
        [Tooltip("Boylam, -180 (Batı) .. 180 (Doğu)")]
        public double longitude;

        public GeoCoordinate(double latitude, double longitude)
        {
            this.latitude = Math.Max(-90.0, Math.Min(90.0, latitude));
            this.longitude = NormalizeLongitude(longitude);
        }

        /// <summary>Büyük daire (kuş uçuşu) mesafesi, Haversine formülü.</summary>
        public static double HaversineKm(GeoCoordinate a, GeoCoordinate b)
        {
            double lat1 = a.latitude * DegToRad;
            double lat2 = b.latitude * DegToRad;
            double dLat = lat2 - lat1;
            double dLon = (b.longitude - a.longitude) * DegToRad;

            double sinLat = Math.Sin(dLat * 0.5);
            double sinLon = Math.Sin(dLon * 0.5);
            double h = sinLat * sinLat + Math.Cos(lat1) * Math.Cos(lat2) * sinLon * sinLon;
            return 2.0 * EarthRadiusKm * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
        }

        /// <summary>
        /// İki nokta arasındaki büyük daire üzerinde t (0..1) oranındaki ara nokta.
        /// 3D haritada hat çizgilerini (yay) örneklemek için kullanılır.
        /// </summary>
        public static GeoCoordinate Interpolate(GeoCoordinate a, GeoCoordinate b, double t)
        {
            double lat1 = a.latitude * DegToRad, lon1 = a.longitude * DegToRad;
            double lat2 = b.latitude * DegToRad, lon2 = b.longitude * DegToRad;

            double angular = HaversineKm(a, b) / EarthRadiusKm;
            if (angular < 1e-9) return a;

            double sinAngular = Math.Sin(angular);
            double wa = Math.Sin((1 - t) * angular) / sinAngular;
            double wb = Math.Sin(t * angular) / sinAngular;

            double x = wa * Math.Cos(lat1) * Math.Cos(lon1) + wb * Math.Cos(lat2) * Math.Cos(lon2);
            double y = wa * Math.Cos(lat1) * Math.Sin(lon1) + wb * Math.Cos(lat2) * Math.Sin(lon2);
            double z = wa * Math.Sin(lat1) + wb * Math.Sin(lat2);

            return new GeoCoordinate(Math.Atan2(z, Math.Sqrt(x * x + y * y)) * RadToDeg, Math.Atan2(y, x) * RadToDeg);
        }

        public static double NormalizeLongitude(double lon)
        {
            lon %= 360.0;
            if (lon > 180.0) lon -= 360.0;
            if (lon < -180.0) lon += 360.0;
            return lon;
        }

        public bool Equals(GeoCoordinate other) => latitude.Equals(other.latitude) && longitude.Equals(other.longitude);
        public override bool Equals(object obj) => obj is GeoCoordinate other && Equals(other);
        public override int GetHashCode() => latitude.GetHashCode() * 397 ^ longitude.GetHashCode();
        public override string ToString() => $"({latitude:F4}, {longitude:F4})";
    }
}
