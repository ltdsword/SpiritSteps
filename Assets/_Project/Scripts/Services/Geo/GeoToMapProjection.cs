using System;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Maps real-world GPS fixes onto the illustrated map's normalized 0-1 coordinate space.
    /// The illustrated map is hand-drawn (not geographically accurate), so the mapping is calibrated from
    /// exactly three known (real-world, normalized-map) landmark pairs rather than a map projection formula.
    /// Three non-collinear pairs fully determine the affine transform (rotation + non-uniform scale + translation)
    /// that best matches how the illustration was drawn relative to reality.
    /// </summary>
    public sealed class GeoToMapProjection
    {
        readonly double _a, _b, _c, _d, _e, _f;

        public GeoToMapProjection(GeoPoint geo0, Vector2 map0, GeoPoint geo1, Vector2 map1, GeoPoint geo2, Vector2 map2)
        {
            double dLat1 = geo1.lat - geo0.lat, dLon1 = geo1.lon - geo0.lon;
            double dLat2 = geo2.lat - geo0.lat, dLon2 = geo2.lon - geo0.lon;
            double det = dLat1 * dLon2 - dLat2 * dLon1;
            if (Math.Abs(det) < 1e-12)
                throw new ArgumentException("The three calibration landmarks are collinear (or duplicated); cannot solve an affine map.");

            double dNx1 = map1.x - map0.x, dNx2 = map2.x - map0.x;
            double dNy1 = map1.y - map0.y, dNy2 = map2.y - map0.y;

            _a = (dNx1 * dLon2 - dNx2 * dLon1) / det;
            _b = (dLat1 * dNx2 - dLat2 * dNx1) / det;
            _c = map0.x - _a * geo0.lat - _b * geo0.lon;

            _d = (dNy1 * dLon2 - dNy2 * dLon1) / det;
            _e = (dLat1 * dNy2 - dLat2 * dNy1) / det;
            _f = map0.y - _d * geo0.lat - _e * geo0.lon;
        }

        /// <summary>Projects a real-world fix to normalized map coordinates. The result is NOT clamped to 0-1 -
        /// a player outside the illustrated area legitimately projects outside it.</summary>
        public Vector2 Project(GeoPoint geo) =>
            new Vector2((float)(_a * geo.lat + _b * geo.lon + _c), (float)(_d * geo.lat + _e * geo.lon + _f));

        /// <summary>Same as <see cref="Project"/> but clamped to the illustrated map's visible 0-1 bounds.</summary>
        public Vector2 ProjectClamped(GeoPoint geo)
        {
            var p = Project(geo);
            return new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y));
        }
    }
}
