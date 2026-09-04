using System;

namespace ARWalking.UI
{
    [Serializable]
    public struct GeoPoint : IEquatable<GeoPoint>
    {
        public double lat;
        public double lon;

        public GeoPoint(double lat, double lon)
        {
            this.lat = lat;
            this.lon = lon;
        }

        public bool Equals(GeoPoint other) => lat.Equals(other.lat) && lon.Equals(other.lon);
        public override bool Equals(object obj) => obj is GeoPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(lat, lon);
        public override string ToString() => $"{lat:F5}, {lon:F5}";
    }
}
