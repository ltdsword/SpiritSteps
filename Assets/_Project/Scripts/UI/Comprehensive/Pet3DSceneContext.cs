namespace ARWalking.UI
{
    /// <summary>One-shot launch data for the standalone, non-AR pet playground.</summary>
    public static class Pet3DSceneContext
    {
        public const string SceneName = "SampleScene";
        public const string PetPreferenceKey = "CorgiAR.Pet";

        public static bool IsActive { get; private set; }
        public static string PetId { get; private set; }
        public static UiRootTab ReturnRoot { get; private set; } = UiRootTab.Companions;

        public static void Begin(string petId, UiRootTab returnRoot)
        {
            PetId = petId;
            ReturnRoot = returnRoot;
            IsActive = true;
        }

        public static void Clear()
        {
            IsActive = false;
            PetId = null;
            ReturnRoot = UiRootTab.Companions;
        }
    }
}
