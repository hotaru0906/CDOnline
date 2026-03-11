using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void PlayOnline()
    {
        SceneManager.LoadScene("PlayOnlineMenu");
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("UI menu");
    }
}