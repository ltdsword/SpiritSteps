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
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.U2D;

namespace ARWalking.Editor
{
    public static class ARWalkingUiPrototypeSetup
    {
        const string ResourceFolder = "Assets/_Project/Resources/UI";

        [MenuItem("Tools/AR Walking/Build Comprehensive UI Prototype")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder(ResourceFolder);
            ConfigureAppUi();
            var panelSettings = CreatePanelSettings();
            CreateCatalog();
            CreateAssetLibrary();
            CreateNavigationGraph();
            ConfigureTexturesAndAtlas();
            ConfigureScene("Assets/_Project/Scenes/Home.unity", panelSettings, true);
            ConfigureScene("Assets/_Project/Scenes/Walk.unity", panelSettings, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AR Walking comprehensive UI prototype created successfully.");
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
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AppUISettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.editorOnly = false;
            settings.autoCorrectUiScale = true;
            settings.includeShadersInPlayerBuild = true;
            settings.autoOverrideAndroidManifest = true;
            EditorUtility.SetDirty(settings);
            EditorBuildSettings.AddConfigObject("com.unity.dt.app-ui", settings, true);
            RemoveDefine(NamedBuildTarget.Android, "APP_UI_EDITOR_ONLY");
            RemoveDefine(NamedBuildTarget.Standalone, "APP_UI_EDITOR_ONLY");
        }

        static void RemoveDefine(NamedBuildTarget target, string define)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => value != define)
                .Distinct();
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        static PanelSettings CreatePanelSettings()
        {
            const string path = ResourceFolder + "/ARWalkingPanelSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 2400);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.referenceDpi = 160f;
            settings.fallbackDpi = 160f;
            settings.clearColor = true;
            settings.colorClearValue = new Color(0.976f, 0.961f, 0.914f, 1f);
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss");
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void CreateCatalog()
        {
            const string path = ResourceFolder + "/PrototypeUiCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<PrototypeUiCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PrototypeUiCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.spirits = new List<SpiritUiData>
            {
                new SpiritUiData { id="leaf", name="Linh Hồn Lá", culturalTitle="The Leaf Spirit", description="A curious companion shaped by shaded parks and the rustle of old trees. It remembers the small pauses between busy streets.", imageKey="spirit-leaf", collected=true, isSelected=true },
                new SpiritUiData { id="lotus", name="Linh Hồn Sen", culturalTitle="The Lotus Spirit", description="A calm companion carrying stories of water, summer rain, and patient growth in the heart of the city.", imageKey="spirit-lotus", collected=true },
                new SpiritUiData { id="star", name="Linh Hồn Sao", culturalTitle="The Starlight Spirit", description="A bright companion still waiting to be discovered on an evening journey.", imageKey="spirit-star", collected=false }
            };
            catalog.seedlings = new List<SeedlingUiData>
            {
                new SeedlingUiData { id="tao-dan", name="Tao Đàn Leaf Seedling", locationName="Công viên Tao Đàn", imageKey="seed-red", currentSteps=4268, requiredSteps=5000 },
                new SeedlingUiData { id="turtle-lake", name="Hồ Con Rùa Lotus Seedling", locationName="Hồ Con Rùa", imageKey="seed-blue", currentSteps=5000, requiredSteps=5000, ready=true },
                new SeedlingUiData { id="book-street", name="Đường Sách Star Seedling", locationName="Đường Sách Nguyễn Văn Bình", imageKey="seed-yellow", currentSteps=1830, requiredSteps=6000 }
            };
            catalog.walks = new List<WalkUiData>
            {
                new WalkUiData { id="walk-aug-27", dateLabel="27 Aug 2026", placeName="Quận 1", steps=2146, durationMinutes=31, distanceKilometres=1.8f, discoveries=3 },
                new WalkUiData { id="walk-aug-23", dateLabel="23 Aug 2026", placeName="Tao Đàn", steps=3890, durationMinutes=48, distanceKilometres=2.9f, discoveries=2 }
            };
            catalog.landmarks = new List<LandmarkUiData>
            {
                new LandmarkUiData { id="independence-palace", name="Dinh Độc Lập", subtitle="Independence Palace", memoryText="A modern landmark held within a deep green garden. Its open geometry and shaded grounds remember many layers of Sài Gòn history.", imageKey="independence-palace", discovered=true },
                new LandmarkUiData { id="post-office", name="Bưu điện Trung tâm Sài Gòn", subtitle="Saigon Central Post Office", memoryText="Sunlit arches, patterned floors, and handwritten messages make this a place where journeys have crossed for generations.", imageKey="post-office", discovered=true },
                new LandmarkUiData { id="notre-dame", name="Nhà thờ Đức Bà Sài Gòn", subtitle="Notre-Dame Cathedral Basilica", memoryText="Red brick and twin towers rise above a lively square, a familiar meeting point remembered by many generations.", imageKey="notre-dame", discovered=true },
                new LandmarkUiData { id="temple-literature", name="Văn Miếu", subtitle="Temple of Literature Memory", memoryText="A cultural memory celebrating learning, careful attention, and the stories preserved through study.", imageKey="temple-literature", discovered=false }
            };
            catalog.journeys = new List<JourneyUiData>
            {
                new JourneyUiData { id="green-thread", title="The Green Thread of Quận 1", dateLabel="Thursday · 27 August", summary="A slow route from shaded gardens to the old post office.", imageKey="journal-1", landmarkId="post-office", steps=2146, memories=3 },
                new JourneyUiData { id="summer-rain", title="After the Summer Rain", dateLabel="Sunday · 23 August", summary="Wet leaves, quiet paths, and a lotus seedling ready to wake.", imageKey="journal-2", landmarkId="independence-palace", steps=3890, memories=2 }
            };
            catalog.photographs = new List<PhotoUiData>
            {
                new PhotoUiData { id="photo-leaf-park", title="Linh Hồn Lá in the park", dateLabel="27 Aug 2026", imageKey="ar-park", saved=true },
                new PhotoUiData { id="photo-lotus-rain", title="Linh Hồn Sen after rain", dateLabel="23 Aug 2026", imageKey="journal-2", saved=true }
            };
            catalog.collectibles = new List<CollectibleUiData>
            {
                new CollectibleUiData { id="stamp-post", name="Bưu điện Postmark", category="City stamp", imageKey="memory-fragment", collected=true },
                new CollectibleUiData { id="lotus-fragment", name="Mảnh Ký Ức Hoa Sen", category="Memory fragment", imageKey="memory-fragment", collected=true },
                new CollectibleUiData { id="book-street-card", name="Đường Sách Card", category="Place card", imageKey="post-office", collected=false },
                new CollectibleUiData { id="rain-note", name="Ghi Chú Mưa Hè", category="Journal decoration", imageKey="journal-2", collected=true }
            };
            catalog.map = new IllustratedMapUiData { textureKey="hcm-illustrated", regionName="Quận 1 · Thành phố Hồ Chí Minh", minimumZoom=1f, maximumZoom=2.8f, initialFocus=new Vector2(0.5f, 0.5f) };
            catalog.markers = new List<MapMarkerUiData>
            {
                Marker("player", MapMarkerType.PlayerSpirit, "You and Linh Hồn Lá", .48f, .58f, "leaf"),
                Marker("palace", MapMarkerType.Landmark, "Dinh Độc Lập", .32f, .45f, "independence-palace"),
                Marker("post", MapMarkerType.Landmark, "Bưu điện Trung tâm Sài Gòn", .62f, .42f, "post-office"),
                Marker("cathedral", MapMarkerType.Landmark, "Nhà thờ Đức Bà Sài Gòn", .55f, .36f, "notre-dame"),
                Marker("seed-tao-dan", MapMarkerType.Seedling, "Tao Đàn seedling", .22f, .62f, "tao-dan"),
                Marker("seed-turtle", MapMarkerType.Seedling, "Hồ Con Rùa seedling", .68f, .23f, "turtle-lake"),
                Marker("collectible-postmark", MapMarkerType.CulturalCollectible, "Bưu điện postmark", .70f, .50f, "stamp-post"),
                Marker("ar-park", MapMarkerType.ArDiscoveryHint, "Companion moment", .39f, .72f, "ar-park")
            };
            EditorUtility.SetDirty(catalog);
        }

        static MapMarkerUiData Marker(string id, MapMarkerType type, string label, float x, float y, string target)
        {
            return new MapMarkerUiData { id=id, type=type, label=label, normalizedPosition=new Vector2(x, y), targetId=target };
        }

        static void CreateAssetLibrary()
        {
            const string path = ResourceFolder + "/PrototypeUiAssets.asset";
            var library = AssetDatabase.LoadAssetAtPath<PrototypeUiAssets>(path);
            if (library == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    AssetDatabase.DeleteAsset(path);
                library = ScriptableObject.CreateInstance<PrototypeUiAssets>();
                AssetDatabase.CreateAsset(library, path);
            }

            library.illustratedMap = Texture("Assets/_Project/Art/UI/Maps/HoChiMinhCity_IllustratedMap_2048.png");
            library.arScene = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/ar-park.png");
            library.journalOne = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/journal-1.png");
            library.journalTwo = Texture("Assets/_Project/Art/UI/ReferenceTemp/Scenes/journal-2.png");
            library.spirits = Textures("Spirits/spirit-leaf.png", "Spirits/spirit-lotus.png", "Spirits/spirit-star.png");
            library.seedlings = Textures("Seedlings/commonPelletRedSprout.png", "Seedlings/commonPelletBlueReady.png", "Seedlings/commonPelletYellowSprout.png");
            library.landmarks = Textures("Landmarks/independence-palace.png", "Landmarks/post-office.png", "Landmarks/notre-dame.png", "Landmarks/temple-literature.png");
            library.icons = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/UI/ReferenceTemp/Icons" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value).Select(Texture).ToArray();
            EditorUtility.SetDirty(library);
        }

        static Texture2D[] Textures(params string[] paths)
        {
            return paths.Select(path => Texture("Assets/_Project/Art/UI/ReferenceTemp/" + path)).ToArray();
        }

        static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static void CreateNavigationGraph()
        {
            const string path = ResourceFolder + "/ARWalkingNavigation.asset";
            if (AssetDatabase.LoadAssetAtPath<NavGraphViewAsset>(path) != null) return;
            var asset = ScriptableObject.CreateInstance<NavGraphViewAsset>();
            AssetDatabase.CreateAsset(asset, path);
            var root = ScriptableObject.CreateInstance<NavGraph>();
            root.name = "ar_walking_graph";
            root.label = "AR Walking";
            asset.AddNode(root);
            var destinations = new Dictionary<UiRoute, NavDestination>();
            for (var i = 0; i < UiRouteCatalog.All.Count; i++)
            {
                var route = UiRouteCatalog.All[i];
                var destination = ScriptableObject.CreateInstance<NavDestination>();
                destination.name = ToRoute(route);
                destination.label = route.ToString();
                destination.parent = root;
                destination.actions = new List<NavAction>();
                destination.arguments = new List<Argument>();
                destination.position = new Vector2(300 + i % 4 * 330, 120 + i / 4 * 220);
                destination.destinationTemplate = new DefaultDestinationTemplate
                {
                    template = typeof(NavigationScreen).AssemblyQualifiedName,
                    showBottomNavBar = IsRootRoute(route),
                    showAppBar = route != UiRoute.OnboardingPermissions && route != UiRoute.ArCompanion && route != UiRoute.ArPhoto,
                    showBackButton = !IsRootRoute(route), showDrawer = false, showNavigationRail = false
                };
                asset.AddNode(destination);
                destinations.Add(route, destination);
            }
            root.startDestination = destinations[UiRoute.HomeMap];
            Link(asset, root, "root_map", destinations[UiRoute.HomeMap]);
            Link(asset, root, "root_garden", destinations[UiRoute.SeedlingGrowth]);
            Link(asset, root, "root_walk_ar", destinations[UiRoute.ArCompanion]);
            Link(asset, root, "root_journal", destinations[UiRoute.JourneyJournal]);
            Link(asset, root, "root_book", destinations[UiRoute.SpiritCollection]);
            Link(asset, destinations[UiRoute.HomeMap], "start_walk", destinations[UiRoute.ActiveWalk]);
            Link(asset, destinations[UiRoute.ActiveWalk], "finish_walk", destinations[UiRoute.WalkSummary]);
            Link(asset, destinations[UiRoute.HomeMap], "open_landmark", destinations[UiRoute.LandmarkMemory]);
            Link(asset, destinations[UiRoute.SeedlingGrowth], "hatch_seedling", destinations[UiRoute.HatchReveal]);
            Link(asset, destinations[UiRoute.SpiritCollection], "open_spirit", destinations[UiRoute.SpiritDetail]);
            Link(asset, destinations[UiRoute.JourneyJournal], "open_journey", destinations[UiRoute.JourneyDetail]);
            Link(asset, destinations[UiRoute.ArCompanion], "take_photo", destinations[UiRoute.ArPhoto]);
            EditorUtility.SetDirty(asset);
        }

        static void Link(NavGraphViewAsset asset, NavGraphViewHierarchicalNode source, string name, NavGraphViewHierarchicalNode destination)
        {
            var action = ScriptableObject.CreateInstance<NavAction>();
            action.name = name;
            action.destination = destination;
            action.defaultArguments = new List<Argument>();
            source.actions.Add(action);
            asset.AddNode(action);
            EditorUtility.SetDirty(source);
        }

        static string ToRoute(UiRoute route)
        {
            return string.Concat(route.ToString().Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
        }

        static bool IsRootRoute(UiRoute route)
        {
            return route == UiRoute.HomeMap || route == UiRoute.SeedlingGrowth || route == UiRoute.ArCompanion || route == UiRoute.JourneyJournal || route == UiRoute.SpiritCollection;
        }

        static void ConfigureTexturesAndAtlas()
        {
            ConfigureTexture("Assets/_Project/Art/UI/Maps/HoChiMinhCity_IllustratedMap_2048.png", false, 2048);
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art/UI/ReferenceTemp" });
            foreach (var guid in guids) ConfigureTexture(AssetDatabase.GUIDToAssetPath(guid), true, 1024);
            const string atlasPath = "Assets/_Project/Art/UI/ReferenceTemp/PrototypeReference.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
                var sprites = guids.Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).Where(value => value is Sprite).ToArray();
                if (sprites.Length > 0) atlas.Add(sprites);
            }
            var packing = atlas.GetPackingSettings();
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            packing.padding = 4;
            atlas.SetPackingSettings(packing);
            var android = atlas.GetPlatformSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            atlas.SetPlatformSettings(android);
            EditorUtility.SetDirty(atlas);
        }

        static void ConfigureTexture(string path, bool sprite, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            var changed = importer.textureType != (sprite ? TextureImporterType.Sprite : TextureImporterType.Default)
                || importer.alphaIsTransparency != sprite
                || importer.mipmapEnabled
                || importer.maxTextureSize != maxSize
                || importer.textureCompression != TextureImporterCompression.CompressedHQ;
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.alphaIsTransparency = sprite;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            changed |= !android.overridden || android.maxTextureSize != maxSize || android.format != TextureImporterFormat.ASTC_6x6 || android.compressionQuality != 100;
            android.overridden = true;
            android.maxTextureSize = maxSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            if (changed) importer.SaveAndReimport();
        }

        static void ConfigureScene(string path, PanelSettings panelSettings, bool home)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(canvas.gameObject, "Disable legacy UI canvas");
                canvas.gameObject.SetActive(false);
            }
            var root = GameObject.Find("AppUIRoot") ?? new GameObject("AppUIRoot");
            var document = root.GetComponent<UIDocument>() ?? Undo.AddComponent<UIDocument>(root);
            document.panelSettings = panelSettings;
            document.sortingOrder = 100;
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
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
