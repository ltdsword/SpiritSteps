using UnityEngine;

namespace ShibaFeeding
{
    /// <summary>
    /// Anything a <see cref="ThrownFood"/> can be thrown to. Implemented by
    /// <see cref="ShibaFeedingController"/> (meadow demo) and by CorgiAR's
    /// DogFeedingController (AR app), so the drag-throw HUD is shared.
    /// </summary>
    public interface IFeedableDog
    {
        bool IsEating { get; }

        /// <summary>World point in front of the dog where a thrown treat should land.</summary>
        Vector3 GetFoodLandingPoint();

        /// <summary>Called while the player holds a treat, so the dog perks up / follows.</summary>
        void BeginFollowingHeldFood(Transform heldFood);

        /// <summary>Called when the player releases (throws or drops) the treat.</summary>
        void EndFollowingHeldFood();

        /// <summary>Try to start eating the given landed treat. False if busy.</summary>
        bool TryEat(ThrownFood food);
    }

    /// <summary>
    /// Optional boundary used by the non-AR meadow. Implementations must be a
    /// transparent no-op when the boundary is inactive so AR keeps its original
    /// tracked-plane throw behaviour.
    /// </summary>
    public interface IThrowBoundary
    {
        bool IsThrowBoundaryActive { get; }
        float ThrowPreviewGroundY { get; }
        Vector3 ConstrainHeldPosition(Camera camera, Vector3 desiredPosition,
            float footprintRadius);
        Vector3 ConstrainLaunchVelocity(Camera camera, Vector3 origin,
            Vector3 initialVelocity, float groundY, float footprintRadius,
            out Vector3 predictedLanding, out bool wasLimited);
        void SetThrowAimActive(bool active);
    }
}
