using System;
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
    public void FindLobby()
    {
        SceneManager.LoadScene("FindLobbyUI");
    }
    public void CreateLobby()
    {
        SceneManager.LoadScene("CreateRoomUI");
    }
    public void ItemCustomization()
    {
        SceneManager.LoadScene("ItemUI");
    }
}