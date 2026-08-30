using UnityEngine;

namespace ARWalking.UI
{
    [CreateAssetMenu(fileName = "PrototypeUiAssets", menuName = "AR Walking/UI Asset Library")]
    public sealed class PrototypeUiAssets : ScriptableObject
    {
        public Texture2D illustratedMap;
        public Texture2D arScene;
        public Texture2D journalOne;
        public Texture2D journalTwo;
        public Texture2D[] spirits;
        public Texture2D[] seedlings;
        public Texture2D[] landmarks;
        public Texture2D[] icons;

        public Texture2D Spirit(int index) => spirits != null && spirits.Length > 0 ? spirits[Mathf.Clamp(index, 0, spirits.Length - 1)] : null;
        public Texture2D Seedling(int index) => seedlings != null && seedlings.Length > 0 ? seedlings[Mathf.Clamp(index, 0, seedlings.Length - 1)] : null;
        public Texture2D Landmark(int index) => landmarks != null && landmarks.Length > 0 ? landmarks[Mathf.Clamp(index, 0, landmarks.Length - 1)] : null;
        public Texture2D Icon(int index) => icons != null && icons.Length > 0 ? icons[Mathf.Clamp(index, 0, icons.Length - 1)] : null;
    }
}
