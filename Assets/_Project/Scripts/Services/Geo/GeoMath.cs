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

        /// <summary>Whether <paramref name="candidate"/> is far enough from <paramref name="lastTrailPoint"/> to be
        /// worth recording as a new trail vertex - keeps a walked-path trail sparse regardless of how often the
        /// location source fires (a real GPS is already throttled, but the Editor simulation fires every frame a
        /// key is held).</summary>
        public static bool ShouldAppendTrailPoint(GeoPoint? lastTrailPoint, GeoPoint candidate, double minSpacingMeters)
            => !lastTrailPoint.HasValue || HaversineMeters(lastTrailPoint.Value, candidate) >= minSpacingMeters;
    }
}
