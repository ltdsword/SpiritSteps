using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CorgiAR.Tests
{
    public sealed class RenderingConfigurationTests
    {
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string CorgiMaterialPath = "Assets/CorgiAR/Materials/Corgi_URP.mat";
        private const string GroundMaterialPath = "Assets/ShibaFeeding/Generated/Playground.mat";

        [Test]
        public void PcRenderer_SsaoFeatureIsDisabled()
        {
            Object ssao = AssetDatabase.LoadAllAssetsAtPath(PcRendererPath)
                .FirstOrDefault(asset => asset != null &&
                    asset.GetType().Name == "ScreenSpaceAmbientOcclusion");

            Assert.IsNotNull(ssao,
                "PC_Renderer must retain its SSAO sub-asset so the disabled state can be verified.");

            var serialized = new SerializedObject(ssao);
            SerializedProperty active = serialized.FindProperty("m_Active");
            Assert.IsNotNull(active, "SSAO feature has no m_Active property.");
            Assert.IsFalse(active.boolValue,
                "SSAO must remain disabled for the lightweight desktop preview configuration.");

            Object rendererData = AssetDatabase.LoadMainAssetAtPath(PcRendererPath);
            var rendererSerialized = new SerializedObject(rendererData);
            Assert.AreEqual(0, rendererSerialized.FindProperty("m_RenderingMode").intValue,
                "PC renderer must use Forward for consistency with the mobile renderer.");
        }

        [TestCase(CorgiMaterialPath)]
        [TestCase(GroundMaterialPath)]
        public void SceneMaterials_UseSimpleLitWithoutPbrSpeckles(string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsNotNull(material, "Material is missing at " + materialPath);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit", material.shader.name,
                materialPath + " must avoid the URP/Lit PBR path that produces flashing pixels.");
            Assert.IsFalse(material.IsKeywordEnabled("_NORMALMAP"),
                materialPath + " must not enable a normal map keyword without a valid normal map.");
        }
    }
}
