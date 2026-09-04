#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace ARWalking.EditorTools
{
    // Root cause: gree/unity-webview's own IPostGenerateGradleAndroidProject step
    // (net.gree.unity-webview's UnityWebViewPostprocessBuild) only patches the
    // FIRST <activity> in the manifest that has a MAIN/LAUNCHER intent-filter.
    // Unity 6's Android template always emits two such activities --
    // UnityPlayerActivity (legacy, android:enabled="false") and
    // UnityPlayerGameActivity (android:enabled="true", the one that actually
    // launches when "Activity" is set to GameActivity in Player Settings) -- with
    // UnityPlayerActivity listed first. gree's script ends up setting
    // android:hardwareAccelerated="true" on the disabled legacy activity while
    // leaving the real, enabled GameActivity at "false" (Unity's own generated
    // value, which silently overrides Assets/Plugins/Android/AndroidManifest.xml
    // during manifest generation). A window without hardware acceleration forces
    // every view it hosts -- including a WebViewObject's embedded native WebView
    // -- into software compositing, so a WebGL canvas (MapLibre GL JS) can't get
    // a working GL context and renders solid black, even though ordinary DOM/CSS
    // content in the same page still paints fine. This step runs after gree's
    // (higher callbackOrder) and forces hardwareAccelerated="true" on every
    // <activity>, so whichever one Unity actually enables ends up correct.
    // Diagnosed and fixed in the PedometerPrototype reference project; ported
    // here as-is.
    public class ForceHardwareAcceleratedActivities : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath)) return;

            const string androidNs = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument();
            using (var reader = new XmlTextReader(manifestPath)) doc.Load(reader);

            var nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("android", androidNs);

            bool changed = false;
            foreach (XmlElement activity in doc.SelectNodes("/manifest/application/activity", nsMgr))
            {
                if (activity.GetAttribute("hardwareAccelerated", androidNs) != "true")
                {
                    activity.SetAttribute("hardwareAccelerated", androidNs, "true");
                    changed = true;
                }
            }

            if (changed)
            {
                using var writer = new XmlTextWriter(manifestPath, new System.Text.UTF8Encoding(false));
                writer.Formatting = Formatting.Indented;
                doc.Save(writer);
                Debug.Log($"[ForceHardwareAcceleratedActivities] Forced hardwareAccelerated=true on all activities in {manifestPath}");
            }
        }
    }
}
#endif
