using System;

namespace ARWalking.UI
{
    /// <summary>Portable geo math shared by the real walk and map providers. No UnityEngine dependency.</summary>
    public static class GeoMath
    {
        const double EarthRadiusMeters = 6371000.0;

        public static double HaversineMeters(GeoPoint a, GeoPoint b)
        {
            double lat1 = a.lat * Math.PI / 180.0;
            double lat2 = b.lat * Math.PI / 180.0;
            double dLat = (b.lat - a.lat) * Math.PI / 180.0;
            double dLon = (b.lon - a.lon) * Math.PI / 180.0;

            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(Math.Min(1.0, h)));
        }

        /// <summary>Initial bearing from <paramref name="a"/> to <paramref name="b"/>, in degrees clockwise from north [0, 360).</summary>
        public static double BearingDegrees(GeoPoint a, GeoPoint b)
        {
            double lat1 = a.lat * Math.PI / 180.0;
            double lat2 = b.lat * Math.PI / 180.0;
            double dLon = (b.lon - a.lon) * Math.PI / 180.0;

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            double bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (bearing + 360.0) % 360.0;
        }
    }
}
