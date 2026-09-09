namespace ARWalking.UI
{
    /// <summary>Rarity tier - drives the rarity pill color/label in Shop and Companion Detail
    /// (design doc "Balance 17 Pets" table) and, indirectly, how far apart companions unlock.</summary>
    public enum CompanionRarity { Starter, Common, Rare, Epic, Legendary }

    /// <summary>
    /// Canonical companion roster: walking-distance unlock thresholds, coin price, base walking
    /// income, and growth EXP thresholds. This is the authoritative source for save-data seeding
    /// (<see cref="PlayerSaveData"/>) and unlock/purchase checks (<see cref="CompanionProgressionService"/>);
    /// <c>CompanionUiData.unlockDistanceKilometres</c> in the UI catalog mirrors these same numbers
    /// for on-screen copy. Numbers follow docs/AR-Walking-Cultural-Exploration-Game-Progression-Shop-Tutorial-Landmark-Journey-Design.md
    /// section 6 ("Balance 17 Pets"), mapped onto the existing 17-companion species order.
    /// Ids match <c>CorgiAR.PetCatalog.Entries</c> exactly - see <see cref="PrototypeIds"/>.
    /// </summary>
    public static class CompanionRoster
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly CompanionRarity Rarity;
            /// <summary>Total walking distance required to unlock; 0 = starter.
            /// <see cref="float.PositiveInfinity"/> = never unlocked by distance (Landmark reward only).</summary>
            public readonly float UnlockDistanceKilometres;
            /// <summary>Coin price to purchase once distance-unlocked; 0 for the free starter and for
            /// Landmark-reward companions (which are never purchased with coins).</summary>
            public readonly int PriceCoins;
            /// <summary>Base walking income at Baby growth, in coins per 100 metres walked while this
            /// companion is the player's lead/active companion. See <see cref="CompanionProgressionService.IncomeOf"/>
            /// for the growth-multiplied value.</summary>
            public readonly float BaseIncomePerHundredMetres;
            /// <summary>Growth EXP required to reach Young.</summary>
            public readonly int YoungExp;
            /// <summary>Growth EXP required to reach Adult.</summary>
            public readonly int AdultExp;

            public Entry(string id, CompanionRarity rarity, float unlockDistanceKilometres, int priceCoins,
                float baseIncomePerHundredMetres, int youngExp, int adultExp)
            {
                Id = id;
                Rarity = rarity;
                UnlockDistanceKilometres = unlockDistanceKilometres;
                PriceCoins = priceCoins;
                BaseIncomePerHundredMetres = baseIncomePerHundredMetres;
                YoungExp = youngExp;
                AdultExp = adultExp;
            }
        }

        public static readonly Entry[] Entries =
        {
            new Entry(PrototypeIds.Corgi, CompanionRarity.Starter, 0f, 0, 4.0f, 40, 80),
            new Entry(PrototypeIds.Husky, CompanionRarity.Common, 1f, 100, 4.5f, 50, 100),
            new Entry(PrototypeIds.Fox, CompanionRarity.Common, 2f, 150, 5.0f, 60, 120),
            new Entry(PrototypeIds.Wolf, CompanionRarity.Common, 4f, 220, 5.5f, 70, 140),
            new Entry(PrototypeIds.Pug, CompanionRarity.Common, 7f, 320, 6.0f, 80, 160),
            new Entry(PrototypeIds.Chihuahua, CompanionRarity.Rare, 11f, 450, 6.5f, 100, 220),
            new Entry(PrototypeIds.ShibaKit, CompanionRarity.Rare, 16f, 600, 7.0f, 120, 270),
            new Entry(PrototypeIds.GermanShepherd, CompanionRarity.Rare, 22f, 800, 7.5f, 140, 310),
            new Entry(PrototypeIds.Shiba, CompanionRarity.Rare, 29f, 1050, 8.0f, 160, 350),
            new Entry(PrototypeIds.Alpaca, CompanionRarity.Rare, 37f, 1350, 9.0f, 180, 400),
            // Deer is a Landmark reward (see LandmarkUiData.companionRewardId), not distance-unlocked or purchased.
            new Entry(PrototypeIds.Deer, CompanionRarity.Rare, float.PositiveInfinity, 0, 6.5f, 100, 220),
            new Entry(PrototypeIds.Stag, CompanionRarity.Epic, 46f, 1700, 10.0f, 210, 480),
            new Entry(PrototypeIds.Donkey, CompanionRarity.Epic, 56f, 2100, 11.0f, 240, 550),
            new Entry(PrototypeIds.Bull, CompanionRarity.Epic, 67f, 2550, 12.0f, 270, 620),
            new Entry(PrototypeIds.Cow, CompanionRarity.Epic, 79f, 3050, 13.0f, 300, 690),
            new Entry(PrototypeIds.Horse, CompanionRarity.Legendary, 92f, 3600, 14.0f, 330, 825),
            new Entry(PrototypeIds.HorseWhite, CompanionRarity.Legendary, 106f, 4200, 16.0f, 360, 900),
        };

        public static Entry Find(string id)
        {
            foreach (var entry in Entries) if (entry.Id == id) return entry;
            return default;
        }
    }
}
