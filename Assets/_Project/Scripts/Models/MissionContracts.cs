using System;

namespace ARWalking.UI
{
    /// <summary>
    /// The three mission sources the design doc explicitly says must share one Mission Card
    /// component rather than three separate UI systems (design doc section 48).
    /// </summary>
    public enum MissionType { Tutorial, Walking, Landmark }

    public enum MissionStatus { Active, Completed, Claimed }

    /// <summary>Static definition of one mission (catalog entry) - see <see cref="MissionService"/>.</summary>
    [Serializable]
    public sealed class MissionDefinition
    {
        public string id;
        public MissionType type;
        /// <summary>Icon glyph/emoji shown in the Mission Card's leading tile.</summary>
        public string iconKey;
        public string title;
        public string description;
        /// <summary>Target progress value: steps for the first tutorial mission, metres for
        /// distance-based tutorial/milestone missions. Landmark missions don't use this - their
        /// target is "within unlock radius", evaluated live rather than accumulated.</summary>
        public float targetValue;
        public int rewardCoins;
        /// <summary>Food id granted on claim, or empty for none (e.g. tutorial "Buy a Rice Ball" mission).</summary>
        public string rewardFoodId;
        public int rewardFoodQuantity;
    }

    /// <summary>Per-player progress on one mission id. Persisted on <see cref="PlayerSaveData"/>.</summary>
    [Serializable]
    public sealed class MissionProgressData
    {
        public string missionId;
        public float currentValue;
        public MissionStatus status;
    }

    /// <summary>Display-ready snapshot of the single mission currently shown on the Map's Mission
    /// Card - built fresh each render by <see cref="MissionService.CurrentMission"/> rather than
    /// stored, since it also covers live-evaluated Landmark proximity.</summary>
    [Serializable]
    public sealed class MissionUiState
    {
        public string missionId;
        public MissionType type;
        public string iconKey;
        public string title;
        public string description;
        public float currentValue;
        public float targetValue;
        public MissionStatus status;
        public string rewardLabel;
        /// <summary>For Landmark missions: the landmark to open when the card's action is tapped.</summary>
        public string landmarkId;
        /// <summary>For Landmark missions: the landmark's display name, for the card's action label.</summary>
        public string landmarkName;
    }
}
