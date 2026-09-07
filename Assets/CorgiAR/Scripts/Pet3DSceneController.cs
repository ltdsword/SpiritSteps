using ARWalking.UI;
using CorgiAR.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CorgiAR
{
    /// <summary>Adds app navigation to SampleScene only when it was opened from SpiritSteps.</summary>
    [DisallowMultipleComponent]
    public sealed class Pet3DSceneController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= InstallMeadowHud;
            SceneManager.sceneLoaded += InstallMeadowHud;
        }

        static void InstallMeadowHud(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != Pet3DSceneContext.SceneName ||
                FindFirstObjectByType<Pet3DGlassHud>() != null)
                return;

            var bridge = new GameObject("Pet 3D App Bridge");
            bridge.AddComponent<UIDocument>();
            if (Pet3DSceneContext.IsActive)
                bridge.AddComponent<Pet3DSceneController>();
            bridge.AddComponent<Pet3DGlassHud>();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ReturnToApp();
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
