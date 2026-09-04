namespace CorgiAR
{
    /// <summary>
    /// Gameplay-facing pet animation states. Integer values are an internal
    /// concern of <see cref="DogAnimationMap"/>; do not depend on them here.
    /// </summary>
    public enum DogAnimationState
    {
        Breathing,
        WigglingTail,
        Walking,
        Running,
        Sitting,
        Eating
    }

    /// <summary>
    /// Maps a <see cref="DogAnimationState"/> to the integer the generated
    /// <c>PetLocomotion</c> Animator Controller expects on its <c>AnimationID</c>
    /// parameter: 0 Breathing, 1 WigglingTail, 2 Walking, 3 Running, 4 Sitting,
    /// 5 Eating. The Sitting and Eating ids drive Start/Cycle/End state chains.
    /// </summary>
    public static class DogAnimationMap
    {
        public static int GetAnimationId(DogAnimationState state) => state switch
        {
            DogAnimationState.Breathing => 0,
            DogAnimationState.WigglingTail => 1,
            DogAnimationState.Walking => 2,
            DogAnimationState.Running => 3,
            DogAnimationState.Sitting => 4,
            DogAnimationState.Eating => 5,
            _ => 0
        };
    }
}
