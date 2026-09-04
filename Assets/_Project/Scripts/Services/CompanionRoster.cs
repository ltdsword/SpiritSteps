namespace ARWalking.UI
{
    /// <summary>
    /// Canonical companion roster and walking-distance unlock thresholds. This is the
    /// authoritative source for save-data seeding (<see cref="PlayerSaveData"/>) and unlock
    /// checks (<see cref="CompanionProgressionService"/>); <c>CompanionUiData.unlockDistanceKilometres</c>
    /// in the UI catalog mirrors these same numbers for on-screen copy.
    /// Ids match <c>CorgiAR.PetCatalog.Entries</c> exactly - see <see cref="PrototypeIds"/>.
    /// </summary>
    public static class CompanionRoster
    {
        public readonly struct Entry
        {
            public readonly string Id;
            /// <summary>Total walking distance required to unlock; 0 = starter.
            /// <see cref="float.PositiveInfinity"/> = never unlocked by distance (Landmark reward only).</summary>
            public readonly float UnlockDistanceKilometres;

            public Entry(string id, float unlockDistanceKilometres)
            {
                Id = id;
                UnlockDistanceKilometres = unlockDistanceKilometres;
            }
        }

        public static readonly Entry[] Entries =
        {
            new Entry(PrototypeIds.Corgi, 0f),
            new Entry(PrototypeIds.Husky, 1f),
            new Entry(PrototypeIds.Fox, 2f),
            new Entry(PrototypeIds.Wolf, 3f),
            new Entry(PrototypeIds.Pug, 4f),
            new Entry(PrototypeIds.Chihuahua, 5f),
            new Entry(PrototypeIds.ShibaKit, 6f),
            new Entry(PrototypeIds.GermanShepherd, 7f),
            new Entry(PrototypeIds.Shiba, 8f),
            new Entry(PrototypeIds.Alpaca, 9f),
            // Deer is a Landmark reward (see LandmarkUiData.companionRewardId), not distance-unlocked.
            new Entry(PrototypeIds.Deer, float.PositiveInfinity),
            new Entry(PrototypeIds.Stag, 10f),
            new Entry(PrototypeIds.Donkey, 12f),
            new Entry(PrototypeIds.Bull, 14f),
            new Entry(PrototypeIds.Cow, 16f),
            new Entry(PrototypeIds.Horse, 18f),
            new Entry(PrototypeIds.HorseWhite, 20f),
        };
    }
}
