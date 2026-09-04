using UnityEngine;
using UnityEngine.SceneManagement;
using ARWalking.UI;

public class Bootstrap : MonoBehaviour
{
    private void Start()
    {
        UiPrototypeRuntime.EnsureExists();
        SceneManager.LoadScene("Home");
    }
}
