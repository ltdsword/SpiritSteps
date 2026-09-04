using System;

namespace CorgiAR
{
    /// <summary>Which asset pack a pet's model + clip naming comes from.</summary>
    public enum PetFamily
    {
        /// <summary>"3D Stylized Animated Dogs Kit" — Rigify DEF-* rig, clips <c>&lt;dog&gt;_Breathing</c> … with Sitting/Eating Start-Cycle-End chains.</summary>
        DogKit,
        /// <summary>"Ultimate Animated Animals" — AnimalArmature rig, clips <c>AnimalArmature|Idle</c>, <c>|Walk</c>, <c>|Gallop</c>, <c>|Eating</c>, <c>|Idle_Headlow</c>.</summary>
        UltimateAnimated,
    }

    /// <summary>One selectable pet.</summary>
    [Serializable]
    public struct PetEntry
    {
        public string Id;
        public string DisplayName;
        public PetFamily Family;
        /// <summary>Dog Kit: the .prefab. UAA: the .fbx (loads as a GameObject).</summary>
        public string SourcePrefabPath;
        public string OverrideControllerPath;
        public string ThumbnailPath;
        /// <summary>Multiplier on the model's native localScale (Dog Kit = 1; the Ultimate
        /// Animated Animals are authored ~8× bigger, so they get scaled down).</summary>
        public float Scale;
    }

    /// <summary>
    /// The fixed 17-pet roster: 5 dogs from the Dog Kit + 12 from Ultimate
    /// Animated Animals (<c>uaa_</c> prefix). Source models are never modified;
    /// override controllers, materials and thumbnails are generated into
    /// <c>Assets/CorgiAR/</c> by <c>DogARSetupGenerator</c>.
    /// </summary>
    public static class PetCatalog
    {
        public const string PrefKey = "CorgiAR.Pet";
        public const string DefaultId = "corgi";

        /// <summary>Down-scale for the Ultimate Animated Animals (authored ~8× the Dog Kit).</summary>
        private const float UaaScale = 0.16f;

        private const string KitPrefabs = "Assets/Bublisher/3D Stylized Animated Dogs Kit/Prefabs/";
        private const string KitModels = "Assets/Bublisher/3D Stylized Animated Dogs Kit/Models/";
        private const string UaaModels = "Assets/CorgiAR/Animals/";
        private const string AnimDir = "Assets/CorgiAR/Animation/";
        private const string ThumbDir = "Assets/CorgiAR/UI/Pets/";

        public static readonly PetEntry[] Entries =
        {
            Kit("corgi", "Corgi"),
            Kit("pug", "Pug"),
            Kit("chihuahua", "Chihuahua"),
            Kit("cur", "Shiba (Kit)"),
            Kit("germanshepherd", "Becgiê"),

            Uaa("uaa_fox", "Cáo", "Fox"),
            Uaa("uaa_husky", "Husky", "Husky"),
            Uaa("uaa_wolf", "Sói", "Wolf"),
            Uaa("uaa_shiba", "Shiba", "ShibaInu", kitModel: true),
            Uaa("uaa_alpaca", "Lạc đà Alpaca", "Alpaca"),
            Uaa("uaa_deer", "Nai", "Deer"),
            Uaa("uaa_stag", "Hươu đực", "Stag"),
            Uaa("uaa_donkey", "Lừa", "Donkey"),
            Uaa("uaa_bull", "Bò tót", "Bull"),
            Uaa("uaa_cow", "Bò sữa", "Cow"),
            Uaa("uaa_horse", "Ngựa", "Horse"),
            Uaa("uaa_horse_white", "Ngựa trắng", "Horse_White"),
        };

        private static PetEntry Kit(string id, string name) => new()
        {
            Id = id,
            DisplayName = name,
            Family = PetFamily.DogKit,
            SourcePrefabPath = KitPrefabs + id + ".prefab",
            OverrideControllerPath = AnimDir + "Pet_" + id + ".overrideController",
            ThumbnailPath = ThumbDir + id + ".png",
            Scale = 1f,
        };

        private static PetEntry Uaa(string id, string name, string model, bool kitModel = false) => new()
        {
            Id = id,
            DisplayName = name,
            Family = PetFamily.UltimateAnimated,
            SourcePrefabPath = (kitModel ? KitModels : UaaModels) + model + ".fbx",
            OverrideControllerPath = AnimDir + "Pet_" + id + ".overrideController",
            ThumbnailPath = ThumbDir + id + ".png",
            Scale = UaaScale,
        };

        public static bool TryGet(string id, out PetEntry entry)
        {
            foreach (PetEntry candidate in Entries)
            {
                if (candidate.Id == id)
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        public static PetEntry Resolve(string id) =>
            TryGet(id, out PetEntry entry) ? entry : Entries[0];
    }
}
