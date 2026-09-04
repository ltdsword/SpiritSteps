using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CorgiAR
{
    /// <summary>
    /// Drives the pet body in Manual (joystick / WASD / gamepad) and Automatic
    /// (wander around the player) modes. Interaction — petting or eating —
    /// temporarily suspends movement without cancelling the selected mode.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DogCompanionController : MonoBehaviour
    {
        private enum RoamState { Idle, Walk, Run, Sit }

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string moveActionMap = "Player";
        [SerializeField] private string moveActionName = "Move";

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 0.75f;
        [SerializeField] private float runSpeed = 1.15f;
        [SerializeField] private float turnSpeedDegrees = 360f;
        [SerializeField] private float runSpeedThreshold = 0.75f;

        [Header("Movement boundary")]
        [Tooltip("Maximum X/Z distance from the placement point. Applies only to automatic roaming and chasing a thrown toy/treat — manual (joystick) movement is unrestricted.")]
        [SerializeField] private Vector2 movementHalfExtents = new(4.4f, 3.6f);

        [Header("Automatic roaming (follows the player)")]
        [SerializeField, Min(0.3f)] private float roamRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float arrivalThreshold = 0.12f;
        [SerializeField, Min(0.1f)] private float anchorDriftSlack = 0.5f;
        [SerializeField] private Vector2 idleTimeRange = new(1.6f, 3.6f);
        [SerializeField] private Vector2 sitTimeRange = new(2.5f, 5f);
        [SerializeField, Range(0f, 1f)] private float sitChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float runChance = 0.3f;
        [SerializeField] private float cameraCenterSharpness = 4f;

        [Header("Commands")]
        [SerializeField, Min(0.1f)] private float comeHereStopDistance = 0.6f;
        [SerializeField, Min(0.1f)] private float comeHereWagSeconds = 1f;

        [Header("References")]
        [SerializeField] private DogAnimatorAdapter animatorAdapter;

        private Rigidbody body;
        private Camera movementCamera;
        private ARRaycastManager arRaycastManager;

        // Manual control was removed from the product (PetAr's HUD no longer exposes it) - the
        // companion always roams automatically, in AR and in the Editor desktop preview alike.
        private CompanionControlMode mode = CompanionControlMode.Automatic;
        private InputAction moveAction;
        private Vector2 virtualInput;
        private bool virtualInputActive;

        private bool isPlaced;
        private float groundY;
        private Vector3 movementCenter;
        private float interactionRemaining;
        private float smoothedSpeed01;
        private DogAnimationState desiredAnim = DogAnimationState.Breathing;

        private Vector3 roamAnchor;
        private RoamState roamState = RoamState.Idle;
        private float roamTimer;
        private Vector3 roamTarget;
        private System.Random rng;

        private Transform chaseTarget;
        private bool chaseRun;
        private float chaseStopDistance;

        private bool sitCommanded;
        private bool comeHereActive;
        private float comeHereWagTimer;
        private float moodSpeedMultiplier = 1f;
        private float moodSitBias;

        private readonly List<ARRaycastHit> groundHits = new();

        public bool IsInteracting => interactionRemaining > 0f;
        public bool IsPlaced => isPlaced;
        public bool IsSitting => sitCommanded;
        public DogAnimationState DesiredAnimation => desiredAnim;
        public CompanionControlMode Mode => mode;
        public Rigidbody Body { get { EnsureBody(); return body; } }

        private float MoveSpeed => moveSpeed * moodSpeedMultiplier;
        private float RunSpeed => runSpeed * moodSpeedMultiplier;

        /// <summary>Toggle / set the "sit and stay" command (HUD button, double-tap or long-press).</summary>
        public void ToggleSit() => SetSitCommand(!sitCommanded);

        public void SetSitCommand(bool sit)
        {
            sitCommanded = sit;
            if (sit)
            {
                comeHereActive = false;
                chaseTarget = null;
                desiredAnim = DogAnimationState.Sitting;
            }
            else if (mode == CompanionControlMode.Automatic)
            {
                EnterRoamIdle(0.2f);
            }
            else
            {
                desiredAnim = DogAnimationState.Breathing;
            }
        }

        /// <summary>Call the pet over to wherever the player (camera) is standing.</summary>
        public void ComeHere()
        {
            sitCommanded = false;
            chaseTarget = null;
            comeHereActive = true;
            comeHereWagTimer = comeHereWagSeconds;
        }

        /// <summary>Mood-driven movement modifiers pushed by <see cref="PetMoodController"/>.</summary>
        public void SetMoodModifiers(float speedMultiplier, float sitBias)
        {
            moodSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 2f);
            moodSitBias = Mathf.Clamp01(sitBias);
        }

        /// <summary>
        /// Override both modes and walk toward a world transform (used by feeding:
        /// follow the held treat, then run to where it lands). Cleared with
        /// <see cref="StopChasing"/>.
        /// </summary>
        public void ChaseTarget(Transform target, bool run, float stopDistance = 0.35f)
        {
            chaseTarget = target;
            chaseRun = run;
            chaseStopDistance = Mathf.Max(0.05f, stopDistance);
        }

        public void StopChasing()
        {
            chaseTarget = null;
            if (mode == CompanionControlMode.Automatic)
                EnterRoamIdle(0.15f);
            else
                desiredAnim = DogAnimationState.Breathing;
        }

        private void Awake()
        {
            EnsureBody();
            rng = new System.Random(unchecked(System.Environment.TickCount * 31 + GetInstanceID()));
            if (animatorAdapter == null)
                animatorAdapter = GetComponent<DogAnimatorAdapter>();
        }

        private void OnEnable()
        {
            moveAction = inputActions?.FindActionMap(moveActionMap, false)
                ?.FindAction(moveActionName, false);
            moveAction?.Enable();
        }

        private void OnDisable() => moveAction?.Disable();

        public void ConfigureAR(Camera cameraValue, ARRaycastManager raycastValue)
        {
            EnsureBody();
            movementCamera = cameraValue;
            arRaycastManager = raycastValue;
        }

        private void EnsureBody()
        {
            if (body != null)
                return;
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        public void SetMode(CompanionControlMode value)
        {
            mode = value;
            virtualInput = Vector2.zero;
            virtualInputActive = false;
            sitCommanded = false;
            comeHereActive = false;
            if (mode == CompanionControlMode.Automatic)
                EnterRoamIdle(0.1f);
        }

        public void SetManualInput(Vector2 value, bool active)
        {
            virtualInput = Vector2.ClampMagnitude(value, 1f);
            virtualInputActive = active;
        }

        public void SetPlacement(Pose pose)
        {
            EnsureBody();
            groundY = pose.position.y;
            movementCenter = pose.position;
            body.position = pose.position;
            roamAnchor = CameraGroundPoint(pose.position);
            sitCommanded = false;
            comeHereActive = false;
            EnterRoamIdle(0.2f);
            isPlaced = true;
        }

        public void BeginInteraction(float seconds, DogAnimationState anim = DogAnimationState.WigglingTail)
        {
            interactionRemaining = Mathf.Max(seconds, 0.1f);
            comeHereActive = false;
            if (anim == DogAnimationState.Eating)
                sitCommanded = false;
            animatorAdapter?.Play(anim);
        }

        public void EndInteraction()
        {
            interactionRemaining = 0f;
            smoothedSpeed01 = 0f;
            if (sitCommanded)
                desiredAnim = DogAnimationState.Sitting;
            else if (mode == CompanionControlMode.Automatic)
                EnterRoamIdle(0.2f);
            else
                desiredAnim = DogAnimationState.Breathing;
            ApplyAnimation();
        }

        private void Update()
        {
            if (interactionRemaining > 0f)
            {
                interactionRemaining -= Time.deltaTime;
                if (interactionRemaining <= 0f)
                    EndInteraction();
                return;
            }

            ApplyAnimation();
        }

        private void FixedUpdate()
        {
            if (!isPlaced || interactionRemaining > 0f || movementCamera == null)
                return;

            float dt = Time.fixedDeltaTime;
            Vector3 direction;
            float speed;

            if (sitCommanded)
            {
                desiredAnim = DogAnimationState.Sitting;
                return;
            }

            if (comeHereActive)
            {
                Vector3 anchor = CameraGroundPoint(body.position);
                Vector3 toAnchor = anchor - body.position;
                toAnchor.y = 0f;
                if (toAnchor.magnitude <= comeHereStopDistance)
                {
                    desiredAnim = DogAnimationState.WigglingTail;
                    Vector3 face = movementCamera.transform.position - body.position;
                    face.y = 0f;
                    if (face.sqrMagnitude > 0.0001f)
                        body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                            Quaternion.LookRotation(face.normalized, Vector3.up), turnSpeedDegrees * dt));
                    comeHereWagTimer -= dt;
                    if (comeHereWagTimer <= 0f)
                    {
                        comeHereActive = false;
                        if (mode == CompanionControlMode.Automatic) EnterRoamIdle(0.2f);
                        else desiredAnim = DogAnimationState.Breathing;
                    }
                    return;
                }

                comeHereWagTimer = comeHereWagSeconds;
                desiredAnim = DogAnimationState.Running;
                MoveAlong(toAnchor, RunSpeed, dt);
                return;
            }

            if (chaseTarget != null)
            {
                Vector3 boundedTarget = ClampToMovementBounds(chaseTarget.position);
                Vector3 toTarget = boundedTarget - body.position;
                toTarget.y = 0f;
                float planar = toTarget.magnitude;
                if (planar <= chaseStopDistance)
                {
                    direction = Vector3.zero;
                    speed = 0f;
                    desiredAnim = DogAnimationState.WigglingTail;
                    // still face the treat
                    if (toTarget.sqrMagnitude > 0.0001f)
                        body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                            Quaternion.LookRotation(toTarget.normalized, Vector3.up),
                            turnSpeedDegrees * dt));
                }
                else
                {
                    direction = toTarget;
                    speed = chaseRun ? RunSpeed : MoveSpeed;
                    desiredAnim = chaseRun ? DogAnimationState.Running : DogAnimationState.Walking;
                }
            }
            else if (mode == CompanionControlMode.Manual)
            {
                Vector2 manual = ReadManualInput();
                direction = CameraRelativeDirection(manual);
                speed = MoveSpeed * manual.magnitude;
                float moving = direction.sqrMagnitude > 0.0001f && manual.magnitude > 0.05f ? 1f : 0f;
                smoothedSpeed01 = Mathf.Lerp(smoothedSpeed01, manual.magnitude * moving,
                    1f - Mathf.Exp(-8f * dt));
                desiredAnim = smoothedSpeed01 < 0.05f
                    ? DogAnimationState.Breathing
                    : smoothedSpeed01 < runSpeedThreshold
                        ? DogAnimationState.Walking
                        : DogAnimationState.Running;
            }
            else
            {
                direction = TickRoam(dt, out speed);
            }

            if (speed > 0.0001f && direction.sqrMagnitude > 0.0001f)
                MoveAlong(direction, speed, dt);
        }

        private void MoveAlong(Vector3 direction, float speed, float dt)
        {
            if (speed <= 0.0001f || direction.sqrMagnitude < 0.0001f)
                return;
            // Not bounds-clamped here: manual (joystick) movement, "come here" and
            // chasing a thrown toy/treat all funnel through this helper, and only
            // the latter two should be held to the play-area box (clamped at their
            // target before it reaches here — see the chaseTarget/roam branches
            // in FixedUpdate). Manual movement is intentionally unrestricted.
            Vector3 next = body.position + direction.normalized * (speed * dt);
            if (TryResolveGround(next, out float resolvedY))
                groundY = resolvedY;
            next.y = groundY;
            if ((next - body.position).sqrMagnitude > 0.000001f)
                body.MovePosition(next);
            else
                desiredAnim = DogAnimationState.Breathing;
            body.MoveRotation(Quaternion.RotateTowards(body.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                turnSpeedDegrees * dt));
        }

        // ---- Automatic roaming FSM ----

        private void EnterRoamIdle(float seconds)
        {
            roamState = RoamState.Idle;
            roamTimer = seconds;
            desiredAnim = DogAnimationState.Breathing;
        }

        private Vector3 TickRoam(float dt, out float speed)
        {
            speed = 0f;
            roamAnchor = Vector3.Lerp(roamAnchor, CameraGroundPoint(body.position),
                1f - Mathf.Exp(-cameraCenterSharpness * dt));
            roamTimer -= dt;

            switch (roamState)
            {
                case RoamState.Sit:
                    desiredAnim = DogAnimationState.Sitting;
                    if (roamTimer <= 0f)
                        EnterRoamIdle(Rand(idleTimeRange));
                    return Vector3.zero;

                case RoamState.Walk:
                case RoamState.Run:
                {
                    bool running = roamState == RoamState.Run;
                    speed = running ? RunSpeed : MoveSpeed;
                    desiredAnim = running ? DogAnimationState.Running : DogAnimationState.Walking;

                    if (RoamPlanner.HasArrived(body.position, roamTarget, arrivalThreshold) ||
                        RoamPlanner.TargetOutOfRange(roamAnchor, roamTarget, roamRadius, anchorDriftSlack) ||
                        roamTimer <= 0f)
                    {
                        EnterRoamIdle(Rand(idleTimeRange) * 0.6f);
                        speed = 0f;
                        return Vector3.zero;
                    }

                    Vector3 toTarget = roamTarget - body.position;
                    toTarget.y = 0f;
                    return toTarget;
                }

                default: // Idle
                    desiredAnim = DogAnimationState.Breathing;
                    if (roamTimer <= 0f)
                        ChooseRoamAction();
                    return Vector3.zero;
            }
        }

        private void ChooseRoamAction()
        {
            if (rng.NextDouble() < Mathf.Clamp01(sitChance + moodSitBias))
            {
                roamState = RoamState.Sit;
                roamTimer = Rand(sitTimeRange);
                return;
            }

            roamState = rng.NextDouble() < runChance ? RoamState.Run : RoamState.Walk;
            roamTarget = RoamPlanner.PickTarget(roamAnchor, roamRadius, rng);
            roamTarget = ClampToMovementBounds(roamTarget);
            roamTarget.y = groundY;
            if (TryResolveGround(roamTarget, out float resolvedY))
                roamTarget.y = resolvedY;
            float distance = Vector3.Distance(body.position, roamTarget);
            float speed = roamState == RoamState.Run ? RunSpeed : MoveSpeed;
            roamTimer = Mathf.Clamp(distance / Mathf.Max(0.1f, speed) + 1.5f, 2f, 9f);
        }

        private float Rand(Vector2 range) =>
            Mathf.Lerp(range.x, range.y, (float)rng.NextDouble());

        private Vector3 CameraGroundPoint(Vector3 fallback)
        {
            if (movementCamera == null)
                return new Vector3(fallback.x, groundY, fallback.z);

            // Use the actual centre of the visible ground, not the camera's own
            // X/Z position behind it. This keeps idle roaming readable on screen.
            Ray ray = movementCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (groundPlane.Raycast(ray, out float distance))
                return ClampToMovementBounds(ray.GetPoint(distance));

            return ClampToMovementBounds(new Vector3(fallback.x, groundY, fallback.z));
        }

        private Vector3 ClampToMovementBounds(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x,
                movementCenter.x - movementHalfExtents.x,
                movementCenter.x + movementHalfExtents.x);
            position.z = Mathf.Clamp(position.z,
                movementCenter.z - movementHalfExtents.y,
                movementCenter.z + movementHalfExtents.y);
            return position;
        }


        // ---- shared helpers ----

        private Vector2 ReadManualInput() => virtualInputActive
            ? virtualInput
            : Vector2.ClampMagnitude(moveAction?.ReadValue<Vector2>() ?? Vector2.zero, 1f);

        private Vector3 CameraRelativeDirection(Vector2 input)
        {
            Vector3 forward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 direction = right * input.x + forward * input.y;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private bool TryResolveGround(Vector3 candidate, out float resolvedY)
        {
            resolvedY = groundY;
            if (arRaycastManager == null) return false;
            groundHits.Clear();
            var ray = new Ray(candidate + Vector3.up * 2f, Vector3.down);
            if (!arRaycastManager.Raycast(ray, groundHits,
                    TrackableType.PlaneWithinPolygon) || groundHits.Count == 0)
                return false;
            resolvedY = groundHits[0].pose.position.y;
            return true;
        }

        private void ApplyAnimation() => animatorAdapter?.Play(desiredAnim);
    }
}
