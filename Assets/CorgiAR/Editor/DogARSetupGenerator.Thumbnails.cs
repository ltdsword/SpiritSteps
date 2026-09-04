using System.IO;
using UnityEditor;
using UnityEngine;

namespace CorgiAR.EditorTools
{
    /// <summary>Renders a transparent PNG portrait of each pet for the selection menu.</summary>
    public static partial class DogARSetupGenerator
    {
        private const string ThumbDir = "Assets/CorgiAR/UI/Pets";

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

            var lightGo = new GameObject("~PetThumbLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            try
            {
                foreach (PetEntry pet in PetCatalog.Entries)
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>(pet.SourcePrefabPath);
                    if (src == null)
                        continue;

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 150f, 0f));

                    Bounds b = RendererWorldBounds(inst);
                    float radius = Mathf.Max(0.1f, b.extents.magnitude);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.2f;
                    cam.transform.position = b.center + new Vector3(0.35f, 0.28f, 1f).normalized * dist;
                    cam.transform.LookAt(b.center);

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
