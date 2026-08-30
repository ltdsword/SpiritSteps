using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARWalking.UI
{
    [DisallowMultipleComponent]
    public sealed class UiPrototypeRuntime : MonoBehaviour
    {
        public static UiPrototypeRuntime Instance { get; private set; }

        public UiNavigationStack Navigator { get; private set; }
        public IUiDataProvider Data { get; private set; }
        public IMapDataProvider MapData { get; private set; }
        public PrototypeUiAssets Assets { get; private set; }
        public int ActiveWalkSteps { get; set; } = 1248;
        public int ActiveWalkMinutes { get; set; } = 18;
        public int SelectedSpiritIndex { get; set; }
        public int SelectedSeedlingIndex { get; set; }
        public int SelectedLandmarkIndex { get; set; }
        public int SelectedJourneyIndex { get; set; }
        public int SavedPhotoCount { get; set; }
        public bool HasCompletedOnboarding { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeScene()
        {
            EnsureExists();
        }

        public static UiPrototypeRuntime EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var existing = FindFirstObjectByType<UiPrototypeRuntime>();
            if (existing != null)
                return existing;

            var go = new GameObject("UiPrototypeRuntime");
            return go.AddComponent<UiPrototypeRuntime>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Navigator = new UiNavigationStack();
            var catalog = Resources.Load<PrototypeUiCatalog>("UI/PrototypeUiCatalog");
            Assets = Resources.Load<PrototypeUiAssets>("UI/PrototypeUiAssets");
            if (catalog == null)
            {
                Debug.LogError("PrototypeUiCatalog is missing from Resources/UI.");
                enabled = false;
                return;
            }

            Data = new StaticUiDataProvider(catalog);
            MapData = new StaticMapDataProvider(catalog);
        }

        public void EnterWalkScene()
        {
            Navigator.SwitchRoot(UiRootTab.WalkAr);
            SceneManager.LoadScene("Walk");
        }

        public void ReturnHome(UiRootTab root = UiRootTab.Map)
        {
            Navigator.SwitchRoot(root);
            SceneManager.LoadScene("Home");
        }
    }
}
