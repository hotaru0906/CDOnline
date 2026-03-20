using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Các Canvas")]
    public GameObject canvasMainMenu;
    public GameObject canvasPlayOnline;
    public GameObject canvasFindLobby;
    public GameObject canvasCreateRoom;
    public GameObject canvasItemUI;

    [Header("Character Preview")]
    public GameObject characterModelHolder;   // Kéo Capsule (hoặc Empty chứa Capsule) vào đây

    private GameObject currentScreen;
    private Stack<GameObject> screenHistory = new Stack<GameObject>();

    void Start()
    {
        // Ẩn hết
        canvasPlayOnline.SetActive(false);
        canvasFindLobby.SetActive(false);
        canvasCreateRoom.SetActive(false);
        canvasItemUI.SetActive(false);
        characterModelHolder.SetActive(false);           // ← Capsule ẩn khi bắt đầu

        currentScreen = canvasMainMenu;
        canvasMainMenu.SetActive(true);
    }

    public void ShowPlayOnline()   { PushAndHideCurrent(); currentScreen = canvasPlayOnline; currentScreen.SetActive(true); }
    public void ShowFindLobby()    { PushAndHideCurrent(); currentScreen = canvasFindLobby;  currentScreen.SetActive(true); }
    public void ShowCreateRoom()   { PushAndHideCurrent(); currentScreen = canvasCreateRoom; currentScreen.SetActive(true); }

    public void ShowItemUI()
    {
        PushAndHideCurrent();
        currentScreen = canvasItemUI;
        currentScreen.SetActive(true);
        characterModelHolder.SetActive(true);           // ← Hiện Capsule khi mở ItemUI
    }

    public void GoBack()
    {
        if (screenHistory.Count > 0)
        {
            currentScreen.SetActive(false);

            // Nếu đang ở ItemUI thì ẩn Capsule luôn
            if (currentScreen == canvasItemUI)
                characterModelHolder.SetActive(false);

            currentScreen = screenHistory.Pop();
            currentScreen.SetActive(true);
        }
    }

    private void PushAndHideCurrent()
    {
        if (currentScreen != null)
        {
            // Nếu đang ở ItemUI thì ẩn Capsule trước khi chuyển sang màn khác
            if (currentScreen == canvasItemUI)
                characterModelHolder.SetActive(false);

            screenHistory.Push(currentScreen);
            currentScreen.SetActive(false);
        }
    }
}