#if UNITY_ANDROID
using UnityEngine;
using UnityEngine.Android;

namespace ARWalking.UI
{
    /// <summary>
    /// Starts/stops WalkTrackingService.java (Assets/Plugins/Android/src/main/java/.../location), the native
    /// foreground service that keeps GPS fixes flowing to AndroidLocationBridge after the screen locks and
    /// Unity's own player loop pauses - Input.location alone stops updating once that happens.
    /// </summary>
    static class AndroidForegroundLocationService
    {
        const string ServiceClass = "com.team06.arwalking.location.WalkTrackingService";

        public static void Start(float minDistanceMeters, float minTimeSeconds)
        {
            RequestNotificationPermissionIfNeeded();
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var serviceClass = new AndroidJavaClass(ServiceClass);
            serviceClass.CallStatic("start", activity, minDistanceMeters, minTimeSeconds);
        }

        public static void Stop()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var serviceClass = new AndroidJavaClass(ServiceClass);
            serviceClass.CallStatic("stop", activity);
        }

        static void RequestNotificationPermissionIfNeeded()
        {
            const string permission = "android.permission.POST_NOTIFICATIONS";
            if (!Permission.HasUserAuthorizedPermission(permission)) Permission.RequestUserPermission(permission);
        }
    }
}
#endif
