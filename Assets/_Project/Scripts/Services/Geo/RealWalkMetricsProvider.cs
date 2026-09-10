using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Real IWalkMetricsProvider: distance is accumulated from consecutive DeviceLocationService GPS fixes
    /// (Haversine sum), steps come from DeviceStepCounterService when available. See
    /// docs/MAP-WALK-PROVIDER-INTEGRATION.md for the contract this fulfils.
    /// </summary>
    public sealed class RealWalkMetricsProvider : IWalkMetricsProvider
    {
        // Keeps the displayed trail sparse - a real GPS fix already arrives at most every few seconds, but the
        // Editor simulation fires on every frame a movement key is held, which would otherwise flood the trail.
        const double MinTrailPointSpacingMeters = 3.0;

        readonly DeviceLocationService _location;
        readonly DeviceStepCounterService _stepCounter;
        readonly List<GeoPoint> _trail = new List<GeoPoint>();

        double _distanceMeters;
        GeoPoint? _lastFix;
        float _startedAtRealtime;

        public bool IsWalking { get; private set; }

        public RealWalkMetricsProvider(DeviceLocationService location, DeviceStepCounterService stepCounter)
        {
            _location = location ?? throw new ArgumentNullException(nameof(location));
            _stepCounter = stepCounter ?? throw new ArgumentNullException(nameof(stepCounter));
        }

        public void StartWalk()
        {
            if (IsWalking) return;
            _location.StartBackgroundTracking();
            _distanceMeters = 0;
            _lastFix = _location.HasFix ? _location.Current : (GeoPoint?)null;
            _startedAtRealtime = Time.realtimeSinceStartup;
            _stepCounter.ResetSession();
            _trail.Clear();
            if (_location.HasFix) _trail.Add(_location.Current);
            _location.OnLocationUpdated += OnLocationUpdated;
            IsWalking = true;
        }

        public WalkMetrics GetLiveMetrics() => IsWalking ? Snapshot() : new WalkMetrics();

        public WalkMetrics StopWalk()
        {
            if (!IsWalking) throw new InvalidOperationException("A walk must be started before it can be stopped.");
            var result = Snapshot();
            _location.OnLocationUpdated -= OnLocationUpdated;
            _location.StopBackgroundTracking();
            IsWalking = false;
            return result;
        }

        void OnLocationUpdated(GeoPoint point)
        {
            if (_lastFix.HasValue) _distanceMeters += GeoMath.HaversineMeters(_lastFix.Value, point);
            _lastFix = point;

            var lastTrailPoint = _trail.Count > 0 ? _trail[_trail.Count - 1] : (GeoPoint?)null;
            if (GeoMath.ShouldAppendTrailPoint(lastTrailPoint, point, MinTrailPointSpacingMeters)) _trail.Add(point);
        }

        WalkMetrics Snapshot() => new WalkMetrics
        {
            distanceKilometres = (float)(_distanceMeters / 1000.0),
            hasSteps = _stepCounter.HasStepCounter,
            steps = _stepCounter.HasStepCounter ? _stepCounter.SessionSteps : 0,
            elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startedAtRealtime),
            trail = _trail.ToArray()
        };
    }
}
