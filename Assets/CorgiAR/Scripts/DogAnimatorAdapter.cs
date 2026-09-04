using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Single owner of the dog Animator's <c>AnimationID</c> int parameter.
    /// Nothing else in the project should call <see cref="Animator.SetInteger(int,int)"/>
    /// on this Animator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DogAnimatorAdapter : MonoBehaviour
    {
        private static readonly int AnimationId = Animator.StringToHash("AnimationID");

        [SerializeField] private Animator animator;

        public DogAnimationState CurrentState { get; private set; }

        public bool Validate(out string error)
        {
            if (animator == null)
            {
                error = "Dog Animator is missing.";
                return false;
            }

            animator.applyRootMotion = false;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == AnimationId &&
                    parameter.type == AnimatorControllerParameterType.Int)
                {
                    error = string.Empty;
                    return true;
                }
            }

            error = "Animator needs int parameter AnimationID.";
            return false;
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
        }

        private void OnEnable()
        {
            CurrentState = DogAnimationState.Breathing;
            if (animator != null)
                animator.SetInteger(AnimationId, DogAnimationMap.GetAnimationId(CurrentState));
        }

        /// <summary>Re-point at a new Animator after the visible pet was swapped.</summary>
        public void Bind(Animator newAnimator)
        {
            animator = newAnimator;
            if (animator == null)
                return;
            animator.applyRootMotion = false;
            animator.speed = 1f;
            CurrentState = DogAnimationState.Breathing;
            animator.SetInteger(AnimationId, DogAnimationMap.GetAnimationId(CurrentState));
        }

        public bool TryGetSingleEatingClip(out AnimationClip clip)
        {
            clip = null;
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip candidate = clips[i];
                if (candidate != null && candidate.name.EndsWith("|Eating"))
                {
                    clip = candidate;
                    return true;
                }
            }
            return false;
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (animator != null)
                animator.speed = Mathf.Clamp(speed, 0.1f, 3f);
        }

        public void Play(DogAnimationState state)
        {
            if (animator == null || CurrentState == state)
                return;

            CurrentState = state;
            if (state != DogAnimationState.Eating)
                animator.speed = 1f;
            animator.SetInteger(AnimationId, DogAnimationMap.GetAnimationId(state));
            // AnimationID is held constant for the whole state (e.g. Sitting stays
            // at its id the entire time it's sat down); the controller's own
            // Start -> Cycle transition (exit time = 1) plays the one-shot entry
            // clip once and then holds the looping cycle clip, so no per-frame
            // re-triggering or extra "settled" id is needed here.
        }
    }
}
