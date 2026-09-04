namespace ARWalking.UI
{
    public enum PendingPetInteraction { None, Feed }

    /// <summary>
    /// Handoff params for the single shared AR scene ("PetAr"). Set by
    /// <see cref="UiPrototypeRuntime.EnterPetAr"/> right before
    /// <c>SceneManager.LoadScene("PetAr")</c> and read once by the AR-side glue
    /// (<c>CorgiAR.PetArContextBinder</c>) after the scene loads. Plain static
    /// fields are enough here - the values only need to survive one scene load,
    /// not the app's lifetime.
    /// </summary>
    public static class PetArSceneContext
    {
        public static string PetId;
        public static bool IsPhotoMode;
        public static PendingPetInteraction Interaction;

        /// <summary>Non-null when entering for a Landmark's AR Memory (History/Architecture/
        /// Did-You-Know + Collect Stamp overlay); null for the plain pet-viewing flows.</summary>
        public static string LandmarkId;

        public static UiRootTab ReturnRoot;
    }
}
