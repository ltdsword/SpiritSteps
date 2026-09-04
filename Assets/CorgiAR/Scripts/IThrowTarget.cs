using UnityEngine;

namespace CorgiAR
{
    /// <summary>
    /// Receiver for the drag-and-throw play toy, mirroring
    /// <see cref="ShibaFeeding.IFeedableDog"/> but for "fetch" instead of "eat".
    /// </summary>
    public interface IThrowTarget
    {
        /// <summary>Where a thrown toy should land if the player just lets go.</summary>
        Vector3 GetThrowAnchorPoint();

        /// <summary>The player is holding a toy — start trotting toward it.</summary>
        void BeginAim(Transform heldToy);

        /// <summary>The player released or cancelled the hold.</summary>
        void EndAim();

        /// <summary>A toy landed — run to it, carry it back, drop it. Returns false if busy.</summary>
        bool TryFetch(ThrownToy toy);

        /// <summary>True while a fetch is in progress (blocks pet swap / feeding).</summary>
        bool IsBusy { get; }
    }
}
