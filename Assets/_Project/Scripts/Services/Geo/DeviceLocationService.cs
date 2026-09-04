using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARWalking.UI
{
    /// <summary>
    /// Single shared wrapper around the device GPS, consumed by both RealWalkMetricsProvider and
    /// RealLandmarkMapProvider so only one Input.location session runs. Requests location permission lazily -
    /// only once <see cref="Activate"/> is called (Map root or an active walk), never during onboarding.
    ///
    /// The Editor has no GPS: when running there, this simulates a walk starting from
    /// <see cref="editorSimulatedStart"/> and nudged with the arrow keys (about 8m per key-repeat) so the
    /// real providers can be exercised without a device. See docs/MAP-WALK-PROVIDER-INTEGRATION.md.
    /// </summary>
    public sealed class DeviceLocationService : MonoBehaviour
    {
        // Saigon Central Post Office - also RasterMapView's default center before any real GPS fix arrives,
        // so the map never shows a blank/arbitrary position while waiting for permission + a fix.
        public static readonly GeoPoint DefaultCenter = new GeoPoint(10.7798, 106.6997);

        [SerializeField] float pollIntervalSeconds = 3f;
        [SerializeField] float desiredAccuracyMeters = 10f;
        [SerializeField] float updateDistanceMeters = 5f;
        [SerializeField] GeoPoint editorSimulatedStart = DefaultCenter;

        public bool HasFix { get; private set; }
        public GeoPoint Current { get; private set; }
        public event Action<GeoPoint> OnLocationUpdated;

        bool _active;
        Coroutine _routine;

        /// <summary>Begins requesting permission (if needed) and polling GPS. Safe to call more than once.</summary>
        public void Activate()
        {
            if (_active) return;
            _active = true;
            Current = editorSimulatedStart;
            _routine = StartCoroutine(Application.isEditor ? RunEditorSimulation() : RunRealGps());
        }

        void OnDestroy()
        {
            if (_routine != null) StopCoroutine(_routine);
        }

        IEnumerator RunEditorSimulation()
        {
            HasFix = true;
            OnLocationUpdated?.Invoke(Current);
            while (true)
            {
                const double metersPerDegreeLat = 111320.0;
                var moved = false;
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    double stepDeg = 8.0 / metersPerDegreeLat;
                    double lonScale = 1.0 / Mathf.Max(0.1f, Mathf.Cos((float)(Current.lat * Mathf.Deg2Rad)));
                    if (keyboard.upArrowKey.isPressed) { Current = new GeoPoint(Current.lat + stepDeg, Current.lon); moved = true; }
                    if (keyboard.downArrowKey.isPressed) { Current = new GeoPoint(Current.lat - stepDeg, Current.lon); moved = true; }
                    if (keyboard.rightArrowKey.isPressed) { Current = new GeoPoint(Current.lat, Current.lon + stepDeg * lonScale); moved = true; }
                    if (keyboard.leftArrowKey.isPressed) { Current = new GeoPoint(Current.lat, Current.lon - stepDeg * lonScale); moved = true; }
                }
                if (moved) OnLocationUpdated?.Invoke(Current);
                yield return null;
            }
        }

        IEnumerator RunRealGps()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                bool answered = false, granted = false;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => { granted = true; answered = true; };
                callbacks.PermissionDenied += _ => answered = true;
                callbacks.PermissionDeniedAndDontAskAgain += _ => answered = true;
                Permission.RequestUserPermission(Permission.FineLocation, callbacks);
                while (!answered) yield return null;
                if (!granted) { Debug.LogWarning("[DeviceLocationService] Location permission denied."); yield break; }
            }
#endif
            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[DeviceLocationService] Location services are disabled on this device.");
                yield break;
            }

            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

            var maxWait = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1f);
                maxWait--;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning($"[DeviceLocationService] Location service failed to start: {Input.location.status}");
                yield break;
            }

            while (true)
            {
                var data = Input.location.lastData;
                var point = new GeoPoint(data.latitude, data.longitude);
                if (!HasFix || !point.Equals(Current))
                {
                    Current = point;
                    HasFix = true;
                    OnLocationUpdated?.Invoke(Current);
                }
                yield return new WaitForSeconds(pollIntervalSeconds);
            }
        }
    }
}
