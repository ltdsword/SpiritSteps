using System;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Real ILandmarkMapProvider: player position and Landmark proximity/direction come from live GPS
    /// (via the shared DeviceLocationService) against data-driven POI coordinates in a LandmarkGeoCatalog.
    /// See docs/MAP-WALK-PROVIDER-INTEGRATION.md for the contract this fulfils.
    /// </summary>
    public sealed class RealLandmarkMapProvider : ILandmarkMapProvider
    {
        readonly DeviceLocationService _location;
        readonly LandmarkGeoCatalog _catalog;
        readonly GeoToMapProjection _projection;

        public RealLandmarkMapProvider(DeviceLocationService location, LandmarkGeoCatalog catalog, GeoToMapProjection projection)
        {
            _location = location ?? throw new ArgumentNullException(nameof(location));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public LandmarkMapState GetMapState()
        {
            _location.Activate();
            return new LandmarkMapState
            {
                hasPlayerPosition = _location.HasFix,
                playerNormalizedPosition = _location.HasFix ? _projection.ProjectClamped(_location.Current) : Vector2.zero,
                mapHeadingDegrees = 0f // No compass sensor wired up yet; the illustrated map does not rotate.
            };
        }

        public LandmarkProximity GetLandmarkProximity(string landmarkId)
        {
            _location.Activate();
            var poi = _catalog.Find(landmarkId);
            if (poi == null) return Unavailable(landmarkId);
            if (!_location.HasFix) return Unavailable(landmarkId);

            var distance = GeoMath.HaversineMeters(_location.Current, poi.Location);
            var bearing = GeoMath.BearingDegrees(_location.Current, poi.Location);
            return new LandmarkProximity
            {
                landmarkId = landmarkId,
                distanceMetres = (float)distance,
                directionDegrees = (float)bearing,
                isWithinUnlockRadius = distance <= poi.unlockRadiusMeters
            };
        }

        static LandmarkProximity Unavailable(string landmarkId) => new LandmarkProximity
        {
            landmarkId = landmarkId, distanceMetres = float.PositiveInfinity, directionDegrees = 0f, isWithinUnlockRadius = false
        };
    }
}
