using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CorgiAR.EditorTools
{
    /// <summary>Renders a transparent PNG portrait of each pet for the selection menu.</summary>
    public static partial class DogARSetupGenerator
    {
        private const string ThumbDir = "Assets/CorgiAR/UI/Pets";
        private const float ModelYawDegrees = 150f;
        /// <summary>Degrees the thumbnail camera swings off dead-on-face - positive turns the
        /// shot toward the character's right as seen by the viewer. Flip the sign if a species
        /// ever needs the mirrored angle.</summary>
        private const float FaceViewOffsetDegrees = -45f;

        // Same head-bone candidates as PetHeadLook, so the thumbnail camera aims at the same
        // joint the in-game head-look behaviour rotates.
        private static readonly string[] DogKitHeadBones = { "DEF-spine.011", "DEF-spine.010", "DEF-spine.009" };
        private static readonly string[] UaaHeadBones = { "Head", "Neck3", "Neck2" };

        [MenuItem("Tools/Corgi/Rebuild Pet Thumbnails")]
        public static void RebuildPetThumbnailsMenu()
        {
            BuildPetThumbnails();
            AssetDatabase.Refresh();
            Debug.Log("PET THUMBNAILS REBUILT: " + ThumbDir);
        }

        private static void BuildPetThumbnails()
        {
            EnsureFolder(ThumbDir);
            const int size = 256;

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var camGo = new GameObject("~PetThumbCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.01f;
            cam.targetTexture = rt;
            UniversalAdditionalCameraData cameraData = camGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.dithering = false;

            var lightGo = new GameObject("~PetThumbLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            // The offscreen camera otherwise still inherits whichever scene happens to be open's
            // skybox/ambient GI, which bleeds colored ambient light into dark, normal-mapped
            // creases (ears, eye sockets) as speckle. Swap it for a neutral gray flat ambient
            // instead of dropping it entirely - zero ambient leaves the directional light's
            // unlit side of the model near-black.
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.35f);

            // The editor's Quality tier forces anisotropic filtering on for every texture, which
            // samples far more texels per pixel at the grazing angles this close-up portrait is
            // full of - on these detail-mapped fur textures that reads as shimmering speckle. The
            // phone build runs a tier that doesn't force this, which is why it never shows there.
            AnisotropicFiltering previousAnisotropicFiltering = QualitySettings.anisotropicFiltering;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // Screen-Space Ambient Occlusion is a URP renderer feature, not a post-processing
            // volume override, so cameraData.renderPostProcessing above never touched it. It
            // samples a dithered pattern that needs several accumulated frames to converge; a
            // single Render() call leaves that dither pattern visible as speckle, worst in the
            // creases and undersides SSAO targets (ears, eye sockets) - exactly what showed up
            // here. This is a real render pipeline behaviour, not something baked into the asset,
            // which is why it never shows on a phone build using a different quality tier.
            List<ScriptableRendererFeature> disabledSsaoFeatures = new();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                var pipelineSo = new SerializedObject(urpAsset);
                SerializedProperty rendererDataList = pipelineSo.FindProperty("m_RendererDataList");
                if (rendererDataList != null)
                {
                    for (int i = 0; i < rendererDataList.arraySize; i++)
                    {
                        if (rendererDataList.GetArrayElementAtIndex(i).objectReferenceValue is not ScriptableRendererData rendererData)
                            continue;
                        foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
                        {
                            if (feature != null && feature.isActive && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                            {
                                feature.SetActive(false);
                                disabledSsaoFeatures.Add(feature);
                            }
                        }
                    }
                }
            }

            try
            {
                foreach (PetEntry pet in PetCatalog.Entries)
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(pet.SourcePrefabPath);
                    if (src == null)
                        continue;

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, ModelYawDegrees, 0f));
                    FlattenGlossForPortrait(inst);

                    Bounds b = RendererWorldBounds(inst);
                    string[] headCandidates = pet.Family == PetFamily.DogKit ? DogKitHeadBones : UaaHeadBones;
                    Transform headBone = BoneResolver.Resolve(headCandidates, inst.transform, "head");
                    Vector3 focus = headBone != null ? headBone.position : b.center;

                    // A head-and-shoulders portrait, not the whole body: frame on a small fraction
                    // of the pet's overall size (a head is a small part of a standing quadruped's
                    // bounding sphere) so the crop stays proportionally tight across wildly
                    // different scales and body plans (a stubby Dog Kit corgi, a lean UAA wolf, a
                    // full-size UAA horse or cow all share roughly the same head-to-whole-body-
                    // bounding-box ratio once it's this small).
                    float bodyRadius = Mathf.Max(0.1f, b.extents.magnitude);
                    float headRadius = Mathf.Max(0.06f, bodyRadius * 0.15f);
                    float dist = headRadius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.6f;
                    // Sized to the pet: a fixed far plane either starves the depth buffer of
                    // precision for a small dog shot from close up, or clips a full-size UAA horse
                    // or cow out of frame entirely when its head-portrait distance is much larger.
                    cam.farClipPlane = dist + 2f;
                    // The model faces world azimuth ModelYawDegrees (Euler yaw measured the same
                    // way: direction = (sin, _, cos)), so a camera sitting along that same
                    // direction looks the pet dead in the face. Swinging FaceViewOffsetDegrees off
                    // that gives the three-quarter angle instead of a flat front-on shot.
                    float cameraAzimuth = (ModelYawDegrees + FaceViewOffsetDegrees) * Mathf.Deg2Rad;
                    Vector3 cameraDirection = new Vector3(Mathf.Sin(cameraAzimuth), 0.45f, Mathf.Cos(cameraAzimuth));
                    cam.transform.position = focus + cameraDirection.normalized * dist;
                    cam.transform.LookAt(focus);

                    cam.Render();

                    RenderTexture.active = rt;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;

                    File.WriteAllBytes(pet.ThumbnailPath, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                    UnityEngine.Object.DestroyImmediate(inst);
                }
            }
            finally
            {
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                QualitySettings.anisotropicFiltering = previousAnisotropicFiltering;
                foreach (ScriptableRendererFeature feature in disabledSsaoFeatures)
                    feature.SetActive(true);
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (PetEntry pet in PetCatalog.Entries)
            {
                if (AssetImporter.GetAtPath(pet.ThumbnailPath) is not TextureImporter importer)
                    continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(pet.ThumbnailPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>Some pet materials drive smoothness/metallic from a per-pixel map (baked to
        /// fake glittery fur at normal viewing distance) with environment reflections on top -
        /// magnified this close for a head portrait, that reads as harsh colored speckle instead
        /// of fur. Flattening to a uniform low gloss and dropping reflections removes it while
        /// leaving the base color/normal detail (the actual fur pattern) intact. Per-renderer
        /// <c>Renderer.materials</c> copies never touch the shared gameplay material asset.</summary>
        private static void FlattenGlossForPortrait(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    if (material.HasProperty("_MetallicGlossMap"))
                        material.SetTexture("_MetallicGlossMap", null);
                    if (material.HasProperty("_Metallic"))
                        material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", 0.15f);
                    if (material.HasProperty("_EnvironmentReflections"))
                        material.SetFloat("_EnvironmentReflections", 0f);
                    if (material.HasProperty("_GlossyReflections"))
                        material.SetFloat("_GlossyReflections", 0f);
                    if (material.HasProperty("_SpecularHighlights"))
                        material.SetFloat("_SpecularHighlights", 0f);
                    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    if (material.HasProperty("_SpecColor"))
                        material.SetColor("_SpecColor", Color.black);
                }
                renderer.materials = materials;
            }
        }

        private static Bounds RendererWorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
