using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ShibaFeeding
{
    /// <summary>Owns the dog's reactions and the eating animation sequence.</summary>
    public sealed class ShibaFeedingController : MonoBehaviour, IFeedableDog
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform mouthBone;
        [SerializeField] private AnimationClip eatingEndClip;

        [Header("Animation IDs (Stylized Dogs controller)")]
        [SerializeField] private string animationParameter = "AnimationID";
        [SerializeField] private int idleAnimationId = 0;
        [SerializeField] private int excitedAnimationId = 1;
        [SerializeField] private int walkingAnimationId = 2;
        [SerializeField] private int runningAnimationId = 3;
        [SerializeField] private int sittingAnimationId = 4;
        [SerializeField] private int eatingAnimationId = 5;

        [Header("Eating feel")]
        [SerializeField, Min(0.2f)] private float chewDuration = 3f;
        [SerializeField, Min(0.1f)] private float eatingEndDuration = 1.25f;
        [SerializeField] private Vector3 foodLandingOffset = new Vector3(0f, 0.1f, 0.72f);
        [SerializeField] private Color popupColor = new Color(1f, 0.45f, 0.12f);

        [Header("Follow held food")]
        [SerializeField, Min(0.1f)] private float followStartDistance = 1.85f;
        [SerializeField, Min(0.1f)] private float followStopDistance = 1.05f;
        [SerializeField, Min(0.1f)] private float followSpeed = 1.35f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 7f;
        [SerializeField] private Vector2 roamingLimit = new Vector2(4.2f, 2.8f);

        [Header("Free-time personality")]
        [SerializeField] private Vector2 idleTimeRange = new Vector2(2.5f, 5.5f);
        [SerializeField] private Vector2 sitTimeRange = new Vector2(3f, 6.5f);
        [SerializeField] private Vector2 strollArea = new Vector2(3.2f, 2.15f);
        [SerializeField, Min(0.1f)] private float strollSpeed = 0.72f;
        [SerializeField, Min(0.1f)] private float playfulRunSpeed = 1.25f;

        private Coroutine eatRoutine;
        private int animationParameterHash;
        private bool hasAnimationParameter;
        private PlayableGraph endingGraph;
        private Transform heldFoodTarget;
        private bool isFollowingFood;
        private int currentAnimationId = int.MinValue;
        private LeisureState leisureState;
        private float leisureTimer;
        private Vector3 leisureTarget;

        private enum LeisureState
        {
            Idle,
            Walking,
            Running,
            Sitting,
            WaitingFood
        }

        public bool IsEating { get; private set; }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            animationParameterHash = Animator.StringToHash(animationParameter);
            hasAnimationParameter = HasIntegerParameter(animator, animationParameterHash);
            BeginIdle();
        }

        private void Update()
        {
            if (heldFoodTarget != null)
            {
                UpdateHeldFoodFollowing();
                return;
            }

            if (!IsEating)
                UpdateLeisureBehaviour();
        }

        public Vector3 GetFoodLandingPoint()
        {
            return transform.TransformPoint(foodLandingOffset);
        }

        public void ReactToFood(bool excited)
        {
            if (!IsEating)
                SetAnimation(excited ? excitedAnimationId : idleAnimationId);
        }

        public void BeginFollowingHeldFood(Transform target)
        {
            if (IsEating)
                return;

            heldFoodTarget = target;
            isFollowingFood = false;
            leisureTimer = 0f;
            SetAnimation(excitedAnimationId);
        }

        public void EndFollowingHeldFood()
        {
            heldFoodTarget = null;
            isFollowingFood = false;
            leisureState = LeisureState.WaitingFood;
            leisureTimer = 1.25f;
            if (!IsEating)
                SetAnimation(excitedAnimationId);
        }

        public bool TryEat(ThrownFood food)
        {
            if (IsEating || food == null)
                return false;

            if (eatRoutine != null)
                StopCoroutine(eatRoutine);
            eatRoutine = StartCoroutine(EatSequence(food));
            return true;
        }

        private IEnumerator EatSequence(ThrownFood food)
        {
            IsEating = true;
            heldFoodTarget = null;
            isFollowingFood = false;
            food.BeginBeingEaten(mouthBone != null ? mouthBone : transform);
            SetAnimation(eatingAnimationId);

            yield return new WaitForSeconds(0.55f);
            CreateYumPopup();
            yield return new WaitForSeconds(Mathf.Max(0f, chewDuration - 0.55f));

            SetAnimation(idleAnimationId);
            if (eatingEndClip != null)
            {
                PlayRealEatingEnd();
                yield return new WaitForSeconds(eatingEndClip.length);
                StopEndingGraph();
                animator.Play("Breathing", 0, 0f);
            }
            else
            {
                yield return new WaitForSeconds(eatingEndDuration);
            }
            IsEating = false;
            BeginIdle();
            eatRoutine = null;
        }

        private void PlayRealEatingEnd()
        {
            StopEndingGraph();
            endingGraph = PlayableGraph.Create("Shiba Real EatingEnd");
            endingGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(endingGraph, "EatingEnd", animator);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(endingGraph, eatingEndClip);
            playable.SetApplyFootIK(true);
            output.SetSourcePlayable(playable);
            endingGraph.Play();
        }

        private void StopEndingGraph()
        {
            if (endingGraph.IsValid())
                endingGraph.Destroy();
        }

        private void OnDestroy()
        {
            StopEndingGraph();
        }

        private void SetAnimation(int id)
        {
            if (currentAnimationId == id)
                return;

            currentAnimationId = id;
            if (animator != null && hasAnimationParameter)
                animator.SetInteger(animationParameterHash, id);
        }

        private void UpdateHeldFoodFollowing()
        {
            if (IsEating || heldFoodTarget == null)
                return;

            Vector3 target = heldFoodTarget.position;
            target.y = transform.position.y;
            Vector3 toFood = target - transform.position;
            float distance = toFood.magnitude;

            if (!isFollowingFood && distance > followStartDistance)
                isFollowingFood = true;
            else if (isFollowingFood && distance <= followStopDistance)
                isFollowingFood = false;

            if (!isFollowingFood)
            {
                SetAnimation(excitedAnimationId);
                return;
            }

            Vector3 direction = toFood / Mathf.Max(distance, 0.001f);
            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);

            Vector3 next = Vector3.MoveTowards(transform.position, target, followSpeed * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, -roamingLimit.x, roamingLimit.x);
            next.z = Mathf.Clamp(next.z, -roamingLimit.y, roamingLimit.y);
            transform.position = next;
            SetAnimation(runningAnimationId);
        }

        private void UpdateLeisureBehaviour()
        {
            leisureTimer -= Time.deltaTime;
            switch (leisureState)
            {
                case LeisureState.Walking:
                    MoveLeisurely(strollSpeed, walkingAnimationId);
                    break;
                case LeisureState.Running:
                    MoveLeisurely(playfulRunSpeed, runningAnimationId);
                    break;
                case LeisureState.Sitting:
                    SetAnimation(sittingAnimationId);
                    if (leisureTimer <= 0f)
                        BeginIdle();
                    break;
                case LeisureState.WaitingFood:
                    SetAnimation(excitedAnimationId);
                    if (leisureTimer <= 0f)
                        BeginIdle();
                    break;
                default:
                    SetAnimation(idleAnimationId);
                    if (leisureTimer <= 0f)
                        ChooseNextLeisureAction();
                    break;
            }
        }

        private void MoveLeisurely(float speed, int animationId)
        {
            Vector3 toTarget = leisureTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.08f || leisureTimer <= 0f)
            {
                BeginIdle();
                return;
            }

            Vector3 direction = toTarget.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), turnSpeed * 0.65f * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, leisureTarget, speed * Time.deltaTime);
            SetAnimation(animationId);
        }

        private void ChooseNextLeisureAction()
        {
            float choice = Random.value;
            if (choice < 0.34f)
            {
                leisureState = LeisureState.Sitting;
                leisureTimer = Random.Range(sitTimeRange.x, sitTimeRange.y);
                SetAnimation(sittingAnimationId);
                return;
            }

            leisureState = choice < 0.82f ? LeisureState.Walking : LeisureState.Running;
            leisureTarget = new Vector3(
                Random.Range(-strollArea.x, strollArea.x),
                transform.position.y,
                Random.Range(-strollArea.y, strollArea.y));
            float distance = Vector3.Distance(transform.position, leisureTarget);
            float speed = leisureState == LeisureState.Walking ? strollSpeed : playfulRunSpeed;
            leisureTimer = Mathf.Clamp(distance / Mathf.Max(0.1f, speed) + 1.2f, 2f, 8f);
        }

        private void BeginIdle()
        {
            leisureState = LeisureState.Idle;
            leisureTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            SetAnimation(idleAnimationId);
        }

        private static bool HasIntegerParameter(Animator target, int hash)
        {
            if (target == null)
                return false;

            foreach (AnimatorControllerParameter parameter in target.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Int)
                    return true;
            }
            return false;
        }

        private void CreateYumPopup()
        {
            Vector3 position = mouthBone != null
                ? mouthBone.position + Vector3.up * 0.35f
                : transform.position + Vector3.up * 1.55f;

            GameObject popup = new GameObject("Yum Popup");
            popup.transform.position = position;
            JuicyPopup effect = popup.AddComponent<JuicyPopup>();
            effect.Initialize("NGON QUÁ!  ♥", popupColor, Camera.main);
        }
    }
}
