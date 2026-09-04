using UnityEngine;
using ShibaFeeding;

namespace CorgiAR
{
    /// <summary>
    /// Touch interaction with the pet. A quick tap or a back-and-forth swipe over
    /// the pet's collider makes it react (tail wag). A <b>double-tap</b> or a
    /// <b>long-press</b> on the pet toggles the "sit and stay" command. Holding a
    /// petting gesture for a couple of seconds makes the pet flop down to rest and
    /// pops a little heart. Works the same in Manual and Automatic mode. The
    /// placement controller feeds pointer events in via <see cref="ProcessTouch"/>;
    /// in the Editor the same path is driven by the left mouse button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DogInteractionController : MonoBehaviour
    {
        [SerializeField] private DogCompanionController companion;
        [SerializeField] private Transform dogRoot;
        [SerializeField] private Camera interactionCamera;

        [Header("Reaction")]
        [SerializeField, Min(0.1f)] private float reactionSeconds = 1.2f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.6f;

        [Header("Petting thresholds")]
        [SerializeField] private float minimumTravelPixels = 70f;
        [SerializeField] private float minimumSegmentPixels = 18f;
        [SerializeField] private double windowSeconds = 1.2;

        [Header("Sit command gesture")]
        [SerializeField, Min(0.05f)] private double doubleTapWindow = 0.4;
        [SerializeField, Min(0.1f)] private double longPressSeconds = 0.6;

        [Header("Rest on long petting")]
        [SerializeField, Min(0.5f)] private float restAfterPettingSeconds = 2.5f;
        [SerializeField, Min(0.5f)] private float restSeconds = 3f;
        [SerializeField, Min(0f)] private float restCooldownSeconds = 4f;
        [SerializeField] private float heartHeight = 0.42f;
        [SerializeField] private Color heartColor = new(1f, 0.42f, 0.62f);
        [SerializeField] private string heartText = "♥ ♥ ♥";

        private PettingDetector touchPetting;
        private bool tapCandidate;
        private float cooldownUntil;

        private double pressTime;
        private Vector2 pressPosition;
        private bool pressOnDog;
        private bool longPressFired;
        private double lastTapTime;

        private float pettingHeldSeconds;
        private float restReadyAt;

        public Transform DogRoot => dogRoot;
        public Camera InteractionCamera => interactionCamera;

        private void Awake()
        {
            touchPetting = new PettingDetector(0.5f, minimumTravelPixels,
                minimumSegmentPixels, windowSeconds);
        }

        public void ConfigureAR(Camera cameraValue) => interactionCamera = cameraValue;

        /// <summary>Feed one pointer event from the placement controller.</summary>
        public void ProcessTouch(Vector2 screenPosition, bool pressed, bool released, double timestamp)
        {
            bool overDog = RaycastDog(screenPosition);

            if (pressed)
            {
                pressTime = timestamp;
                pressPosition = screenPosition;
                pressOnDog = overDog;
                longPressFired = false;
                tapCandidate = overDog;
                pettingHeldSeconds = 0f;
            }

            if (!released)
            {
                if (pressOnDog && !longPressFired && overDog &&
                    timestamp - pressTime >= longPressSeconds &&
                    (screenPosition - pressPosition).magnitude < minimumSegmentPixels * 2f)
                {
                    longPressFired = true;
                    tapCandidate = false;
                    companion?.ToggleSit();
                    return;
                }

                if (touchPetting.AddSample(new PettingSample(screenPosition, overDog, 1f, timestamp)))
                    TriggerPetting();

                if (overDog)
                {
                    pettingHeldSeconds += Time.unscaledDeltaTime;
                    if (pettingHeldSeconds >= restAfterPettingSeconds && Time.unscaledTime >= restReadyAt)
                        TriggerRest();
                }
                else
                {
                    pettingHeldSeconds = 0f;
                }
                return;
            }

            if (!longPressFired && tapCandidate && overDog)
            {
                if (timestamp - lastTapTime <= doubleTapWindow)
                {
                    companion?.ToggleSit();
                    lastTapTime = 0;
                }
                else
                {
                    TriggerPetting();
                    lastTapTime = timestamp;
                }
            }

            tapCandidate = false;
            pettingHeldSeconds = 0f;
            touchPetting.Reset();
        }

        private void TriggerPetting()
        {
            if (Time.unscaledTime < cooldownUntil || companion == null || companion.IsSitting)
                return;
            companion.BeginInteraction(reactionSeconds);
            cooldownUntil = Time.unscaledTime + reactionSeconds + cooldownSeconds;
            touchPetting.Reset();
        }

        private void TriggerRest()
        {
            if (companion == null)
                return;
            companion.BeginInteraction(restSeconds, DogAnimationState.Sitting);
            restReadyAt = Time.unscaledTime + restSeconds + restCooldownSeconds;
            pettingHeldSeconds = 0f;
            touchPetting.Reset();
            SpawnHearts();
        }

        private void SpawnHearts()
        {
            if (dogRoot == null)
                return;
            var popup = new GameObject("Heart Popup");
            popup.transform.position = dogRoot.position + Vector3.up * heartHeight;
            popup.AddComponent<JuicyPopup>().Initialize(heartText, heartColor,
                interactionCamera != null ? interactionCamera : Camera.main);
        }

        private bool RaycastDog(Vector2 screenPosition)
        {
            if (interactionCamera == null || dogRoot == null)
                return false;

            Ray ray = interactionCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionCamera.farClipPlane,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                return false;

            Transform t = hit.collider.transform;
            return t == dogRoot || t.IsChildOf(dogRoot);
        }
    }
}
