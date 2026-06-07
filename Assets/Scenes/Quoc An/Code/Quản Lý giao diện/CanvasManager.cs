using UnityEngine;

public class CanvasManager : MonoBehaviour
{
    public static CanvasManager Instance;

    [Header("All Canvas")]
    public GameObject canvasUIMenu;
    public GameObject canvasPlayOnline;
    public GameObject canvasCreateRoom;
    public GameObject canvasFindLobby;
    public GameObject canvasSetting;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Khởi đầu chỉ hiện UI Menu
        ShowUIMenu();
    }

    // Tắt tất cả canvas
    void HideAllCanvas()
    {
        canvasUIMenu.SetActive(false);
        canvasPlayOnline.SetActive(false);
        canvasCreateRoom.SetActive(false);
        canvasFindLobby.SetActive(false);
        canvasSetting.SetActive(false);
    }

    // Hiện UI Menu
    public void ShowUIMenu()
    {
        HideAllCanvas();
        canvasUIMenu.SetActive(true);
        Debug.Log("Đang ở UI Menu");
    }

    // Hiện Play Online
    public void ShowPlayOnline()
    {
        HideAllCanvas();
        canvasPlayOnline.SetActive(true);
        Debug.Log("Đang ở Play Online");
    }

    // Hiện Create Room
    public void ShowCreateRoom()
    {
        HideAllCanvas();
        canvasCreateRoom.SetActive(true);
        Debug.Log("Đang ở Create Room");
    }

    // Hiện Find Lobby
    public void ShowFindLobby()
    {
        HideAllCanvas();
        canvasFindLobby.SetActive(true);
        Debug.Log("Đang ở Find Lobby");
    }

    // Hiện Setting
    public void ShowSetting()
    {
        HideAllCanvas();
        canvasSetting.SetActive(true);
        Debug.Log("Đang ở Setting");
    }
}