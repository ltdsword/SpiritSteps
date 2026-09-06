using ARWalking.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CorgiAR
{
    /// <summary>Adds app navigation to SampleScene only when it was opened from SpiritSteps.</summary>
    [DisallowMultipleComponent]
    public sealed class Pet3DSceneController : MonoBehaviour
    {
        const float ReferenceWidth = 720f;
        const float ReferenceHeight = 1600f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= InstallForAppLaunch;
            SceneManager.sceneLoaded += InstallForAppLaunch;
        }

        static void InstallForAppLaunch(Scene scene, LoadSceneMode mode)
        {
            if (!Pet3DSceneContext.IsActive ||
                scene.name != Pet3DSceneContext.SceneName ||
                FindFirstObjectByType<Pet3DSceneController>() != null)
                return;

            new GameObject("Pet 3D App Bridge").AddComponent<Pet3DSceneController>();
        }

        void Awake() => BuildBackButton();

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ReturnToApp();
        }

        void BuildBackButton()
        {
            var canvasObject = new GameObject("Pet 3D Navigation", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;

            var safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            var safeArea = safeAreaObject.GetComponent<RectTransform>();
            Rect safe = Screen.safeArea;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;

            var buttonObject = new GameObject("Back To Companions", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(safeArea, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(164f, 72f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(84, 190, 107, 245);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(ReturnToApp);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.text = "<  App";
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 28;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
        }

        void ReturnToApp()
        {
            UiPrototypeRuntime runtime = UiPrototypeRuntime.Instance;
            if (runtime != null)
                runtime.ReturnFromPet3D();
            else
            {
                Pet3DSceneContext.Clear();
                SceneManager.LoadScene("Home");
            }
        }
    }
}
