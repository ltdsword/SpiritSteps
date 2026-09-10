using System;
using System.Globalization;
using UnityEngine;

namespace ARWalking.UI
{
    /// <summary>
    /// Receives GPS fixes sent by WalkTrackingService.java (Assets/Plugins/Android/src/main/java/.../location)
    /// via UnitySendMessage. The GameObject name here ("AndroidLocationBridge") is hardcoded on the Java side
    /// as its UnitySendMessage target, so it must not change without updating both sides.
    /// </summary>
    public sealed class AndroidLocationBridge : MonoBehaviour
    {
        const string GameObjectName = "AndroidLocationBridge";

        public event Action<GeoPoint> OnFixReceived;

        static AndroidLocationBridge _instance;

        public static AndroidLocationBridge EnsureInstance()
        {
            if (_instance != null) return _instance;
            var go = GameObject.Find(GameObjectName) ?? new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            _instance = go.GetComponent<AndroidLocationBridge>();
            if (_instance == null) _instance = go.AddComponent<AndroidLocationBridge>();
            return _instance;
        }

        // Invoked by WalkTrackingService.onLocationChanged via UnitySendMessage(GameObjectName, "OnNativeLocationFix", "lat,lon").
        void OnNativeLocationFix(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length != 2) return;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;
            OnFixReceived?.Invoke(new GeoPoint(lat, lon));
        }
    }
}
