using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARWalking.UI
{
    /// <summary>
    /// Wraps the platform step counter sensor (InputSystem StepCounter on Android/iOS). Steps are optional and
    /// supplemental per docs/UI-strategy.md - <see cref="HasStepCounter"/> is false whenever no trustworthy count
    /// is available, and callers must not treat 0 as "no steps yet" without checking it.
    ///
    /// In the Editor (no sensor hardware) this simulates steps with the space bar, one step per press.
    /// </summary>
    public sealed class DeviceStepCounterService : MonoBehaviour
    {
        public bool HasStepCounter { get; private set; }
        public int SessionSteps { get; private set; }
        public event Action<int> OnStepCountChanged;

        int _lastSystemStepCount = -1;

        void Start()
        {
            if (Application.isEditor)
            {
                HasStepCounter = true;
                return;
            }

#if UNITY_ANDROID
            CheckAndroidPermission();
#elif UNITY_IOS
            InitializeSensor();
#endif
        }

#if UNITY_ANDROID
        void CheckAndroidPermission()
        {
            const string permission = "android.permission.ACTIVITY_RECOGNITION";
            if (Permission.HasUserAuthorizedPermission(permission)) { InitializeSensor(); return; }
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => InitializeSensor();
            callbacks.PermissionDenied += _ => Debug.LogWarning("[DeviceStepCounterService] Activity recognition permission denied.");
            callbacks.PermissionDeniedAndDontAskAgain += _ => Debug.LogWarning("[DeviceStepCounterService] Activity recognition permission denied.");
            Permission.RequestUserPermission(permission, callbacks);
        }
#endif

        void InitializeSensor()
        {
            if (StepCounter.current == null)
            {
                Debug.LogWarning("[DeviceStepCounterService] No step counter hardware detected.");
                return;
            }
            InputSystem.EnableDevice(StepCounter.current);
            Invoke(nameof(ActivateTracking), 0.5f);
        }

        void ActivateTracking()
        {
            if (StepCounter.current != null && StepCounter.current.enabled) HasStepCounter = true;
            else Debug.LogWarning("[DeviceStepCounterService] Step counter sensor failed to start.");
        }

        void Update()
        {
            if (!HasStepCounter) return;

            if (Application.isEditor)
            {
                if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) IncrementStep(1);
                return;
            }

            if (StepCounter.current == null) return;
            var currentSystemSteps = StepCounter.current.stepCounter.ReadValue();
            if (_lastSystemStepCount == -1) { _lastSystemStepCount = currentSystemSteps; return; }
            if (currentSystemSteps <= _lastSystemStepCount) return;
            IncrementStep(currentSystemSteps - _lastSystemStepCount);
            _lastSystemStepCount = currentSystemSteps;
        }

        void IncrementStep(int stepDelta)
        {
            SessionSteps += stepDelta;
            OnStepCountChanged?.Invoke(SessionSteps);
        }

        /// <summary>Resets the session step count to zero for a fresh walk. Does not touch sensor state.</summary>
        public void ResetSession() => SessionSteps = 0;
    }
}
