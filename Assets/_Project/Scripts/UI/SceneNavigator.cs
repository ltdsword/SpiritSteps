using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigator : MonoBehaviour
{
    public void GoHome()
    {
        SceneManager.LoadScene("Home");
    }

    public void GoWalk()
    {
        SceneManager.LoadScene("Walk");
    }
}
