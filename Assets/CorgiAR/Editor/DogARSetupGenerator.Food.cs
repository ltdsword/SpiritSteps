using System;
using ShibaFeeding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CorgiAR.EditorTools
{
    public static partial class DogARSetupGenerator
    {
        private const string OnigiriModelPath = "Assets/CorgiAR/onigiri/source/rice.fbx";
        private const string OnigiriIconPath = "Assets/CorgiAR/onigiri/onigiri.png";
        private const string ChangeIconPath = "Assets/CorgiAR/icon/change_icon.png";
        private const string SphereIconPath = "Assets/CorgiAR/icon/sphere.png";
        private const string OnigiriGeneratedFolder = "Assets/CorgiAR/onigiri/Generated";
        private const string OnigiriPrefabPath = OnigiriGeneratedFolder + "/OnigiriFood.prefab";
        private const string RiceMaterialPath = OnigiriGeneratedFolder + "/Onigiri_Rice.mat";
        private const string NoriMaterialPath = OnigiriGeneratedFolder + "/Onigiri_Nori.mat";
        private const string RiceAlbedoPath = "Assets/CorgiAR/onigiri/textures/rice_albedo.jpg";
        private const string RiceNormalPath = "Assets/CorgiAR/onigiri/textures/rice_normal.tga.png";
        private const string RiceAoPath = "Assets/CorgiAR/onigiri/textures/rice_AO.tga.png";
        private const string RiceRoughnessPath = "Assets/CorgiAR/onigiri/textures/rice_roughness.tga.png";
        private const string NoriAlbedoPath = "Assets/CorgiAR/onigiri/textures/nori_albedo.jpg";
        private const string NoriAoPath = "Assets/CorgiAR/onigiri/textures/nori_AO.tga.png";
        private const string NoriRoughnessPath = "Assets/CorgiAR/onigiri/textures/nori_Roughness.tga.png";

        [MenuItem("Tools/Corgi/Upgrade Food Selector")]
        public static void UpgradeFoodSelectorMenu()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open " + ScenePath + " before upgrading the food selector.");

            GameObject foodButton = Find(scene, "Food Button")
                ?? throw new InvalidOperationException("Food Button was not found in SampleScene.");
            FoodDragThrowUI foodDrag = foodButton.GetComponent<FoodDragThrowUI>()
                ?? throw new InvalidOperationException("Food Button is missing FoodDragThrowUI.");
            GameObject chickenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoodPrefabPath);

            ConfigureFoodSelector(foodButton, foodDrag, chickenPrefab);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the upgraded food selector in SampleScene.");
            AssetDatabase.SaveAssets();
            Debug.Log("FOOD SELECTOR UPGRADED: Chicken + Onigiri.", foodButton);
        }

        private static void ConfigureFoodSelector(GameObject foodButton,
            FoodDragThrowUI foodDrag, GameObject chickenPrefab)
        {
            if (foodButton == null || foodDrag == null)
                return;

            Canvas hudCanvas = foodButton.GetComponentInParent<Canvas>();
            if (hudCanvas != null)
            {
                hudCanvas.pixelPerfect = true;
                EditorUtility.SetDirty(hudCanvas);
            }

            GameObject onigiriPrefab = EnsureOnigiriFoodPrefab();
            ConfigureIconImporter(FoodIconPath);
            ConfigureIconImporter(OnigiriIconPath);
            ConfigureCrispUiIconImporter(ChangeIconPath, 256, FilterMode.Point);
            ConfigureCrispUiIconImporter(SphereIconPath, 512, FilterMode.Bilinear);
            Sprite chickenIcon = AssetDatabase.LoadAssetAtPath<Sprite>(FoodIconPath);
            Sprite onigiriIcon = AssetDatabase.LoadAssetAtPath<Sprite>(OnigiriIconPath);
            Sprite changeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ChangeIconPath);
            Sprite sphereIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SphereIconPath);
            if (chickenIcon == null || onigiriIcon == null || changeIcon == null || sphereIcon == null)
                throw new InvalidOperationException("One or more food UI images could not be imported as a Sprite.");

            Transform legacyRuntimeIcon = foodButton.transform.Find("Runtime Drumstick Icon");
            if (legacyRuntimeIcon != null)
                UnityEngine.Object.DestroyImmediate(legacyRuntimeIcon.gameObject);

            Transform iconTransform = foodButton.transform.Find("Food Icon")
                                      ?? foodButton.transform.Find("Drumstick Icon");
            Image iconImage;
            if (iconTransform == null)
            {
                var iconObject = new GameObject("Food Icon", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(foodButton.transform, false);
                iconTransform = iconObject.transform;
                iconImage = iconObject.GetComponent<Image>();
            }
            else
            {
                iconTransform.name = "Food Icon";
                iconImage = iconTransform.GetComponent<Image>();
            }

            RectTransform iconRect = (RectTransform)iconTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            // The chicken PNG has generous transparent margins. This restores the
            // large, readable food silhouette used before the selector was added.
            iconRect.sizeDelta = new Vector2(285f, 285f);
            iconTransform.gameObject.SetActive(true);
            iconImage.enabled = true;
            iconImage.sprite = chickenIcon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject nameBadge = GetOrCreateUiObject(foodButton.transform, "Food Name Badge",
                typeof(Image));
            nameBadge.SetActive(false);
            RectTransform badgeRect = (RectTransform)nameBadge.transform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0f);
            badgeRect.anchoredPosition = new Vector2(0f, 7f);
            badgeRect.sizeDelta = new Vector2(116f, 32f);
            Image badgeImage = nameBadge.GetComponent<Image>();
            badgeImage.sprite = RoundSprite;
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = new Color(0.04f, 0.08f, 0.08f, 0.78f);
            badgeImage.raycastTarget = false;
            Text nameLabel = nameBadge.transform.Find("Label")?.GetComponent<Text>();
            if (nameLabel == null)
                nameLabel = Label(nameBadge.transform, "Label", "G\u00C0", 20);
            nameLabel.fontSize = 20;
            nameLabel.raycastTarget = false;

            GameObject quantityBadge = GetOrCreateUiObject(foodButton.transform, "Food Quantity Badge",
                typeof(Image));
            RectTransform quantityRect = (RectTransform)quantityBadge.transform;
            quantityRect.anchorMin = quantityRect.anchorMax = new Vector2(0.5f, 0f);
            quantityRect.pivot = new Vector2(0.5f, 0.5f);
            quantityRect.anchoredPosition = new Vector2(0f, -20f);
            quantityRect.sizeDelta = new Vector2(76f, 76f);
            Image quantityImage = quantityBadge.GetComponent<Image>();
            quantityImage.sprite = sphereIcon;
            quantityImage.type = Image.Type.Simple;
            quantityImage.preserveAspect = true;
            quantityImage.color = Color.white;
            quantityImage.raycastTarget = false;
            Text quantityLabel = quantityBadge.transform.Find("Amount")?.GetComponent<Text>();
            if (quantityLabel == null)
                quantityLabel = Label(quantityBadge.transform, "Amount", "20", 44);
            quantityLabel.text = "20";
            quantityLabel.fontSize = 44;
            quantityLabel.fontStyle = FontStyle.Bold;
            quantityLabel.resizeTextForBestFit = true;
            quantityLabel.resizeTextMinSize = 32;
            quantityLabel.resizeTextMaxSize = 44;
            quantityLabel.color = Color.black;
            quantityLabel.raycastTarget = false;

            GameObject switchObject = GetOrCreateUiObject(foodButton.transform, "Switch Food",
                typeof(Image), typeof(Button));
            RectTransform switchRect = (RectTransform)switchObject.transform;
            switchRect.anchorMin = switchRect.anchorMax = new Vector2(1f, 0.5f);
            switchRect.pivot = new Vector2(0.5f, 0.5f);
            switchRect.anchoredPosition = new Vector2(23f, 8f);
            switchRect.sizeDelta = new Vector2(82f, 82f);
            Image switchImage = switchObject.GetComponent<Image>();
            switchImage.sprite = sphereIcon;
            switchImage.type = Image.Type.Simple;
            switchImage.preserveAspect = true;
            switchImage.color = Color.white;
            Button switchButton = switchObject.GetComponent<Button>();
            switchButton.targetGraphic = switchImage;
            Text arrow = switchObject.transform.Find("Arrow")?.GetComponent<Text>();
            if (arrow != null)
                arrow.gameObject.SetActive(false);

            GameObject changeIconObject = GetOrCreateUiObject(switchObject.transform, "Change Icon",
                typeof(Image));
            RectTransform changeRect = (RectTransform)changeIconObject.transform;
            changeRect.anchorMin = changeRect.anchorMax = new Vector2(0.5f, 0.5f);
            changeRect.pivot = new Vector2(0.5f, 0.5f);
            changeRect.anchoredPosition = new Vector2(0f, -1f);
            changeRect.sizeDelta = new Vector2(58f, 58f);
            Image changeImage = changeIconObject.GetComponent<Image>();
            changeImage.sprite = changeIcon;
            changeImage.type = Image.Type.Simple;
            changeImage.preserveAspect = true;
            changeImage.color = Color.white;
            changeImage.raycastTarget = false;

            var choices = new[]
            {
                new FoodDragThrowUI.FoodChoice
                {
                    DisplayName = "G\u00C0",
                    Prefab = chickenPrefab,
                    Icon = chickenIcon,
                    Quantity = 20,
                    HudIconSize = 285f,
                    WorldSize = 0.28f,
                    TrailStart = new Color(1f, 0.82f, 0.28f, 0.58f),
                    TrailEnd = new Color(1f, 0.35f, 0.05f, 0f)
                },
                new FoodDragThrowUI.FoodChoice
                {
                    DisplayName = "C\u01A0M N\u1EAEM",
                    Prefab = onigiriPrefab,
                    Icon = onigiriIcon,
                    Quantity = 20,
                    HudIconSize = 190f,
                    WorldSize = 0.16f,
                    TrailStart = new Color(1f, 0.96f, 0.76f, 0.62f),
                    TrailEnd = new Color(0.24f, 0.52f, 0.22f, 0f)
                }
            };
            foodDrag.ConfigureFoodChoices(choices, 0, iconImage, nameLabel, quantityLabel, switchButton);
            EditorUtility.SetDirty(foodDrag);
            EditorUtility.SetDirty(iconImage);
            EditorUtility.SetDirty(nameLabel);
            EditorUtility.SetDirty(quantityLabel);
            EditorUtility.SetDirty(quantityImage);
            EditorUtility.SetDirty(switchImage);
            EditorUtility.SetDirty(changeImage);
            EditorUtility.SetDirty(switchButton);
        }

        private static GameObject GetOrCreateUiObject(Transform parent, string objectName,
            params Type[] additionalComponents)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
                return existing.gameObject;

            var types = new Type[2 + additionalComponents.Length];
            types[0] = typeof(RectTransform);
            types[1] = typeof(CanvasRenderer);
            Array.Copy(additionalComponents, 0, types, 2, additionalComponents.Length);
            var created = new GameObject(objectName, types);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject EnsureOnigiriFoodPrefab()
        {
            EnsureFolder(OnigiriGeneratedFolder);
            ConfigureIconImporter(OnigiriIconPath);
            ConfigureLinearTexture(RiceAoPath);
            ConfigureLinearTexture(RiceRoughnessPath);
            ConfigureLinearTexture(NoriAoPath);
            ConfigureLinearTexture(NoriRoughnessPath);
            ConfigureNormalImporter(RiceNormalPath);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material rice = BuildOnigiriMaterial(RiceMaterialPath, lit,
                RiceAlbedoPath, RiceNormalPath, RiceAoPath, 0.24f);
            Material nori = BuildOnigiriMaterial(NoriMaterialPath, lit,
                NoriAlbedoPath, null, NoriAoPath, 0.36f);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(OnigiriModelPath);
            if (source == null)
                throw new InvalidOperationException("Onigiri FBX not found at " + OnigiriModelPath);

            var root = new GameObject("OnigiriFood");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.name = "Onigiri Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Material authored = renderer.sharedMaterial;
                    bool isNori = renderer.name.IndexOf("plane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  (authored != null && authored.name.IndexOf("nori",
                                      StringComparison.OrdinalIgnoreCase) >= 0);
                    renderer.sharedMaterial = isNori ? nori : rice;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, OnigiriPrefabPath);
                if (saved == null)
                    throw new InvalidOperationException("Could not save " + OnigiriPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(OnigiriPrefabPath);
        }

        private static Material BuildOnigiriMaterial(string materialPath, Shader shader,
            string albedoPath, string normalPath, string aoPath, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = string.IsNullOrEmpty(normalPath)
                ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 0.7f);
            if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", ao);
            if (material.HasProperty("_OcclusionStrength")) material.SetFloat("_OcclusionStrength", 0.75f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            else material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureIconImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency || importer.mipmapEnabled;
            if (!changed)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureCrispUiIconImporter(string path, int maxSize, FilterMode filterMode)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.alphaIsTransparency || importer.mipmapEnabled ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed ||
                           importer.maxTextureSize != maxSize || importer.filterMode != filterMode;
            if (!changed)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.maxTextureSize = maxSize;
            importer.filterMode = filterMode;
            importer.SaveAndReimport();
        }

        private static void ConfigureNormalImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap)
                return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static void ConfigureLinearTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !importer.sRGBTexture)
                return;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }
}
