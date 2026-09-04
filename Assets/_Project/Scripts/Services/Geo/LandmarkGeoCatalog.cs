using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>Real-world coordinates for one Landmark POI. Add or remove entries here to change what
    /// RealLandmarkMapProvider tracks - no code changes needed. <see cref="id"/> must match a
    /// <see cref="LandmarkUiData.id"/> in PrototypeUiCatalog (see <see cref="PrototypeIds"/>).</summary>
    [System.Serializable]
    public sealed class LandmarkGeoData
    {
        public string id;
        [Tooltip("Decimal degrees, WGS84.")] public double latitude;
        [Tooltip("Decimal degrees, WGS84.")] public double longitude;
        [Min(0f)] public float unlockRadiusMeters = 100f;
        [Tooltip("Used to calibrate the real-world-to-illustrated-map projection. Exactly three entries in this " +
                 "catalog must have this checked, and they must not be collinear on the map.")]
        public bool isMapCalibrationAnchor;

        public GeoPoint Location => new GeoPoint(latitude, longitude);
    }

    [CreateAssetMenu(fileName = "LandmarkGeoCatalog", menuName = "AR Walking/Landmark Geo Catalog")]
    public sealed class LandmarkGeoCatalog : ScriptableObject
    {
        public List<LandmarkGeoData> landmarks = new List<LandmarkGeoData>();

        public LandmarkGeoData Find(string id)
        {
            foreach (var landmark in landmarks)
                if (landmark.id == id) return landmark;
            return null;
        }
    }
}
