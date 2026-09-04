using System;
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
        readonly DeviceLocationService _location;
        readonly DeviceStepCounterService _stepCounter;

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
            _location.Activate();
            _distanceMeters = 0;
            _lastFix = _location.HasFix ? _location.Current : (GeoPoint?)null;
            _startedAtRealtime = Time.realtimeSinceStartup;
            _stepCounter.ResetSession();
            _location.OnLocationUpdated += OnLocationUpdated;
            IsWalking = true;
        }

        public WalkMetrics GetLiveMetrics() => IsWalking ? Snapshot() : new WalkMetrics();

        public WalkMetrics StopWalk()
        {
            if (!IsWalking) throw new InvalidOperationException("A walk must be started before it can be stopped.");
            var result = Snapshot();
            _location.OnLocationUpdated -= OnLocationUpdated;
            IsWalking = false;
            return result;
        }

        void OnLocationUpdated(GeoPoint point)
        {
            if (_lastFix.HasValue) _distanceMeters += GeoMath.HaversineMeters(_lastFix.Value, point);
            _lastFix = point;
        }

        WalkMetrics Snapshot() => new WalkMetrics
        {
            distanceKilometres = (float)(_distanceMeters / 1000.0),
            hasSteps = _stepCounter.HasStepCounter,
            steps = _stepCounter.HasStepCounter ? _stepCounter.SessionSteps : 0,
            elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startedAtRealtime)
        };
    }
}
