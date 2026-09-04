#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ARWalking.UI;
using Unity.AppUI.Core;
using Unity.AppUI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARWalking.Editor
{
    public static class ARWalkingUiPrototypeSetup
    {
        const string ResourceFolder = "Assets/_Project/Resources/UI";

        [MenuItem("Tools/AR Walking/Build Animal Companion Prototype")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder(ResourceFolder);
            ConfigureAppUi();
            var panelSettings = CreatePanelSettings();
            var arPanelSettings = CreateArPanelSettings();
            CreateCatalog();
            CreateAssetLibrary();
            CreateNavigationGraph();
            ConfigureTextures();
            ConfigureScene("Assets/_Project/Scenes/Home.unity", panelSettings, true);
            ConfigureScene("Assets/_Project/Scenes/PetAr.unity", arPanelSettings, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AR Walking animal companion prototype regenerated successfully.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void ConfigureAppUi()
        {
            const string path = ResourceFolder + "/ARWalkingAppUISettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<AppUISettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<AppUISettings>(); AssetDatabase.CreateAsset(settings, path); }
            settings.editorOnly = false;
            // Keep the explicitly authored ScaleWithScreenSize contract stable across Editor and
            // device runs; physical/DPI-based correction makes mobile proportions unpredictable.
            settings.autoCorrectUiScale = false;
            settings.includeShadersInPlayerBuild = true;
            settings.autoOverrideAndroidManifest = true;
            EditorUtility.SetDirty(settings);
            EditorBuildSettings.AddConfigObject("com.unity.dt.app-ui", settings, true);
            RemoveDefine(NamedBuildTarget.Android, "APP_UI_EDITOR_ONLY");
            RemoveDefine(NamedBuildTarget.Standalone, "APP_UI_EDITOR_ONLY");
        }

        static void RemoveDefine(NamedBuildTarget target, string define)
        {
            var values = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => value != define).Distinct();
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", values));
        }

        static PanelSettings CreatePanelSettings()
        {
            const string path = ResourceFolder + "/ARWalkingPanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(settings, path); }
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.scale = 1f;
            settings.referenceResolution = new Vector2Int(720, 1600);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.referenceDpi = 160f;
            settings.fallbackDpi = 160f;
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.976f, 0.961f, 0.914f, 1f);
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss");
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// PanelSettings for the PetAr scene's overlay (WalkUiController): must NOT clear to an
        /// opaque color like ARWalkingPanelSettings does for Home, or that opaque fill paints over
        /// the real AR camera feed and the CorgiAR uGUI HUD underneath every frame, regardless of
        /// individual VisualElement transparency.
        /// </summary>
        static PanelSettings CreateArPanelSettings()
        {
            const string path = ResourceFolder + "/ARWalkingArPanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(settings, path); }
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.scale = 1f;
            settings.referenceResolution = new Vector2Int(720, 1600);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.referenceDpi = 160f;
            settings.fallbackDpi = 160f;
            settings.clearColor = false;
            settings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss");
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // Display name for each CompanionRoster id - mirrors CorgiAR.PetCatalog.Entries[].DisplayName
        // (English equivalents for the dog-breed / model names; ARWalking cannot reference CorgiAR
        // directly, see PrototypeIds, so this table is kept in sync by hand).
        static readonly Dictionary<string, string> CompanionDisplayNames = new Dictionary<string, string>
        {
            { PrototypeIds.Corgi, "Corgi" },
            { PrototypeIds.Pug, "Pug" },
            { PrototypeIds.Chihuahua, "Chihuahua" },
            { PrototypeIds.ShibaKit, "Shiba Inu (Kit)" },
            { PrototypeIds.GermanShepherd, "German Shepherd" },
            { PrototypeIds.Fox, "Fox" },
            { PrototypeIds.Husky, "Husky" },
            { PrototypeIds.Wolf, "Wolf" },
            { PrototypeIds.Shiba, "Shiba Inu" },
            { PrototypeIds.Alpaca, "Alpaca" },
            { PrototypeIds.Deer, "Deer" },
            { PrototypeIds.Stag, "Stag" },
            { PrototypeIds.Donkey, "Donkey" },
            { PrototypeIds.Bull, "Bull" },
            { PrototypeIds.Cow, "Cow" },
            { PrototypeIds.Horse, "Horse" },
            { PrototypeIds.HorseWhite, "White Horse" },
        };

        static void CreateCatalog()
        {
            const string path = ResourceFolder + "/PrototypeUiCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<PrototypeUiCatalog>(path);
            var isNew = catalog == null;
            if (isNew) { catalog = ScriptableObject.CreateInstance<PrototypeUiCatalog>(); AssetDatabase.CreateAsset(catalog, path); }

            var matchesCurrentRoster = !isNew && catalog.companions != null &&
                catalog.companions.Count == CompanionRoster.Entries.Length &&
                catalog.companions.TrueForAll(c => c != null && Array.Exists(CompanionRoster.Entries, e => e.Id == c.id));
            if (!isNew && catalog.companions != null && catalog.companions.Count > 0 && matchesCurrentRoster)
            {
                // The catalog already matches the current roster (likely hand-edited with real copy
                // on top of it). Re-running this tool must not silently overwrite it. Delete
                // PrototypeUiCatalog.asset first to fully regenerate from code.
                Debug.Log("PrototypeUiCatalog already matches the current roster; leaving companions/foods/landmarks/map/markers untouched.");
                return;
            }

            catalog.companions = new List<CompanionUiData>();
            foreach (var entry in CompanionRoster.Entries)
            {
                var starter = entry.UnlockDistanceKilometres <= 0f;
                var landmarkOnly = float.IsPositiveInfinity(entry.UnlockDistanceKilometres);
                var name = CompanionDisplayNames.TryGetValue(entry.Id, out var displayName) ? displayName : entry.Id;
                catalog.companions.Add(new CompanionUiData
                {
                    id = entry.Id,
                    name = name,
                    description = starter
                        ? "A loyal starter who gains Growth EXP from completed kilometres and food."
                        : landmarkOnly
                            ? "A companion unlocked by completing the Central Post Office AR Memory."
                            : "A companion unlocked after " + entry.UnlockDistanceKilometres.ToString("0.#") + " km of total walking distance.",
                    imageKey = entry.Id,
                    unlockHint = starter
                        ? "Starter companion"
                        : landmarkOnly
                            ? "Complete the Central Post Office AR Memory"
                            : "Walk " + entry.UnlockDistanceKilometres.ToString("0.#") + " km total to unlock",
                    unlockDistanceKilometres = landmarkOnly ? 0f : entry.UnlockDistanceKilometres
                });
            }
            catalog.foods = new List<FoodUiData>
            {
                new FoodUiData { id="basic-food", name="Basic Food", coinCost=20, growthExperience=20, description="A simple snack for any unlocked companion." },
                new FoodUiData { id="better-food", name="Better Food", coinCost=40, growthExperience=40, description="A larger meal for any unlocked companion." }
            };
            catalog.landmarks = new List<LandmarkUiData>
            {
                new LandmarkUiData { id=PrototypeIds.IndependencePalace, name="Independence Palace", localName="Dinh Doc Lap", history="A major Ho Chi Minh City landmark associated with pivotal events in modern Vietnamese history.", architecture="The building is known for its modernist composition, shaded facades, and broad ceremonial spaces.", didYouKnow="Its grounds form a large green landmark in the centre of District 1.", imageKey="independence-palace" },
                new LandmarkUiData { id=PrototypeIds.CentralPostOffice, name="Central Post Office", localName="Buu dien Trung tam Sai Gon", history="Built in the late nineteenth century, the post office has connected residents and travellers across generations.", architecture="Its long vaulted hall, patterned tile floor, and arched windows create a bright civic interior.", didYouKnow="The building still operates as a post office while welcoming visitors.", imageKey="post-office", imageTargetReady=true, companionRewardId=PrototypeIds.Deer },
                new LandmarkUiData { id=PrototypeIds.NotreDameBasilica, name="Notre-Dame Basilica", localName="Nha tho Duc Ba Sai Gon", history="The basilica is a familiar historic landmark beside the Central Post Office in District 1.", architecture="Red brick walls and two bell towers define its prominent silhouette on the square.", didYouKnow="Its central location makes it a common meeting point and city reference.", imageKey="notre-dame" }
            };
            catalog.map = new IllustratedMapUiData { textureKey="hcm-illustrated", regionName="District 1, Ho Chi Minh City", minimumZoom=1f, maximumZoom=2.8f, initialFocus=new Vector2(0.5f, 0.5f) };
            catalog.markers = new List<MapMarkerUiData>
            {
                Marker("player", MapMarkerType.Player, "Mock player position", .48f, .58f, string.Empty),
                Marker("palace", MapMarkerType.Landmark, "Independence Palace", .32f, .45f, PrototypeIds.IndependencePalace),
                Marker("post-office", MapMarkerType.Landmark, "Central Post Office", .62f, .42f, PrototypeIds.CentralPostOffice),
                Marker("basilica", MapMarkerType.Landmark, "Notre-Dame Basilica", .55f, .36f, PrototypeIds.NotreDameBasilica)
            };
            EditorUtility.SetDirty(catalog);
        }

        static MapMarkerUiData Marker(string id, MapMarkerType type, string label, float x, float y, string target) =>
            new MapMarkerUiData { id=id, type=type, label=label, normalizedPosition=new Vector2(x, y), targetId=target };

        static void CreateAssetLibrary()
        {
            const string path = ResourceFolder + "/PrototypeUiAssets.asset";
            var library = AssetDatabase.LoadAssetAtPath<PrototypeUiAssets>(path);
            if (library == null) { library = ScriptableObject.CreateInstance<PrototypeUiAssets>(); AssetDatabase.CreateAsset(library, path); }
            library.illustratedMap = Texture("Assets/_Project/Art/UI/Maps/HoChiMinhCity_IllustratedMap_2048.png");
            library.arScene = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/ar-park.png");
            library.journeyOne = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/journal-1.png");
            library.journeyTwo = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/journal-2.png");
            // One thumbnail per CompanionRoster entry, in the same order as catalog.companions -
            // PrototypeUiAssets.Companion(index) is purely positional.
            library.companions = CompanionRoster.Entries
                .Select(entry => Texture("Assets/CorgiAR/UI/Pets/" + entry.Id + ".png")).ToArray();
            library.archivedPlantPlaceholders = Textures("Seedlings/commonPelletRedSprout.png", "Seedlings/commonPelletBlueReady.png", "Seedlings/commonPelletYellowSprout.png");
            library.foods = new[]
            {
                Texture("Assets/_Project/Art/UI/Food/rice-ball.png"),
                Texture("Assets/_Project/Art/UI/Food/fruit-bowl.png")
            };
            library.landmarks = Textures("Landmarks/independence-palace.png", "Landmarks/post-office.png", "Landmarks/notre-dame.png");
            library.icons = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/UI/ReferenceTemp/Icons" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value).Select(Texture).ToArray();
            library.iconAr = Icon("icon_AR.png");
            library.iconBack = Icon("icon_Back_Left1.png");
            library.iconCalendar = Icon("icon_Calendar.png");
            library.iconCamera = Icon("icon_Camera.png");
            library.iconClose = Icon("icon_Close.png");
            library.iconCompass = Icon("icon_Compass.png");
            library.iconHelp = Icon("icon_Help.png");
            library.iconJourney = Icon("icon_Lifelog.png");
            library.iconLocation = Icon("icon_MyLocation.png");
            library.iconMap = Icon("icon_Place.png");
            library.iconCompanions = Icon("icon_Seedling.png");
            library.iconSettings = Icon("icon_Setting.png");
            library.iconShop = Icon("icon_Star01_Fill.png");
            library.iconSteps = Icon("icon_Steps_A.png");
            EditorUtility.SetDirty(library);
        }

        static Texture2D[] Textures(params string[] paths) => paths.Select(path => Texture("Assets/_Project/Art/UI/ReferenceTemp/" + path)).ToArray();
        static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        static Texture2D Icon(string fileName) => Texture("Assets/_Project/Art/UI/ReferenceTemp/Icons/" + fileName);

        static void CreateNavigationGraph()
        {
            const string path = ResourceFolder + "/ARWalkingNavigation.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            var asset = ScriptableObject.CreateInstance<NavGraphViewAsset>();
            AssetDatabase.CreateAsset(asset, path);
            var root = ScriptableObject.CreateInstance<NavGraph>();
            root.name = "animal_companion_graph"; root.label = "AR Walking"; asset.AddNode(root);
            var destinations = new Dictionary<UiRoute, NavDestination>();
            for (var i = 0; i < UiRouteCatalog.All.Count; i++)
            {
                var route = UiRouteCatalog.All[i];
                var destination = ScriptableObject.CreateInstance<NavDestination>();
                destination.name = ToRoute(route); destination.label = route.ToString(); destination.parent = root;
                destination.actions = new List<NavAction>(); destination.arguments = new List<Argument>();
                destination.position = new Vector2(300 + i % 4 * 330, 120 + i / 4 * 220);
                destination.destinationTemplate = new DefaultDestinationTemplate
                {
                    template = typeof(NavigationScreen).AssemblyQualifiedName,
                    showBottomNavBar = IsRootRoute(route), showAppBar = route != UiRoute.OnboardingSetup && route != UiRoute.PetAr,
                    showBackButton = !IsRootRoute(route), showDrawer = false, showNavigationRail = false
                };
                asset.AddNode(destination); destinations.Add(route, destination);
            }
            root.startDestination = destinations[UiRoute.HomeMap];
            Link(asset, root, "root_map", destinations[UiRoute.HomeMap]);
            Link(asset, root, "root_companions", destinations[UiRoute.CompanionCollection]);
            Link(asset, root, "root_shop", destinations[UiRoute.ShopFood]);
            Link(asset, root, "root_journey", destinations[UiRoute.JourneyList]);
            Link(asset, destinations[UiRoute.HomeMap], "start_walk", destinations[UiRoute.ActiveWalk]);
            Link(asset, destinations[UiRoute.ActiveWalk], "finish_walk", destinations[UiRoute.WalkResult]);
            Link(asset, destinations[UiRoute.HomeMap], "open_landmark", destinations[UiRoute.LandmarkDetail]);
            Link(asset, destinations[UiRoute.HomeMap], "open_pet_ar", destinations[UiRoute.PetAr]);
            Link(asset, destinations[UiRoute.LandmarkDetail], "open_pet_ar", destinations[UiRoute.PetAr]);
            Link(asset, destinations[UiRoute.CompanionCollection], "open_companion", destinations[UiRoute.CompanionDetail]);
            Link(asset, destinations[UiRoute.CompanionDetail], "open_pet_ar", destinations[UiRoute.PetAr]);
            Link(asset, destinations[UiRoute.JourneyList], "open_journey", destinations[UiRoute.JourneyDetail]);
            Link(asset, destinations[UiRoute.HomeMap], "open_activity_dashboard", destinations[UiRoute.ActivityDashboard]);
            EditorUtility.SetDirty(asset);
        }

        static void Link(NavGraphViewAsset asset, NavGraphViewHierarchicalNode source, string name, NavGraphViewHierarchicalNode destination)
        {
            var action = ScriptableObject.CreateInstance<NavAction>(); action.name = name; action.destination = destination;
            action.defaultArguments = new List<Argument>(); source.actions.Add(action); asset.AddNode(action); EditorUtility.SetDirty(source);
        }

        static string ToRoute(UiRoute route) => string.Concat(route.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
        static bool IsRootRoute(UiRoute route) => route == UiRoute.HomeMap || route == UiRoute.CompanionCollection || route == UiRoute.ShopFood || route == UiRoute.JourneyList;

        static void ConfigureTextures()
        {
            ConfigureTexture("Assets/_Project/Art/UI/Maps/HoChiMinhCity_IllustratedMap_2048.png", false, 2048);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/UI/ReferenceTemp" }))
                ConfigureTexture(AssetDatabase.GUIDToAssetPath(guid), true, 1024);
        }

        static void ConfigureTexture(string path, bool sprite, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaIsTransparency = sprite; importer.mipmapEnabled = false; importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = maxSize; android.format = TextureImporterFormat.ASTC_6x6; android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
        }

        static void ConfigureScene(string path, PanelSettings panelSettings, bool home)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            // PetAr.unity intentionally hosts CorgiAR's uGUI HUD Canvas alongside the UIDocument
            // (memory overlay). Every other scene predates App UI and any leftover Canvas there
            // is dead legacy UI safe to clear.
            if (!path.EndsWith("PetAr.unity"))
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Undo.DestroyObjectImmediate(canvas.gameObject);
            var legacyController = GameObject.Find("UIController");
            if (legacyController != null) Undo.DestroyObjectImmediate(legacyController);
            var root = GameObject.Find("AppUIRoot") ?? new GameObject("AppUIRoot");
            var document = root.GetComponent<UIDocument>() ?? Undo.AddComponent<UIDocument>(root);
            document.panelSettings = panelSettings; document.sortingOrder = 100;
            if (home)
            {
                if (root.GetComponent<HomeUiController>() == null) Undo.AddComponent<HomeUiController>(root);
                var old = root.GetComponent<WalkUiController>(); if (old != null) Undo.DestroyObjectImmediate(old);
            }
            else
            {
                if (root.GetComponent<WalkUiController>() == null) Undo.AddComponent<WalkUiController>(root);
                var old = root.GetComponent<HomeUiController>(); if (old != null) Undo.DestroyObjectImmediate(old);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
