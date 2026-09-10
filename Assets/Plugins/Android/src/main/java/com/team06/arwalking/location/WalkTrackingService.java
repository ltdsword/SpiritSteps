package com.team06.arwalking.location;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Build;
import android.os.Bundle;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;

import androidx.core.app.NotificationCompat;

import com.unity3d.player.UnityPlayer;

/**
 * Foreground service that keeps polling GPS via LocationManager independently of Unity's own player loop,
 * which Android pauses once the screen locks even with Application.runInBackground = true. A foreground
 * service (backed by the persistent notification below) is exempt from that suspension, so this keeps
 * receiving fixes with the phone locked in a pocket. Fixes are forwarded to the "AndroidLocationBridge"
 * GameObject; if Unity's loop is currently paused, UnitySendMessage calls simply queue and are processed in
 * order the next time it resumes, so distance accumulated while locked is not lost - just delayed.
 */
public final class WalkTrackingService extends Service implements LocationListener {

    private static final String CHANNEL_ID = "walk_tracking";
    private static final int NOTIFICATION_ID = 4201;
    private static final String UNITY_TARGET_OBJECT = "AndroidLocationBridge";
    private static final String UNITY_TARGET_METHOD = "OnNativeLocationFix";

    private static final String EXTRA_MIN_DISTANCE = "minDistanceMeters";
    private static final String EXTRA_MIN_TIME = "minTimeSeconds";

    private LocationManager locationManager;
    private PowerManager.WakeLock wakeLock;

    public static void start(Context context, float minDistanceMeters, float minTimeSeconds) {
        Intent intent = new Intent(context, WalkTrackingService.class);
        intent.putExtra(EXTRA_MIN_DISTANCE, minDistanceMeters);
        intent.putExtra(EXTRA_MIN_TIME, minTimeSeconds);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.startForegroundService(intent);
        } else {
            context.startService(intent);
        }
    }

    public static void stop(Context context) {
        context.stopService(new Intent(context, WalkTrackingService.class));
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        // Must happen within seconds of startForegroundService(), before any other setup.
        startForegroundNotification();
        float minDistanceMeters = intent != null ? intent.getFloatExtra(EXTRA_MIN_DISTANCE, 5f) : 5f;
        float minTimeSeconds = intent != null ? intent.getFloatExtra(EXTRA_MIN_TIME, 3f) : 3f;
        beginLocationUpdates(minDistanceMeters, minTimeSeconds);
        return START_REDELIVER_INTENT;
    }

    private void startForegroundNotification() {
        NotificationManager manager = getSystemService(NotificationManager.class);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                    CHANNEL_ID, "Walk tracking", NotificationManager.IMPORTANCE_LOW);
            channel.setDescription("Keeps recording your walk distance while the screen is off.");
            manager.createNotificationChannel(channel);
        }

        Intent launchIntent = getPackageManager().getLaunchIntentForPackage(getPackageName());
        int piFlags = Build.VERSION.SDK_INT >= Build.VERSION_CODES.M
                ? PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
                : PendingIntent.FLAG_UPDATE_CURRENT;
        PendingIntent contentIntent = launchIntent != null
                ? PendingIntent.getActivity(this, 0, launchIntent, piFlags)
                : null;

        Notification notification = new NotificationCompat.Builder(this, CHANNEL_ID)
                .setContentTitle("Walk in progress")
                .setContentText("Tracking your distance in the background.")
                .setSmallIcon(android.R.drawable.ic_menu_mylocation)
                .setOngoing(true)
                .setContentIntent(contentIntent)
                .build();

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION);
        } else {
            startForeground(NOTIFICATION_ID, notification);
        }
    }

    private void beginLocationUpdates(float minDistanceMeters, float minTimeSeconds) {
        if (locationManager != null) return; // already running, e.g. Android redelivered the start intent

        PowerManager powerManager = (PowerManager) getSystemService(Context.POWER_SERVICE);
        wakeLock = powerManager.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "SpiritSteps:WalkTracking");
        wakeLock.acquire(12 * 60 * 60 * 1000L); // 12h safety cap in case stop() is ever missed

        locationManager = (LocationManager) getSystemService(Context.LOCATION_SERVICE);
        long minTimeMs = (long) (minTimeSeconds * 1000f);
        try {
            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)) {
                locationManager.requestLocationUpdates(
                        LocationManager.GPS_PROVIDER, minTimeMs, minDistanceMeters, this, Looper.getMainLooper());
            }
            if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                locationManager.requestLocationUpdates(
                        LocationManager.NETWORK_PROVIDER, minTimeMs, minDistanceMeters, this, Looper.getMainLooper());
            }
        } catch (SecurityException e) {
            // Permission was revoked between the C# check and here; nothing usable to do but stop.
            stopSelf();
        }
    }

    @Override
    public void onLocationChanged(Location location) {
        String csv = location.getLatitude() + "," + location.getLongitude();
        UnityPlayer.UnitySendMessage(UNITY_TARGET_OBJECT, UNITY_TARGET_METHOD, csv);
    }

    @Override
    public void onStatusChanged(String provider, int status, Bundle extras) {}

    @Override
    public void onProviderEnabled(String provider) {}

    @Override
    public void onProviderDisabled(String provider) {}

    @Override
    public void onDestroy() {
        if (locationManager != null) {
            locationManager.removeUpdates(this);
            locationManager = null;
        }
        if (wakeLock != null && wakeLock.isHeld()) {
            wakeLock.release();
        }
        super.onDestroy();
    }
}
