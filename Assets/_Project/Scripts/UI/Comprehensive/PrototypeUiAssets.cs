using UnityEngine;
using UnityEngine.Serialization;

namespace ARWalking.UI
{
    [CreateAssetMenu(fileName = "PrototypeUiAssets", menuName = "AR Walking/UI Asset Library")]
    public sealed class PrototypeUiAssets : ScriptableObject
    {
        public Texture2D illustratedMap;
        public Texture2D arScene;
        [FormerlySerializedAs("journalOne")]
        public Texture2D journeyOne;
        [FormerlySerializedAs("journalTwo")]
        public Texture2D journeyTwo;
        [FormerlySerializedAs("spirits")]
        public Texture2D[] companions;
        [FormerlySerializedAs("seedlings")]
        public Texture2D[] archivedPlantPlaceholders;
        public Texture2D[] landmarks;
        public Texture2D[] icons;
        public Texture2D iconAr;
        public Texture2D iconBack;
        public Texture2D iconCalendar;
        public Texture2D iconCamera;
        public Texture2D iconClose;
        public Texture2D iconCompass;
        public Texture2D iconHelp;
        public Texture2D iconJourney;
        public Texture2D iconLocation;
        public Texture2D iconMap;
        public Texture2D iconCompanions;
        public Texture2D iconSettings;
        public Texture2D iconShop;
        public Texture2D iconSteps;

        public Texture2D Companion(int index) => companions != null && companions.Length > 0 ? companions[Mathf.Clamp(index, 0, companions.Length - 1)] : null;
        public Texture2D Landmark(int index) => landmarks != null && landmarks.Length > 0 ? landmarks[Mathf.Clamp(index, 0, landmarks.Length - 1)] : null;
        public Texture2D Icon(int index) => icons != null && icons.Length > 0 ? icons[Mathf.Clamp(index, 0, icons.Length - 1)] : null;
    }
}
